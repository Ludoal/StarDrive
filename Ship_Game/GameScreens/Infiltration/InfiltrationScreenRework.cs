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
    // Ludoal fork v2 (player design): one COLUMN per major empire, everything built
    // ONCE in LoadContent (the previous draft leaked: EmpireButton re-Added elements
    // on every layout pass). Portraits Diplomacy-style, non-selectable. Sections:
    // BUDGET (player: budget multiplier + cost; others: infiltration weight, limit
    // level, points/turn, target level + progress), DEFENSE (player: defense weight;
    // others: their shield ratio), then the five levels with ALL options — passives
    // and actives with live checkboxes, grayed until their level is reached.
    public sealed class InfiltrationScreenRework : GameScreen
    {
        public readonly UniverseScreen Universe;
        public Empire SelectedEmpire; // legacy bookkeeping (external callers)
        readonly Empire Player;
        public static readonly Color PanelBackground = new Color(23, 20, 14);

        Submenu GroupTabs; // Ludoal fork: the Diplomacy group's tab row, this screen being one tab
        Rectangle LeftRect;

        Array<EmpireColumn> Columns = new();

        Font Font12 = Fonts.Arial12;
        Font Font12Bold = Fonts.Arial12Bold;
        Font NameFont = Fonts.Arial14Bold;

        // fixed vertical anatomy (aligned across columns)
        const int HeaderH = 110;
        const int BudgetH = 200; // sliders overflow their rect (title above, ticks below): breathe
        // Ludoal fork: where the SETTINGS band opens inside the player's BUDGET block - below its
        // cost line and clear of DEFENSE, which stays at BudgetH + 24 for every column. Measured
        // from col.Y so the band and the checkbox under it share ONE origin: LoadContent and
        // DrawColumn each have a local budgetY and they differ by 24.
        const int SettingsBandY = HeaderH + 110;
        const int DefenseH = 52;

        class EmpireColumn
        {
            public Empire E;
            public Ship_Game.Espionage Esp; // null for the player
            public Rectangle Rect;
            public FloatSlider Weight;      // infiltration weight (others) / defense weight (player)
            public FloatSlider Budget;      // player only
            public UIButton LimitBtn;       // others only
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

            public OpBox(GameScreen screen, Ship_Game.Espionage esp, Empire player, byte level,
                         InfiltrationOpsType type, LocalizedText label, LocalizedText tip,
                         Vector2 pos, float turnsX, bool updatesDefense = false)
            {
                Esp = esp;
                Type = type;
                Level = level;
                Player = player;
                UpdatesDefense = updatesDefense;
                Box = screen.Add(new UICheckBox(() => Flag, Fonts.Arial12, label, tip));
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
                Box.Enabled = reached;
                Flag = reached && Esp.IsOperationActive(Type);
                Box.TextColor = reached ? Color.White : Color.Gray;
                Turns.Visible = reached;
                if (reached)
                {
                    Turns.Text = Esp.RemainingTurnsForOps(Type);
                    Turns.Color = Flag ? Color.LightGreen : Color.White;
                }
            }
        }

        public InfiltrationScreenRework(UniverseScreen parent) : base(parent, toPause: parent)
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
            var tab = (MainDiplomacyScreenRework.Tab)index;
            if (tab == MainDiplomacyScreenRework.Tab.Espionage)
                return;
            ExitScreen();
            ScreenManager.AddScreen(new MainDiplomacyScreenRework(Universe, tab));
        }

        public override void LoadContent()
        {
            // Ludoal fork: the Espionage tab of the Diplomacy group - same tab row as the other
            // three, in place of the title cartouche and its brass surround. Right under the top
            // bar's button row (Y=64 on a 24px texture).
            const int tabRowY = 64 + 24;
            const int margin = 10;
            LeftRect = new Rectangle(margin, tabRowY, ScreenWidth - 2 * margin,
                                     ScreenHeight - tabRowY - margin);

            GroupTabs = Add(new Submenu(new RectF(LeftRect.X, LeftRect.Y, LeftRect.Width, LeftRect.Height),
                                        new LocalizedText[]
            {
                "Intelligence", "Bonuses", "Relationships", "Espionage"
            }));
            GroupTabs.OnTabChange = OnGroupTabChanged;
            GroupTabs.PerformLayout(); // ClientArea is only known once the tabs are laid out
            GroupTabs.SelectedIndex = (int)MainDiplomacyScreenRework.Tab.Espionage;

            CloseButton(LeftRect.Right - 40, LeftRect.Y + 20);

            Empire[] majors = Universe.UState.ActiveMajorEmpires;
            int n = majors.Length.LowerBound(1);
            RectF client = GroupTabs.ClientArea;
            int colW = (((int)client.W - 40) / n).UpperBound(230);
            int drawnW = colW * n - 8;
            int x0 = (int)client.X + ((int)client.W - drawnW) / 2;

            for (int i = 0; i < majors.Length; ++i)
            {
                Empire e = majors[i];
                // Ludoal fork: inside the tab frame's client area, like the other tabs
                int colTop = (int)client.Y + 6;
                var col = new Rectangle(x0 + i * colW, colTop, colW - 8, (int)client.Bottom - colTop - 12);
                var c = new EmpireColumn { E = e, Rect = col };
                Columns.Add(c);

                bool known = e == Player || Player.IsKnown(e);
                if (!known || e.IsDefeated)
                    continue;

                float budgetY = col.Y + HeaderH + 24;
                if (e == Player)
                {
                    // BUDGET: multiplier (+ cost label drawn live); DEFENSE: weight
                    var budgetRect = new Rectangle(col.X + 8, (int)budgetY + 4, col.Width - 60, 40);
                    c.Budget = new FloatSlider(SliderStyle.Decimal1, budgetRect, GameText.EspioangeBudgetMuliplier, 1f, 5f, value: Player.EspionageBudgetMultiplier);
                    c.Budget.Tip = GameText.EspioangeBudgetMuliplierTip;
                    c.Budget.OnChange = s =>
                    {
                        Player.UpdateEspionageDefenseRatio();
                        Player.SetEspionageBudgetMultiplier(s.AbsoluteValue.RoundToFractionOf10());
                    };
                    Add(c.Budget);

                    // Ludoal fork: Disable Messages moves off the title bar into a SETTINGS band,
                    // in the room the player's BUDGET block leaves empty. Same offset as the band
                    // drawn in DrawColumn, plus its 24px height.
                    Add(new UICheckBox(col.X + 8, col.Y + SettingsBandY + 26,
                                       () => Player.data.SpyMute, Fonts.Arial12,
                                       "Disable Messages", "Disable all Espionage notifications."));

                    var defRect = new Rectangle(col.X + 8, col.Y + HeaderH + BudgetH + 52, col.Width - 60, 40); // below the DEFENSE band
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

                var weightRect = new Rectangle(col.X + 8, (int)budgetY + 4, col.Width - 60, 40);
                c.Weight = new FloatSlider(weightRect, GameText.EspioangeInfiltrationWeight, min: 0, max: 10, value: esp.GrossWeight);
                c.Weight.Tip = GameText.EspioangeInfiltrationWeightTip;
                c.Weight.OnChange = s =>
                {
                    esp.SetWeight(s.AbsoluteValue.RoundUpTo(1));
                    Player.UpdateEspionageDefenseRatio();
                };
                Add(c.Weight);

                c.LimitBtn = Add(new UIButton(ButtonStyle.Low100, new Vector2(col.X + 8, budgetY + 44), GameText.EspionageLimitLevel));
                c.LimitBtn.Tooltip = GameText.EspionageLimitLevelTip;
                c.LimitBtn.AcceptRightClicks = true;
                c.LimitBtn.OnClick = b =>
                {
                    byte limit = (byte)(esp.LimitLevel + (Input.RightMouseReleased ? -1 : 1));
                    if (limit > Ship_Game.Espionage.MaxLevel) limit = 1;
                    if (limit < 1) limit = Ship_Game.Espionage.MaxLevel;
                    esp.SetLimitLevel(limit);
                };

                // the five levels, ALL options — grayed until reached
                float y = col.Y + HeaderH + BudgetH + DefenseH + 24;
                for (byte level = 1; level <= Ship_Game.Espionage.MaxLevel; ++level)
                {
                    y += 24; // band
                    y += Font12.LineSpacing + 4; // passive line
                    foreach ((InfiltrationOpsType type, LocalizedText label, LocalizedText tip, bool def) in ActiveOpsFor(level))
                    {
                        c.Ops.Add(new OpBox(this, esp, Player, level, type, label, tip,
                                            new Vector2(col.X + 8, y), col.Right - 64, def));
                        y += Font12.LineSpacing + 5;
                    }
                    y += 6;
                }
            }

            GameAudio.MuteRacialMusic();
        }

        static (InfiltrationOpsType, LocalizedText, LocalizedText, bool)[] ActiveOpsFor(byte level) => level switch
        {
            2 => new[] { (InfiltrationOpsType.PlantMole, (LocalizedText)GameText.PlantAgent, (LocalizedText)GameText.PlantAgentTip, false) },
            3 => new[] { (InfiltrationOpsType.Uprise, (LocalizedText)GameText.ArrangeUprise, (LocalizedText)GameText.ArrangeUpriseTip, false),
                         (InfiltrationOpsType.CounterEspionage, (LocalizedText)GameText.CounterEspioangeOps, (LocalizedText)GameText.CounterEspioangeOpsTip, true) },
            4 => new[] { (InfiltrationOpsType.Sabotage, (LocalizedText)GameText.Sabotage, (LocalizedText)GameText.EspioangeOpsSabotageTip, false),
                         (InfiltrationOpsType.SlowResearch, (LocalizedText)GameText.EspioangeOpsSlowResearch, (LocalizedText)GameText.EspioangeOpsSlowResearchTip, false) },
            5 => new[] { (InfiltrationOpsType.Rebellion, (LocalizedText)GameText.EspioangeOpsRebellion, (LocalizedText)GameText.EspioangeOpsRebellionTip, false),
                         (InfiltrationOpsType.DisruptProjection, (LocalizedText)GameText.EspioangeOpsDisruptProjection, (LocalizedText)GameText.EspioangeOpsDisruptProjectionTip, false) },
            _ => System.Array.Empty<(InfiltrationOpsType, LocalizedText, LocalizedText, bool)>(),
        };

        static string PassiveFor(byte level) => level switch
        {
            1 => "Scan their ships",
            2 => "Projectors alert",
            3 => "Homeworld mole",
            4 => "Leech technology",
            _ => "Leech income",
        };

        public override void Update(float fixedDeltaTime)
        {
            foreach (EmpireColumn c in Columns)
                foreach (OpBox op in c.Ops)
                    op.Sync();
            base.Update(fixedDeltaTime);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();

            foreach (EmpireColumn c in Columns)
                DrawColumn(batch, c);

            base.Draw(batch, elapsed); // sliders, checkboxes, buttons, close
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
                batch.DrawString(Font12Bold, "Unknown", new Vector2(col.X + (col.Width - Font12Bold.TextWidth("Unknown")) / 2f, portrait.Bottom + 4), Color.Gray);
                return;
            }

            if (e != Player && !e.IsDefeated && Player.IsAtWarWith(e))
                batch.DrawRectangle(new Rectangle(portrait.X - 2, portrait.Y - 2, portrait.Width + 4, portrait.Height + 4), Color.Red);

            batch.Draw(ResourceManager.Texture("Portraits/" + e.data.PortraitName), portrait, Color.White);
            string name = e.data.Traits.Name;
            float nameW = NameFont.TextWidth(name) + 24;
            float nameX = col.X + (col.Width - nameW) / 2f;
            batch.Draw(ResourceManager.Flag(e.data.Traits.FlagIndex), new Rectangle((int)nameX, portrait.Bottom + 4, 18, 18), e.EmpireColor);
            batch.DrawDropShadowText1(name, new Vector2(nameX + 24, portrait.Bottom + 4), NameFont, e.EmpireColor);

            if (e.IsDefeated)
            {
                batch.Draw(ResourceManager.ErrorTexture, portrait, Color.White);
                batch.DrawString(Font12, "Defeated", new Vector2(col.X + 8, col.Y + HeaderH + 4), Color.Gray);
                return;
            }

            float budgetY = col.Y + HeaderH;
            SectionBand(batch, col, budgetY, "BUDGET");
            float defenseY = col.Y + HeaderH + BudgetH + 24;
            SectionBand(batch, col, defenseY, "DEFENSE");

            if (e == Player)
            {
                // budget cost line under the slider (legacy formula)
                float espionageCost = Player.GetEspionageCost();
                string cost = $"{(espionageCost > 0 ? -espionageCost : espionageCost).String(1)} BC/turn";
                batch.DrawString(Font12, cost, new Vector2(col.X + 8, budgetY + 70), espionageCost > 0 ? Color.Pink : Color.LightGreen);
                // Ludoal fork: SETTINGS rides in the room the player's BUDGET block leaves empty -
                // the other columns fill all of BudgetH, this one stops at the cost line. It sits
                // ABOVE defenseY on purpose: DEFENSE stays level with the other columns.
                SectionBand(batch, col, col.Y + SettingsBandY, "SETTINGS");
                return;
            }

            Ship_Game.Espionage esp = c.Esp;

            // BUDGET section extras: limit level value, points/turn, target + progress
            batch.DrawString(Font12Bold, esp.LimitLevel.ToString(), new Vector2(col.X + 124, budgetY + 70), Player.EmpireColor);
            float ppt = esp.GetProgressToIncrease(Player.EspionagePointsPerTurn, Player.CalcTotalEspionageWeight());
            string pptTxt = "Points/turn: " + ppt.String(3);
            batch.DrawString(Font12, pptTxt, new Vector2(col.X + 8, budgetY + 96), Color.Wheat);

            if (esp.Level < Ship_Game.Espionage.MaxLevel)
            {
                byte target = (byte)(esp.Level + 1);
                batch.DrawString(Font12, $"Infiltrating level {target}", new Vector2(col.X + 8, budgetY + 116), Color.Wheat);
                float max = esp.LevelCost(target);
                float cur = esp.LevelProgress.UpperBound(max);
                var barRect = new Rectangle(col.X + 8, (int)budgetY + 137, col.Width - 16, 12);
                batch.FillRectangle(barRect, new Color(10, 10, 10));
                if (max > 0f && cur > 0f)
                    batch.FillRectangle(new Rectangle(barRect.X + 1, barRect.Y + 1, (int)((barRect.Width - 2) * (cur / max)), 10), new Color(30, 120, 30));
                batch.DrawRectangle(barRect, new Color(60, 54, 40));
                string nums = $"{(int)cur}/{(int)max}";
                batch.DrawString(Font12, nums, new Vector2(col.Right - 8 - Font12.TextWidth(nums), budgetY + 153), Color.Wheat);
            }
            else
            {
                batch.DrawString(Font12, "Fully infiltrated", new Vector2(col.X + 8, budgetY + 116), Color.LightGreen);
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
                batch.DrawString(Font12Bold, "lvl 3", new Vector2(spyR.Right + 4, spyR.Y), new Color(105, 105, 105));
            }

            // the five levels: band + passive + (checkboxes drawn by base.Draw)
            float y = col.Y + HeaderH + BudgetH + DefenseH + 24;
            for (byte level = 1; level <= Ship_Game.Espionage.MaxLevel; ++level)
            {
                bool reached = esp.Level >= level;
                bool active = reached && esp.LimitLevel >= level;
                SectionBand(batch, col, y, $"LEVEL {level}", reached ? (active ? Color.LightGreen : Color.Gray) : new Color(110, 100, 80));
                y += 24;
                batch.DrawString(Font12, PassiveFor(level), new Vector2(col.X + 8, y), active ? Color.LightGreen : Color.Gray);
                y += Font12.LineSpacing + 4;
                int nOps = ActiveOpsFor(level).Length;
                y += nOps * (Font12.LineSpacing + 5);
                y += 6;
            }
        }

        public override bool HandleInput(InputState input)
        {
            // Ludoal fork (bench 46.173): the closing key is tested BEFORE the top bar, not
            // after. The bar reads the same key to OPEN this screen and returns true, so with the
            // bar first the key never reached the line below and the screen would not close on
            // its own hotkey (maintainer feedback). The stock screen has no bar, which is why it never showed.
            if (input.KeyPressed(Keys.E) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;


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
