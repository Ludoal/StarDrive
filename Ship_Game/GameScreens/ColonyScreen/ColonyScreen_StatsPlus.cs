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
    // Design (maquette v1.1): five blocks, ONE unit per block stated in the
    // block title, and budget lines that actually sum to the displayed net.
    // All figures are per TURN: Empire.DoMoney() credits NetIncome once per
    // turn, so the engine's "BC/Y" labels are display convention, not data —
    // the raw values already are per-turn amounts.
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
            batch.DrawString(TextFont, value, new Vector2(c.X + 170, c.Y), valueColor);
            c.Y += TextFont.LineSpacing + 2;
        }

        void SPGap(ref Vector2 c) => c.Y += TextFont.LineSpacing;

        static string SPSigned(float v, int digits = 2) => (v >= 0 ? "+" : "") + v.String(digits);
        static Color SPTone(float v) => v > 0 ? Color.LightGreen : v < 0 ? Color.Pink : Color.LightGray;

        void DrawStatsPlusTab(SpriteBatch batch, Vector2 bCursor)
        {
            float turnTimer = P.Universe.P.TurnTimer;

            var left  = bCursor;
            var right = new Vector2(bCursor.X + 340, bCursor.Y);

            // ── BUDGET (BC / turn) — lines sum to Net ──
            float gross    = P.Money.GrossRevenue;
            float upkeep   = P.Money.Maintenance + P.SpaceDefMaintenance;
            float troops   = P.Money.TroopMaint;
            float net      = gross - upkeep - troops;

            SPHeader(ref left, batch, "BUDGET (BC / turn)");
            SPLine(ref left, batch, Localizer.Token(GameText.GrossIncome), SPSigned(gross), SPTone(gross));
            SPLine(ref left, batch, Localizer.Token(GameText.Expenditure2), SPSigned(-upkeep), SPTone(-upkeep));
            SPLine(ref left, batch, "Troops", SPSigned(-troops), SPTone(-troops));
            SPLine(ref left, batch, Localizer.Token(net >= 0 ? GameText.NetIncome : GameText.NetLosses),
                   SPSigned(net), net >= 0 ? Color.Green : Color.Red);
            SPGap(ref left);

            // ── POPULATION (millions) — same unit as the universe view ──
            SPHeader(ref left, batch, "POPULATION (millions)");
            SPLine(ref left, batch, "Per habitable tile", P.PopPerTileFor(Player).String(0), Color.White);
            SPLine(ref left, batch, "Per biosphere", P.PopPerBiosphere(Player).String(0), Color.White);

            // ── YIELDS (per turn) ──
            float foodCol = P.Food.NetYieldPerColonist - P.FoodConsumptionPerColonist;
            float prodCol = P.Prod.NetYieldPerColonist - P.ProdConsumptionPerColonist;
            float resCol  = P.Res.NetYieldPerColonist;

            SPHeader(ref right, batch, "YIELDS (per turn)");
            SPLine(ref right, batch, Localizer.Token(GameText.Food),
                   $"{SPSigned(foodCol, 1)} /col  ·  {SPSigned(P.Food.NetFlatBonus, 1)} flat", SPTone(foodCol));
            SPLine(ref right, batch, Localizer.Token(GameText.Production),
                   $"{SPSigned(prodCol, 1)} /col  ·  {SPSigned(P.Prod.NetFlatBonus, 1)} flat", SPTone(prodCol));
            SPLine(ref right, batch, Localizer.Token(GameText.Research),
                   $"{SPSigned(resCol, 1)} /col  ·  {SPSigned(P.Res.NetFlatBonus, 1)} flat", SPTone(resCol));
            SPGap(ref right);

            // ── CONSTRUCTION (per turn) ──
            SPHeader(ref right, batch, "CONSTRUCTION (per turn)");
            SPLine(ref right, batch, "Max prod to queue", P.CurrentProductionToQueue.String(1), Color.White);
            SPLine(ref right, batch, "  of which from storage", P.InfraStructure.String(1), Color.LightGray);
            SPGap(ref right);

            // ── DEFENSE (per turn) — engine rate is per second, converted honestly ──
            float repairPerTurn = P.GeodeticManager.GetPlanetRepairRatePerSecond() * turnTimer;
            string combat = P.SpaceCombatNearPlanet ? " (space combat)" : "";

            SPHeader(ref right, batch, "DEFENSE (per turn)");
            SPLine(ref right, batch, Localizer.Token(GameText.ShipRepair) + combat,
                   repairPerTurn.String(0), Color.White);
        }
    }
}
