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
        public Rectangle LockRect; // the padlock's own column - the Auto toggle above it seats on this

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
        // the FOOD row on a cybernetic colony, in EITHER state - what it wears and what it is
        // called answer to this, so the row cannot be named one thing and drawn as another
        bool IsCyberneticFoodRow => Type == ColonyResType.Food && P.IsCybernetic;
        // ⚠ bench 529: it shows in BOTH states now. The row exists either way and the space was
        // sitting empty; what changes is only WHICH quantity it reads - see Value.
        public bool IsSubsistenceGauge => IsCyberneticFoodRow;

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

        // ⚠ bench 531: ONE quantity, in both states - the WATERLINE. The share of labour at
        // which production exactly meets consumption; its end is where the colony stops feeding
        // itself. It does not depend on who is driving, so the bar means the same thing whether
        // Auto is on or off, and the two states can be read against each other.
        //
        // It used to show the pilot's FLOOR under Auto, which was a different quantity wearing
        // the same bar: the floor is the waterline plus a margin that SWINGS WITH STORAGE (the
        // pilot's own correction, -35%..+50%), so it neither added up against the max nor held
        // still. Two references, one gauge - the maintainer read it straight off the screen.
        //
        // Nothing is lost with it: under Auto the pilot's margin is now the GAP between this
        // bar's end and the production cursor below, which says more than the old bar did.
        float SubsistenceShare => P.Prod.EstPercentForNetIncome(0);

        // ⚠ bench 532: on a cybernetic colony these two rows are read against ONE scale, and it
        // runs from 0 to what the colony makes at full labour. They used to print NET income
        // against a NET maximum: the figure swung through negative numbers, the maximum stood
        // for an output the cursor could never produce, and what the population ate was named
        // rather than counted. Now production says what it MAKES, the gauge says what is EATEN,
        // and the difference between them is the net the player used to read - visibly.
        bool IsCyberneticProdRow => Type == ColonyResType.Prod && P.IsCybernetic;
        float GrossOutput    => Resource.AfterTax(Resource.GrossIncome);
        float GrossMaxOutput => Resource.AfterTax(Resource.GrossMaxPotential + Resource.FlatBonus);
        // the waterline, in the same terms as the two figures beside it
        bool UnderWater => P.IsCybernetic && P.Prod.AfterTax(P.Prod.GrossIncome) < P.Consumption;

        // ⚠ bench 530: the production row shows the WHOLE share, bar and numbers alike. It used
        // to draw the surplus while its figures reported the total - one row speaking two scales,
        // which is what made the bar leap when Auto was switched off: nothing had changed in the
        // colony, only what the bar was measuring. The gauge above says what that production owes
        // before anything else; the difference between the two bars is what is left.
        public float Value
        {
            get => IsSubsistenceGauge ? SubsistenceShare : Resource.Percent;
            set => Resource.Percent = value.NaNChecked(0f, "ColonySlider.Value");
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
        // Ludoal fork (bench 526): this row does not move - because the player pinned it, or
        // because the pilot is holding it. The 3-way solver and the drag gate ask THIS: a managed
        // row that answered as "free" was being pushed around by its neighbours, which is exactly
        // what Auto promised it would not do.
        // ⚠ IsDisabled belongs here too, and its absence was a real leak: a cybernetic colony's
        // FOOD row refuses the click but was still the first row the solver reached for when it
        // needed somewhere to put the difference. Labour landed there, where those people yield
        // nothing at all - and the row showed it.
        public bool Pinned => LockedByUser || LaborIsManaged || IsDisabled;

        public bool LockedByUser
        {
            get => !IsDisabled && Resource.PercentLock;
            set => Resource.PercentLock = value;
        }

        // Ludoal fork: one source for "is THIS row managed for you". It used to be deduced from
        // the colony type here and again in ColonySliderGroup, two readings of one rule.
        //
        // ⚠ bench 525: it is per ROW, not per colony. A governor manages the whole split, so all
        // three stop answering. The sustenance pilot holds ONE row and leaves the others to the
        // player - blocking all three there took away exactly what Auto promised to leave.
        public bool LaborIsManaged => P.AutoLabor && (P.HasLaborGovernor || IsSustenanceRow);

        // the row the pilot holds when there is no governor: food for most, production for the
        // cybernetic, who eat it
        bool IsSustenanceRow => Type == (P.IsCybernetic ? ColonyResType.Prod : ColonyResType.Food);

        public override bool HandleInput(InputState input)
        {
            // ⚠ bench 532: a row that refuses the CLICK still has something to say. Bailing out
            // here made the subsistence gauge's own tooltip unreachable by construction - the
            // gauge exists only on a cybernetic colony, and that is exactly the colony where
            // this row is disabled. Hover is not input the row acts on; it is the row
            // explaining itself.
            if (IsDisabled)
            {
                if (DrawIcons && IconRect().HitTest(input.CursorPosition) && P.Universe.Screen.IsActive)
                    ToolTip.CreateTooltip(Tooltip());
                return false;
            }

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
            // the waterline turns red when the colony is under it: the net income already
            // accounts for what these people eat, so there is nothing to compute (bench 529)
            Color sliderTint = IsSubsistenceGauge && UnderWater ? Color.Red
                             : IsDisabled                      ? Color.DarkGray
                                                               : Color.White;

            // the track is the socle's drawing now - one arithmetic for every slider
            FloatSlider.DrawTrack(batch, Rect, Slider, Value, SliderHover, sliderTint);

            if (DrawIcons)
            {
                // the row measures production, so it wears production's icon rather than the
                // food icon it inherited - in both states (maintainer, bench 525)
                SubTexture icon = IsCyberneticFoodRow ? ResourceManager.Texture("NewUI/icon_production") : Icon;
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
            // the decimal column is shared by every numeric row, the gauge included, so the
            // consumption lines up under the output it is subtracted from
            float unitsW   = font.TextWidth("-100"); // room for 3 digits + a sign in each column
            float curComma = left + unitsW;
            float value = IsCyberneticProdRow ? GrossOutput : NetValue;
            if (value > -0.05f && value < 0.05f)
                value = 0f; // what rounds to zero neither shows a minus nor wears pink

            // ⚠ on a cybernetic colony the FOOD row is named rather than numbered, in both
            // states (maintainer, bench 525). No number: its income figures would be
            // production's, which the row right below already prints, and the same number beside
            // a different bar reads as a contradiction. "n/a" said only that the row was not for
            // them; "Consumption" says what the bar is - the share of their output they eat.
            if (IsCyberneticFoodRow)
            {
                // grey: it is a demand, not something the player is producing or steering
                DrawAlignedNumber(batch, font, P.Consumption.String(), curComma, y, Color.Gray);
                return;
            }

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
            // red rather than the usual pink for a negative: it answers to the same test as the
            // gauge bar above it, and the two must read as one alarm
            Color color = IsCyberneticProdRow ? (UnderWater ? Color.Red : Colors.Cream)
                        : value < 0f         ? Color.LightPink
                                             : Colors.Cream;
            DrawAlignedNumber(batch, font, value.String(), curComma, y, color);

            if (ShowMaxValue)
            {
                float slashX   = curComma + font.TextWidth(".0") + 4; // fraction room + gap
                float maxComma = slashX + font.TextWidth("/ ") + unitsW;
                batch.DrawString(font, "/", new Vector2(slashX, y), Colors.Cream.Alpha(0.5f));
                // The max is a potential, not a state: grey so it informs without rivalling the real value.
                DrawAlignedNumber(batch, font, (IsCyberneticProdRow ? GrossMaxOutput : MaxValue).String(),
                                  maxComma, y, Color.Gray);
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
