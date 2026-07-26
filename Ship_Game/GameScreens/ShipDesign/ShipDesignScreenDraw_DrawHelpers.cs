using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    public sealed partial class ShipDesignScreen
    {
        enum ValueTint
        {
            None,
            Bad,
            GoodBad,
            BadLowerThan2,
            BadPercentLowerThan1,
            CompareValue
        }

        struct StatValue
        {
            public LocalizedText Title;
            public Color TitleColor;
            public float Value;
            public float CompareValue;
            public LocalizedText Tooltip;
            public ValueTint Tint;
            public bool IsPercent;
            public float Spacing;
            public int LineSpacing;

            public StatValue(in LocalizedText title, float value, in LocalizedText tooltip,
                             Color titleColor, ValueTint tint = ValueTint.None, float spacing = 165, int lineSpacing = 1)
            {
                Title = title;
                TitleColor = titleColor;
                Value = value;
                CompareValue = 0f;
                Tooltip = tooltip;
                Tint = tint;
                IsPercent = false;
                Spacing = spacing;
                LineSpacing = lineSpacing;
            }

            public Color ValueColor
            {
                get
                {
                    switch (Tint)
                    {
                        case ValueTint.GoodBad:              return Value > 0f ? Color.LightGreen : Color.LightPink;
                        case ValueTint.Bad:                  return Color.LightPink;
                        case ValueTint.BadLowerThan2:        return Value > 2f ? Color.LightGreen : Color.LightPink;
                        case ValueTint.BadPercentLowerThan1: return Value > 1f ? Color.LightGreen : Color.LightPink;
                        case ValueTint.CompareValue:         return CompareValue < Value ? Color.LightGreen : Color.LightPink;
                        case ValueTint.None:
                        default: return Color.White;
                    }
                }
            }

            public string ValueText => IsPercent ? Value.ToString("P0") : Value.GetNumberString();
        }

        static void WriteLine(ref Vector2 cursor, int lines = 1)
        {
            cursor.Y += Fonts.Arial12Bold.LineSpacing * lines;
        }

        // Ludoal fork: no trailing colon on stat labels (Ludo). The columns are right-aligned
        // against a fixed value column, so the separator was doing no work — it only added a
        // ragged edge to an otherwise clean vertical line.
        static StatValue MakeStat(in LocalizedText title, float value, LocalizedText tooltip, Color titleColor, ValueTint tint = ValueTint.None, float spacing = 165, int lineSpacing = 1)
            => new StatValue(title.Text, value, tooltip, titleColor, tint, spacing, lineSpacing);

        static StatValue TintedValue(in LocalizedText title, float value, LocalizedText tooltip, Color titleColor, float spacing = 165, int lineSpacing = 1)
            => new StatValue(title.Text, value, tooltip, titleColor, ValueTint.GoodBad, spacing, lineSpacing);

        void DrawStatColor(ref Vector2 cursor, StatValue stat)
        {
            Graphics.Font font = Fonts.Arial12Bold;

            WriteLine(ref cursor);
            cursor.Y += stat.LineSpacing;

            Vector2 statCursor = new(cursor.X + stat.Spacing, cursor.Y);
            string title = stat.Title.Text;
            DrawString(FontSpace(statCursor, -20, title, font), stat.TitleColor, title, font); // @todo Replace with DrawTitle?

            string valueText = stat.ValueText;
            DrawString(statCursor, stat.ValueColor, valueText, font);

            if (stat.Tooltip.IsValid)
            {
                RectF tipRect = new(cursor.X, cursor.Y, font.TextWidth(title) + font.TextWidth(valueText), font.LineSpacing);
                if (tipRect.HitTest(MousePos))
                    ToolTip.CreateTooltip(stat.Tooltip);
            }
        }

        // Ludoal fork: same row geometry as DrawStat, for a value that is a word rather than a
        // number ("INF" on an endurance that never runs out). Lives here so every stat row in
        // the shipyard — modules and designs alike — shares one geometry.
        public void DrawStatText(ref Vector2 cursor, LocalizedText words, string value,
                                 Color titleColor, LocalizedText tooltipId, float spacing = 165,
                                 Color? valueColor = null)
        {
            Graphics.Font font = Fonts.Arial12Bold;

            WriteLine(ref cursor);
            cursor.Y += 1;

            Vector2 statCursor = new(cursor.X + spacing, cursor.Y);
            string title = words.Text;
            DrawString(FontSpace(statCursor, -20, title, font), titleColor, title, font);
            DrawString(statCursor, valueColor ?? Color.White, value, font);

            if (tooltipId.IsValid)
            {
                RectF tipRect = new(cursor.X, cursor.Y, font.TextWidth(title) + font.TextWidth(value), font.LineSpacing);
                if (tipRect.HitTest(MousePos))
                    ToolTip.CreateTooltip(tooltipId);
            }
        }

        public void DrawStat(ref Vector2 cursor, LocalizedText words, float stat, Color color, LocalizedText tooltipId, bool doGoodBadTint = true, bool isPercent = false, float spacing = 165)
        {
            StatValue sv = TintedValue(words, stat, tooltipId, color, spacing, 0);
            sv.IsPercent = isPercent;
            DrawStatColor(ref cursor, sv);
        }

        public void DrawStatBadPercentLower1(ref Vector2 cursor, LocalizedText words, float stat, Color color, LocalizedText tooltipId, float spacing = 165)
        {
            StatValue sv = MakeStat(words, stat, tooltipId, color, ValueTint.BadPercentLowerThan1, spacing, 0);
            sv.IsPercent = true;
            DrawStatColor(ref cursor, sv);
        }
    }
}
