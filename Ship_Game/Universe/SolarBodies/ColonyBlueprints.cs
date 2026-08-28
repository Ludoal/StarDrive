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
        HashSet<string> PlannedBuildings => Template.PlannedBuildings;
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
            int totalRequiredBuilt = P.Buildings.ToArray().Count(IsRequired);
            float completion = (float)totalRequiredBuilt / PlannedBuildings.Count;
            PercentCompleted = (int)(completion * 100);

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

            for (int i = 0; i < buildingCanBuild.Count; i++)
            {
                Building building = buildingCanBuild[i];
                if (IsRequired(building))
                    PlannedBuildingsWeCanBuild.Add(building);
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