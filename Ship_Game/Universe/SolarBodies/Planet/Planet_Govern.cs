using SDGraphics;
using Ship_Game.AI.Budget;
using System.Linq;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Universe.SolarBodies;
using System.Collections.Generic;

namespace Ship_Game
{
    public partial class Planet
    {
        public bool GovernorOn  => CType != ColonyType.Colony;
        public bool GovernorOff => CType == ColonyType.Colony;
        int NumHabitableTiles => TilesList.Count(t => t.Habitable); // Bioshperes are counted here
        public float CurrentProductionToQueue => Prod.NetIncome + InfraStructure;
        public float EstimatedAverageProduction => (Prod.NetMaxPotential / (IsCybernetic ? 2 : 3)).LowerBound(0.1f);
        float EstimatedAverageFood => (Food.NetMaxPotential / 3).LowerBound(0.1f);

        [StarData] public ColonyBlueprints Blueprints {get; private set;}

        // Ludoal fork: this colony defers its plan to the empire's default for its governor type
        // (Policies > Colony). A plain bool, and NEW - so an older save has no value for it, every
        // existing colony reads false, and nothing can quietly slip under a policy the player never
        // set. Custom is still what having a plan without this flag means; None is neither.
        //
        // It survives the plan itself on purpose: changing the governor type to one that cannot
        // carry a plan wipes the blueprints, and that wipe is MECHANICAL, not a choice the player
        // made. The flag stays, the colony resolves to no plan, and it follows its row again the
        // day it goes back to a type that can carry one.
        [StarData] public bool GovBlueprintAuto;

        // Ludoal fork: is this colony's LABOUR managed for it? Auto means yes - by the governor
        // if it has one, by the sustenance pilot if it does not.
        //
        // THREE states in one int, and the third is the point. The default differs by status
        // (governed colonies are managed today, free ones are not), so no single bool default
        // can reproduce both from a save that predates the field: every governed colony would
        // load with its labour handed back to the player. 0 means "never set - follow the
        // status", so an older save behaves exactly as it did. 0 never reaches the screen: the
        // toggle shows ON or OFF, and the player's first click leaves the undecided state for
        // good. A stored choice then OUTLIVES a change of status - assigning a governor to a
        // colony the player set to OFF gives it the build queues, not the labour.
        [StarData] int LaborAutoValue; // 0 = never set, 1 = on, 2 = off

        // The labour share the sustenance pilot holds to keep this colony alive - food for
        // flesh, production for Opteris. Saved because the player's own share is measured FROM
        // it: on a cybernetic colony the floor and the player's surplus live inside the same
        // production number, and once the floor moves there is no way to tell them apart
        // without knowing where it used to be.
        [StarData] public float SubsistenceFloor { get; private set; }

        // A colony with a governor has its whole labour split managed; one without has only its
        // sustenance held. The two types that carry no governor are the two that carry no plan.
        public bool HasLaborGovernor => CType is not (ColonyType.Colony or ColonyType.TradeHub);

        public bool AutoLabor
        {
            get => LaborAutoValue switch
            {
                1 => true,
                2 => false,
                _ => HasLaborGovernor,
            };
            set => LaborAutoValue = value ? 1 : 2;
        }

        public bool HasBlueprints => Blueprints != null;
        public bool HasExclusiveBlueprints => Blueprints?.Exclusive == true;
        bool RequiredInBlueprints(Building b) => Blueprints?.IsRequired(b) == true;

        public void DoGoverning()
        {
            RefreshBuildingsWeCanBuildHere();
            UpdateBiospheresBeingBuilt();
            if (RecentCombat)
                return; // Cant Build stuff when there is combat on the planet

            BuildTroops();
            BuildTroopsForEvents(); // For AI to explore event in colony
            TryBuildTerraformers(TerraformBudget); // Build Terraformers if needed/enabled
            TryBuildDysonSwarm();

            // If there is no Outpost or Capital, build it. This is done for non governor planets as well
            BuildOutpostOrCapitalIfAble();

            // Ludoal fork (wishlist, auto-supplies): the Trade Hub role is retired - its
            // flux duty is exactly what the Auto toggles do on a governorless colony.
            // Old saves land there with identical behaviour (toggles default to Auto).
            if (CType == ColonyType.TradeHub)
                CType = ColonyType.Colony;

            if (CType == ColonyType.Colony)
            {
                if (AutoLabor)
                    AutoAssignSustenance(); // no governor, but the labour is still managed
                ManageSupplyStates(); // flux is decoupled from governance: it runs here too
                return; // No Governor? Only construction minds.
            }

            // Switch to Core for AI if there is nothing in the research queue (Does not actually change assigned Governor)
            if ((!OwnerIsPlayer || Owner.AutoResearch) && CType == ColonyType.Research && Owner.Research.NoTopic)
                CType = ColonyType.Core;

            // Change to core colony if there is only 1 planet so the AI can build stuff
            if (!OwnerIsPlayer && Owner.GetPlanets().Count == 1)
                CType = ColonyType.Core;

            // Ludoal fork: read ONCE, here - the reset below and every assignment in the switch
            // answer to the same value, or a colony could be wiped by one and left unassigned by
            // the other. With Auto off, the governor still builds and scraps; the labour split
            // stays exactly where the player put it, which means NOT zeroing it each turn.
            bool governsLabor = AutoLabor;
            if (CType != ColonyType.TradeHub && governsLabor)
            {
                Food.Percent = 0;
                Prod.Percent = 0;
                Res.Percent = 0;
            }

            CreateAndOrUpdateBudget();
            switch (CType) // New resource management by Gretman
            {
                // case ColonyType.TradeHub: retired role (auto-supplies) - flux-only
                //     governance is now the Auto toggles on a governorless colony. Kept
                //     here in case the role returns with another function some day.
                case ColonyType.Core:
                    BuildAndScrapBuildings(Budget);
                    if (governsLabor) AssignCoreWorldWorkers();
                    break;
                case ColonyType.Industrial:
                    BuildAndScrapBuildings(Budget);
                    // Farm to 30% storage, then devote the rest to Work, then to research when that starts to fill up
                    if (governsLabor) AssignOtherWorldsWorkers(0.33f, 1, 0, 2);
                    break;
                case ColonyType.Research:
                    //This governor will rely on imports, focusing on research as long as no one is starving
                    BuildAndScrapBuildings(Budget);
                    if (governsLabor) AssignOtherWorldsWorkers(0.15f, 0.15f, 0, 0);
                    break;
                case ColonyType.Agricultural:
                    BuildAndScrapBuildings(Budget);
                    if (governsLabor) AssignOtherWorldsWorkers(1, 0.333f, Storage.Max - Storage.Food , 0);
                    break;
                case ColonyType.Military:
                    BuildAndScrapBuildings(Budget);
                    if (governsLabor) AssignOtherWorldsWorkers(0.3f, 0.7f, 0, 1.5f);
                    break;
            }

            ManageSupplyStates(); // after the switch: an AI type reassignment above must use its final thresholds
            BuildPlatformsAndStations(Budget);
        }

        // Ludoal fork (wishlist, auto-supplies): the flux states, decoupled from governance.
        // Runs for EVERY colony each governing tick; each resource is gated by its Auto
        // toggle - a manual resource is never overwritten, the player's override HOLDS.
        // Governor types keep their tuned thresholds (unchanged numbers, extracted from the
        // old switch); a governorless colony uses the retired Trade Hub's neutral pair.
        void ManageSupplyStates()
        {
            if (!FoodManual)
            {
                (float imp, float exp) = CType switch
                {
                    ColonyType.Core         => (0.2f, 0.5f),
                    ColonyType.Industrial   => (0.75f, 0.99f),
                    ColonyType.Research     => (0.75f, 0.95f),
                    ColonyType.Agricultural => (0.1f, 0.2f),
                    ColonyType.Military     => (0.75f, 1f), // Import if it drops below 75%; only exports excess FlatFood
                    _                       => (0.2f, 0.8f),
                };
                DetermineFoodState(imp, exp);
            }
            if (!ProdManual)
            {
                (float imp, float exp) = CType switch
                {
                    ColonyType.Core         => (0.2f, 0.5f),
                    ColonyType.Industrial   => NonCybernetic ? (0f, 0.5f) : (0.2f, 0.66f),
                    ColonyType.Research     => (0.25f, 0.95f),
                    ColonyType.Agricultural => (0.25f, 0.95f),
                    ColonyType.Military     => (0.75f, 0.95f),
                    _                       => (0.2f, 0.8f),
                };
                DetermineProdState(imp, exp);
            }
        }

        void UpdateBiospheresBeingBuilt()
        {
            BiosphereInTheWorks = BuildingInQueue(Building.BiospheresId);
        }

        public float CivilianBuildingsMaintenance  => Money.Maintenance - GroundDefMaintenance;

        public float GetColonyInitialBudgetTolerance()
        {
            if (Owner == null || GovernorOff || MaxPopBillionNoBuildingBonus >= 5f || PopulationBillion > 5f)
                return 0;

            float ratio = 0.1f * (5 - PopulationBillion); // bigger pop = less tolerance - between 0 and 0.5
            float fertilityBonus = IsCybernetic ? 0 : MaxFertility;
            float richnessBonus = IsCybernetic ? MineralRichness : MineralRichness * 0.5f;
            switch (CType)
            {
                case ColonyType.Agricultural: fertilityBonus *= 1.5f; break;
                case ColonyType.Industrial:   richnessBonus  *= 1.5f; break;
                case ColonyType.Core:         richnessBonus  *= 1.25f; fertilityBonus *= 1.25f; break;
            }

            float totalTolerancePerTile = (richnessBonus + fertilityBonus) * ratio;
            float lowMoneyRatio = (Owner.Money * 0.0005f).Clamped(0, 1); // money / 2000
            float total = totalTolerancePerTile * NumHabitableTiles * lowMoneyRatio; // Biospheres will increase tolerance
            return total.LowerBound(0);
        }

        void BuildAndScrapBuildings(PlanetBudget colonyBudget)
        {
            // Ludoal fork (maintainer feedback): Specialized Trade Hub used to silently stop
            // all construction here. What the governor may build is its own command now, so
            // the hub carries the trade regime alone.
            BuildAndScrapCivilianBuildings(colonyBudget.RemainingCivilian, colonyBudget.CivilianTolerance);
            BuildAndScrapMilitaryBuildings(colonyBudget.RemainingGroundDef, colonyBudget.GroundDefTolerance);
        }

        // returns the amount of production to spend in the build queue based on import/export state
        public float LimitedProductionExpenditure(float availableProductionToQueue)
        {
            float prodToSpend;
            float prodIncome = Prod.NetIncome > 0 ? Prod.NetIncome : InfraStructure.UpperBound(Storage.Prod);
            bool empireCanExport = Owner.TotalProdExportSlots - FreeProdExportSlots > Level.LowerBound(3);
            if (CType == ColonyType.Colony)
            {
                switch (PS)
                {
                    default: // Importing
                        prodToSpend = ProdHere; // we are manually importing, so let's spend it all
                        break;
                    case GoodState.STORE:
                        if (Storage.ProdRatio.AlmostEqual(1))
                            prodToSpend = prodIncome; // Spend all our Income since storage is full
                        else
                            prodToSpend = prodIncome * 0.5f; // Store 50% of our prod income
                        break;
                    case GoodState.EXPORT:
                        if (OutgoingProdFreighters > 0)
                            prodToSpend = prodIncome * Storage.ProdRatio; // We are actively exporting so save some for storage
                        else
                            prodToSpend = ProdHere * 0.5f; // Spend 50% from our current stores and net production
                        break;
                }
            }
            else // Governor is auto managing good state
            {
                switch (PS)
                {
                    default: // Importing
                        if (!empireCanExport)
                        {
                            prodToSpend = prodIncome; 
                            break;
                        }

                        if (IncomingProdFreighters > 0)
                            prodToSpend = ProdHere + prodIncome; // We have incoming prod, so we can spend more now
                        else
                            prodToSpend = prodIncome + Storage.ProdRatio*2; // Spend less since nothing is coming
                        break;
                    case GoodState.STORE:
                        if (empireCanExport)
                        {
                            prodToSpend = prodIncome + 10; // Our empire has open export slots, so we can allow storage to dwindle
                            break;
                        }
                        if (Storage.ProdRatio.AlmostEqual(1))
                            prodToSpend = prodIncome; // Spend all our Income since storage is full
                        else
                            prodToSpend = prodIncome * 0.5f; // Store 50% of our prod income
                        break;
                    case GoodState.EXPORT:
                        if (empireCanExport)
                        {
                            prodToSpend = ProdHere; // Our empire has open export slots, so we can allow storage to dwindle
                            break;
                        }

                        if (Storage.ProdRatio > 0.8f)
                            prodToSpend = prodIncome + Storage.Prod * 0.1f; // We are actively exporting but can afford some storage spending
                        else
                            prodToSpend = prodIncome * Storage.ProdRatio; // We are actively exporting so save some for storage
                        break;
                }
            }

            if (IsStarving && Construction.FirstItemCanFeedUs())
                prodToSpend = ProdHere;

            // if we have negative NetIncome (cybernetics)  - we try to take amonut of Infra (if available) to continue building the queue
            float upperBound = Prod.NetIncome <= 0 ? prodIncome : availableProductionToQueue;
            return prodToSpend.UpperBound(upperBound);
        }

        void CreateAndOrUpdateBudget()
        {
            if (Budget == null || Owner != Budget.Owner)
                CreatePlanetBudget(Owner);

            Budget.Update();
        }

        public bool BestCivilianBuildingToBuildDifferentThen(IReadOnlyList<Building> buildings, Building queuedBuilding)
        {
            CreateAndOrUpdateBudget();
            Array<Building> updatedBuildings = GetBuildingsListToChooseFrom(buildings).ToArrayList();
            updatedBuildings.Add(queuedBuilding);
            float civilianBudget = Budget.RemainingCivilian + queuedBuilding.ActualMaintenance(this);
            ChooseBestBuilding(updatedBuildings, civilianBudget, replacing: false, out Building bestBuilding);
            return bestBuilding != null && bestBuilding.Name != queuedBuilding.Name;
        }

        IReadOnlyList<Building> GetBuildingsListToChooseFrom(IReadOnlyList<Building> buildingsCanBuild)
        {
            if (!HasBlueprints)
                return buildingsCanBuild;

            if (Blueprints.Exclusive) // build only blueprints buildings, even if nothing can be built
                return Blueprints.PlannedBuildingsWeCanBuild;

            if (Blueprints.PlannedBuildingsWeCanBuild.Count == 0)
                return buildingsCanBuild; // build whatever we can if no blueprints building available
            else 
                return Blueprints.PlannedBuildingsWeCanBuild; // priorizite blueprints buildings
        }

        public void RemoveBlueprints()
        {
            Blueprints = null;
        }

        public void AddBlueprints(BlueprintsTemplate template, Empire owner)
        {
            Blueprints = new ColonyBlueprints(template, this, Owner);
        }

        public void DestroyBuildingInUprise(UpriseBuildingType type, out string buildingNameDestroyed)
        {
            buildingNameDestroyed = "";
            if (type is UpriseBuildingType.None)
                return;

            Building[] potentialBuildings = BuildingList.Filter(b => b.Scrappable && !b.IsBiospheres);
            if (potentialBuildings.Length == 0)
                return;

            switch (type) 
            {
                case UpriseBuildingType.HighestPrice: 
                    potentialBuildings = potentialBuildings.SortedDescending(b => b.Cost).Take(5).ToArray();
                    break;
                case UpriseBuildingType.Storage:      
                    potentialBuildings = potentialBuildings.SortedDescending(b => b.StorageAdded).Take(5).ToArray();
                    break;
                case UpriseBuildingType.AllMilitary: 
                    for (int i = BuildingList.Count - 1; i >= 0; i--)
                    {
                        Building b = BuildingList[i];
                        if (b.Scrappable && b.IsMilitary)
                        {
                            DestroyBuilding(b);
                            buildingNameDestroyed = $"{Localizer.Token(GameText.UpriseAllMilitaryBuildings)}.";
                        }
                    }

                    if (buildingNameDestroyed.NotEmpty())
                        return;

                    break; // fallback to random buildings
            }

            Building toDestroy = Universe.Random.Item(potentialBuildings);
            buildingNameDestroyed = toDestroy.TranslatedName.Text;
            DestroyBuilding(toDestroy);
        }
    }
}
