using System;
using System.Linq;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.AI.Budget;
using Ship_Game.Commands.Goals;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;

namespace Ship_Game
{
    public partial class Planet 
    {
        [StarData] public byte WantedPlatforms { get; private set; }
        [StarData] public byte WantedStations  { get; private set; }
        [StarData] public byte WantedShipyards { get; private set; }
        [StarData] public bool GovOrbitals      = false;
        // superseded by GovBuildMandate - kept so old saves load and migrate, nothing reads it
        [StarData] public bool GovGroundDefense = false;

        // Ludoal fork (maintainer feedback): the governor's building rights, split from its
        // means (the per-area budgets). Blocking a family by zeroing its budget destroys the
        // player's figures; a mandate suspends the right and leaves them stored.
        // Defaults reproduce vanilla conduct: it built civilian only, and never demolished
        // military at all.
        // Stored as ints, not as the enum: BuildMandate does not exist in vanilla, and a build
        // without the deleted-enum skip cannot read past a type it has never heard of. An int is
        // a fundamental type every build reads, so the save stays loadable downstream. The
        // initializers carry the defaults for a save written before this layout.
        // Set once the mandates have been seeded from the old flags; distinguishes a save
        // written before they existed from one where the player has already set them.
        // Initialized TRUE and left without a DefaultValue on purpose: a colony born in a new
        // game never passes through deserialization, so the initializer is what marks it done,
        // while a save written before this field reads the serializer's own false and seeds.
        [StarData] public bool MandatesSeeded = true;
        // DefaultValue states each mandate's default explicitly. A field equal to the writer's
        // default is never stored, and the reader assigns that same default back over the field
        // initializer - so without it a save that predates these fields would read All (0)
        // instead of the intended default, and hand the governor rights it never had.
        [StarData(DefaultValue = 1)] int GovBuildMandateValue = (int)BuildMandate.EconomicOnly;
        [StarData(DefaultValue = 3)] int GovScrapMandateValue = (int)BuildMandate.None;
        public BuildMandate GovBuildMandate
        {
            get => (BuildMandate)GovBuildMandateValue;
            set => GovBuildMandateValue = (int)value;
        }
        public BuildMandate GovScrapMandate
        {
            get => (BuildMandate)GovScrapMandateValue;
            set => GovScrapMandateValue = (int)value;
        }
        [StarData] public bool AutoBuildTroops  = false;
        [StarData] public bool ManualOrbitals   = false;
        [StarData] public int GarrisonSize;
        // Ludoal fork (maintainer feedback): manual/auto is a flag PER AREA, so the three
        // amounts are just amounts - zero included - and one area can be manual while the
        // others stay automatic. The state used to be inferred from the values (0 meant auto),
        // which made "spend nothing here" impossible to express and forced a 0.01 floor in
        // the UI. Old saves carry no flag: any stored amount means that area was manual.
        [StarData] public bool ManualCivBudgetOn { get; private set; }
        [StarData] public bool ManualGrdBudgetOn { get; private set; }
        [StarData] public bool ManualSpcBudgetOn { get; private set; }
        [StarData] public float ManualCivilianBudget { get; private set; }
        [StarData] public float ManualGrdDefBudget   { get; private set; }
        [StarData] public float ManualSpcDefBudget   { get; private set; }

        private void BuildPlatformsAndStations(PlanetBudget budget) // Rewritten by Fat Bastard
        {
            // Ludoal fork (maintainer feedback): the trade hub no longer stops orbital
            // construction either - that is the Gov. Manages Space Defense toggle's job.
            if (CType == ColonyType.Colony
                || OwnerIsPlayer && !GovOrbitals
                || SpaceCombatNearPlanet
                || !HasSpacePort)
            {
                return;
            }

            int rank             = GetColonyRank();
            var currentPlatforms = FilterOrbitals(RoleName.platform);
            var currentStations  = FilterOrbitals(RoleName.station);
            UpdateWantedOrbitals(rank);

            BuildOrScrapShipyard(WantedShipyards, budget.RemainingSpaceDef);
            BuildOrScrapStations(currentStations, WantedStations, budget.RemainingSpaceDef, budget.SpaceDefTolerance);
            BuildOrScrapPlatforms(currentPlatforms, WantedPlatforms, budget.RemainingSpaceDef, budget.SpaceDefTolerance);
        }

        public int GetColonyRank()
        {
            int rank = (int)Math.Round(ColonyValue/Owner.MaxColonyValue * 10, 0);
            return ApplyRankModifiers(rank);
        }

        void BuildOrScrapStations(Array<Ship> orbitals, byte wanted, float budget, float tolerance)
            => BuildOrScrapOrbitals(orbitals, wanted, RoleName.station, budget, tolerance);

        void BuildOrScrapPlatforms(Array<Ship> orbitals, byte wanted, float budget, float tolerance)
            => BuildOrScrapOrbitals(orbitals, wanted, RoleName.platform, budget, tolerance);

        // Ludoal fork (maintainer feedback): the Scrap Mandate absorbed the old
        // "Governor Will Not Scrap Buildings" toggle - None says the same thing, and one
        // control per decision beats two that must agree.
        bool GovernorShouldNotScrapBuilding => !MayScrapCivilian;

        // The mandates bind the player's governors only: an AI runs its own empire.
        bool MayBuild(BuildMandate mandate, bool military)
        {
            if (!OwnerIsPlayer)
                return true;

            switch (mandate)
            {
                case BuildMandate.All:          return true;
                case BuildMandate.EconomicOnly: return !military;
                case BuildMandate.DefenseOnly:  return military;
                default:                        return false;
            }
        }

        public bool MayBuildMilitary  => MayBuild(GovBuildMandate, military: true);
        public bool MayBuildCivilian  => MayBuild(GovBuildMandate, military: false);
        public bool MayScrapMilitary  => MayBuild(GovScrapMandate, military: true);
        public bool MayScrapCivilian  => MayBuild(GovScrapMandate, military: false);

        private Array<Ship> FilterOrbitals(RoleName role)
        {
            var orbitalList = new Array<Ship>();
            foreach (Ship orbital in OrbitalStations)
            {
                if (orbital.ShipData.Role == role && !orbital.ShipData.IsShipyard  // shipyards are not defense stations
                                                  && !orbital.IsConstructor)
                {
                    orbitalList.Add(orbital);
                }
            }
            return orbitalList;
        }

        public int OrbitalsBeingBuilt(RoleName role) => OrbitalsBeingBuilt(role, Owner);

        int OrbitalsBeingBuilt(RoleName role, Empire owner)
        {
            if (owner == null)
                return 0;

            // this also counts construction ships on the way, by checking the empire goals
            int numOrbitals = 0;
            var goals = owner.AI.Goals;
            for (int i = 0; i < goals.Count; i++)
            {
                Goal g = goals[i];
                if (g is DeepSpaceBuildGoal bg && bg.IsBuildingOrbitalFor(this))
                {
                    IShipDesign orbital = bg.ToBuild;
                    if (orbital.Role == role && !orbital.IsShipyard)
                        ++numOrbitals;
                }
            }

            return numOrbitals;
        }

        public int ShipyardsBeingBuilt() => ShipyardsBeingBuilt(Owner);

        private int ShipyardsBeingBuilt(Empire owner)
        {
            if (owner == null)
                return 0;

            int shipyardsInQ = owner.AI.CountGoals(g => g is DeepSpaceBuildGoal b
                                                     && b.IsBuildingOrbitalFor(this)
                                                     && b.ToBuild.IsShipyard);
            return shipyardsInQ;
        }

        private void BuildOrScrapOrbitals(Array<Ship> orbitalList, byte orbitalsWeWant, RoleName role, float budget, float tolerance)
        {
            int orbitalsWeHave = orbitalList.Filter(o => !o.ShipData.IsShipyard).Length + OrbitalsBeingBuilt(role);
            if (IsPlanetExtraDebugTarget())
                Log.Info($"{role}s we have: {orbitalsWeHave}, {role}s we want: {orbitalsWeWant}");
            var eAI = Owner.AI;

            if (orbitalList.NotEmpty && (orbitalsWeHave > orbitalsWeWant || budget < tolerance))
            {
                Ship weakest = orbitalList.FindMin(s => s.BaseStrength);
                if (weakest != null)
                    ScrapOrbital(weakest);
                return;
            }

            if (budget > 0)
            {
                if (orbitalsWeHave < orbitalsWeWant) // lets build an orbital
                    BuildOrbital(role, budget);
                else if (orbitalList.Count > 0)
                    ReplaceOrbital(orbitalList, role, budget);  // check if we can replace an orbital with a better one
            }
        }

        private void ScrapOrbital(Ship orbital)
        {
            float expectedStorage = Storage.Prod + orbital.GetCost(Owner) / 2;
            if (expectedStorage > Storage.Max) // taxed excess cost will go to empire treasury
            {
                Storage.Prod = Storage.Max;
                Owner.AddMoney((expectedStorage - Storage.Max) * Owner.data.TaxRate);
            }
            else
            {
                Storage.Prod = expectedStorage;
            }

            if (IsPlanetExtraDebugTarget())
                Log.Info(ConsoleColor.Magenta,$"{Name}, {Owner.Name} - SCRAPPED Orbital ----- {orbital.Name}" +
                         $", STR: {orbital.BaseStrength}");

            orbital.QueueTotalRemoval();
        }

        private void BuildOrbital(RoleName role, float budget)
        {
            if (OrbitalsInTheWorks)
                return;

            IShipDesign orbital = PickOrbitalToBuild(role, budget);
            if (orbital == null)
                return;

            AddOrbital(orbital);
        }

        private int TimeVsCostThreshold => (int)(40 + EstimatedAverageProduction*Level + Owner.Money/250);

        // Adds an Orbital to ConstructionQueue
        public void AddOrbital(IShipDesign orbital)
        {
            if (IsPlanetExtraDebugTarget())
                Log.Info(ConsoleColor.Green,$"{Name}, {Owner.Name} - ADDED Orbital ----- {orbital.Name}, " +
                         $"cost: {orbital.GetCost(Owner)}, STR: {orbital.BaseStrength}");

            Goal buildOrbital = new BuildOrbital(this, orbital.Name, Owner);
            Owner.AI.AddGoal(buildOrbital);
        }

        private void ReplaceOrbital(Array<Ship> orbitalList, RoleName role, float budget)
        {
            if (orbitalList.IsEmpty || OrbitalsInTheWorks)
                return;

            Ship weakestWeHave = orbitalList.FindMin(s => s.BaseStrength);
            if (weakestWeHave.AI.State == AIState.Refit)
                return; // refit one orbital at a time

            float weakestMaint  = weakestWeHave.GetMaintCost(Owner);
            IShipDesign bestWeCanBuild = PickOrbitalToBuild(role, budget + weakestMaint);

            if (bestWeCanBuild == null)
                return;

            if (bestWeCanBuild.BaseStrength.Less(weakestWeHave.BaseStrength * 1.1f))
                return; // replace only if str is 10% more than the current weakest orbital

            string debugReplaceOrRefit;
            if (weakestWeHave.DesignRole == bestWeCanBuild.Role)
            {
                Goal refitOrbital = new RefitOrbital(weakestWeHave, bestWeCanBuild, Owner);
                Owner.AI.AddGoalAndEvaluate(refitOrbital);
                debugReplaceOrRefit = "REFITTING";
            }
            else
            {
                ScrapOrbital(weakestWeHave);
                AddOrbital(bestWeCanBuild);
                debugReplaceOrRefit = "REPLACING";
            }

            if (IsPlanetExtraDebugTarget())
                Log.Info(ConsoleColor.Cyan, $"{Name}, {Owner.Name} - {debugReplaceOrRefit} Orbital ----- {weakestWeHave.Name}" +
                         $" with {bestWeCanBuild.Name}, STR: {weakestWeHave.BaseStrength} to {bestWeCanBuild.BaseStrength}");
        }

        private IShipDesign PickOrbitalToBuild(RoleName role, float budget)
        {
            IShipDesign orbital = GetBestOrbital(role, budget);
            if (IsPlanetExtraDebugTarget())
                Log.Info($"Orbitals Budget: {budget}");

            if (orbital != null)
            {
                // If we can build the selected orbital in a timely, select it.
                if (LogicalBuiltTimeVsCost(orbital.GetCost(Owner), TimeVsCostThreshold))
                    return orbital;
            }

            // We cannot build the best in the empire, lets try building something cheaper for now
            // and check if this can be built in a timely manner.
            float maxCost = EstimatedAverageProduction * TimeVsCostThreshold + Storage.Prod;
            maxCost /= ShipCostModifier;
            orbital = GetBestOrbital(role, budget, maxCost);

            return orbital;
        }

        // This returns the best orbital the empire can build
        private IShipDesign GetBestOrbital(RoleName role, float budget)
        {
            if (budget < 0)
                return null;

            IShipDesign orbital = null;
            switch (role)
            {
                case RoleName.platform: orbital = Owner.BestPlatformWeCanBuild; break;
                case RoleName.station: orbital = Owner.BestStationWeCanBuild; break;
            }

            if (orbital != null)
            {
                budget = (float)Math.Ceiling(budget);
                float cost = orbital.GetMaintenanceCost(Owner);
                if (cost > budget)
                    orbital = null;
            }
            return orbital;
        }

        //This returns the best orbital the Planet can build based on cost
        IShipDesign GetBestOrbital(RoleName role, float budget, float maxCost)
        {
            IShipDesign orbital = null;
            switch (role)
            {
                case RoleName.station:
                case RoleName.platform: orbital = ShipBuilder.PickCostEffectiveShipToBuild(role, Owner, maxCost, budget); break;
            }
            return orbital;
        }

        private bool LogicalBuiltTimeVsCost(float cost, int threshold)
        {
            float netCost = (cost - Storage.Prod).LowerBound(0) * ShipCostModifier;
            float ratio   = netCost / EstimatedAverageProduction;
            return ratio < threshold;
        }

        int ApplyRankModifiers(int currentRank)
        {
            int rank = currentRank + ((int)(Owner.Money / 10000)).Clamped(-3, 3);
            if      (Owner.Money < 500)  rank -= 2;
            else if (Owner.Money < 1000) rank -= 1;

            if (MaxPopulationBillion.LessOrEqual(3))
                rank -= 2;

            switch (CType)
            {
                case ColonyType.Core:     rank += 1; break;
                case ColonyType.Military: rank += 3; break;
            }

            rank += Owner.DifficultyModifiers.ColonyRankModifier;
            return rank.Clamped(0, 15);
        }

        private void BuildOrScrapShipyard(int numWantedShipyards, float budget)
        {
            if (numWantedShipyards == 0 || OrbitalsInTheWorks
                                        || !Owner.CanBuildShip(Owner.data.DefaultShipyard)
                                        || !HasSpacePort)
            {
                return;
            }

            int totalShipyards = NumShipyards + ShipyardsBeingBuilt();
            if (totalShipyards < numWantedShipyards)
            {
                string shipyardName = Owner.data.DefaultShipyard;
                if (ResourceManager.Ships.GetDesign(shipyardName, out IShipDesign shipyard)
                    && shipyard.GetMaintenanceCost(Owner) < budget
                    && LogicalBuiltTimeVsCost(shipyard.GetCost(Owner), TimeVsCostThreshold))
                {
                    AddOrbital(shipyard);
                }
            }
            else if (totalShipyards > numWantedShipyards)
            {
                if (!Construction.CancelShipyard())
                {
                    Ship shipyard = OrbitalStations.Where(o => o.ShipData.IsShipyard).LastOrDefault();
                    if (shipyard != null)
                        ScrapOrbital(shipyard);
                    else
                        Log.Warning("BuildOrScrapShipyard: could not find shipyard in OrbitalStations.");
                }
            }
        }

        public int NumPlatforms => FilterOrbitals(RoleName.platform).Count;
        public int NumStations  => FilterOrbitals(RoleName.station).Count;

        // extraPending: orbitals already queued in the same batch that the marshalled goal-add
        // (RunOnSimThread) hasn't applied to the empire goals list yet, so OrbitalsBeingBuilt/
        // ShipyardsBeingBuilt still under-counts them. Callers that enqueue in a tight loop pass it.
        // Assumes a homogeneous batch (one design repeated): extraPending is added to both the orbital
        // and shipyard counts, which is only safe because the unused count's gate can't fire for that
        // design (a platform never trips the shipyard branch). Mixed-design batches would miscount.
        public bool IsOutOfOrbitalsLimit(IShipDesign ship, int extraPending = 0) => IsOutOfOrbitalsLimit(ship, Owner, 0, extraPending);
        public bool IsOverOrbitalsLimit(IShipDesign ship)  => IsOutOfOrbitalsLimit(ship, Owner, 1, 0);

        bool IsOutOfOrbitalsLimit(IShipDesign ship, Empire owner, int overLimit, int extraPending)
        {
            int numOrbitals  = OrbitalStations.Count + OrbitalsBeingBuilt(ship.Role, owner) + extraPending;
            int numShipyards = OrbitalStations.Count(s => s.ShipData.IsShipyard) + ShipyardsBeingBuilt(owner) + extraPending;
            if (numOrbitals >= ShipBuilder.OrbitalsLimit + overLimit && ship.IsPlatformOrStation)
                return true;

            if (numShipyards >= ShipBuilder.ShipYardsLimit + overLimit && ship.IsShipyard)
                return true;

            return false;
        }

        // Used when mostly the player places orbital in orbit of unowned planet
        public void TryRemoveExcessOrbital(Ship orbital)
        {
            if (Owner == orbital.Loyalty || !IsOverOrbitalsLimit(orbital.ShipData))
                return;

            float cost = orbital.GetCost(orbital.Loyalty) * orbital.Loyalty.DifficultyModifiers.CreditsMultiplier;
            orbital.Loyalty.AddMoney(cost);
            if (orbital.Loyalty == Universe.Player)
                Universe.Notifications.AddOrbitalOverLimit(this, (int)cost, orbital.BaseHull.IconPath);

            orbital.QueueTotalRemoval();
        }

        public void BuildTroopsForEvents()
        {
            if (Troops.Count > 0 || OwnerIsPlayer || TroopsInTheWorks || !EventsOnTiles())
                return;

            if (CanBuildInfantry)
                BuildSingleMilitiaTroop();
        }

        public void BuildTroops() // Relevant only for players with the Garrison Checkbox checked.
        {
            if (!OwnerIsPlayer || !AutoBuildTroops || RecentCombat)
                return;

            int numTroopsInTheWorks = NumTroopsInTheWorks;
            if (numTroopsInTheWorks > 0)
                return; // We are already building troops

            int troopsWeHave = Troops.Count; // No need to filter our troops here since the planet must not be in RecentCombat
            if (troopsWeHave < GarrisonSize && GetFreeTiles(Owner) > 0)
            {
                if (CanBuildInfantry)
                    BuildSingleMilitiaTroop();
                else
                    TryBuildMilitaryBase();
            }
        }

        void TryBuildMilitaryBase()
        {
            if (MilitaryBuildingInTheWorks)
                return;

            var cheapestInfantryBuilding = BuildingsCanBuild.FindMinFiltered(b => b.AllowInfantry, b => b.ActualCost(Owner));
            if (cheapestInfantryBuilding != null)
                Construction.Enqueue(cheapestInfantryBuilding);
        }

        void BuildSingleMilitiaTroop()
        {
            if (TroopsInTheWorks)
                return;  // Build one militia at a time

            // CanBuildInfantry only checks the building flag; the empire may
            // still have zero unlocked troop templates (no troop tech, or a
            // race whose troops are all locked in the active mod). Skip the
            // build instead of throwing inside the sim thread.
            Troop[] templates = ResourceManager.GetTroopTemplatesFor(Owner);
            if (templates.Length == 0)
                return;
            Construction.Enqueue(templates[0], QueueItemType.Troop);
        }

        void BuildAndScrapMilitaryBuildings(float budget, float tolerance)
        {
            // Ludoal fork (maintainer feedback): the single GovGroundDefense gate used to bar
            // building AND scrapping at once. The two rights are separate commands now, so the
            // gate is split: a family the governor may not build is not one it may demolish.
            // Blueprints keep overriding both - an explicit plan is an explicit order.
            bool mayBuild = MayBuildMilitary || HasBlueprints;
            bool mayScrap = MayScrapMilitary || HasBlueprints;

            if (MilitaryBuildingInTheWorks || !mayBuild && !mayScrap)
                return;

            if (budget < tolerance && mayScrap)
            {
                TryScrapMilitaryBuilding();
                return;
            }

            if ((HasExclusiveBlueprints || HasBlueprints && !Blueprints.Exclusive && FreeHabitableTiles == 0 && !Blueprints.IsAchievableCompleted)
                && BuildingList.Any(b => b.IsMilitary && !RequiredInBlueprints(b)))
            {
                if (mayScrap)
                    TryScrapMilitaryBuilding();
            }
            else if (mayBuild)
            {
                TryBuildMilitaryBuilding(budget);
            }
        }

        void TryBuildMilitaryBuilding(float budget)
        {
            if (FreeHabitableTiles == 0)
                return;

            Building best = null;
            if (HasBlueprints)
            {
                best = BuildingsCanBuild.FindMaxFiltered(b => b.IsMilitary && RequiredInBlueprints(b) && b.ActualMaintenance(this) <= budget,
                                                         b => b.CostEffectiveness);
            }

            if (best == null)
            {
                if (!HasExclusiveBlueprints && MayBuildMilitary)
                    best = BuildingsCanBuild.FindMaxFiltered(b => b.IsMilitary && b.ActualMaintenance(this) <= budget,
                                                             b => b.CostEffectiveness);
            }

            if (best != null)
                Construction.Enqueue(best);
        }
        
        void TryScrapMilitaryBuilding()
        {
            Building weakest = null;
            if (HasBlueprints)
            {
                weakest = BuildingList.FindMinFiltered(b => b.IsMilitary && b.Scrappable && !RequiredInBlueprints(b),
                                                       b => b.CostEffectiveness);
            }

            if (weakest == null)
                weakest = BuildingList.FindMinFiltered(b => b.IsMilitary && b.Scrappable && !b.IsPlayerAdded,
                                                       b => b.CostEffectiveness);

            if (weakest != null)
                ScrapBuilding(weakest);
        }

        public void AddTroop(Troop troop, PlanetGridSquare tile)
        {
            Troops.AddTroop(tile, troop);
            troop.SetPlanet(this);
        }

        public void UpdateWantedOrbitals(int rank)
        {
            if (ManualOrbitals)
                return;

            switch (rank)
            {
                case 1:  WantedPlatforms = 0; WantedStations = 0; WantedShipyards = 0; break;
                case 2:  WantedPlatforms = 0; WantedStations = 0; WantedShipyards = 0; break;
                case 3:  WantedPlatforms = 3; WantedStations = 1; WantedShipyards = 0; break;
                case 4:  WantedPlatforms = 3; WantedStations = 1; WantedShipyards = 1; break;
                case 5:  WantedPlatforms = 4; WantedStations = 2; WantedShipyards = 2; break;
                case 6:  WantedPlatforms = 4; WantedStations = 2; WantedShipyards = 3; break;
                case 7:  WantedPlatforms = 5; WantedStations = 3; WantedShipyards = 3; break;
                case 8:  WantedPlatforms = 5; WantedStations = 3; WantedShipyards = 3; break;
                case 9:  WantedPlatforms = 6; WantedStations = 3; WantedShipyards = 3; break;
                case 10: WantedPlatforms = 7; WantedStations = 4; WantedShipyards = 3; break;
                case 11: WantedPlatforms = 8; WantedStations = 4; WantedShipyards = 3; break;
                case 12: WantedPlatforms = 9; WantedStations = 5; WantedShipyards = 3; break;
                case 13: WantedPlatforms = 9; WantedStations = 5; WantedShipyards = 3; break;
                case 14: WantedPlatforms = 9; WantedStations = 6; WantedShipyards = 3; break;
                case 15: WantedPlatforms = 9; WantedStations = 6; WantedShipyards = 3; break;
                default: WantedPlatforms = 0; WantedStations = 0; WantedShipyards = 0; break;
            }

            // Research planets are not a good platform for building ships
            if (CType == ColonyType.Research)
                WantedShipyards = 0;

            if (!Owner.IsAtWarWithMajorEmpire)
            {
                WantedPlatforms /= 2;
                WantedStations  /= 2;
            }
        }

        public void SetWantedPlatforms(byte num)
        {
            WantedPlatforms = num;
        }

        public void SetWantedShipyards(byte num)
        {
            WantedShipyards = num;
        }

        public void SetWantedStations(byte num)
        {
            WantedStations = num;
        }


        public void SetBuildMandate(BuildMandate mandate) => GovBuildMandate = mandate;
        public void SetScrapMandate(BuildMandate mandate) => GovScrapMandate = mandate;

        public void SetManualCivBudgetOn(bool manual) => ManualCivBudgetOn = manual;
        public void SetManualGrdBudgetOn(bool manual) => ManualGrdBudgetOn = manual;
        public void SetManualSpcBudgetOn(bool manual) => ManualSpcBudgetOn = manual;

        public void SetManualCivBudget(float num)
        {
            ManualCivilianBudget = num;
        }

        public void SetManualGroundDefBudget(float num)
        {
            ManualGrdDefBudget = num;
        }

        public void SetManualSpaceDefBudget(float num)
        {
            ManualSpcDefBudget = num;
        }
    }
}