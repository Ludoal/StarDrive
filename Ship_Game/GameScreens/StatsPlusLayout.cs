using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game
{
    // Ludoal fork (bench 353): the Stats+ layout, extracted from ColonyScreen_StatsPlus so the
    // Colony facilities panel AND the Blueprints hover panel share ONE geometry - it is tuned to
    // the millimetre for a 1440px frame and must never drift into a second copy. All helpers are
    // pure formatting: they take the font and the block width, hold no screen state. The column
    // pivots live in SPCols (was instance fields on ColonyScreen) so two screens can draw at once
    // without stepping on shared statics.
    public struct SPCols
    {
        public float YieldColPop, YieldColFlat, YieldColEaten, YieldColTotal, ValueCol;
    }

    public static class StatsPlusLayout
    {
        public static string SPSigned(float v, int digits = 2) => (v >= 0 ? "+" : "") + v.String(digits);
        public static Color   SPTone(float v) => v > 0 ? Color.LightGreen : v < 0 ? Color.Pink : Color.LightGray;

        public static void SPHeader(ref Vector2 c, SpriteBatch batch, string title)
        {
            batch.DrawString(Fonts.Arial14Bold, title, c, Color.Wheat);
            c.Y += Fonts.Arial14Bold.LineSpacing + 4;
        }

        public static void SPLine(ref Vector2 c, SpriteBatch batch, Font font, in SPCols cols,
                                  string label, string value, Color valueColor)
        {
            batch.DrawString(font, label, new Vector2(c.X + 10, c.Y), Color.LightGray);
            // decimal-aligned like the yields grid: the pivot leaves room for "+999"
            SPNum(batch, font, c.X + cols.ValueCol + 28, c.Y, value, valueColor);
            c.Y += font.LineSpacing + 2;
        }

        public static void SPGap(ref Vector2 c, Font font) => c.Y += font.LineSpacing;

        // Yield table columns, relative to the block cursor. Fractions of the block width, not
        // bench-measured pixels: the tab is 2/3 of a 2/3-screen menu, so its width follows the
        // resolution. Ratios calibrated on the 1080p layout (105/165/220/280 out of 350px), so
        // 1080p is unchanged and narrower frames compress instead of spilling out of the panel.
        public static SPCols SPSetColumns(Font font, float blockW)
        {
            var cols = new SPCols();
            // the label/value split of SPLine, floored so the value clears the widest label
            // ("Net growth (M / turn)") at any width (maintainer bench 285)
            cols.ValueCol      = Math.Max(blockW * 0.543f, font.TextWidth("Net growth (M / turn)") + 10);
            // ⚠ the yield columns are PIVOTS (the decimal point sits on them); they spread over the
            // RIGHT block, which starts at 44% and runs to the edge - wider than the left one
            float rightW       = blockW * 1.30f;
            // the pop pivot keeps clear of the widest row label ("Production") at any width
            cols.YieldColPop   = Math.Max(rightW * 0.335f, font.TextWidth("Production") + 42);
            cols.YieldColFlat  = rightW * 0.55f;
            cols.YieldColEaten = rightW * 0.70f;
            cols.YieldColTotal = rightW * 0.86f;
            return cols;
        }

        // decimal-aligned draw: the INTEGER part ends at the pivot, the fraction hangs right of it -
        // every number of a column shares one decimal point (maintainer bench)
        public static void SPNum(SpriteBatch batch, Font font, float pivotX, float y, string s, Color color)
        {
            int dot = s.IndexOf('.');
            if (dot < 0)
            {
                // no decimal point: the leading numeric token still ends AT the pivot. Anchoring the
                // whole string put suffixed values ("8  (~25 turns)", "+0") on their own labels.
                dot = 0;
                while (dot < s.Length && (char.IsDigit(s[dot]) || s[dot] == '+' || s[dot] == '-'))
                    ++dot;
            }
            string intPart = s.Substring(0, dot);
            batch.DrawString(font, s, new Vector2(pivotX - font.TextWidth(intPart), y), color);
        }

        public static void SPYieldHeader(ref Vector2 c, SpriteBatch batch, Font font, in SPCols cols)
        {
            // headers CENTRE on their pivot (maintainer bench 284): a decimal column's visual middle
            // is its decimal point, near enough
            Vector2 pos = c; // a local function cannot capture a ref parameter (CS1628)
            void H(string h, float pivot) =>
                batch.DrawString(font, h, new Vector2(pos.X + pivot - font.TextWidth(h) / 2f, pos.Y), Color.DarkGray);
            H("pop",   cols.YieldColPop);
            H("flat",  cols.YieldColFlat);
            H("eaten", cols.YieldColEaten);
            H("total", cols.YieldColTotal);
            c.Y += font.LineSpacing + 2;
        }

        // One yield row: from pop + flat [- eaten] = total, sums exactly to NetIncome (AfterTax is
        // linear, so the parts are each net of tax like the total).
        public static void SPYield(ref Vector2 c, SpriteBatch batch, Font font, in SPCols cols,
                                   string label, float fromColonists, float netFlatBonus, float total, float eaten)
        {
            batch.DrawString(font, label, new Vector2(c.X + 10, c.Y), Color.LightGray);
            SPNum(batch, font, c.X + cols.YieldColPop,  c.Y, SPSigned(fromColonists, 2), Color.White);
            SPNum(batch, font, c.X + cols.YieldColFlat, c.Y, SPSigned(netFlatBonus, 2), Color.White);
            if (eaten.NotZero())
                SPNum(batch, font, c.X + cols.YieldColEaten, c.Y, "-" + eaten.String(2), Color.Pink); // ASCII minus — the game font has no U+2212
            SPNum(batch, font, c.X + cols.YieldColTotal, c.Y, SPSigned(total, 2), SPTone(total));
            c.Y += font.LineSpacing + 2;
        }
    }
}
