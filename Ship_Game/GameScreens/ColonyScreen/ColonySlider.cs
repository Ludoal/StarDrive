using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Universe.SolarBodies;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public enum ColonyResType
    {
        Food,
        Prod,
        Res
    }

    public class ColonySlider : UIElementV2
    {
        public delegate void SliderChangeEvent(ColonySlider slider, float difference);
        public SliderChangeEvent OnSliderChange;

        readonly ColonyResType Type;
        public Planet P;
        readonly SubTexture Slider, Icon;
        readonly SubTexture Lock = ResourceManager.Texture("NewUI/icon_lock");
        Rectangle LockRect;

        bool SliderHover;
        bool LockHover;
        readonly bool DrawIcons;

        public bool IsDragging { get; private set; }
        public bool CanDrag;
        public bool IsDisabled;
        public bool IsCrippled; // PRODUCTION resource: are we crippled?
        public bool IsInvasion; // PRODUCTION resource: invasion leaves us crippled as well?

        public ColonySlider(ColonyResType type, Planet p, bool drawIcons = true)
        {
            Height = 6;
            Type = type;
            P = p;
            var sliders = new[]{ "green", "brown", "blue" };
            var icons   = new[]{ "food", "production", "science" };
            Slider    = ResourceManager.Texture($"NewUI/slider_grd_{sliders[(int)type]}");
            Icon      = ResourceManager.Texture($"NewUI/icon_{icons[(int)type]}");
            DrawIcons = drawIcons;
            RequiresLayout = true;
        }

        public override void PerformLayout()
        {
            base.PerformLayout();
            LockRect = new Rectangle(Rect.Right + 10, 
                                     Rect.Center.Y + 2 - Lock.Height / 2, Lock.Width, Lock.Height);
        }

        // Ludoal fork: on a cybernetic colony the FOOD row has no food to show - those people
        // eat production. Under Auto it stops being a dead row and becomes the SUBSISTENCE
        // GAUGE: the share of production the pilot holds to keep the colony alive. The row below
        // then shows what is left for the player, so the three rows still read as a whole.
        //
        // ⚠ This is a reading of the model, never a second copy of it: Food.Percent stays at
        // zero for these people - labour put there would yield nothing at all - and the split
        // lives only on screen.
        public bool IsSubsistenceGauge => Type == ColonyResType.Food && P.IsCybernetic && P.AutoLabor;
        bool ShowsProdSurplus => Type == ColonyResType.Prod && P.IsCybernetic && P.AutoLabor;

        LocalizedText Tooltip()
        {
            if (IsSubsistenceGauge)
                return GameText.SubsistenceGaugeTip;
            switch (Type)
            {
                default: return P.IsCybernetic ? GameText.YourPeopleAreCyberneticAnd : GameText.FoodIsEatenByYour;
                case ColonyResType.Prod: return GameText.ProductionIsRequiredForThe;
                case ColonyResType.Res:  return GameText.ResearchPointsAreAddedInto;
            }
        }

        public ColonyResource Resource
        {
            get
            {
                if (IsSubsistenceGauge)
                    return P.Prod; // what it measures, so its income figures come from there
                switch (Type)
                {
                    default:                 return P.Food;
                    case ColonyResType.Prod: return P.Prod;
                    case ColonyResType.Res:  return P.Res;
                }
            }
        }

        public float Value
        {
            get => IsSubsistenceGauge ? P.SubsistenceFloor
                 : ShowsProdSurplus   ? (P.Prod.Percent - P.SubsistenceFloor).LowerBound(0f)
                                      : Resource.Percent;
            // the player drags the SURPLUS; the floor rides underneath it untouched
            set => Resource.Percent = (ShowsProdSurplus ? P.SubsistenceFloor + value : value)
                                      .NaNChecked(0f, "ColonySlider.Value");
        }

        public float NetValue => Resource.NetIncome;
        // The yield at 100% labor, from the same sim pass as NetValue (no UI recompute).
        public float MaxValue => Resource.NetMaxPotential;
        public bool ShowMaxValue; // draw the max beside the current value (gated by the host)

        // ⚠ A row that refuses the click cannot have been locked BY THE USER, so a lock found
        // on one is a ghost - and there are ghosts in old saves, where the screen used to force
        // this flag on for cybernetics. Ignored at the one point everything reads through (the
        // count that gates dragging, the draw, the click), so no save is rewritten and no
        // migration is needed: what cannot be true is simply not reported.
        public bool LockedByUser
        {
            get => !IsDisabled && Resource.PercentLock;
            set => Resource.PercentLock = value;
        }

        // Ludoal fork: one source for "the labour is managed for you" - it used to be deduced
        // from the colony type HERE and again in ColonySliderGroup, two readings of the same
        // rule that could disagree the day the rule gained a toggle. It has one now.
        bool LaborIsManaged => P.AutoLabor;

        public override bool HandleInput(InputState input)
        {
            if (IsDisabled)
                return false;

            Vector2 mousePos = input.CursorPosition;
            bool mouseOverSlider = !LockedByUser && !LaborIsManaged && Rect.Bevel(5).HitTest(mousePos);

            // slider drag is stateful to give user more convenient slide experience
            if (IsDragging)
            {
                if (!input.LeftMouseHeldDown) // LMB not down anymore?
                    IsDragging = false; // stop sliding
            }
            else if (CanDrag)
            {
                if (mouseOverSlider && input.LeftMouseClick)
                    IsDragging = true;
            }

            // @note No tooltips or other stuff during sliding
            if (IsDragging)
            {
                SliderHover = true;
                HandleDragging((int)mousePos.X);
                return true;
            }

            SliderHover = mouseOverSlider;

            LockHover = false;
            if (!LaborIsManaged) // Auto off: the padlocks answer again, and they kept their state
            {
                LockHover = LockRect.HitTest(mousePos);
                if (LockHover) // hovering over lock?
                {
                    if (input.LeftMouseClick)
                    {
                        LockedByUser = !LockedByUser;
                        GameAudio.AcceptClick();
                    }
                    ToolTip.CreateTooltip(GameText.LocksThisSliderPreventingThe);
                }
            }
            if (DrawIcons && !LockHover) // maybe hovering over icon?
            {
                if (IconRect().HitTest(input.CursorPosition) && P.Universe.Screen.IsActive)
                    ToolTip.CreateTooltip(Tooltip());
            }
            return false;
        }

        void HandleDragging(int mouseX)
        {
            float newRelX = (mouseX - Rect.Left) / (float)Rect.Width;
            float difference = newRelX.Clamped(0f, 1f) - Value;
            if (Math.Abs(difference) >= 0.001f)
            {
                OnSliderChange?.Invoke(this, difference);
            }
        }

        Rectangle IconRect()
        {
            return new Rectangle(Rect.X-40, Rect.Center.Y - Icon.CenterY, Icon.Width, Icon.Height);
        }
        
        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            Color sliderTint = IsDisabled ? Color.DarkGray : Color.White;

            // the track is the socle's drawing now - one arithmetic for every slider
            FloatSlider.DrawTrack(batch, Rect, Slider, Value, SliderHover, sliderTint);

            if (DrawIcons)
            {
                // the gauge measures production, so it wears production's icon rather than the
                // food icon of the row it borrows
                SubTexture icon = IsSubsistenceGauge ? ResourceManager.Texture("NewUI/icon_production") : Icon;
                batch.Draw(icon, IconRect(), sliderTint);
            }

            if (!IsDisabled)
                FloatSlider.DrawKnob(batch, Rect, Value, SliderHover, sliderTint);

            DrawLock(batch);
            DrawValueText(batch);
        }

        void DrawLock(SpriteBatch batch)
        {
            if (IsDisabled) return;

            if (!LockedByUser && !LaborIsManaged)
            {
                Color color = (LockHover ? new Color(255, 255, 255, 150) : new Color(255, 255, 255, 50)).Premultiplied();
                batch.Draw(Lock, LockRect, color);
            }
            else
            {
                batch.Draw(Lock, LockRect, Color.White);
            }
        }

        void DrawValueText(SpriteBatch batch)
        {
            var font = Fonts.Arial12Bold;
            float left = LockRect.Right + 10;
            float y    = Rect.CenterY() - font.LineSpacing / 2;
            float value = NetValue;
            if (value > -0.05f && value < 0.05f)
                value = 0f; // what rounds to zero neither shows a minus nor wears pink

            // ⚠ the subsistence gauge shows NO number. Its income figures would be production's,
            // which the row right below it already prints - a second copy of the same number
            // beside a different bar reads as a contradiction. The bar is what it has to say.
            if (IsSubsistenceGauge)
                return;

            // non-numeric states keep the plain left-aligned label
            if (IsDisabled || IsCrippled || IsInvasion)
            {
                string label = IsDisabled ? "n/a"
                             : IsCrippled ? Localizer.Token(GameText.Sabotaged)
                                          : Localizer.Token(GameText.Invasion);
                batch.DrawString(font, label, new Vector2(left, y), Colors.Cream);
                return;
            }

            // Align the numbers on the decimal point. The integer part is RIGHT-aligned on a
            // fixed comma column (room for 3 digits + a sign), the fraction runs to its right -
            // so "7", "100.2" and "-3.2" all line their point/units up instead of floating.
            // The current value and the max (100%-labor) value are two INDEPENDENT decimal
            // columns, each aligned on its own comma, with a fixed "/" between them - so a wide
            // max like "12.4" never shoves the current value out of line.
            float unitsW = font.TextWidth("-100"); // room for 3 digits + a sign in each column
            float curComma = left + unitsW;
            Color color = value < 0f ? Color.LightPink : Colors.Cream;
            DrawAlignedNumber(batch, font, value.String(), curComma, y, color);

            if (ShowMaxValue)
            {
                float slashX   = curComma + font.TextWidth(".0") + 4; // fraction room + gap
                float maxComma = slashX + font.TextWidth("/ ") + unitsW;
                batch.DrawString(font, "/", new Vector2(slashX, y), Colors.Cream.Alpha(0.5f));
                // The max is a potential, not a state: grey so it informs without rivalling the real value.
                DrawAlignedNumber(batch, font, MaxValue.String(), maxComma, y, Color.Gray);
            }
        }

        // Right-aligns the integer part on commaX and runs the fraction to its right.
        // Public: the Governor budget rows align their figures the same way, and a second
        // implementation would drift from this one.
        public static void DrawAlignedNumber(SpriteBatch batch, Graphics.Font font, string text, float commaX, float y, Color color)
        {
            int dot = text.IndexOf('.');
            string intPart  = dot < 0 ? text : text.Substring(0, dot);
            string fracPart = dot < 0 ? "" : text.Substring(dot);
            batch.DrawString(font, intPart, new Vector2(commaX - font.TextWidth(intPart), y), color);
            if (fracPart.Length > 0)
                batch.DrawString(font, fracPart, new Vector2(commaX, y), color);
        }
    }
}
