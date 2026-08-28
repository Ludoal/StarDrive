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
        [StarData] BlueprintsTemplate Template;

        public string Name => Template.Name;
        public string LinkedBlueprintsName => Template.LinkTo ?? "";
        public bool Exclusive => Template.Exclusive; // Build only these buildings and remove the rest
        // ⚠ never dereferenced blind: a template restored from a pre-532 save arrives with no
        // plan until its own deserialization hook has run, and the hooks fire in an order this
        // side does not get to choose. An empty plan is a colony with nothing planned - which
        // is a state the whole class already handles - where a null was a crash mid-load.
        static readonly Array<string> NoPlan = new();
        Array<string> PlannedBuildings => Template?.PlannedBuildings ?? NoPlan;
        public ColonyType ColonyType => Template.ColonyType;
        bool Completed => PercentCompleted == 100;

        public bool IsAchievableCompleted => PercentAchievable == 0 || PercentAchievable == PercentCompleted;
        bool IsHalfAchievableCompleted => PercentAchievable == 0 || PercentAchievable >= PercentCompleted/2;

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
            RefreshPlannedBuildingsWeCanBuild(P.GetBuildingsCanBuild());
            UpdateCompletion();
            UpdatePercentAchievable();
        }

        void ChangeColonyType()
        {
            if (ColonyType != ColonyType.Colony)
                P.CType = ColonyType;
        }

        public void UpdateCompletion()
        {
            int totalPlanned = PlannedBuildings.Count;
            if (totalPlanned == 0) // nothing planned is nothing completed, not a division by zero
            {
                PercentCompleted = 0;
                return;
            }

            // ⚠ counted by NAME, like the achievable figure beside it (maintainer, bench 535).
            // It counted building INSTANCES: a colony holding two of the same planned building -
            // biospheres, terraformers, anything the governor raises more than once - reported
            // one entry twice and hid a planned building that was never built. A plan showing
            // 100% completed next to 75% achievable was the achievable one telling the truth.
            //
            // The two figures share a denominator, so they must share a unit, or comparing them
            // means nothing - and completion is what makes a linked plan hand over.
            var built = new HashSet<string>();
            foreach (Building b in P.Buildings)
                if (IsRequired(b))
                    built.Add(b.Name);

            PercentCompleted = (int)(100f * built.Count / totalPlanned);

            if (Completed)
                ChangeTemplateIfLinked();
        }

        // How much of this plan this COLONY can actually end up with.
        //
        // ⚠ It used to count the empire's unlocked buildings and nothing else (maintainer, bench
        // 529), so it read the same on every world and promised a completion the colony could
        // never reach: the ground it stands on was not consulted, and neither were the mandates.
        // A plan holding military buildings on a colony whose Build Mandate is Economic Only
        // reported 100% achievable and then stalled for ever, with nothing on screen to say why.
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
                return;
            }

            var reachable = new HashSet<string>();
            foreach (Building b in P.Buildings)
                if (IsRequired(b))
                    reachable.Add(b.Name);

            foreach (Building b in P.GetBuildingsCanBuild())
                if (IsRequired(b) && (b.IsMilitary ? P.MayBuildMilitary : P.MayBuildCivilian))
                    reachable.Add(b.Name);

            PercentAchievable = (int)(100 * (float)reachable.Count / totalPlannedBuildings);
        }

        void ChangeTemplateIfLinked()
        {
            if (LinkedBlueprintsName != null
                && ResourceManager.TryGetBlueprints(LinkedBlueprintsName, out BlueprintsTemplate template))
            {
                ChangeTemplate(template);
            }
        }

        public void RefreshPlannedBuildingsWeCanBuild(IReadOnlyList<Building> buildingCanBuild)
        {
            PlannedBuildingsWeCanBuild.Clear();
            if (!P.HasOutpost && !P.HasCapital)
                return;

            // ⚠ walked in the PLAN's order, not the colony's. The plan is a chronology now and
            // this list is what the governor picks from, so its order IS the build order. Walking
            // the colony's own buildable list would hand back the same buildings arranged by
            // whatever that list happens to hold - which is how the plan had no say before.
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
        }

        public bool ShouldScrapNonRequiredBuilding()
        {
            return Exclusive ? ContainsNotRequiredBuildings() 
                             : P.FreeHabitableTiles == 0 
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