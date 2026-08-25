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
