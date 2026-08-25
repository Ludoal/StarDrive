using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Data.Serialization;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game.AI.Budget
{
    using static HelperFunctions;

    [StarDataType]
    public class PlanetBudget
    {
        [StarData] public readonly Planet P;
        [StarData] public readonly Empire Owner;
        [StarData] float TotalRemaining;
        [StarData] public float RemainingCivilian { get; private set; }
        [StarData] public float RemainingSpaceDef { get; private set; }
        [StarData] public float RemainingGroundDef { get; private set; }

        [StarData] public float CivilianAlloc { get; private set; }
        [StarData] public float GrdDefAlloc { get; private set; }
        [StarData] public float SpcDefAlloc { get; private set; }
        [StarData] public float TotalAlloc { get; private set; }
        // not serialized: recomputed every Update from the live empire budgets
        public float TargetAlloc { get; private set; }
        bool SnapNextUpdate;

        // Ludoal fork (maintainer bench 405): leaving a manual budget seeds the EMA with the
        // last manual value, and the smoothing takes ages to walk back to the auto target -
        // the sliders looked stuck. Re-ticking Auto snaps the allocations to the raw target.
        public void SnapToTarget() => SnapNextUpdate = true;

        float EmpireRatio;

        float EmpireColonizationBudget => Owner.AI.ColonyBudget;
        float EmpireDefenseBudget => Owner.AI.DefenseBudget;

        [StarDataConstructor] PlanetBudget() { }

        public PlanetBudget(Planet planet, Empire owner)
        {
            P = planet;
            Owner = owner;
        }


        public void Update()
        {
            EmpireRatio = P.ColonyPotentialValue(Owner, useBaseMaxFertility: true) / Owner.TotalColonyPotentialValues;
            float defenseRatio = P.ColonyBaseValue(Owner) / Owner.TotalColonyValues;

            float defenseBudget = EmpireDefenseBudget * defenseRatio;
            float groundRatio   = MilitaryBuildingsBudgetRatio();
            float orbitalRatio  = 1 - groundRatio;
            float civBudget     = EmpireColonizationBudget * EmpireRatio + P.GetColonyInitialBudgetTolerance() + P.TerraformBudget;
            float grdBudget     = defenseBudget * groundRatio;
            // Ludoal fork (maintainer spec): the Governor Spending tap - the player throttles
            // what governors may spend of their AUTO allocations; treasury keeps the rest.
            // Manual overrides below bypass it: an explicit order is not throttled.
            if (Owner.isPlayer)
            {
                float tap = Owner.Universe.P.GovernorSpendingRatio;
                civBudget     *= tap;
                grdBudget     *= tap;
                defenseBudget *= tap;
            }
            if (!Owner.isPlayer && P.System.HostileForcesPresent(Owner))
                grdBudget *= 3; // Try to add more temp ground defense to clear enemies

            // the raw (pre-smoothing) target the EMA allocations converge to - the colony
            // Budget tab shows it beside the smoothed value while on Auto
            TargetAlloc = civBudget + grdBudget + defenseBudget * orbitalRatio;

            if (SnapNextUpdate)
            {
                SnapNextUpdate = false;
                GrdDefAlloc   = grdBudget;
                SpcDefAlloc   = defenseBudget * orbitalRatio;
                CivilianAlloc = civBudget;
            }

            // Ludoal fork (maintainer feedback): manual/auto is a flag per area, never inferred
            // from the amounts - a manual budget of zero is a legitimate order, and one area
            // can be manual while the others keep tracking the governor.
            // The EMA converges on a zero target geometrically and never lands, so the auto
            // side snaps its last crumb: otherwise the panels round it to a misleading figure.
            GrdDefAlloc = P.ManualGrdBudgetOn ? P.ManualGrdDefBudget
                        : SnapSpentTail(ExponentialMovingAverage(GrdDefAlloc, grdBudget));

            SpcDefAlloc = P.ManualSpcBudgetOn ? P.ManualSpcDefBudget
                        : SnapSpentTail(ExponentialMovingAverage(SpcDefAlloc, defenseBudget * orbitalRatio));

            CivilianAlloc = P.ManualCivBudgetOn ? P.ManualCivilianBudget + P.TerraformBudget
                          : SnapSpentTail(ExponentialMovingAverage(CivilianAlloc, civBudget));

            RemainingGroundDef = (GrdDefAlloc - P.GroundDefMaintenance).RoundToFractionOf10();
            RemainingSpaceDef  = (SpcDefAlloc - P.SpaceDefMaintenance).RoundToFractionOf10();
            RemainingCivilian  = (CivilianAlloc - P.CivilianBuildingsMaintenance).RoundToFractionOf10();
            TotalRemaining = RemainingSpaceDef + RemainingGroundDef + RemainingCivilian; // total remaining budget for this planet
            TotalAlloc     = GrdDefAlloc + SpcDefAlloc + CivilianAlloc;
        }

        // Below this the allocation is a rounding tail of a decaying EMA, not a budget:
        // 0.05 BC/turn buys nothing and the panels round it to a misleading 0.03.
        const float MinMeaningfulAlloc = 0.05f;

        static float SnapSpentTail(float alloc) => alloc < MinMeaningfulAlloc ? 0f : alloc;

        public float CivilianTolerance => (CivilianAlloc * 0.1f).RoundToFractionOf10().LowerBound(0.1f);

        public float GroundDefTolerance => (-GrdDefAlloc * 0.1f).RoundToFractionOf10();

        public float SpaceDefTolerance => (-SpcDefAlloc * 0.1f).RoundToFractionOf10();

        public void UpdateManualUI()
        {
            if (!P.ManualBudget)
                return;

            GrdDefAlloc   = P.ManualGrdDefBudget;
            SpcDefAlloc   = P.ManualSpcDefBudget;
            CivilianAlloc = P.ManualCivilianBudget + P.TerraformBudget;
        }

        /// <summary>
        /// This is Orbitals vs. Military Buildings ratio of budget, since Building maintenance is much less than Orbitals.
        /// </summary>
        // public so the Budget tab can derive the Ground/Space split for a colony that has no
        // allocation yet (it shows what this colony WOULD receive from the empire split)
        public float MilitaryBuildingsBudgetRatio()
        {
            float preference;
            switch (P.CType)
            {
                case Planet.ColonyType.Military: preference = 0.5f;  break;
                case Planet.ColonyType.Core:     preference = 0.3f;  break;
                default:                         preference = 0.25f; break;
            }

            return P.HabitablePercentage * preference;
        }

        public void DrawBudgetInfo(UniverseScreen screen)
        {
            string drawText = $"<\nTotal Budget: {TotalRemaining.String(2)}" +
                              $"\nImportance: {EmpireRatio.String(2)}" +
                              $"\nCivilianBudget: {RemainingCivilian.String(2)}" +
                              $"\nDefenseBudge (orbitals and ground): {(RemainingSpaceDef + RemainingGroundDef).String(2)}" +
                              $"\nOrbitals: {RemainingSpaceDef.String(2)}" +
                              $"\nMilitaryBuildings: {RemainingGroundDef.String(2)}";

            screen.DrawStringProjected(P.Position + new Vector2(1000, 0), 0f, 1f, Color.LightGray, drawText);
        }
    }
}
