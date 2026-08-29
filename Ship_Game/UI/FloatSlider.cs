using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public enum SliderStyle
    {
        Decimal, // example: 42000
        Percent, // example: 51%
        Decimal1 // example: 0.5
    }

    public sealed class FloatSlider : UIElementV2
    {
        Rectangle SliderRect; // colored slider
        Rectangle KnobRect;   // knob area used to move the slider value
        public LocalizedText Text;
        public LocalizedText Tip;
        public LocalizedText ZeroString; // Display this string if the value is 0

        public Action<FloatSlider> OnChange;

        bool Hover, Dragging;
        float Min, Max, Value;
        public SliderStyle Style = SliderStyle.Decimal;

        // If Step != 0, then AbsoluteValue can only change in increments of this value
        public float Step = 0;
        // Ludoal fork: printed right after the value, for a rail whose number carries a unit.
        // Kept out of the zero test below, so a ZeroString still speaks for the left stop.
        public string ValueSuffix = "";
        // Ludoal fork: an inline row draws its own value label - silence the built-in one
        public bool DrawValueText = true;

        // Ludoal fork (maintainer, bench 529): the greyed LOOK. UIElementV2.Enabled already
        // refuses the drag, but a slider that refuses it while looking live is exactly the dead
        // control the bench keeps catching. Set both together; this one only paints.
        public bool Greyed;
        public float Range => Max-Min;

        float GetAbsValue(float relValue)
        {
            return Min + relValue * Range;
        }

        public float AbsoluteValue
        {
            get => GetAbsValue(RelativeValue);
            set
            {
                RelativeValue = (value.Clamped(Min, Max) - Min) / Range;
                RequiresLayout = true;
                UpdateSliderRect();
            }
        }

        public float RelativeValue
        {
            get => Value;
            set
            {
                Value = value.Clamped(0f, 1f);
                RequiresLayout = true;
                UpdateSliderRect();
                OnChange?.Invoke(this);
            }
        }

        public override string ToString() => $"{TypeName} {ElementDescr} r:{Value} a:{AbsoluteValue} [{Min}..{Max}] {Text}";

        static int ContentId;
        static SubTexture SliderKnob;
        static SubTexture SliderKnobHover;
        static SubTexture SliderMinute;
        static SubTexture SliderMinuteHover;
        static SubTexture SliderGradient;   // background gradient for the slider

        static void EnsureTextures()
        {
            if (SliderKnob == null || ContentId != ResourceManager.ContentId)
            {
                ContentId = ResourceManager.ContentId;
                SliderKnob        = ResourceManager.Texture("NewUI/slider_crosshair");
                SliderKnobHover   = ResourceManager.Texture("NewUI/slider_crosshair_hover");
                SliderMinute      = ResourceManager.Texture("NewUI/slider_minute");
                SliderMinuteHover = ResourceManager.Texture("NewUI/slider_minute_hover");
                SliderGradient    = ResourceManager.Texture("NewUI/slider_grd_green");
            }
        }

        // Ludoal fork: the track drawn ONCE for every slider in the game - fill, themed
        // outline, eleven ticks. ColonySlider carried its own copy of these lines (and of the
        // outline browns the theme now owns); the socle lends its drawing instead, the same
        // way Submenu lends its frame.
        public static void DrawTrack(SpriteBatch batch, in Rectangle track, SubTexture gradient,
                                     float relValue, bool hover, Color tint)
        {
            EnsureTextures();
            var fill = new Rectangle(track.X, track.Y, (int)(relValue * track.Width), track.Height);
            batch.Draw(gradient ?? SliderGradient, fill, tint);
            UITheme.DrawControlOutline(batch, track, hover);

            SubTexture minute = hover ? SliderMinuteHover : SliderMinute;
            var tickPos = new Vector2(track.X, track.Bottom + 1);
            for (int i = 0; i < 11; ++i)
            {
                tickPos.X = track.X + (int)(((track.Width - 1) / 10f) * i); // @note Yeah, cast is important
                batch.Draw(minute, tickPos, tint);
            }
        }

        /// the crosshair, centred on the value - shared for the same reason as the track
        public static void DrawKnob(SpriteBatch batch, in Rectangle track, float relValue, bool hover, Color tint)
        {
            EnsureTextures();
            SubTexture knob = hover ? SliderKnobHover : SliderKnob;
            var r = new Rectangle(track.X + (int)(relValue * track.Width) - knob.CenterX,
                                  track.CenterY() - knob.CenterY, knob.Width, knob.Height);
            batch.Draw(knob, r, tint);
        }

        public FloatSlider(Rectangle r, LocalizedText text, float min = 0f, float max = 10000f, float value = 5000f)
            : base(r)
        {
            EnsureTextures();
            Text  = text;
            Min   = min;
            Max   = max;
            Value = (value.Clamped(Min, Max) - Min) / Range;
            UpdateSliderRect();
        }

        public FloatSlider(SliderStyle style, Rectangle r, LocalizedText text, float min, float max, float value)
            : this(r, text, min, max, value)
        {
            Style = style;
            SetStyle(style);
        }

        public FloatSlider(SliderStyle style, Vector2 size, LocalizedText text, float min, float max, float value)
            : this(new Rectangle(0, 0,(int)size.X, (int)size.Y), text, min, max, value)
        {
            Style = style;
            SetStyle(style);
        }

        public FloatSlider(SliderStyle style, float w, float h, LocalizedText text, float min, float max, float value)
        {
            EnsureTextures();
            Size = new Vector2(w, h);
            Text  = text;
            Min   = min;
            Max   = max;
            Value = (value.Clamped(Min, Max) - Min) / Range;
            SetStyle(style);
            UpdateSliderRect();
        }

        void SetStyle(SliderStyle style)
        {
            Style = style;
            Step = style == SliderStyle.Percent ? 0.01f : 0f;
        }

        // Ludoal fork: track offset below the vertical centre. Default 3 keeps every existing
        // slider unchanged; lower it to tuck the track up under a title when the box is tall enough
        // to contain the knob but the centred track sits too low.
        public int TrackYOffset = 3;

        void UpdateSliderRect()
        {
            SliderRect = new Rectangle((int)Pos.X, (int)Pos.Y + (int)Height/2 + TrackYOffset, (int)Width - 32, 6);
            KnobRect = new Rectangle(SliderRect.X + (int)(SliderRect.Width * Value), 
                                     SliderRect.Y + SliderRect.Height / 2 - SliderKnob.Height / 2, 
                                     SliderKnob.Width, SliderKnob.Height);
        }

        public override void PerformLayout()
        {
            if (!Visible)
                return;

            base.PerformLayout();
            UpdateSliderRect();
        }

        public string StyledValue
        {
            get
            {
                string value; 
                switch (Style)
                {
                    case SliderStyle.Decimal:  value = ((int)Math.Round(AbsoluteValue)).ToString(); break;
                    case SliderStyle.Decimal1: value = (AbsoluteValue).String(1);                   break;
                    case SliderStyle.Percent:  value = (AbsoluteValue * 100f).ToString("00") + "%"; break;
                    default:                   value = RelativeValue.String(2); break;
                }

                if (ValueSuffix.NotEmpty())
                    value += ValueSuffix;

                if (ZeroString.IsValid && AbsoluteValue < 1)
                    value = ZeroString.Text;

                return value;
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Visible)
                return;

            Color tint = Greyed ? Color.DarkGray : Color.White;
            batch.DrawString(Fonts.Arial12Bold, Text, Pos, Greyed ? Color.Gray : UITheme.TextPrimary);

            DrawTrack(batch, SliderRect, SliderGradient, RelativeValue, Hover && !Greyed, tint);

            Rectangle knobRect = KnobRect;
            knobRect.X -= knobRect.Width / 2;
            batch.Draw(Hover && !Greyed ? SliderKnobHover : SliderKnob, knobRect, tint);

            if (DrawValueText)
            {
                var textPos = new Vector2(SliderRect.Right + 8, SliderRect.Y + SliderRect.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2);
                batch.DrawString(Fonts.Arial12Bold, StyledValue, textPos, UITheme.TextPrimary);
            }

            if (Hover)
            {
                if (Tip.IsValid)
                {
                    ToolTip.CreateTooltip(Tip, "", new Vector2(Right, CenterY));
                }
            }
        }

        public bool HandleInput(InputState input, ref float currentValue, float dynamicMaxValue)
        {
            Max = Math.Min(500000f, dynamicMaxValue);
           
            if (!Rect.HitTest(input.CursorPosition) || !input.LeftMouseHeld())
            {
                AbsoluteValue = currentValue;
                return false;
            }
            HandleInput(input);
            currentValue = AbsoluteValue;
            return true;
        }

        public override bool HandleInput(InputState input)
        {
            Hover = Rect.HitTest(input.CursorPosition);

            Rectangle clickCursor = KnobRect;
            clickCursor.X -= KnobRect.Width / 2;

            if (clickCursor.HitTest(input.CursorPosition) && input.LeftMouseHeldDown)
                Dragging = true;

            if (Dragging)
            {
                KnobRect.X = (int)input.CursorPosition.X;
                if (KnobRect.X > SliderRect.Right)  KnobRect.X = SliderRect.Right;
                else if (KnobRect.X < SliderRect.X) KnobRect.X = SliderRect.X;

                if (input.LeftMouseReleased)
                    Dragging = false;

                float newRelPos = 1f - (SliderRect.Right - KnobRect.X) / (float)SliderRect.Width;
                if (Step != 0)
                {
                    float oldAbsVal = AbsoluteValue;
                    float newAbsVal = GetAbsValue(newRelPos);
                    float diff = newAbsVal - oldAbsVal;
                    int steps = (int)Math.Round(diff / Step);
                    if (steps != 0)
                    {
                        AbsoluteValue = (float)Math.Round((oldAbsVal + steps*Step)*100) / 100;
                        OnChange?.Invoke(this);
                    }
                }
                else
                {
                    RelativeValue = newRelPos;
                }
            }
            return Dragging;
        }

    }
}