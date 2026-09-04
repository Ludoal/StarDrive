using System;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Color = Microsoft.Xna.Framework.Color;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game.GameScreens
{
    // Ludoal fork: one COLUMN per major empire, everything built ONCE in LoadContent.
    // Portraits Diplomacy-style, and they open negotiation. Sections:
    // BUDGET (player: budget multiplier + cost; others: infiltration weight, limit
    // level, points/turn, target level + progress), DEFENSE (player: defense weight;
    // others: their shield ratio), then the five levels with ALL options — passives
    // and actives with live checkboxes, grayed until their level is reached.
    public sealed class InfiltrationScreen : GameScreen
    {
        public readonly UniverseScreen Universe;
        // the player's mole-planet rows, harvested at draw, clicked in HandleInput
        readonly Array<(Rectangle Rect, Planet P)> MoleRows = new();
        public Empire SelectedEmpire; // legacy bookkeeping (external callers)
        readonly Empire Player;
        // Ludoal fork: where each portrait landed this frame, so the click can find it - the
        // portrait is the way in to negotiation on every Diplomacy tab.
        readonly Map<Empire, Rectangle> PortraitRects = new();
        public static readonly Color PanelBackground = new Color(23, 20, 14);

        Submenu GroupTabs; // Ludoal fork: the Diplomacy group's tab row, this screen being one tab
        // Ludoal fork: this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => GroupTabs?.Rect ?? base.PageFrame;
        Rectangle LeftRect;

        Array<EmpireColumn> Columns = new();
        // Combined Arms fields more than eight majors: the row scrolls by whole columns;
        // this screen's widgets are real UI children, so a scroll REPOSITIONS them and
        // hides the columns outside the window
        readonly ScreenGroups.RaceRowScroller Scroller = new();
        int AppliedFirst = -1; // last scroll position applied to the widgets

        Font Font12 = Fonts.Arial12;
        Font Font12Bold = Fonts.Arial12Bold;
        Font NameFont = Fonts.Arial14Bold;

        // fixed vertical anatomy (aligned across columns)
        const int HeaderH = 110;
        const int BudgetH = 231; // maintainer measured: three 69px blocks plus the 24px inset

        // maintainer feedback: the BUDGET block is a grid of NINE rows - three blocks of three,
        // one text line apart. A block is a label, its slider and the figure the slider produces;
        // the same row carries the same KIND of thing in every column, so the player's spending
        // control lines up with a rival's. Placement (LoadContent) and labels (DrawColumn) both
        // read these, off ONE origin - they used to sit 24px apart and the numbers looked
        // comparable when they were not.
        const int RowH   = 23;                 // one text line - the grid's unit
        const int Block1 = 4;                  // rows 1-3
        const int Block2 = Block1 + 3 * RowH;  // rows 4-6  (73)
        const int Block3 = Block2 + 3 * RowH;  // rows 7-9  (142)
        const int RowValue = 2 * RowH;         // third row of a block: the figure, under the slider
        const int DefenseH = 33; // the 19px the budget grid took: a shield and its figure, no more

        class EmpireColumn
        {
            public Empire E;
            public Ship_Game.Espionage Esp; // null for the player
            public Rectangle Rect;
            public Rectangle Base;   // unscrolled position - Rect = Base shifted by the scroll
            public bool Shown = true;
            public FloatSlider Weight;      // infiltration weight (others) / defense weight (player)
            public FloatSlider Budget;      // player only
            public FloatSlider Limit;       // others only: infiltration level ceiling
            public Array<OpBox> Ops = new();
        }

        // one live operation checkbox, bound exactly like the legacy ops panels
        class OpBox
        {
            public bool Flag;
            public readonly UICheckBox Box;
            public readonly UILabel Turns;
            public readonly Ship_Game.Espionage Esp;
            public readonly InfiltrationOpsType Type;
            public readonly byte Level;
            readonly Empire Player;
            readonly bool UpdatesDefense;
            // the label folds only while the turns counter needs the room beside it AND the
            // full name would actually reach it. The geometry is fixed at construction, so the
            // collision is computed once - a wide column keeps its full names even with the
            // counter shown
            readonly LocalizedText Folded, Full;
            readonly bool FoldNeeded;
            bool ShowsFolded;
            public bool ColumnShown = true; // the scroll window; Sync composes it in

            public OpBox(GameScreen screen, Ship_Game.Espionage esp, Empire player, byte level,
                         InfiltrationOpsType type, LocalizedText folded, LocalizedText full,
                         LocalizedText tip, Vector2 pos, float turnsX, bool updatesDefense = false)
            {
                Esp = esp;
                Type = type;
                Level = level;
                Player = player;
                UpdatesDefense = updatesDefense;
                Folded = folded;
                Full = full;
                // 12 + 4 mirrors UICheckBox's own box size and text padding
                FoldNeeded = pos.X + 16 + Fonts.Arial12.TextWidth(full.Text) + 6 > turnsX;
                ShowsFolded = FoldNeeded && esp.Level >= level;
                Box = screen.Add(new UICheckBox(() => Flag, Fonts.Arial12, ShowsFolded ? folded : full, tip));
                Box.Pos = pos;
                Box.OnChange = OnChanged;
                Turns = screen.Add(new UILabel(new Vector2(turnsX, pos.Y), "", Fonts.Arial12));
            }

            void OnChanged(UICheckBox b)
            {
                if (Flag)
                    Esp.ActivateOpsIfAble(Type);
                else
                    Esp.RemoveOperation(Type);
                if (UpdatesDefense)
                    Player.UpdateEspionageDefenseRatio();
            }

            public void Sync()
            {
                bool reached = Esp.Level >= Level;
                bool wantFolded = reached && FoldNeeded;
                if (wantFolded != ShowsFolded)
                {
                    ShowsFolded = wantFolded;
                    Box.Text = wantFolded ? Folded : Full;
                    Box.PerformLayout(); // the hit rect follows the text
                }
                Box.Enabled = reached && ColumnShown;
                Box.Visible = ColumnShown;
                Flag = reached && Esp.IsOperationActive(Type);
                Box.TextColor = reached ? Color.White : Color.Gray;
                Turns.Visible = reached && ColumnShown;
                if (reached)
                {
                    Turns.Text = Esp.RemainingTurnsForOps(Type);
                    Turns.Color = Flag ? Color.LightGreen : Color.White;
                }
            }
        }

        public InfiltrationScreen(UniverseScreen parent) : base(parent, toPause: parent)
        {
            Universe = parent;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            Player = Universe.Player;
            SelectedEmpire = Player;
        }

        // Ludoal fork: the other three tabs live on the Diplomacy screen, so leaving Espionage
        // hands over to it on the right tab. Espionage itself is a no-op: we are already here.
        void OnGroupTabChanged(int index)
        {
            // Ludoal fork: the hosted colony's tab, appended past the stock four
            if (ScreenGroups.IsHostedTab(ScreenGroups.Group.Diplomacy, index, Universe))
            {
                ExitScreen();
                Universe.OpenHostedTabPanel?.Invoke();
                return;
            }
            var tab = (MainDiplomacyScreen.Tab)index;
            if (tab == MainDiplomacyScreen.Tab.Espionage)
                return;
            ExitScreen();
            ScreenManager.AddScreen(new MainDiplomacyScreen(Universe, tab));
        }

        public override void LoadContent()
        {
            // Ludoal fork: the Espionage tab of the Diplomacy group - same frame and tab row as
            // its three siblings, from ScreenGroups, in place of the title and its surround.
            // The frame hugs the race columns, so the majors come first.
            Empire[] majors = Universe.UState.ActiveMajorEmpires;
            LeftRect = ScreenGroups.RaceColumnsFrame(ScreenWidth, ScreenHeight, majors.Length);
            GroupTabs = Add(new Submenu(new RectF(LeftRect.X, LeftRect.Y, LeftRect.Width, LeftRect.Height),
                                        ScreenGroups.LiveTitles(ScreenGroups.Group.Diplomacy, Universe)));
            GroupTabs.OnTabChange = OnGroupTabChanged;
            GroupTabs.PerformLayout(); // ClientArea is only known once the tabs are laid out
            GroupTabs.SelectedIndex = (int)MainDiplomacyScreen.Tab.Espionage;

            Vector2 closePos = ScreenGroups.GroupClosePos(GroupTabs.ClientArea);
            CloseButton(closePos.X, closePos.Y);

            // the race-column doctrine: one bounded pitch, the row centred in its hugging
            // frame - and scrolling by whole columns when the majors outnumber the window
            RectF client = GroupTabs.ClientArea;
            int colW = ScreenGroups.RaceColumnPitch(ScreenWidth, majors.Length);
            int visCols = ScreenGroups.RaceVisibleColumns(ScreenWidth, majors.Length);
            int x0 = ScreenGroups.RaceColumnsLeft(client, colW, visCols);
            Scroller.Count = majors.Length;
            Scroller.VisibleCols = visCols;
            Scroller.Pitch = colW;
            // the rail sits INSIDE the frame, its foot 5px off the bottom border, and the
            // columns give it the room
            Scroller.Track = new Rectangle(x0, (int)client.Bottom - 5, visCols * colW - ScreenGroups.ColumnGap, 9);
            Scroller.WheelArea = new Rectangle((int)client.X, (int)client.Y, (int)client.W, (int)client.H);
            int colH = ScreenGroups.GroupColumnHeight(client) - (Scroller.Overflowing ? 14 : 0);

            for (int i = 0; i < majors.Length; ++i)
            {
                Empire e = majors[i];
                // Ludoal fork: inside the tab frame's client area, like the other tabs
                var col = new Rectangle(x0 + i * colW, ScreenGroups.GroupColumnTop(client),
                                        colW - ScreenGroups.ColumnGap, colH);
                var c = new EmpireColumn { E = e, Rect = col, Base = col };
                Columns.Add(c);

                bool known = e == Player || Player.IsKnown(e);
                if (!known || e.IsDefeated)
                    continue;

                float budgetY = col.Y + HeaderH + 24;
                if (e == Player)
                {
                    // BUDGET: multiplier (+ cost label drawn live); DEFENSE: weight
                    var budgetRect = new Rectangle(col.X + 8, (int)budgetY + Block1, col.Width - 60, 40);
                    c.Budget = new FloatSlider(SliderStyle.Decimal1, budgetRect, GameText.EspioangeBudgetMuliplier, 1f, 5f, value: Player.EspionageBudgetMultiplier);
                    c.Budget.Tip = GameText.EspioangeBudgetMuliplierTip;
                    c.Budget.OnChange = s =>
                    {
                        Player.UpdateEspionageDefenseRatio();
                        Player.SetEspionageBudgetMultiplier(s.AbsoluteValue.RoundToFractionOf10());
                    };
                    Add(c.Budget);

                    // maintainer feedback: Defense Weight is a spending decision, so it belongs
                    // with the budget - on the same row the rival columns give Level Max, which
                    // keeps every column's second slider on one line.
                    var defRect = new Rectangle(col.X + 8, (int)budgetY + Block2, col.Width - 60, 40);
                    c.Weight = new FloatSlider(defRect, GameText.EspioangeDefenseWeight, min: 0,
                                               max: Empire.MaxEspionageDefenseWeight, value: Player.EspionageDefenseWeight);
                    c.Weight.Tip = GameText.EspioangeDefenseWeightTip;
                    c.Weight.OnChange = s =>
                    {
                        Player.SetEspionageDefenseWeight(s.AbsoluteValue.RoundUpTo(1));
                        Player.UpdateEspionageDefenseRatio();
                    };
                    Add(c.Weight);
                    continue;
                }

                Ship_Game.Espionage esp = Player.GetEspionage(e);
                c.Esp = esp;

                var weightRect = new Rectangle(col.X + 8, (int)budgetY + Block2, col.Width - 60, 40);
                c.Weight = new FloatSlider(weightRect, GameText.EspioangeInfiltrationWeight, min: 0, max: 10, value: esp.GrossWeight);
                c.Weight.Tip = GameText.EspioangeInfiltrationWeightTip;
                c.Weight.OnChange = s =>
                {
                    esp.SetWeight(s.AbsoluteValue.RoundUpTo(1));
                    Player.UpdateEspionageDefenseRatio();
                };
                Add(c.Weight);

                // Ludoal fork: a slider rather than a click-to-cycle button. Five discrete levels
                // is what a slider says plainly, the two other settings of this column already are
                // one, and the button showed no value - it stretches to the width it is given, so
                // the figure drawn beside it fell underneath.
                var limitRect = new Rectangle(col.X + 8, (int)budgetY + Block1, col.Width - 60, 40);
                c.Limit = new FloatSlider(SliderStyle.Decimal, limitRect, GameText.IfLevelMax,
                                          1f, Ship_Game.Espionage.MaxLevel, value: esp.LimitLevel);
                c.Limit.Tip = GameText.EspionageLimitLevelTip;
                c.Limit.OnChange = s => esp.SetLimitLevel((byte)s.AbsoluteValue.RoundUpTo(1));
                Add(c.Limit);

                // the five levels, ALL options — grayed until reached. Rows come from the shared
                // cascade so they land exactly where DrawColumn paints their level.
                ForEachInfiltrationRow(col, null, (level, rowY, i) =>
                {
                    var (type, folded, full, tip, def) = ActiveOpsFor(level)[i];
                    c.Ops.Add(new OpBox(this, esp, Player, level, type, folded, full, tip,
                                        new Vector2(col.X + 16, rowY), col.Right - 72, def));
                });
            }

            GameAudio.MuteRacialMusic();
        }

        // Ludoal fork: ONE band, "INFILTRATION", and the five levels as bold text lines under it -
        // five stacked bands read as five sections when it is one subject. The layout of that block
        // lives here so LoadContent (which places the operation checkboxes) and DrawColumn (which
        // paints the labels) cannot disagree.
        //
        // Yields the Y of each level's title line, then of each of its operation rows.
        void ForEachInfiltrationRow(Rectangle col, Action<byte, float, bool> onLevelTitle,
                                    Action<byte, float, int> onOpRow)
        {
            float y = col.Y + HeaderH + BudgetH + DefenseH + 14 + 24;
            for (byte level = 1; level <= Ship_Game.Espionage.MaxLevel; ++level)
            {
                onLevelTitle?.Invoke(level, y, true);
                y += Font12Bold.LineSpacing + 2;
                onLevelTitle?.Invoke(level, y, false); // the passive line, under the title
                y += Font12.LineSpacing + 4;
                var ops = ActiveOpsFor(level);
                for (int i = 0; i < ops.Length; ++i)
                {
                    onOpRow?.Invoke(level, y, i);
                    y += Font12.LineSpacing + 5;
                }
                y += 8; // breathing room before the next level
            }
        }

        // folded op labels: the long names do not fit a 900p column beside their turn counters,
        // so a checkbox wears the tail word WHILE the counter shows (level reached) and the
        // full name the rest of the time; the tooltip always opens on the full name. Sabotage
        // is one word already and keeps its token.
        static (InfiltrationOpsType, LocalizedText, LocalizedText, LocalizedText, bool) Op(InfiltrationOpsType type,
            string folded, GameText fullName, GameText tip, bool def)
            => (type, folded, Localizer.Token(fullName),
                Localizer.Token(fullName) + "\n\n" + Localizer.Token(tip), def);

        static (InfiltrationOpsType, LocalizedText, LocalizedText, LocalizedText, bool)[] ActiveOpsFor(byte level) => level switch
        {
            2 => new[] { Op(InfiltrationOpsType.PlantMole, "Agent", GameText.PlantAgent, GameText.PlantAgentTip, false) },
            3 => new[] { Op(InfiltrationOpsType.Uprise, "Uprise", GameText.ArrangeUprise, GameText.ArrangeUpriseTip, false),
                         Op(InfiltrationOpsType.CounterEspionage, "Counter", GameText.CounterEspioangeOps, GameText.CounterEspioangeOpsTip, true) },
            4 => new[] { (InfiltrationOpsType.Sabotage, (LocalizedText)GameText.Sabotage, (LocalizedText)GameText.Sabotage, (LocalizedText)GameText.EspioangeOpsSabotageTip, false),
                         Op(InfiltrationOpsType.SlowResearch, "Research", GameText.EspioangeOpsSlowResearch, GameText.EspioangeOpsSlowResearchTip, false) },
            5 => new[] { Op(InfiltrationOpsType.Rebellion, "Rebellion", GameText.EspioangeOpsRebellion, GameText.EspioangeOpsRebellionTip, false),
                         Op(InfiltrationOpsType.DisruptProjection, "Projection", GameText.EspioangeOpsDisruptProjection, GameText.EspioangeOpsDisruptProjectionTip, false) },
            _ => System.Array.Empty<(InfiltrationOpsType, LocalizedText, LocalizedText, LocalizedText, bool)>(),
        };

        static string PassiveFor(byte level) => level switch
        {
            1 => Localizer.Token(GameText.UhScanTheirShips),
            2 => Localizer.Token(GameText.IfProjectorsAlert),
            3 => Localizer.Token(GameText.IfHomeworldMole),
            4 => Localizer.Token(GameText.IfLeechTechnology),
            _ => Localizer.Token(GameText.IfLeechIncome),
        };

        public override void Update(float fixedDeltaTime)
        {
            if (AppliedFirst != Scroller.First)
                ApplyScroll();
            foreach (EmpireColumn c in Columns)
                foreach (OpBox op in c.Ops)
                    op.Sync();
            base.Update(fixedDeltaTime);
        }

        // reposition every column and its widgets for the current scroll window - the
        // widgets are real UI children, so sliding the window means moving them
        void ApplyScroll()
        {
            AppliedFirst = Scroller.First;
            int dx = Scroller.Overflowing ? Scroller.OffsetX : 0;
            for (int i = 0; i < Columns.Count; ++i)
            {
                EmpireColumn c = Columns[i];
                bool shown = !Scroller.Overflowing || Scroller.Shows(i);
                c.Shown = shown;
                Rectangle col = c.Base;
                col.X -= dx;
                c.Rect = col;
                if (c.Weight != null)
                {
                    c.Weight.Visible = shown;
                    c.Weight.Rect = new Rectangle(col.X + 8, (int)c.Weight.Rect.Y, (int)c.Weight.Rect.Width, (int)c.Weight.Rect.Height);
                }
                if (c.Budget != null)
                {
                    c.Budget.Visible = shown;
                    c.Budget.Rect = new Rectangle(col.X + 8, (int)c.Budget.Rect.Y, (int)c.Budget.Rect.Width, (int)c.Budget.Rect.Height);
                }
                if (c.Limit != null)
                {
                    c.Limit.Visible = shown;
                    c.Limit.Rect = new Rectangle(col.X + 8, (int)c.Limit.Rect.Y, (int)c.Limit.Rect.Width, (int)c.Limit.Rect.Height);
                }
                foreach (OpBox op in c.Ops)
                {
                    op.ColumnShown = shown;
                    op.Box.Pos = new Vector2(col.X + 16, op.Box.Pos.Y);
                    op.Box.PerformLayout();
                    op.Turns.Pos = new Vector2(col.Right - 72, op.Turns.Pos.Y);
                }
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();

            // Ludoal fork: the frame fill goes down FIRST, by hand. As a Submenu background it is
            // one of the screen's children, so base.Draw would paint it AFTER the columns below -
            // SendToBackZOrder only orders it among the other children.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GroupTabs), ScreenGroups.GroupFrameFill);

            // ⚠ cleared every pass: a column that stops drawing its portrait must not leave a
            // clickable rect behind over whatever takes its place.
            PortraitRects.Clear();
            foreach (EmpireColumn c in Columns)
                if (c.Shown)
                    DrawColumn(batch, c);

            base.Draw(batch, elapsed); // sliders, checkboxes, buttons, close
            Scroller.Draw(batch); // over the border band, after the frame painted it
            ScreenGroups.DrawGroupTabTip(GroupTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        void SectionBand(SpriteBatch batch, Rectangle col, float y, string title, Color? textColor = null)
        {
            var band = new Rectangle(col.X + 1, (int)y, col.Width - 2, 18);
            batch.FillRectangle(band, new Color(54, 46, 24));
            batch.DrawString(Font12Bold, title, new Vector2(band.X + (band.Width - Font12Bold.TextWidth(title)) / 2f, band.Y + 2), textColor ?? Colors.Cream);
        }

        void DrawColumn(SpriteBatch batch, EmpireColumn c)
        {
            Empire e = c.E;
            Rectangle col = c.Rect;
            batch.FillRectangle(col, PanelBackground);
            batch.DrawRectangle(col, new Color(60, 54, 40));

            bool known = e == Player || Player.IsKnown(e);
            var portrait = new Rectangle(col.X + (col.Width - 56) / 2, col.Y + 6, 56, 70);

            if (!known)
            {
                batch.Draw(ResourceManager.Texture("Portraits/unknown"), portrait, Color.White);
                string unknown = Localizer.Token(GameText.EspInfilUnknown);
                batch.DrawString(Font12Bold, unknown, new Vector2(col.X + (col.Width - Font12Bold.TextWidth(unknown)) / 2f, portrait.Bottom + 4), Color.Gray);
                return;
            }

            if (e != Player && !e.IsDefeated && Player.IsAtWarWith(e))
                batch.DrawRectangle(new Rectangle(portrait.X - 2, portrait.Y - 2, portrait.Width + 4, portrait.Height + 4), Color.Red);

            batch.Draw(ResourceManager.Texture("Portraits/" + e.data.PortraitName), portrait, Color.White);

            // Ludoal fork: the portrait opens negotiation here too - the four Diplomacy tabs
            // behave the same way, and this one drawing its portraits inert would be the odd one.
            if (e != Player && !e.IsDefeated)
            {
                PortraitRects[e] = portrait;
                batch.DrawRectangle(portrait, e.EmpireColor,
                                    portrait.HitTest(Input.CursorPosition) ? 3 : 1);
            }
            // the race flag rides LEFT OF THE PORTRAIT, a touch bigger, so the name gets
            // the column's full width
            batch.Draw(ResourceManager.Flag(e.data.Traits.FlagIndex),
                       new Rectangle(portrait.X - 30, portrait.Y, 24, 24), e.EmpireColor);
            string name = e.data.Traits.Name;
            float nameX = col.X + (col.Width - NameFont.TextWidth(name)) / 2f;
            batch.DrawDropShadowText1(name, new Vector2(nameX, portrait.Bottom + 4), NameFont, e.EmpireColor);

            if (e.IsDefeated)
            {
                batch.Draw(ResourceManager.ErrorTexture, portrait, Color.White);
                batch.DrawString(Font12, Localizer.Token(GameText.IfDefeated), new Vector2(col.X + 8, col.Y + HeaderH + 4), Color.Gray);
                return;
            }

            SectionBand(batch, col, col.Y + HeaderH, Localizer.Token(GameText.IfBudget));
            float budgetY = col.Y + HeaderH + 24; // the SAME origin the sliders are placed on
            // 30 higher; the INFILTRATION block below is keyed
            // on BudgetH + DefenseH and does not follow
            float defenseY = col.Y + HeaderH + BudgetH - 6;
            SectionBand(batch, col, defenseY, Localizer.Token(GameText.IfDefense));

            if (e == Player)
            {
                // budget cost line under the slider (legacy formula)
                float espionageCost = Player.GetEspionageCost();
                string cost = $"{(espionageCost > 0 ? -espionageCost : espionageCost).String(1)} " + Localizer.Token(GameText.IfBcPerTurn);
                batch.DrawString(Font12, cost, new Vector2(col.X + 8, budgetY + Block1 + RowValue), espionageCost > 0 ? Color.Pink : Color.LightGreen);

                // maintainer feedback: defence shares the denominator with every infiltration
                // weight, so raising those shrinks its slice - and nothing said so. Two figures
                // now do: the points defence absorbs, on row 6 under its own slider like a
                // rival's yield, and the share it takes, in the DEFENSE band below.
                int ownTotal = Player.CalcTotalEspionageWeight();
                float ownPpt = ownTotal > 0 ? Player.EspionagePointsPerTurn * Player.EspionageDefenseWeight / ownTotal : 0;
                batch.DrawString(Font12, Localizer.Token(GameText.IfPointsPerTurn) + ownPpt.String(3),
                                 new Vector2(col.X + 8, budgetY + Block2 + RowValue), Color.Wheat);

                SubTexture ownShield = ResourceManager.Texture("UI/icon_shield");
                var ownShieldRect = new Rectangle(col.X + 8, (int)defenseY + 24, ownShield.Width, ownShield.Height);
                batch.Draw(ownShield, ownShieldRect, Color.White);
                int ownShare = ownTotal > 0 ? (int)(Player.EspionageDefenseWeight * 100f / ownTotal) : 100;
                batch.DrawString(Font12Bold, $"{ownShare.String()}%",
                                 new Vector2(ownShieldRect.Right + 6, ownShieldRect.Y + 4), Color.White);
                // Ludoal fork: the SETTINGS band that lived here (Disable Messages) moved to the
                // Automation tab of the Empire group, with the other notification switches.

                // the player's own INFILTRATION block - the planets our moles sit on, clickable
                // (opens that colony in mole vision, like a map double-click). Rects harvested
                // here, hit-tested in HandleInput like the portraits.
                float infilY = col.Y + HeaderH + BudgetH + DefenseH + 14;
                SectionBand(batch, col, infilY, Localizer.Token(GameText.IfInfiltration));
                MoleRows.Clear();
                float rowY = infilY + 24;
                batch.DrawString(Font12Bold, Localizer.Token(GameText.UhPlanetsWithMoles), new Vector2(col.X + 8, rowY), Colors.Cream);
                rowY += Font12Bold.LineSpacing + 4;
                var moles = Player.data.MoleList;
                if (moles.Count == 0)
                {
                    batch.DrawString(Font12, Localizer.Token(GameText.IfNoneYet), new Vector2(col.X + 8, rowY), Color.Gray);
                }
                else
                {
                    for (int i = 0; i < moles.Count; i++)
                    {
                        Planet moleP = Universe.UState.GetPlanet(moles[i].PlanetId);
                        if (moleP == null)
                            continue;
                        if (rowY + Font12.LineSpacing > col.Bottom - 6)
                            break; // column is full
                        var rowRect = new Rectangle(col.X + 8, (int)rowY, col.Width - 16, Font12.LineSpacing + 2);
                        bool hover = rowRect.HitTest(Input.CursorPosition);
                        batch.DrawString(Font12, moleP.Name, new Vector2(rowRect.X, rowRect.Y),
                                         hover ? Color.White : (moleP.Owner?.EmpireColor ?? Colors.Cream));
                        MoleRows.Add((rowRect, moleP));
                        rowY += Font12.LineSpacing + 2;
                    }
                }
                return;
            }

            Ship_Game.Espionage esp = c.Esp;

            // BUDGET section extras: points/turn, target + progress. The level ceiling needs no
            // label of its own - its slider shows the figure.
            float ppt = esp.GetProgressToIncrease(Player.EspionagePointsPerTurn, Player.CalcTotalEspionageWeight());
            string pptTxt = Localizer.Token(GameText.IfPointsPerTurn) + ppt.String(3);
            batch.DrawString(Font12, pptTxt, new Vector2(col.X + 8, budgetY + Block2 + RowValue), Color.Wheat);

            if (esp.Level < Ship_Game.Espionage.MaxLevel)
            {
                byte target = (byte)(esp.Level + 1);
                batch.DrawString(Font12Bold, string.Format(Localizer.Token(GameText.IfInfiltratingLevel), target), new Vector2(col.X + 8, budgetY + Block3), Color.Wheat);
                float max = esp.LevelCost(target);
                float cur = esp.LevelProgress.UpperBound(max);
                var barRect = new Rectangle(col.X + 8, (int)budgetY + Block3 + RowH, col.Width - 16, 12);
                batch.FillRectangle(barRect, new Color(10, 10, 10));
                if (max > 0f && cur > 0f)
                    batch.FillRectangle(new Rectangle(barRect.X + 1, barRect.Y + 1, (int)((barRect.Width - 2) * (cur / max)), 10), new Color(30, 120, 30));
                batch.DrawRectangle(barRect, new Color(60, 54, 40));
                string nums = $"{(int)cur}/{(int)max}";
                batch.DrawString(Font12, nums, new Vector2(col.Right - 8 - Font12.TextWidth(nums), budgetY + Block3 + RowValue), Color.Wheat);
            }
            else
            {
                batch.DrawString(Font12, Localizer.Token(GameText.IfFullyInfiltrated), new Vector2(col.X + 8, budgetY + Block3), Color.LightGreen);
            }

            // DEFENSE: their shield ratio (gated like the legacy header icon)
            SubTexture shield = ResourceManager.Texture("UI/icon_shield");
            var defenseIcon = new Rectangle(col.X + 8, (int)defenseY + 24, shield.Width, shield.Height);
            batch.Draw(shield, defenseIcon, Color.White);
            bool canSeeDef = esp.CanViewDefenseRatio;
            if (canSeeDef)
            {
                string defTxt = $"{((int)(e.EspionageDefenseRatio * 100)).String()}%";
                batch.DrawString(Font12Bold, defTxt, new Vector2(defenseIcon.Right + 6, defenseIcon.Y + 4), Color.White);
            }
            else // spy icon + "lvl 3" — the level that unlocks it, like the Diplomacy placeholders
            {
                SubTexture spyIcon = ResourceManager.Texture("UI/icon_spy");
                int h = Font12.LineSpacing;
                var spyR = new Rectangle(defenseIcon.Right + 6, defenseIcon.Y + 4, spyIcon.Width * h / spyIcon.Height, h);
                batch.Draw(spyIcon, spyR, new Color(105, 105, 105));
                batch.DrawString(Font12Bold, Localizer.Token(GameText.IfLvl3), new Vector2(spyR.Right + 4, spyR.Y), new Color(105, 105, 105));
            }

            // Ludoal fork: a line's worth of reserve is kept under the Defense row (DefenseH holds
            // it) even though nothing is drawn here now - the "Known Infiltration Level" readout was
            // removed (its only source, a Counter-Espionage Phenomenal, wipes the level it reveals,
            // so the value was caduc on arrival). The reserve keeps the Infiltration band from
            // riding up if a defensive readout is added here later.

            // Ludoal fork: one INFILTRATION band, then each level as a bold text line - cream once
            // the level is uncovered, grey while it is not. Five bands for one subject read as five
            // separate sections.
            SectionBand(batch, col, col.Y + HeaderH + BudgetH + DefenseH + 14, Localizer.Token(GameText.IfInfiltration));
            ForEachInfiltrationRow(col, (level, rowY, isTitle) =>
            {
                bool reached = esp.Level >= level;
                if (isTitle)
                {
                    batch.DrawString(Font12Bold, $"Level {level}", new Vector2(col.X + 8, rowY),
                                     reached ? Colors.Cream : Color.Gray);
                }
                else
                {
                    bool active = reached && esp.LimitLevel >= level;
                    batch.DrawString(Font12, PassiveFor(level), new Vector2(col.X + 16, rowY),
                                     active ? Color.LightGreen : Color.Gray);
                }
            }, null);
        }

        public override bool HandleInput(InputState input)
        {
            // Ludoal fork: the closing key is tested BEFORE the top bar, not after - the bar
            // reads the same key to OPEN this screen and returns true, so with the bar first
            // the key would never reach the line below and the screen would not close on its
            // own hotkey.
            if (input.KeyPressed(Keys.E) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (Scroller.HandleInput(input))
                return true;

            // DOUBLE-click opens the mole colony - checked before the
            // single-click block, or the second click would pan again instead of opening
            if (input.LeftMouseDoubleClick)
            {
                foreach ((Rectangle rect, Planet moleP) in MoleRows)
                {
                    if (rect.HitTest(input.CursorPosition))
                    {
                        GameAudio.AcceptClick();
                        // Ludoal fork: the mole colony rides the Diplomacy seat,
                        // Espionage as the Esc origin.
                        Universe.HostColonyTab(moleP, GameScreens.ScreenGroups.Group.Diplomacy,
                                               (int)MainDiplomacyScreen.Tab.Espionage);
                        // a stacked page like every tab: exit + open in
                        // the same frame, the fresh ctor claims the pause before any tick
                        ExitScreen();
                        Universe.ScreenManager.AddScreen(new ColonyScreen(Universe, moleP, Universe.EmpireUI));
                        return true;
                    }
                }
            }

            // Ludoal fork: the portrait opens negotiation, as on the other Diplomacy tabs. The
            // rects come from the last draw, which is where the columns are laid out.
            if (input.LeftMouseClick)
            {
                foreach (var kv in PortraitRects)
                {
                    if (kv.Value.HitTest(input.CursorPosition))
                    {
                        GameAudio.AcceptClick();
                        // ⚠ fully qualified: this file sits in Ship_Game.GameScreens, where the
                        // DiplomacyScreen NAMESPACE shadows the class of the same name
                        DiplomacyScreen.DiplomacyScreen.Show(kv.Key, "Greeting", parent: this);
                        return true;
                    }
                }

                // a SINGLE click pans the map to the mole planet, live in the band behind the
                // page. Opening the colony is on the double-click.
                foreach ((Rectangle rect, Planet moleP) in MoleRows)
                {
                    if (rect.HitTest(input.CursorPosition))
                    {
                        GameAudio.AcceptClick();
                        // pan at the CURRENT zoom - the cartouche's own buttons do the zooming
                        Universe.PanToPlanetKeepZoom(moleP);
                        return true;
                    }
                }
            }

            return base.HandleInput(input);
        }

        // legacy bookkeeping — external callers and old panels reference these
        public void RefreshSelectedEmpire(Empire selectedEmpire)
        {
            SelectedEmpire = selectedEmpire;
        }

        public void RefreshInfiltrationLevelStatus(Ship_Game.Espionage espionage)
        {
        }
    }
}
