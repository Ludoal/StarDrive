using System;
using SDGraphics;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Universe.SolarBodies;

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    public partial class Planet
    {
        [StarDataConstructor]
        Planet() : base(0, GameObjectType.Planet)
        {
            GeodeticManager = new GeodeticManager(this);
            Money = new ColonyMoney(this);
        }

        [StarDataDeserialized(typeof(Building))]
        void OnDeserialized()
        {
            // TODO: just for save compatibility, remove later
            Troops ??= new(this);

            // Ludoal fork: saves written before the per-area flags encode the state in the
            // amounts themselves - a stored amount meant that area was manual. Carry it over
            // verbatim so a loaded colony keeps behaving exactly as it did.
            if (!ManualCivBudgetOn && ManualCivilianBudget > 0) SetManualCivBudgetOn(true);
            if (!ManualGrdBudgetOn && ManualGrdDefBudget   > 0) SetManualGrdBudgetOn(true);
            if (!ManualSpcBudgetOn && ManualSpcDefBudget   > 0) SetManualSpcBudgetOn(true);

            // Ludoal fork: the two mandates replace GovGroundDefense and the no-scrap toggle.
            // The old flags carry over so no colony changes conduct: ground defense ON meant
            // the governor handled military too, and it never demolished military otherwise.
            // Seeded ONCE: DontScrapBuildings is false on a fresh save too, so an ungated
            // carry-over rewrote the scrap mandate on every single load (bench 505).
            if (!MandatesSeeded)
            {
                if (GovGroundDefense)
                    GovBuildMandate = BuildMandate.All;

                if (!DontScrapBuildings)
                    GovScrapMandate = GovGroundDefense ? BuildMandate.All : BuildMandate.EconomicOnly;

                MandatesSeeded = true;
            }

            UpdatePositionOnly();
            InitPlanetType(PType, Scale, fromSave: true);

            foreach (Building b in BuildingList)
                UpdatePlanetStatsFromPlacedBuilding(b);

            UpdateMaxPopulation();
            UpdateIncomes();
            UpdatePlanetShields();
            UpdateDevelopmentLevel();
        }
    }
}
