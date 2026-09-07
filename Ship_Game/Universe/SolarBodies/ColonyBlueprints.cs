using System;
using System.Collections.Generic;
using System.Linq;
using SDGraphics;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;
using static Ship_Game.Planet;

namespace Ship_Game.Universe.SolarBodies
{
    [StarDataType]
    public class ColonyBlueprints
    {
        [StarData] readonly Empire Owner;
        [StarData] readonly Planet P;
        [StarData] public Array<Building> PlannedBuildingsWeCanBuild { get; private set; }
        [StarData] public int PercentCompleted { get; private set; }
        [StarData] public int PercentAchievable { get; private set; }

        // Counts behind the two percentages, for a screen that needs "3/12" rather than "25%".
        // Not serialized: they are written by the same passes, and a plan that has not refreshed
        // yet shows the same staleness its percentages already show.
        public int PlannedCount { get; private set; }
        public int BuiltCount { get; private set; }
        public int ReachableCount { get; private set; }
        public int NotAchievableCount => (PlannedCount - ReachableCount).LowerBound(0);
        // Ludoal fork (bench 554): ⚠ nothing is diagnosed from the three counts above until they
        // have been computed at least once. They are NOT serialized, so a plan restored from a save
        // arrives at nought across the board, and nought everywhere satisfies Blocked's arithmetic
        // by accident. A FLAG rather than a test on the values: a plan whose every entry waits on a
        // technology shows the same zeroes and IS a true stall - only the flag tells the two apart.
        // ⚠ Set by the ONE path that rebuilds the offer first, never by a bare Refresh() - an empty
        // offer cannot be told from an exhausted one.
        public bool Measured { get; private set; }
        [StarData] BlueprintsTemplate Template;

        public string Name => Template.Name;
        public string LinkedBlueprintsName => Template.LinkTo ?? "";
        public bool Exclusive => Template.Exclusive; // Build only these buildings and remove the rest
        // ⚠ never dereferenced blind: a template arrives with no plan until its own deserialization
        // hook has run, and the hooks fire in an order this side does not get to choose. An empty
        // plan is a colony with nothing planned - a state the whole class already handles - where a
        // null crashes mid-load.
        static readonly Array<string> NoPlan = new();
        Array<string> PlannedBuildings => Template?.PlannedBuildings ?? NoPlan;
        public ColonyType ColonyType => Template.ColonyType;
        bool Completed => PercentCompleted == 100;

        public bool IsAchievableCompleted => PercentAchievable == 0 || PercentAchievable == PercentCompleted;
        // a standing building counts as reachable too, so achievable is never below completed:
        // the gate asks whether half of what this colony CAN reach is already up.
        bool IsHalfAchievableCompleted => PercentAchievable == 0 || PercentCompleted >= PercentAchievable/2;

        // Ludoal fork (maintainer feedback): everything this colony can reach is up, and the list
        // still is not finished - entries wait on a technology, the ground or a mandate. The chain
        // does not fire on its own here (that needs the WHOLE list), so the player is offered the
        // hand-over instead. A plan with no link has nothing to move on to: this is its end state.
        public bool Blocked => Measured
                               && PlannedCount > 0
                               && BuiltCount == PlannedCount - NotAchievableCount
                               && BuiltCount < PlannedCount;


        // An exclusive plan with the whole list up and nowhere to hand over to: not a stall, the
        // colony it was written to produce. It keeps watch from here on.
        public bool FinalState => Measured && Exclusive && PlannedCount > 0
                                  && BuiltCount == PlannedCount
                                  && LinkedBlueprintsName.Length == 0;

        public bool MoveOnToLink()
        {
            if (!ResourceManager.TryGetBlueprints(LinkedBlueprintsName, out BlueprintsTemplate template))
                return false;

            ChangeTemplate(template);
            return true;
        }

        public bool IsRequired(Building b) => PlannedBuildings.Contains(b.Name);
        public bool IsNotRequired(Building b) => !IsRequired(b);

        public bool OkToBuildTerraformers => PlannedBuildings.Count < P.TileArea / 2 ? IsAchievableCompleted : IsHalfAchievableCompleted;

        [StarDataConstructor]
        public ColonyBlueprints() { }

        public ColonyBlueprints(BlueprintsTemplate template, Planet planet, Empire owner)
        {
            Owner = owner;
            P = planet;
            PlannedBuildingsWeCanBuild = new();
            ChangeTemplate(template);
        }

        public void ChangeTemplate(BlueprintsTemplate template) 
        {
            Template = template;
            OnTemplateChanged();
        }

        void OnTemplateChanged()
        {
            ChangeColonyType();
            // refreshes the pair on its way out, so the two calls that stood here are gone
            RefreshPlannedBuildingsWeCanBuild(P.GetBuildingsCanBuild());
        }

        void ChangeColonyType()
        {
            if (ColonyType != ColonyType.Colony)
                P.CType = ColonyType;
        }

        // ⚠ the two figures are shown side by side and share a denominator, so they are
        // refreshed TOGETHER (bench 535): two numbers a player compares cannot be computed at
        // two different moments.
        // ⚠ claimed by the ONE site that has just REBUILT the offer - which is not this class.
        // RefreshPlannedBuildingsWeCanBuild is handed a list rather than building one, and the
        // load path hands it the cache while that cache is still empty, so the flag set there
        // would be set on nothing.
        public void MarkMeasured() => Measured = true;

        public void Refresh()
        {
            UpdateCompletion();
            UpdatePercentAchievable();
        }

        public void UpdateCompletion()
        {
            int totalPlanned = PlannedBuildings.Count;
            if (totalPlanned == 0) // nothing planned is nothing completed, not a division by zero
            {
                PercentCompleted = 0;
                PlannedCount = 0;   // an empty plan inherits nothing from its predecessor (bench 603)
                BuiltCount = 0;
                return;
            }

            // ⚠ counted by NAME, like the achievable figure beside it (bench 535): a colony can hold
            // two of the same planned building - biospheres, terraformers, anything the governor
            // raises more than once - so counting instances would report one entry twice. The two
            // figures share a denominator, so they must share a unit.
            var built = new HashSet<string>();
            foreach (Building b in P.Buildings)
                if (IsRequired(b))
                    built.Add(b.Name);

            PlannedCount = totalPlanned;
            BuiltCount = built.Count;
            PercentCompleted = (int)(100f * built.Count / totalPlanned);
        }

        // How much of this plan this COLONY can actually end up with.
        //
        // ⚠ counted against THIS colony, not the empire's unlocked buildings: the ground it stands
        // on and its mandates both bound what it can reach (bench 529).
        //
        // A planned building is reachable when it already STANDS here, or when this colony can
        // raise it and its mandate allows it. Counted by name, since a building can be both
        // standing and still offered.
        public void UpdatePercentAchievable()
        {
            int totalPlannedBuildings = PlannedBuildings.Count;
            if (totalPlannedBuildings == 0 || Owner == null)
            {
                PercentAchievable = 0;
                ReachableCount = 0;
                PlannedCount = 0;
                return;
            }

            var reachable = new HashSet<string>();
            foreach (Building b in P.Buildings)
                if (IsRequired(b))
                    reachable.Add(b.Name);

            foreach (Building b in P.GetBuildingsCanBuild())
                if (IsRequired(b) && (b.IsMilitary ? P.MayBuildMilitary : P.MayBuildCivilian))
                    reachable.Add(b.Name);

            // a planned building already under construction is reachable by definition, and the
            // offer it came from does not list it: a unique leaves the buildable set the moment it
            // is queued, so the two sets above alone would call it unreachable while it is being
            // raised.
            foreach (QueueItem q in P.ConstructionQueue)
                if (q.isBuilding && IsRequired(q.Building))
                    reachable.Add(q.Building.Name);

            PlannedCount = totalPlannedBuildings;
            ReachableCount = reachable.Count;
            PercentAchievable = (int)(100 * (float)reachable.Count / totalPlannedBuildings);
        }

        // Ludoal fork (maintainer feedback): a completed plan hands over to its link, on the
        // WHOLE list and never the reachable part of it - a plan longer than the ground, or
        // holding a technology not yet taken, would otherwise hand over in the middle of an era.
        // A plan with no link STAYS: it costs nothing to keep and it is what rebuilds the colony
        // after a volcano.
        // ⚠ Checked once per governing turn rather than the moment a building lands - swapping a
        // plan is a decision about the colony, and a save being read is not the place to take it.
        public void EndIfCompleted()
        {
            if (Completed && ResourceManager.TryGetBlueprints(LinkedBlueprintsName, out BlueprintsTemplate template))
                ChangeTemplate(template);
        }

        public void RefreshPlannedBuildingsWeCanBuild(IReadOnlyList<Building> buildingCanBuild)
        {
            PlannedBuildingsWeCanBuild.Clear();
            if (!P.HasOutpost && !P.HasCapital)
                return;

            // ⚠ walked in the PLAN's order, not the colony's. The plan is a chronology and this list
            // is what the governor picks from, so its order IS the build order. Walking the colony's
            // own buildable list would arrange them by whatever that list happens to hold, and the
            // plan would have no say.
            foreach (string planned in PlannedBuildings)
            {
                for (int i = 0; i < buildingCanBuild.Count; i++)
                {
                    if (buildingCanBuild[i].Name == planned)
                    {
                        PlannedBuildingsWeCanBuild.Add(buildingCanBuild[i]);
                        break;
                    }
                }
            }

            // Ludoal fork (maintainer feedback): what this colony can REACH is read off the very
            // list rebuilt above, so it is recomputed here rather than waiting for a building to
            // rise or fall - a save being read arrives with that list still empty.
            // ⚠ the WHOLE pair, not the achievable half of it: the two figures share a denominator
            // and are read side by side, so they are refreshed together (bench 554).
            Refresh();
        }

        // Ludoal fork (maintainer feedback): a plan directs what gets RAISED. Only an exclusive
        // one also directs what makes way, and only when the ground is actually needed - an
        // ordinary plan gives a colony no reason to demolish that a colony without one lacks.
        // Ludoal fork (maintainer feedback): the plan's order, exposed for the one case where a
        // list is longer than the ground it stands on. Rank 0 is the top; a building the plan
        // does not name has no rank at all, which is what makes it the first to go.
        public int RankOf(Building b) => PlannedBuildings.IndexOf(b.Name);

        // The standing plan member the player ranked lowest, skipping anything the caller
        // refuses to touch. Walked from the bottom of the list, so the first hit is the answer.
        public Building LowestRankedStanding(Func<Building, bool> mayScrap)
        {
            for (int i = PlannedBuildings.Count - 1; i >= 0; i--)
            {
                string planned = PlannedBuildings[i];
                foreach (Building b in P.Buildings)
                {
                    if (b.Name == planned && mayScrap(b))
                        return b;
                }
            }

            return null;
        }

        public bool ShouldScrapNonRequiredBuilding()
        {
            return Exclusive
                && P.FreeHabitableTiles == 0
                && P.GetBuildingsCanBuild().Any(IsRequired)
                && ContainsNotRequiredBuildings();

            bool ContainsNotRequiredBuildings()
            {
                foreach (Building b in P.Buildings)
                {
                    if (b.IsSuitableForBlueprints && IsNotRequired(b))
                        return true;
                }

                return false;
            }
        }
    }
}