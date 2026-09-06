using System;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;

namespace Ship_Game.Commands.Goals
{
    [StarDataType]
    public class IncreaseFreighters : BuildShipsGoalBase
    {
        // Ludoal fork (maintainer, 6 Sep '26): which trade zone ordered this hull, 0 for the
        // empire's own. An exclusive zone below its target used to be able to REQUISITION an idle
        // hull and nothing else - so once the empire had none idle, the zone stayed short for
        // good and nobody built for it.
        [StarData] public int ForZoneId;

        [StarDataConstructor]
        public IncreaseFreighters() : base(GoalType.IncreaseFreighters, null)
        {
            InitSteps();
        }

        public IncreaseFreighters(Empire owner, TradeZone forZone = null) : base(GoalType.IncreaseFreighters, owner)
        {
            InitSteps();
            ForZoneId = forZone?.Id ?? 0;
            Build = new(BuildableShip.GetFreighter(owner));
        }

        void InitSteps()
        {
            Steps = new Func<GoalStep>[]
            {
                FindPlanetToBuildAt,
                WaitForShipBuilt
            };
        }

        GoalStep FindPlanetToBuildAt()
        {
            if (!Owner.FindPlanetToBuildShipAt(Owner.SafeSpacePorts, Build.Template, out Planet planet, priority: 0.1f))
                return GoalStep.GoalFailed;

            PlanetBuildingAt = planet;
            planet.Construction.Enqueue(Build.Template, QueueItemType.Freighter, this, notifyOnEmpty: false,
                                        tradeZoneId: ForZoneId);
            return GoalStep.GoToNextStep;
        }
    }
}
