using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.Audio;
using SDGraphics;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public enum ButtonTextAlign
    {
        Center, // the default setting
        Left, // text is left justified
        Right, // text is right justified
    }

    // Refactored by RedFox
    public partial class UIButton : UIElementV2
    {
        public enum PressState
        {
            Default, Hover, Pressed
        }

        public PressState State = PressState.Default;

        // Ludoal fork: the rule around a painted button - the same brass line the reworked
        // screens and the top bar draw, so a plate reads as part of one interface.
        static Color PlateRule => GameScreens.ReworkScreens.FrameRule;

        public SubTexture Normal;
        public SubTexture Hover;
        public SubTexture Pressed;

        // Text Colors
        public Color DefaultTextColor = new Color(255, 240, 189);
        public Color HoverTextColor   = new Color(255, 240, 189);
        public Color PressTextColor   = new Color(255, 240, 189);

        // Fallback background colors if Normal texture is null
        public bool DrawBackground = true;
        public Color DefaultColor = new Color(96, 81, 49);
        public Color HoverColor   = new Color(106, 91, 59);
        public Color PressColor   = new Color(86, 71, 39);

        ButtonStyle CurrentStyle = ButtonStyle.Default;
        public ButtonTextAlign TextAlign = ButtonTextAlign.Center;
        
        // Rich text element.
        // Can be accessed directly to create multi-font text labels
        public readonly PrettyText RichText;

        /// <summary>
        /// Optional override Function for text. Called Dynamically every frame.
        /// Completely overrides RichText
        /// </summary>
        public Func<string> DynamicText;

        public LocalizedText Tooltip;
        public string ClickSfx = "echo_affirm";

        // If set TRUE, this button will also capture Right Mouse Clicks
        public bool AcceptRightClicks;

        // If set TRUE, text will be drawn with dark shadow
        public bool TextShadows;

        // If set TRUE, will draw UI element bounds
        public bool DebugDraw;

        public Action<UIButton> OnClick;
        public InputBindings.IBinding Hotkey;
        // Ludoal fork: shown in the tooltip like Hotkey is, but WITHOUT binding the key to this
        // button. For a screen whose key is already read elsewhere, arming Hotkey here would
        // give one keypress two readers; this only tells the player which key does the job.
        public string TooltipHotkey;

        public override string ToString() => $"{TypeName} '{Text}' visible:{Visible} enabled:{Enabled} state:{State}";
        
        public UIButton(ButtonStyle style, in LocalizedText text)
        {
            Style = style;
            Size = GetInitialSize();
            RichText = new PrettyText(elemToUpdateSize: this, text: text);
        }
        
        public UIButton(ButtonStyle style, Vector2 pos, in LocalizedText text) : base(pos)
        {
            Style = style;
            Size = GetInitialSize();
            RichText = new PrettyText(elemToUpdateSize: this, text: text);
        }

        public UIButton(StyleTextures customStyle, Vector2 size, in LocalizedText text)
        {
            SetStyle(customStyle);
            Size = size;
            RichText = new PrettyText(elemToUpdateSize: this, text: text);
        }

        public ButtonStyle Style
        {
            get => CurrentStyle;
            set => SetStyle(value);
        }

        public LocalizedText Text
        {
            get => RichText.Text;
            set => RichText.SetText(value);
        }

        public Graphics.Font Font
        {
            get => RichText.DefaultFont;
            set => RichText.DefaultFont = value;
        }

        protected virtual void OnButtonClicked()
        {
            OnClick?.Invoke(this);
            if (ClickSfx.NotEmpty())
                GameAudio.PlaySfxAsync(ClickSfx);
        }

        public static SubTexture StyleTexture(ButtonStyle style = ButtonStyle.Default)
        {
            // ⚠ Callers want this for SIZE, not for pixels: the layout parser auto-fits a button
            // from its aspect ratio ("AbsSize: [200, 0]" reads the height off it). The slice asset
            // is square, so handing it back turned every laid-out button into a 200x200 slab -
            // the size reference is the one that still carries the proportions.
            StyleTextures s = GetDefaultStyle(style);
            return s.SizeRef ?? s.Normal;
        }

        // Ludoal fork: draw `tex` into `r` as a nine-slice - the four corners keep their pixel
        // size, the edges stretch along one axis and the middle along both. The slices are cut
        // Ludoal fork: SEPARATE bar and corner textures, the way Submenu's NineSliceSprite does
        // it - because that is the frame whose edges stay clean at any size. Slicing one bitmap
        // put the edge bands at the mercy of the middle's height: a 32px asset had to squeeze
        // 20px of source into the 12px a 24px-tall button leaves, and a 1px rule at 0.6 lands
        // between two pixels. The bar is 2x2 and uniform, so there is nothing in it to squeeze;
        // the corners are drawn once at their own size and never stretched at all.
        // `tint` multiplies: the assets are greyscale, the colour lives in the code.
        public static void DrawPlate(SpriteBatch batch, in Rectangle r, Color tint)
        {
            // Ludoal fork: drawn ROW BY ROW, no texture anywhere. Every attempt at sampling one
            // ended the same way - a bitmap scaled to a button's size lands between pixels, and
            // the linear filter turns a 1px rule into a smear and a gradient into mush. A row of
            // solid fill is exact at any size, and the radius becomes a number rather than an
            // asset that has to be redrawn.
            int radius = Math.Min(UITheme.Theme.CornerRadius, Math.Min(r.Width, r.Height) / 2);
            if (r.Width <= 0 || r.Height <= 0)
                return;

            for (int y = r.Y; y < r.Bottom; ++y)
            {
                // how far this row is inset by the arc, if it is in a corner band at all
                int dy = y < r.Y + radius        ? radius - (y - r.Y) - 1
                       : y >= r.Bottom - radius  ? radius - (r.Bottom - 1 - y) - 1
                       : 0;
                int inset = 0;
                if (dy > 0)
                {
                    // the horizontal half-chord of the circle at this height
                    double dx = Math.Sqrt(Math.Max(0, radius * radius - dy * dy));
                    inset = radius - (int)Math.Round(dx);
                }

                int x = r.X + inset, w = r.Width - 2 * inset;
                if (w <= 0)
                    continue;

                // the FACE: lighter at the top, darker at the foot - the relief, in one place
                float t = (y - r.Y) / (float)Math.Max(1, r.Height - 1);
                float face = UITheme.Theme.FaceTop + (UITheme.Theme.FaceBottom - UITheme.Theme.FaceTop) * t;
                batch.FillRectangle(new Rectangle(x, y, w, 1), tint.Alpha(face));

                // the RULE: the first and last rows are a full line, every other row gets its
                // two end pixels - which is what draws the arc, one row at a time.
                // ⚠ Not the tint at full strength: a saturated line right round a small control
                // reads as neon beside the stock buttons, whose edge is a thin darker brown.
                Color rule = tint.Alpha(UITheme.Theme.RuleStrength);
                bool edgeRow = y == r.Y || y == r.Bottom - 1;
                if (edgeRow)
                {
                    batch.FillRectangle(new Rectangle(x, y, w, 1), rule);
                }
                else
                {
                    batch.FillRectangle(new Rectangle(x, y, 1, 1), rule);
                    batch.FillRectangle(new Rectangle(x + w - 1, y, 1, 1), rule);
                }
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Visible)
                return;

            if (DynamicText != null)
            {
                string text = DynamicText();
                RichText.SetText(new LocalizedText(text, LocalizationMethod.RawText));
            }

            Rectangle r = Rect;
            SubTexture texture = ButtonTexture();
            if (Plated)
            {
                // Ludoal fork: ONE mechanism for every button - the painted plate, tinted and
                // faded from code. A 52px and a 182px button are the same control; the tint
                // carries the meaning (neutral, active, hostile) that used to need a texture of
                // its own, and the two tiny assets restyle the whole game when redrawn.
                DrawPlate(batch, r, BackgroundColor().Alpha(Enabled ? Opacity : Opacity * 0.5f));
            }
            else if (texture != null)
            {
                batch.Draw(texture, r, Color.White);
            }
            else if (DrawBackground)
            {
                Color c = BackgroundColor();
                batch.FillRectangle(r, c.Alpha(Enabled ? 0.85f : 0.55f));
                batch.DrawRectangle(r, PlateRule.Alpha(Enabled ? 0.75f : 0.35f));
            }
            // else: we only draw Text, nothing else

            if (RichText.NotEmpty)
            {
                Vector2 textCursor;
                if (TextAlign == ButtonTextAlign.Center)
                    textCursor.X = (r.X + r.Width / 2) - RichText.Size.X * 0.5f;
                else if (TextAlign == ButtonTextAlign.Left)
                    textCursor.X = r.X + 25f;
                else
                    textCursor.X = r.Right - RichText.Size.X;

                textCursor.Y = r.Y + r.Height / 2 - RichText.Size.Y * 0.5f;
                if (State == PressState.Pressed)
                    textCursor.Y += 1f; // pressed down effect

                Color textColor = Enabled ? TextColor() : Color.Gray;
                RichText.Draw(batch, textCursor, textColor, TextShadows);
            }

            if (DebugDraw)
            {
                batch.DrawRectangle(Rect, Color.Red);
                batch.DrawString(Fonts.Arial11Bold, this.ToString(), Pos, Color.Red);
            }
        }

        bool Released(InputState input) => input.LeftMouseReleased || (AcceptRightClicks && input.RightMouseReleased);
        bool Clicked(InputState input)  => input.LeftMouseClick    || (AcceptRightClicks && input.RightMouseClick);
        bool HeldDown(InputState input) => input.LeftMouseHeldDown || (AcceptRightClicks && input.RightMouseHeldDown);

        public override bool HandleInput(InputState input)
        {
            if (!Visible)
                return false;

            // before any of the early returns, check for hotkey match
            if (Hotkey != null && Hotkey.IsTriggered(input))
            {
                OnButtonClicked();
                return true;
            }

            if (!Rect.HitTest(input.CursorPosition)) // not hovering?
            {
                State = PressState.Default;
                return false;
            }

            // we are now hovering

            // not hovering last frame? trigger mouseover sfx
            if (State != PressState.Hover && State != PressState.Pressed)
            {
                GameAudio.MouseOver();
            }

            if (State == PressState.Pressed && Released(input))
            {
                State = PressState.Hover;
                OnButtonClicked();
                return true;
            }

            if (State != PressState.Pressed && Clicked(input))
            {
                State = PressState.Pressed;
                return true;
            }
            if (State == PressState.Pressed && HeldDown(input))
            {
                State = PressState.Pressed;
                return true;
            }

            // only trigger tooltip if we were hovering last frame as well as this one
            if (State == PressState.Hover)
            {
                if (Tooltip.IsValid)
                {
                    ToolTip.CreateTooltip(Tooltip, Hotkey?.Hotkey ?? TooltipHotkey, Pos + Size);
                }
            }

            State = PressState.Hover;

            // @note This should return true to capture the hover input,
            //       however most UI code doesn't use UIElementV2 system yet,
            //       so returning true would falsely trigger a lot of old style buttons
            //       Semantic differences:
            //         old system: true means click/event happened
            //         UIElementV2: true means input was handled/captured and should not propagate to other elements
            return false;
        }
    }
}
