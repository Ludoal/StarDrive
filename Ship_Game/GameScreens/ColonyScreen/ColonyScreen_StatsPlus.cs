using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Universe.SolarBodies;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game
{
    // Ludoal fork: "Stats+" add-on tab in the colony facilities panel.
    // Everything about the tab lives in this file; the only hooks in existing
    // code are one AddTab() call and one dispatch line in DrawDetailInfo().
    //
    // Totals and diagnostics on the panel; marginal per-colonist rates belong to
    // the Assign Labor tooltips, dead lines are omitted. Budget is decomposed like
    // the Economic Review and its lines sum to the displayed net. All figures are
    // per TURN: Empire.DoMoney() credits NetIncome once per turn — the engine's
    // per-year labels (BC/turn) are display convention, not data.
    // TODO localization pass: block titles/short labels need GameText tokens.
    public partial class ColonyScreen
    {
        public const string StatsPlusTabTitle = "Stats+"; // working title, trivial to rename

        bool IsStatsPlusTabSelected => PFacilities.IsTabSelected(StatsPlusTabTitle);

        // Layout helpers (SPHeader/SPLine/SPNum/SPSetColumns/SPYield*/SPSigned/SPTone) live in
        // the shared StatsPlusLayout so Colony and Blueprints draw the exact same panel.

        // Read-only mirror of Planet.GrowPopulation() (same branches, no side
        // effects) — the engine computes the rate inline and never exposes it.
        // PR note: worth extracting upstream so display and simulation share one formula.
        float SPPopGrowthPerTurn()
        {
            if (P.RecentCombat || !P.CanRepairOrHeal())
                return 0f;

            float popRaw    = P.PopulationBillion * 1000f; // raw thousands, same unit as engine Population
            float maxPopRaw = P.MaxPopulationBillion * 1000f;

            if (P.PopulationRatio > 1f)
                return -Math.Clamp((P.PopulationRatio - 1f) * 1000f, 100f, 10000f)
                        .UpperBound(popRaw - maxPopRaw);

            if (P.IsStarving) // Unfed is private; when starving the storage is empty, so it equals the net deficit
                return Math.Min(P.IsCybernetic ? P.Prod.NetIncome : P.Food.NetIncome, 0f) * 10f;

            float balanceGrowth = Math.Clamp(1f - P.PopulationRatio, 0.25f, 1f);
            float repRate = P.Owner.data.BaseReproductiveRate * (popRaw / 3f) * balanceGrowth;
            if (P.Owner.data.Traits.PopGrowthMax.NotZero())
                repRate = Math.Min(repRate, P.Owner.data.Traits.PopGrowthMax * 1000f);

            repRate  = Math.Max(repRate, P.Owner.data.Traits.PopGrowthMin * 1000f);
            repRate += P.PlusFlatPopulationPerTurn;
            repRate += repRate * P.Owner.data.Traits.ReproductionMod;
            if (P.ShortOnFood())
                repRate *= 0.1f;

            return Math.Min(repRate, maxPopRaw - popRaw);
        }

        void DrawStatsPlusTab(SpriteBatch batch, Vector2 bCursor)
        {
            // Two side-by-side blocks inside the tab, each half of the usable width
            // (20px inner margin per side).
            float blockW = (PFacilities.Width - 40) * 0.5f;
            SPCols cols = StatsPlusLayout.SPSetColumns(TextFont, blockW);

            var left  = bCursor;
            // The yields grid closes flush on the panel's right edge: its last pivot plus a
            // fraction of room lands at the margin, and the block start derives from that.
            float usable = PFacilities.Width - 40;
            // +10: the yields grid borrows the panel's right inner margin.
            var right = new Vector2(bCursor.X + usable + 10 - (cols.YieldColTotal + TextFont.TextWidth(".00") + 2), bCursor.Y);

            // ── BUDGET (BC / turn) — gross sources as the building screen promises them,
            // the tax mill as one visible line, everything still sums to Net exactly ──
            float colInc   = P.PopulationBillion * P.Money.IncomePerColonist;
            float bldgInc  = P.Money.IncomeFromBuildings;
            float sources  = colInc + bldgInc;
            // Ludoal fork (maintainer): each source is shown ALREADY TAXED rather than gross with
            // the multiplier on a line of its own - a player budgets with what actually arrives.
            // ⚠ The factor is the whole mill, tax AND the exotic credits bonus, so splitting it
            // between the two sources is a share, not a provenance. The block still sums to Net.
            float mill     = sources.NotZero() ? P.Money.GrossRevenue / sources : 1f;
            float bldgUp   = P.Money.Maintenance;
            float spaceDef = P.SpaceDefMaintenance;
            float troops   = P.Money.TroopMaint;
            float net      = P.Money.GrossRevenue - bldgUp - spaceDef - troops;

            StatsPlusLayout.SPHeader(ref left, batch, "BUDGET (BC / turn)");
            float taxInc   = colInc * mill;
            float bldgNet  = bldgInc * mill;
            // the rate rides the label rather than a line of its own: it is a lever the player
            // sets, and a number with no visible cause is worse than one line too many.
            if (taxInc.NotZero())   SPLineTip(ref left, batch, cols, blockW,
                   "Tax income (" + (P.Money.TaxRate * 100f).String(0) + " %)",
                   StatsPlusLayout.SPSigned(taxInc), StatsPlusLayout.SPTone(taxInc), GameText.SpColonistIncomeTip);
            if (bldgNet.NotZero())  SPLineTip(ref left, batch, cols, blockW, "Building income", StatsPlusLayout.SPSigned(bldgNet), StatsPlusLayout.SPTone(bldgNet), GameText.SpBuildingIncomeTip);
            if (bldgUp.NotZero())   SPLineTip(ref left, batch, cols, blockW, "Building upkeep", StatsPlusLayout.SPSigned(-bldgUp), StatsPlusLayout.SPTone(-bldgUp), GameText.SpBuildingUpkeepTip);
            if (spaceDef.NotZero()) SPLineTip(ref left, batch, cols, blockW, Localizer.Token(GameText.SpaceDefenseUpkeep), StatsPlusLayout.SPSigned(-spaceDef), StatsPlusLayout.SPTone(-spaceDef), GameText.SpSpaceDefUpkeepTip);
            if (troops.NotZero())   SPLineTip(ref left, batch, cols, blockW, "Troop upkeep", StatsPlusLayout.SPSigned(-troops), StatsPlusLayout.SPTone(-troops), GameText.SpTroopUpkeepTip);
            SPLineTip(ref left, batch, cols, blockW, Localizer.Token(net >= 0 ? GameText.NetIncome : GameText.NetLosses),
                   StatsPlusLayout.SPSigned(net), net >= 0 ? Color.Green : Color.Red, GameText.SpNetIncomeTip);
            StatsPlusLayout.SPGap(ref left, TextFont);

            // ── POPULATION — net growth and saturation (totals live in Planet Info already) ──
            float growth = SPPopGrowthPerTurn(); // raw thousands = millions of colonists
            StatsPlusLayout.SPHeader(ref left, batch, "POPULATION");
            SPLineTip(ref left, batch, cols, blockW, Localizer.Token(GameText.NetGrowthPerTurn), StatsPlusLayout.SPSigned(growth, 1),
                   P.IsStarving ? Color.Red : StatsPlusLayout.SPTone(growth), GameText.SpNetGrowthTip);
            SPLineTip(ref left, batch, cols, blockW, "Saturation", (P.PopulationRatio * 100f).String(1) + " %",
                   P.PopulationRatio > 1f ? Color.Orange : Color.White, GameText.SpSaturationTip);

            // ── YIELDS (per turn) — per-source sums, same principle as the Budget ──
            StatsPlusLayout.SPHeader(ref right, batch, "YIELDS (per turn)");
            StatsPlusLayout.SPYieldHeader(ref right, batch, TextFont, cols);
            SPDrawYield(ref right, batch, TextFont, cols, blockW, Localizer.Token(GameText.Food), P.Food,
                    P.NonCybernetic ? P.Consumption : 0f, GameText.SpYieldFoodTip);
            SPDrawYield(ref right, batch, TextFont, cols, blockW, Localizer.Token(GameText.Production), P.Prod,
                    P.IsCybernetic ? P.Consumption : 0f, GameText.SpYieldProdTip);
            SPDrawYield(ref right, batch, TextFont, cols, blockW, Localizer.Token(GameText.Research), P.Res, 0f, GameText.SpYieldResearchTip);
            StatsPlusLayout.SPGap(ref right, TextFont);

            // ── CONSTRUCTION (per turn) — flow to the queue + how long storage holds ──
            StatsPlusLayout.SPHeader(ref right, batch, "CONSTRUCTION (per turn)");
            SPLineTip(ref right, batch, cols, blockW, Localizer.Token(GameText.MaxProdToQueue), P.CurrentProductionToQueue.String(1), Color.White, GameText.SpMaxProdToQueueTip);
            if (P.InfraStructure.NotZero() && P.ProdHere.NotZero())
            {
                float turnsLeft = P.ProdHere / P.InfraStructure;
                SPLineTip(ref right, batch, cols, blockW, "  from storage",
                       P.InfraStructure.String(1) + "  (~" + turnsLeft.String(0) + " turns)", Color.LightGray, GameText.SpFromStorageTip);
            }
            StatsPlusLayout.SPGap(ref right, TextFont);

            // ── DEFENSE (per turn) — only when there is something to say ──
            float repairPerTurn = P.GeodeticManager.GetPlanetRepairRatePerSecond() * P.Universe.P.TurnTimer;
            if (repairPerTurn.NotZero())
            {
                StatsPlusLayout.SPHeader(ref right, batch, "DEFENSE (per turn)");
                SPLineTip(ref right, batch, cols, blockW, "Ship repair" + (P.SpaceCombatNearPlanet ? " (space combat)" : ""),
                       repairPerTurn.String(0), Color.White, GameText.SpShipRepairTip);
            }
        }

        // Policies phase 0 (design review texts): every STATS+ line says where its figure
        // comes from - the hover rect spans the block's width for the line just drawn
        void SPLineTip(ref Vector2 c, SpriteBatch batch, in SPCols cols, float blockW,
                       string label, string value, Color valueColor, GameText tip)
        {
            float top = c.Y;
            StatsPlusLayout.SPLine(ref c, batch, TextFont, cols, label, value, valueColor);
            if (new RectF(c.X, top, blockW, c.Y - top).HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(Localizer.Token(tip));
        }

        // Colony's yield row pulls the three numbers off the live ColonyResource, then hands them to
        // the shared layout (which no longer knows about ColonyResource - Blueprints has no such thing)
        void SPDrawYield(ref Vector2 c, SpriteBatch batch, Font font, in SPCols cols, float blockW,
                         string label, ColonyResource res, float eaten, GameText tip)
        {
            float top = c.Y;
            StatsPlusLayout.SPYield(ref c, batch, font, cols, label,
                res.ColonistIncome(res.NetYieldPerColonist), res.NetFlatBonus, res.NetIncome, eaten);
            if (new RectF(c.X, top, blockW, c.Y - top).HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(Localizer.Token(tip));
        }
    }
}
