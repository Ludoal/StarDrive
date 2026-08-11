using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using System;
using System.Linq.Expressions;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    using BoolExpression = Expression<Func<bool>>;

    public sealed class UICheckBox : UIElementV2
    {
        public readonly Graphics.Font Font;
        // Ludoal fork: mutable - the espionage ops swap folded/full labels with their
        // state; call PerformLayout after a swap so the hit rect follows the text
        public LocalizedText Text;
        public readonly LocalizedText Tooltip;
        Ref<bool> Binding;

        public Action<UICheckBox> OnChange;
        public Color TextColor = Color.White;
        public Color CheckedTextColor = Color.White;

        int TextPadding = 4;
        int CheckBoxSize = 12;
        // Ludoal fork (bench 392): a horizontal indent for a subordinate checkbox. Added to the
        // draw and the hit-test, NOT to Pos - a parent UIList rewrites Pos every layout, so the
        // indent has to live outside it.
        public int Indent;

        // Ludoal fork: greyed + click-refused when its parent option is off (a subordinate box).
        // Draw dims the label; HandleInput below ignores the click while this is set.
        public bool Greyed;

        public bool Checked => Binding.Value;
        public override string ToString() => $"{TypeName} {ElementDescr} Text={Text} Checked={Checked}";

        public UICheckBox(float x, float y, Ref<bool> binding, Graphics.Font font,
                          in LocalizedText title, in LocalizedText tooltip)
        {
            Pos = new Vector2(x, y);
            Binding = binding;
            Font    = font;
            Text    = title;
            Tooltip = tooltip;
            PerformLayout();
        }

        public UICheckBox(BoolExpression binding, Graphics.Font font,
                          in LocalizedText title, in LocalizedText tooltip)
        {
            Binding = new Ref<bool>(binding);
            Font    = font;
            Text    = title;
            Tooltip = tooltip;
            PerformLayout();
        }

        public UICheckBox(float x, float y, BoolExpression binding, Graphics.Font font,
                          in LocalizedText title, in LocalizedText tooltip)
            : this(x, y, new Ref<bool>(binding), font, title, tooltip)
        {
        }

        public UICheckBox(float x, float y, Func<bool> getter, Action<bool> setter, Graphics.Font font,
                          in LocalizedText title, in LocalizedText tooltip)
            : this(x, y, new Ref<bool>(getter, setter), font, title, tooltip)
        {
        }

        public void Bind(BoolExpression binding)
        {
            Binding = new Ref<bool>(binding);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            var checkBox = new Rectangle((int)Pos.X + Indent, (int)CenterY - CheckBoxSize/2, CheckBoxSize, CheckBoxSize);
            UITheme.DrawControlOutline(batch, checkBox);
            //batch.DrawRectangle(Rect, Color.Red); // DEBUG

            if (Text.NotEmpty)
            {
                var textPos = new Vector2(checkBox.X + CheckBoxSize + TextPadding, (int)CenterY - Font.LineSpacing / 2);
                Color ink = Greyed ? Color.Gray : (Binding.Value ? CheckedTextColor : TextColor);
                batch.DrawString(Font, Text, textPos, ink);
            }

            if (Binding.Value)
            {
                var check = ResourceManager.Texture("NewUI/Checkmark10x");
                var checkMark = checkBox.Bevel(-1);
                batch.Draw(check, checkMark, Color.White);
            }
        }

        public override bool HandleInput(InputState input)
        {
            // Ludoal fork (bench 392): the indent shifts the hit rect with the drawn box.
            var hit = new Rectangle((int)Pos.X + Indent, (int)Rect.Y, (int)Rect.Width, (int)Rect.Height);
            if (!hit.HitTest(input.CursorPosition))
                return false;

            // greyed = subordinate to an option that is off: read-only, but still eats the click
            if (Greyed)
                return true;

            if (input.LeftMouseClick)
            {
                Binding.Value = !Binding.Value;
                OnChange?.Invoke(this);
            }
            else if (Tooltip.IsValid)
            {
                ToolTip.CreateTooltip(Tooltip);
            }

            // always capture input to prevent clicks from reaching elements under us
            return true;
        }

        public override void PerformLayout()
        {
            RequiresLayout = false;
            Pos.X = (int)Pos.X;
            Pos.Y = (int)Pos.Y;
            int h = Math.Max(CheckBoxSize, Font.LineSpacing);
            Size = new Vector2(CheckBoxSize + TextPadding + Font.TextWidth(Text), h);
        }
    }
}