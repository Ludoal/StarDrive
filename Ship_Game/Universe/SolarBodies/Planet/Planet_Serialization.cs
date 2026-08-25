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

            // Ludoal fork: saves written before ManualBudget existed encode the state in the
            // amounts themselves - any stored amount meant manual. Carry that over verbatim so
            // a loaded colony keeps behaving exactly as it did.
            if (!ManualBudget && (ManualCivilianBudget > 0 || ManualGrdDefBudget > 0 || ManualSpcDefBudget > 0))
                SetManualBudget(true);

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
