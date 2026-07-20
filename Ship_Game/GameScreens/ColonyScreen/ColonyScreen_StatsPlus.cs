using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;

namespace Ship_Game
{
    // Ludoal fork: "Stats+" add-on tab in the colony facilities panel.
    // Everything about the tab lives in this file; the only hooks in existing
    // code are one AddTab() call and one dispatch line in DrawDetailInfo().
    //
    // Design v1.2 (Ludo's review, 20 Jul): totals and diagnostics on the panel,
    // marginal per-colonist rates belong to the Assign Labor tooltips, dead
    // lines are omitted. Budget is decomposed like the Economic Review and its
    // lines sum to the displayed net. All figures are per TURN: Empire.DoMoney()
    // credits NetIncome once per turn — the engine's "BC/Y" labels are display
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
            batch.DrawString(TextFont, value, new Vector2(c.X + 190, c.Y), valueColor);
            c.Y += TextFont.LineSpacing + 2;
        }

        void SPGap(ref Vector2 c) => c.Y += TextFont.LineSpacing;

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
            var left  = bCursor;
            var right = new Vector2(bCursor.X + 350, bCursor.Y);

            // ── BUDGET (BC / turn) — decomposed like the Economic Review, lines sum to Net ──
            float taxRaw   = P.PopulationBillion * P.Money.IncomePerColonist * P.Money.TaxRate;
            float bldgRaw  = P.Money.IncomeFromBuildings * P.Money.TaxRate;
            float rawSum   = taxRaw + bldgRaw;
            float scale    = rawSum.NotZero() ? P.Money.GrossRevenue / rawSum : 1f; // exotic credits bonus etc., keeps the sum exact
            float taxes    = taxRaw * scale;
            float bldgInc  = bldgRaw * scale;
            float bldgUp   = P.Money.Maintenance;
            float spaceDef = P.SpaceDefMaintenance;
            float troops   = P.Money.TroopMaint;
            float net      = P.Money.GrossRevenue - bldgUp - spaceDef - troops;

            SPHeader(ref left, batch, "BUDGET (BC / turn)");
            if (taxes.NotZero())    SPLine(ref left, batch, "Colonist taxes", SPSigned(taxes), SPTone(taxes));
            if (bldgInc.NotZero())  SPLine(ref left, batch, "Building income", SPSigned(bldgInc), SPTone(bldgInc));
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
            SPLine(ref left, batch, "Saturation", (P.PopulationRatio * 100f).String(0) + " %",
                   P.PopulationRatio > 1f ? Color.Orange : Color.White);
            float bioPop = P.PopPerBiosphere(Player);
            if (bioPop.NotZero())
                SPLine(ref left, batch, "Per biosphere (M)", bioPop.String(0), Color.White);

            // ── YIELDS (per turn) — the real net flows; marginal rates live on the labor sliders ──
            SPHeader(ref right, batch, "YIELDS (per turn)");
            SPLine(ref right, batch, Localizer.Token(GameText.Food),
                   SPSigned(P.Food.NetIncome, 1), SPTone(P.Food.NetIncome));
            SPLine(ref right, batch, Localizer.Token(GameText.Production),
                   SPSigned(P.Prod.NetIncome, 1), SPTone(P.Prod.NetIncome));
            SPLine(ref right, batch, Localizer.Token(GameText.Research),
                   SPSigned(P.Res.NetIncome, 1), SPTone(P.Res.NetIncome));
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
