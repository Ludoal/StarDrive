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
        // ⚠ bench 529: it shows in BOTH states - the row exists either way, and only WHICH
        // quantity it reads changes (see Value).
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
        // ⚠ never the pilot's FLOOR: that is the waterline plus a margin that SWINGS WITH
        // STORAGE (-35%..+50%), so it neither adds up against the max nor holds still. Under
        // Auto the pilot's margin is the GAP between this bar's end and the production cursor.
        float SubsistenceShare => P.Prod.EstPercentForNetIncome(0);

        // ⚠ bench 532: on a cybernetic colony these two rows are read against ONE scale, running
        // from 0 to what the colony makes at full labour. Production says what it MAKES, the
        // gauge says what is EATEN, and the difference between them is the net, visibly.
        // ⚠ not NET income against a NET maximum: that figure swings through negative numbers
        // and its maximum stands for an output the cursor can never produce.
        bool IsCyberneticProdRow => Type == ColonyResType.Prod && P.IsCybernetic;
        float GrossOutput    => Resource.AfterTax(Resource.GrossIncome);
        float GrossMaxOutput => Resource.AfterTax(Resource.GrossMaxPotential + Resource.FlatBonus);
        // the waterline, in the same terms as the two figures beside it
        bool UnderWater => P.IsCybernetic && P.Prod.AfterTax(P.Prod.GrossIncome) < P.Consumption;

        // ⚠ bench 530: the production row shows the WHOLE share, bar and numbers alike. A row
        // drawing the surplus while its figures report the total speaks two scales, and its bar
        // leaps when Auto is switched off. The gauge above says what that production owes before
        // anything else; the difference between the two bars is what is left.
        public float Value
        {
            get => IsSubsistenceGauge ? SubsistenceShare : Resource.Percent;
            set => Resource.Percent = value.NaNChecked(0f, "ColonySlider.Value");
        }

        // ⚠ bench 533: what this row ALLOCATES, which is not what it always SHOWS. The
        // subsistence gauge reads a waterline - a share nobody is working on - while the labour
        // actually put on that row is zero, because these people never farm. The solver splits
        // LABOUR, so it asks this and never the display: reading the gauge as an allocation
        // reserves a third of the workforce for nothing, and the production cursor can then
        // never reach its own maximum.
        public float LaborShare => IsCyberneticFoodRow ? P.Food.Percent : Value;

        public float NetValue => Resource.NetIncome;
        // The yield at 100% labor, from the same sim pass as NetValue (no UI recompute).
        public float MaxValue => Resource.NetMaxPotential;
        public bool ShowMaxValue; // draw the max beside the current value (gated by the host)

        // Ludoal fork (bench 526): this row does not move - because the player pinned it, or
        // because the pilot is holding it. The 3-way solver and the drag gate both ask THIS: a
        // managed row that answers as "free" gets pushed around by its neighbours, which is
        // exactly what Auto promises it will not do.
        // ⚠ IsDisabled belongs here too: a cybernetic colony's FOOD row refuses the click, and
        // without it the solver reaches for that row first when it needs somewhere to put the
        // difference - labour landing where those people yield nothing at all.
        // ⚠ a row that refuses the click cannot have been locked BY THE USER, so a lock found on
        // one is a ghost, and old saves carry them. Ignored at the one point everything reads
        // through (the count that gates dragging, the draw, the click), so no save is rewritten
        // and no migration is needed: what cannot be true is simply not reported.
        public bool Pinned => LockedByUser || LaborIsManaged || IsDisabled;

        public bool LockedByUser
        {
            get => !IsDisabled && Resource.PercentLock;
            set => Resource.PercentLock = value;
        }

        // Ludoal fork: ONE source for "is THIS row managed for you" - deduced from the colony
        // type at each site, it becomes two readings of one rule.
        //
        // ⚠ bench 525: it is per ROW, not per colony. A governor manages the whole split, so all
        // three stop answering; the sustenance pilot holds ONE row and leaves the other two to
        // the player.
        public bool LaborIsManaged => P.AutoLabor && (P.HasLaborGovernor || IsSustenanceRow);

        // the row the pilot holds when there is no governor: food for most, production for the
        // cybernetic, who eat it
        bool IsSustenanceRow => Type == (P.IsCybernetic ? ColonyResType.Prod : ColonyResType.Food);

        public override bool HandleInput(InputState input)
        {
            // ⚠ bench 532: a row that refuses the CLICK still has something to say. Bailing out
            // before the hover makes the subsistence gauge's own tooltip unreachable by
            // construction - that gauge exists only on a cybernetic colony, which is exactly
            // where this row is disabled. Hover is not input the row acts on; it is the row
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

            // the track is the socle's drawing - one arithmetic for every slider
            FloatSlider.DrawTrack(batch, Rect, Slider, Value, SliderHover, sliderTint);

            if (DrawIcons)
            {
                // the row measures production, so it wears production's icon and not the food
                // one, in both states (bench 525)
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

            // ⚠ on a cybernetic colony the FOOD row is named rather than numbered, in both states
            // (bench 525). No number: its income figures would be production's, which the row
            // right below already prints, and the same number beside a different bar reads as a
            // contradiction. "Consumption" says what the bar is - the share of their output they eat.
            if (IsCyberneticFoodRow)
            {
                // grey and SIGNED: it is a demand, not something the player is producing - and
                // the minus says out loud that it comes off the production above it (bench 535)
                DrawAlignedNumber(batch, font, "-" + P.Consumption.StringFixed1(), curComma, y, Color.Gray);
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
            // the decimal is always written: "0.#" drops it on a whole number and the figure
            // jumps off the comma column the moment 9.8 becomes 10 (bench 608)
            DrawAlignedNumber(batch, font, value.StringFixed1(), curComma, y, color);

            if (ShowMaxValue)
            {
                float slashX   = curComma + font.TextWidth(".0") + 4; // fraction room + gap
                float maxComma = slashX + font.TextWidth("/ ") + unitsW;
                batch.DrawString(font, "/", new Vector2(slashX, y), Colors.Cream.Alpha(0.5f));
                // The max is a potential, not a state: grey so it informs without rivalling the real value.
                DrawAlignedNumber(batch, font, (IsCyberneticProdRow ? GrossMaxOutput : MaxValue).StringFixed1(),
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
