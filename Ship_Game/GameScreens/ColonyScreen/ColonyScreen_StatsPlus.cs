using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Universe.SolarBodies;

namespace Ship_Game
{
    // Ludoal fork: "Stats+" add-on tab in the colony facilities panel.
    // Everything about the tab lives in this file; the only hooks in existing
    // code are one AddTab() call and one dispatch line in DrawDetailInfo().
    //
    // Design v1.2 (maintainer feedback): totals and diagnostics on the panel,
    // marginal per-colonist rates belong to the Assign Labor tooltips, dead
    // lines are omitted. Budget is decomposed like the Economic Review and its
    // lines sum to the displayed net. All figures are per TURN: Empire.DoMoney()
    // credits NetIncome once per turn — the engine's per-year labels (now BC/turn) are display
    // convention, not data.
    // TODO localization pass: block titles/short labels need GameText tokens.
    public partial class ColonyScreen
    {
        public const string StatsPlusTabTitle = "Stats+"; // working title, trivial to rename

        bool IsStatsPlusTabSelected => PFacilities.IsTabSelected(StatsPlusTabTitle);

        void SPHeader(ref Vector2 c, SpriteBatch batch, string title)
        {
            batch.DrawString(Fonts.Arial14Bold, title, c, Color.Wheat);
            c.Y += Fonts.Arial14Bold.LineSpacing + 4;
        }

        void SPLine(ref Vector2 c, SpriteBatch batch, string label, string value, Color valueColor)
        {
            batch.DrawString(TextFont, label, new Vector2(c.X + 10, c.Y), Color.LightGray);
            // decimal-aligned like the yields grid: the pivot leaves room for "+999"
            SPNum(batch, c.X + SPValueCol + 28, c.Y, value, valueColor);
            c.Y += TextFont.LineSpacing + 2;
        }

        void SPGap(ref Vector2 c) => c.Y += TextFont.LineSpacing;

        // Yield table columns, relative to the block cursor. Fractions of the block
        // width, not bench-measured pixels: the tab is 2/3 of a 2/3-screen menu, so
        // its width follows the resolution. The ratios are calibrated on the original
        // 1080p layout (105/165/220/280 out of a 350px block), so 1080p is unchanged
        // and narrower frames compress instead of spilling out of the panel.
        float SPYieldColPop, SPYieldColFlat, SPYieldColEaten, SPYieldColTotal, SPValueCol;

        void SPSetColumns(float blockW)
        {
            SPValueCol      = blockW * 0.543f; // the label/value split of SPLine
            // ⚠ the yield columns are PIVOTS now (the decimal point sits on them) and they
            // spread over the RIGHT block, which starts at 44% and runs to the edge - wider
            // than the left one (maintainer bench, 900p)
            float rightW    = blockW * 1.12f;
            SPYieldColPop   = rightW * 0.335f;
            SPYieldColFlat  = rightW * 0.505f;
            SPYieldColEaten = rightW * 0.660f;
            SPYieldColTotal = rightW * 0.830f;
        }

        // decimal-aligned draw: the INTEGER part ends at the pivot, the fraction hangs right
        // of it - every number of a column shares one decimal point (maintainer bench)
        void SPNum(SpriteBatch batch, float pivotX, float y, string s, Color color)
        {
            int dot = s.IndexOf('.');
            string intPart = dot < 0 ? s : s.Substring(0, dot);
            batch.DrawString(TextFont, s, new Vector2(pivotX - TextFont.TextWidth(intPart), y), color);
        }

        void SPYieldHeader(ref Vector2 c, SpriteBatch batch)
        {
            // headers close on the same right edge the numbers reach (pivot + a ".00" of room)
            float frac = TextFont.TextWidth(".00");
            void H(string h, float pivot) =>
                batch.DrawString(TextFont, h, new Vector2(c.X + pivot + frac - TextFont.TextWidth(h), c.Y), Color.DarkGray);
            H("from pop", SPYieldColPop);
            H("flat",     SPYieldColFlat);
            H("eaten",    SPYieldColEaten);
            H("total",    SPYieldColTotal);
            c.Y += TextFont.LineSpacing + 2;
        }

        // One yield row: from pop + flat [- eaten] = total, sums exactly to NetIncome
        // (AfterTax is linear, so the parts are each net of tax like the total).
        void SPYield(ref Vector2 c, SpriteBatch batch, string label, ColonyResource res, float eaten)
        {
            float fromColonists = res.ColonistIncome(res.NetYieldPerColonist);
            float total = res.NetIncome;
            batch.DrawString(TextFont, label, new Vector2(c.X + 10, c.Y), Color.LightGray);
            SPNum(batch, c.X + SPYieldColPop, c.Y, SPSigned(fromColonists, 2), Color.White);
            SPNum(batch, c.X + SPYieldColFlat, c.Y, SPSigned(res.NetFlatBonus, 2), Color.White);
            if (eaten.NotZero())
                SPNum(batch, c.X + SPYieldColEaten, c.Y, "-" + eaten.String(2), Color.Pink); // ASCII minus — the game font has no U+2212 (renders '?')
            SPNum(batch, c.X + SPYieldColTotal, c.Y, SPSigned(total, 2), SPTone(total));
            c.Y += TextFont.LineSpacing + 2;
        }

        static string SPSigned(float v, int digits = 2) => (v >= 0 ? "+" : "") + v.String(digits);
        static Color SPTone(float v) => v > 0 ? Color.LightGreen : v < 0 ? Color.Pink : Color.LightGray;

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
            // (20px inner margin per side) — was a flat +350, which overflowed the
            // panel as soon as the frame was narrower than 1080p.
            float blockW = (PFacilities.Width - 40) * 0.5f;
            SPSetColumns(blockW);

            var left  = bCursor;
            // the right block starts at 44% rather than half (maintainer bench, 900p): the
            // yields grid slides left as one piece and regains the margin its rightmost
            // column was eating into
            var right = new Vector2(bCursor.X + blockW * 0.88f, bCursor.Y);

            // ── BUDGET (BC / turn) — gross sources as the building screen promises them,
            // the tax mill as one visible line, everything still sums to Net exactly ──
            float colInc   = P.PopulationBillion * P.Money.IncomePerColonist;
            float bldgInc  = P.Money.IncomeFromBuildings;
            float sources  = colInc + bldgInc;
            float taxMill  = P.Money.GrossRevenue - sources; // × tax rate (and exotic credits bonus)
            float bldgUp   = P.Money.Maintenance;
            float spaceDef = P.SpaceDefMaintenance;
            float troops   = P.Money.TroopMaint;
            float net      = P.Money.GrossRevenue - bldgUp - spaceDef - troops;

            SPHeader(ref left, batch, "BUDGET (BC / turn)");
            if (colInc.NotZero())   SPLine(ref left, batch, "Colonist income", SPSigned(colInc), SPTone(colInc));
            if (bldgInc.NotZero())  SPLine(ref left, batch, "Building income", SPSigned(bldgInc), SPTone(bldgInc));
            if (sources.NotZero())  SPLine(ref left, batch,
                   "× tax rate (" + (P.Money.TaxRate * 100f).String(0) + " %)",
                   SPSigned(taxMill), SPTone(taxMill));
            if (bldgUp.NotZero())   SPLine(ref left, batch, "Building upkeep", SPSigned(-bldgUp), SPTone(-bldgUp));
            if (spaceDef.NotZero()) SPLine(ref left, batch, "Space defense upkeep", SPSigned(-spaceDef), SPTone(-spaceDef));
            if (troops.NotZero())   SPLine(ref left, batch, "Troop upkeep", SPSigned(-troops), SPTone(-troops));
            SPLine(ref left, batch, Localizer.Token(net >= 0 ? GameText.NetIncome : GameText.NetLosses),
                   SPSigned(net), net >= 0 ? Color.Green : Color.Red);
            SPGap(ref left);

            // ── POPULATION — net growth and saturation (totals live in Planet Info already) ──
            float growth = SPPopGrowthPerTurn(); // raw thousands = millions of colonists
            SPHeader(ref left, batch, "POPULATION");
            SPLine(ref left, batch, "Net growth (M / turn)", SPSigned(growth, 1),
                   P.IsStarving ? Color.Red : SPTone(growth));
            SPLine(ref left, batch, "Saturation", (P.PopulationRatio * 100f).String(1) + " %",
                   P.PopulationRatio > 1f ? Color.Orange : Color.White);

            // ── YIELDS (per turn) — per-source sums, same principle as the Budget ──
            SPHeader(ref right, batch, "YIELDS (per turn)");
            SPYieldHeader(ref right, batch);
            SPYield(ref right, batch, Localizer.Token(GameText.Food), P.Food,
                    P.NonCybernetic ? P.Consumption : 0f);
            SPYield(ref right, batch, Localizer.Token(GameText.Production), P.Prod,
                    P.IsCybernetic ? P.Consumption : 0f);
            SPYield(ref right, batch, Localizer.Token(GameText.Research), P.Res, 0f);
            SPGap(ref right);

            // ── CONSTRUCTION (per turn) — flow to the queue + how long storage holds ──
            SPHeader(ref right, batch, "CONSTRUCTION (per turn)");
            SPLine(ref right, batch, "Max prod to queue", P.CurrentProductionToQueue.String(1), Color.White);
            if (P.InfraStructure.NotZero() && P.ProdHere.NotZero())
            {
                float turnsLeft = P.ProdHere / P.InfraStructure;
                SPLine(ref right, batch, "  from storage",
                       P.InfraStructure.String(1) + "  (~" + turnsLeft.String(0) + " turns)", Color.LightGray);
            }
            SPGap(ref right);

            // ── DEFENSE (per turn) — only when there is something to say ──
            float repairPerTurn = P.GeodeticManager.GetPlanetRepairRatePerSecond() * P.Universe.P.TurnTimer;
            if (repairPerTurn.NotZero())
            {
                SPHeader(ref right, batch, "DEFENSE (per turn)");
                SPLine(ref right, batch, "Ship repair" + (P.SpaceCombatNearPlanet ? " (space combat)" : ""),
                       repairPerTurn.String(0), Color.White);
            }
        }
    }
}
