using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens;
using Ship_Game.GameScreens.Espionage;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public sealed class EmpireUIOverlay
    {
        public Empire Player;
        Rectangle res1;
        Rectangle res2;
        Rectangle res3;
        Rectangle res4;
        Rectangle res5;
        Array<Button> Buttons = new Array<Button>();
        bool LowRes;
        UniverseScreen Universe;

        public EmpireUIOverlay(Empire playerEmpire, GraphicsDevice device, UniverseScreen universe)
        {
            Player = playerEmpire;
            Universe = universe;

            var iRes1 = ResourceManager.Texture("EmpireTopBar/empiretopbar_res1");
            var iRes2 = ResourceManager.Texture("EmpireTopBar/empiretopbar_res2");
            var iRes3 = ResourceManager.Texture("EmpireTopBar/empiretopbar_res3");
            var iRes4 = ResourceManager.Texture("EmpireTopBar/empiretopbar_res4");
            var iRes5 = ResourceManager.Texture("EmpireTopBar/empiretopbar_res5");

            Vector2 Cursor = Vector2.Zero;
            res1 = new Rectangle((int)Cursor.X, 2, iRes1.Width, iRes1.Height);
            Cursor.X = Cursor.X + iRes1.Width;

            res2 = new Rectangle((int)Cursor.X, 2, iRes2.Width, iRes2.Height);
            Cursor.X = Cursor.X + iRes2.Width;

            res3 = new Rectangle((int)Cursor.X, 2, iRes3.Width, iRes3.Height);
            Cursor.X = Cursor.X + iRes3.Width;

            res4 = new Rectangle((int)Cursor.X, 2, iRes4.Width, iRes4.Height);
            Cursor.X = Cursor.X + iRes4.Width;

            Cursor.X = Universe.ScreenWidth - iRes5.Width;
            res5 = new Rectangle((int)Cursor.X, 2, iRes5.Width, iRes5.Height);

            Button r1 = new Button();
            r1.Rect = res1;
            r1.NormalTexture  = ResourceManager.Texture("EmpireTopBar/empiretopbar_res1");
            r1.HoverTexture   = ResourceManager.Texture("EmpireTopBar/empiretopbar_res1_hover");
            r1.PressedTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_res1_press");
            r1.launches = "Research";
            Buttons.Add(r1);

            Button r2 = new Button();
            r2.Rect = res2;
            r2.NormalTexture  = ResourceManager.Texture("EmpireTopBar/empiretopbar_res2");
            r2.HoverTexture   = ResourceManager.Texture("EmpireTopBar/empiretopbar_res2");
            r2.PressedTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_res2");
            r2.launches = "Research";
            Buttons.Add(r2);

            Button r3 = new Button();
            r3.Rect = res3;
            r3.NormalTexture  = ResourceManager.Texture("EmpireTopBar/empiretopbar_res3");
            r3.HoverTexture   = ResourceManager.Texture("EmpireTopBar/empiretopbar_res3_hover");
            r3.PressedTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_res3_press");
            r3.launches = "Budget";
            Buttons.Add(r3);

            Button r4 = new Button();
            r4.Rect = res4;
            r4.NormalTexture  = ResourceManager.Texture("EmpireTopBar/empiretopbar_res4");
            r4.HoverTexture   = ResourceManager.Texture("EmpireTopBar/empiretopbar_res4");
            r4.PressedTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_res4");
            r4.launches = "Budget";
            Buttons.Add(r4);

            Button r5 = new Button();
            r5.Rect = res5;
            r5.NormalTexture  = ResourceManager.Texture("EmpireTopBar/empiretopbar_res5");
            r5.HoverTexture   = ResourceManager.Texture("EmpireTopBar/empiretopbar_res5");
            r5.PressedTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_res5");
            Buttons.Add(r5);

            // Ludoal fork (reorg): single-row 132px layout in three tinted groups —
            // Empire/Diplomacy/Espionage (heritage bronze), Planets/Ships/Troops (steel
            // blue, dip-family hue), Fleets/Shipyard/Blueprints/Patrols (muted red,
            // military-family hue). Exotic omitted: Planets<->Exotic cross-buttons exist
            // in-panel. Width is adaptive: full 132px when the span allows (1440p+),
            // shrunk to fit narrower screens (~120px at 1920). Minimap buttons stay.
            var g1  = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px");
            var g1h = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_hover");
            var g1p = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_pressed");
            var g2  = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_menu");
            var g2h = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_menu_hover");
            var g2p = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_menu_pressed");
            Color tintLists    = new Color(186, 210, 246); // -> (62,70,82) over the grey base
            Color tintMilitary = new Color(255, 213, 222); // -> (85,71,74) over the grey base

            (string launch, string text, int group)[] row =
            {
                ("Empire",     Localizer.Token(GameText.Empire),     0),
                ("Diplomacy",  Localizer.Token(GameText.Diplomacy),  0),
                ("Espionage",  Localizer.Token(GameText.Espionage2), 0),
                ("Planets",    "Planets",                            1),
                ("ShipList",   "Ships",                              1),
                ("Troops",     "Troops",                             1),
                ("Fleets",     Localizer.Token(GameText.Fleets),     2),
                ("Shipyard",   Localizer.Token(GameText.Shipyard),   2),
                ("Patrols",    "Patrols",                            2),
                ("Blueprints", "Blueprints",                         2),
            };
            const float innerGap = 4f, groupGap = 16f, edgePad = 10f;
            float span = r5.Rect.X - (r4.Rect.X + r4.Rect.Width);
            float gapsTotal = 7 * innerGap + 2 * groupGap;
            float wf = (span - gapsTotal - 2 * edgePad) / row.Length;
            int rowBtnW = (int)(wf > 132f ? 132f : wf);
            int rowBtnH = g1.Height;
            float rowWidth = row.Length * rowBtnW + gapsTotal;
            Cursor.X = r4.Rect.X + r4.Rect.Width + (span - rowWidth) / 2f;
            int prevGroup = 0;
            foreach ((string launch, string text, int group) in row)
            {
                if (group != prevGroup)
                {
                    Cursor.X += groupGap - innerGap;
                    prevGroup = group;
                }
                Button rb = new Button();
                rb.Rect = new Rectangle((int)Cursor.X, 2, rowBtnW, rowBtnH);
                bool heritage = group == 0;
                rb.NormalTexture  = heritage ? g1 : g2;
                rb.HoverTexture   = heritage ? g1h : g2h;
                rb.PressedTexture = heritage ? g1p : g2p;
                rb.Tint = group == 1 ? tintLists : group == 2 ? tintMilitary : Color.White;
                rb.Text = text;
                rb.launches = launch;
                Buttons.Add(rb);
                Cursor.X += rowBtnW + innerGap;
            }

            Button MainMenu = new Button();
            MainMenu.Rect = new Rectangle(res5.X + 52, 39, ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px").Width, ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px").Height);
            MainMenu.NormalTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_menu");
            MainMenu.HoverTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_menu_hover");
            MainMenu.PressedTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_menu_pressed");
            MainMenu.launches = "Main Menu";
            MainMenu.Text = Localizer.Token(GameText.MainMenu);
            Buttons.Add(MainMenu);
            Cursor.X = Cursor.X + (ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px_hover").Width + 5);

            Button Help = new Button();
            Help.Rect = new Rectangle(res5.X + 72, 64, ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_68px").Width, ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px").Height);
            Help.NormalTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_68px_menu");
            Help.HoverTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_68px_menu_hover");
            Help.PressedTexture = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_68px_menu_pressed");
            Help.Text = "Help";
            Help.launches = "?";
            Buttons.Add(Help);

            // Ludoal fork: game speed - / + right of Help (68px menu family squeezed
            // to 28px; the speed readout draws just below this row). 61px available
            // between Help and the screen edge: 28+2+28 fits flush.
            var sTex  = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_68px_menu");
            var sTexH = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_68px_menu_hover");
            var sTexP = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_68px_menu_pressed");
            int speedX = res5.X + 72 + sTex.Width + 3;
            foreach ((string sign, string launch) in new[] { ("-", "SpeedDown"), ("+", "SpeedUp") })
            {
                Button sb = new Button();
                sb.Rect = new Rectangle(speedX, 64, 28, ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px").Height);
                sb.NormalTexture  = sTex;
                sb.HoverTexture   = sTexH;
                sb.PressedTexture = sTexP;
                sb.Text = sign;
                sb.launches = launch;
                Buttons.Add(sb);
                speedX += 28 + 2;
            }
        }

        public void Draw(SpriteBatch batch)
        {
            if (Universe.IsExiting || Universe.IsDisposed)
                return;

            Vector2 textCursor = new Vector2();
            foreach (Button b in Buttons)
            {
                if (!string.IsNullOrEmpty(b.Text))
                {
                    textCursor.X = b.Rect.X + b.Rect.Width / 2 - Fonts.Arial12Bold.MeasureString(b.Text).X / 2f;
                    textCursor.Y = b.Rect.Y + b.Rect.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2 - (LowRes ? 1 : 0);
                }
                if (b.State == PressState.Normal)
                {
                    batch.Draw(b.NormalTexture, b.Rect, b.Tint);
                    if (string.IsNullOrEmpty(b.Text))
                    {
                        continue;
                    }
                    batch.DrawString(Fonts.Arial12Bold, b.Text, textCursor, new Color(255, 240, 189));
                }
                else if (b.State != PressState.Hover)
                {
                    if (b.State != PressState.Pressed)
                    {
                        continue;
                    }
                    batch.Draw(b.PressedTexture, b.Rect, b.Tint);
                    if (string.IsNullOrEmpty(b.Text))
                    {
                        continue;
                    }
                    textCursor.Y = textCursor.Y + 1f;
                    batch.DrawString(Fonts.Arial12Bold, b.Text, textCursor, new Color(255, 240, 189));
                }
                else
                {
                    batch.Draw(b.HoverTexture, b.Rect, b.Tint);
                    if (string.IsNullOrEmpty(b.Text))
                    {
                        continue;
                    }
                    batch.DrawString(Fonts.Arial12Bold, b.Text, textCursor, new Color(255, 240, 189));
                }
            }

            string money = Player.Money.GetNumberString(compact: true);
            float damoney = Player.EstimateNetIncomeAtTaxRate(Player.data.TaxRate);
            string sign = damoney > 0f ? "+" : "";
            string moneyText = $"{money} ({sign}{damoney.String(1)})";
            textCursor.X = res4.X + res2.Width - 30 - Fonts.Arial12Bold.MeasureString(moneyText).X;
            textCursor.Y = res2.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2;
            batch.DrawString(Fonts.Arial12Bold, moneyText, textCursor, new Color(255, 240, 189));

            var starDatePos = new Vector2(res5.X + 75, textCursor.Y);
            string starDateText = LowRes ? Universe.StarDateString : "StarDate: " + Universe.StarDateString;
            batch.DrawString(Fonts.Arial12Bold, starDateText, starDatePos, new Color(255, 240, 189));

            if (Player.Research.NoTopic)
            {
                textCursor.X = res2.X + res2.Width - 30 - Fonts.Arial12Bold.MeasureString(Localizer.Token(GameText.Choose)+"...").X;
                textCursor.Y = res2.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2;
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.Choose)+"...", textCursor, new Color(255, 240, 189));
            }
            else
            {
                int xOffset = (int)(Player.Research.Current.PercentResearched * res2.Width);
                Rectangle gradientSourceRect = res2;
                gradientSourceRect.X = 159 - xOffset;
                Universe.ScreenManager.SpriteBatch.Draw(ResourceManager.Texture("EmpireTopBar/empiretopbar_res2_gradient"), new Rectangle(res2.X, res2.Y, res2.Width, res2.Height), gradientSourceRect, Color.White);
                Universe.ScreenManager.SpriteBatch.Draw(ResourceManager.Texture("EmpireTopBar/empiretopbar_res2_over"), res2, Color.White);
                string research = Player.Research.Current.Progress.GetNumberString(compact: true);
                string resCost = Player.Research.Current.TechCost.GetNumberString(compact: true);
                float plusRes = Player.Research.NetResearch;
                float x = res2.X + res2.Width - 30;
                Graphics.Font arial12Bold = Fonts.Arial12Bold;
                bool disrupted = Player.Research.DisruptionMultiplier < 1f;
                Color baseColor = new Color(255, 240, 189);
                textCursor.Y = res2.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2;
                if (disrupted)
                {
                    string baseText = $"{research}/{resCost} ";
                    string netText = $"(+{plusRes.String(1)})";
                    int iconSize = (int)((res2.Height - 6) * 0.6f);
                    const int iconPad = 4;
                    Color netColor = new Color(255, 96, 96);
                    float baseW = arial12Bold.MeasureString(baseText).X;
                    float netW  = arial12Bold.MeasureString(netText).X;
                    textCursor.X = x - (baseW + netW + iconPad + iconSize);
                    batch.DrawString(arial12Bold, baseText, textCursor, baseColor);
                    float netX = textCursor.X + baseW;
                    batch.DrawString(arial12Bold, netText, new Vector2(netX, textCursor.Y), netColor);
                    float iconX = netX + netW + iconPad;
                    var iconRect = new Rectangle((int)iconX, res2.Y -3 + (res2.Height - iconSize) / 2, iconSize, iconSize);
                    batch.Draw(ResourceManager.Texture("UI/icon_spy_small"), iconRect, netColor);
                }
                else
                {
                    string text = $"{research}/{resCost} (+{plusRes.String(1)})";
                    textCursor.X = x - arial12Bold.MeasureString(text).X;
                    batch.DrawString(arial12Bold, text, textCursor, baseColor);
                }
            }
        }

        // @return true if input was captured
        // Ludoal fork: one tooltip per top-bar button, shown from BOTH input variants
        // (map view and any panel with the live bar). Anchored slightly below the
        // button, aligned with its beveled bottom-left corner.
        void ShowButtonTooltip(Button b)
        {
            if (b.launches == null)
                return;
            // Help and the speed buttons sit above the game-speed readout: their
            // tooltips drop lower so they don't mask it.
            bool lowRow = b.launches == "?" || b.launches == "SpeedUp" || b.launches == "SpeedDown";
            Vector2 tipPos = new Vector2(b.Rect.X, b.Rect.Bottom + (lowRow ? 26 : 4));
            switch (b.launches)
            {
                case "Research":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.ResearchScreen) + "\n\n" + Localizer.Token(GameText.CurrentResearch) + ": " + Player.Research.TopicLocText.Text, "R", tipPos);
                    break;
                case "Budget":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.EconomicOverview2), "T", tipPos);
                    break;
                case "Main Menu":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheMainMenu), "O", tipPos);
                    break;
                case "Shipyard":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheShipyard), "Y", tipPos);
                    break;
                case "Empire":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheEmpireOverviewScreen), "U", tipPos);
                    break;
                case "Diplomacy":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheDiplomacyOverviewScreen), "I", tipPos);
                    break;
                case "Espionage":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheEspionageManagementScreen), "E", tipPos);
                    break;
                case "ShipList":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheShipRoster), "K", tipPos);
                    break;
                case "Fleets":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheFleetManager), "J", tipPos);
                    break;
                case "Planets":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensPlanetReconnaissancePanel), "L", tipPos);
                    break;
                case "Troops":
                    ToolTip.CreateTooltip("Opens the Troops Array", "C", tipPos);
                    break;
                case "Patrols":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.EmpirePatrolsScreenTip), "P", tipPos);
                    break;
                case "Blueprints":
                    ToolTip.CreateTooltip(Localizer.Token(GameText.BlueprintsScreenTip), "F", tipPos);
                    break;
                case "?":
                    // the real help binding is F1 (CodexHelp)
                    ToolTip.CreateTooltip(Localizer.Token(GameText.OpensTheHelpMenu), "F1", tipPos);
                    break;
                case "SpeedDown":
                    ToolTip.CreateTooltip("Slower game speed", "-", tipPos);
                    break;
                case "SpeedUp":
                    ToolTip.CreateTooltip("Faster game speed", "+", tipPos);
                    break;
            }
        }

        public bool HandleInput(InputState input)
        {
            if (!GlobalStats.TakingInput)
            {
                if (input.KeyPressed(Keys.R))
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(new ResearchScreenNew(Universe, Universe, this));
                    return true;
                }
                if (input.KeyPressed(Keys.T))
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(new BudgetScreen(Universe));
                    return true;
                }
                if (input.KeyPressed(Keys.Y))
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(new ShipDesignScreen(Universe, this));
                    return true;
                }
                if (input.KeyPressed(Keys.U))
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(new EmpireManagementScreen(Universe, this));
                    return true;
                }
                if (input.KeyPressed(Keys.I))
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(new MainDiplomacyScreen(Universe));
                    return true;
                }
                if (input.KeyPressed(Keys.O))
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(new GamePlayMenuScreen(Universe));
                    return true;
                }
                if (input.KeyPressed(Keys.E))
                {
                    GameAudio.EchoAffirmative();
                    if (Universe.Player.LegacyEspionageEnabled)
                        Universe.ScreenManager.AddScreen(new EspionageScreen(Universe));
                    else
                        Universe.ScreenManager.AddScreen(new InfiltrationScreen(Universe));
                    return true;
                }
                if (input.Codex)
                {
                    GameAudio.TacticalPause();
                    Universe.ScreenManager.AddScreen(new Codex.CodexScreen(Universe));
                    return true;
                }
                if (input.KeyPressed(Keys.Home))
                {
                    if (Player.GetCurrentCapital(out Planet capital))
                    {
                        GameAudio.SubBassWhoosh();
                        Universe.SetSelectedPlanet(capital);
                        Universe.CamDestination = new Vector3d(capital.Position.X, capital.Position.Y + 400f, 9000);
                    }
                    else
                    {
                        GameAudio.NegativeClick();
                    }
                    return true;
                }
            }

            foreach (Button b in Buttons)
            {
                if (!b.Rect.HitTest(input.CursorPosition))
                {
                    b.State = PressState.Normal;
                }
                else
                {
                    ShowButtonTooltip(b); // Ludoal fork: shared tooltip helper, anchored bottom-left
                    if (b.State != PressState.Hover && b.State != PressState.Pressed)
                    {
                        GameAudio.MouseOver();
                    }
                    b.State = PressState.Hover;
                    if (input.LeftMouseHeldDown)
                    {
                        b.State = PressState.Pressed;
                    }
                    if (input.InGameSelect)
                    {
                        if (b.launches == null)
                        {
                            continue;
                        }
                        if (b.launches == "SpeedUp" || b.launches == "SpeedDown")
                        {
                            // Ludoal fork: speed buttons never open/close anything
                            GameAudio.AcceptClick();
                            Universe.AdjustGameSpeed(b.launches == "SpeedUp");
                            return true;
                        }
                        if (b.launches == "Research")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new ResearchScreenNew(Universe, Universe, this));
                        }
                        else if (b.launches == "Budget")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new BudgetScreen(Universe));
                        }

                        if (b.launches == "Main Menu")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new GamePlayMenuScreen(Universe));
                        }
                        else if (b.launches == "Shipyard")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new ShipDesignScreen(Universe, this));
                        }
                        else if (b.launches == "Fleets")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new FleetDesignScreen(Universe, this));
                        }
                        else if (b.launches == "ShipList")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new ShipListScreen(Universe, this));
                        }
                        // Ludoal fork: provisional second-row buttons
                        else if (b.launches == "Planets")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new PlanetListScreen(Universe, this));
                        }
                        else if (b.launches == "Exotic")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new ExoticSystemsListScreen(Universe, this));
                        }
                        else if (b.launches == "Patrols")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new EmpirePatrolsScreen(Universe, Universe.Player));
                        }
                        else if (b.launches == "Blueprints")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new BlueprintsScreen(Universe, Universe.Player));
                        }
                        else if (b.launches == "Troops")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new TroopListScreen(Universe, this));
                        }
                        else if (b.launches == "Empire")
                        {
                            Universe.ScreenManager.AddScreen(new EmpireManagementScreen(Universe, this));
                            GameAudio.EchoAffirmative();
                        }
                        else if (b.launches == "Diplomacy")
                        {
                            Universe.ScreenManager.AddScreen(new MainDiplomacyScreen(Universe));
                            GameAudio.EchoAffirmative();
                        }
                        else if (b.launches == "Espionage")
                        {
                            if (Universe.Player.LegacyEspionageEnabled)
                                Universe.ScreenManager.AddScreen(new EspionageScreen(Universe));
                            else
                                Universe.ScreenManager.AddScreen(new InfiltrationScreen(Universe));

                            GameAudio.EchoAffirmative();
                        }
                        else if (b.launches == "?")
                        {
                            GameAudio.TacticalPause();
                            Universe.ScreenManager.AddScreen(new Codex.CodexScreen(Universe));
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        // TODO: This is utterly retarded, needs a complete rewrite
        public bool HandleInput(InputState input, GameScreen caller)
        {
            foreach (Button b in Buttons)
            {
                if (!b.Rect.HitTest(input.CursorPosition))
                {
                    b.State = PressState.Normal;
                }
                else
                {
                    ShowButtonTooltip(b); // Ludoal fork: tooltips also on panel-hosted bars
                    if (b.State != PressState.Hover && b.State != PressState.Pressed)
                    {
                        GameAudio.MouseOver();
                    }
                    b.State = input.LeftMouseHeldDown ? PressState.Pressed : PressState.Hover;

                    if (input.LeftMouseClick)
                    {
                        // Ludoal fork: unified caller path. A decorative button (no launch)
                        // no longer closes the calling screen.
                        if (b.launches == null)
                        {
                            continue;
                        }

                        // Ludoal fork: speed buttons act in place — no screen is closed
                        // or opened, whatever panel hosts the bar.
                        if (b.launches == "SpeedUp" || b.launches == "SpeedDown")
                        {
                            GameAudio.AcceptClick();
                            Universe.AdjustGameSpeed(b.launches == "SpeedUp");
                            return true;
                        }

                        // Shipyard keeps its dedicated exit (unsaved-design prompt);
                        // its LaunchScreen() then opens the requested target.
                        if (caller is ShipDesignScreen shipDesigner)
                        {
                            if (b.launches == "Shipyard")
                            {
                                continue;
                            }
                            shipDesigner.ExitToMenu(b.launches);
                            return true;
                        }

                        // Everyone else (FleetDesign included): close the caller, then
                        // the dispatch below opens the target. Clicking a screen's own
                        // button just closes it (toggle) via the per-branch self-guards.
                        caller.ExitScreen();

                        if (b.launches == "Research")
                        {
                            GameAudio.EchoAffirmative();
                            if (!(caller is ResearchScreenNew))
                            {
                                Universe.ScreenManager.AddScreen(new ResearchScreenNew(Universe, Universe, this));
                            }
                        }
                        else if (b.launches == "Budget")
                        {
                            GameAudio.EchoAffirmative();
                            if (!(caller is BudgetScreen))
                            {
                                Universe.ScreenManager.AddScreen(new BudgetScreen(Universe));
                            }
                        }
                        else if (b.launches == "Main Menu")
                        {
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new GamePlayMenuScreen(Universe));
                        }
                        else if (b.launches == "Shipyard")
                        {
                            if (caller is ShipDesignScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new ShipDesignScreen(Universe, this));
                        }
                        else if (b.launches == "Fleets")
                        {
                            if (caller is FleetDesignScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new FleetDesignScreen(Universe, this));
                        }
                        // Ludoal fork: ShipList and Espionage were missing from the caller
                        // dispatch — harmless while only Shipyard/Fleets kept the bar live,
                        // a dead button once every full-screen does (top-bar standard).
                        else if (b.launches == "ShipList")
                        {
                            if (caller is ShipListScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new ShipListScreen(Universe, this));
                        }
                        // Ludoal fork: provisional second-row buttons (self-click = toggle close)
                        else if (b.launches == "Planets")
                        {
                            if (caller is PlanetListScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new PlanetListScreen(Universe, this));
                        }
                        else if (b.launches == "Exotic")
                        {
                            if (caller is ExoticSystemsListScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new ExoticSystemsListScreen(Universe, this));
                        }
                        else if (b.launches == "Patrols")
                        {
                            if (caller is EmpirePatrolsScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new EmpirePatrolsScreen(Universe, Universe.Player));
                        }
                        else if (b.launches == "Blueprints")
                        {
                            if (caller is BlueprintsScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new BlueprintsScreen(Universe, Universe.Player));
                        }
                        else if (b.launches == "Troops")
                        {
                            if (caller is TroopListScreen)
                            {
                                continue;
                            }
                            GameAudio.EchoAffirmative();
                            Universe.ScreenManager.AddScreen(new TroopListScreen(Universe, this));
                        }
                        else if (b.launches == "Espionage")
                        {
                            if (caller is EspionageScreen or InfiltrationScreen)
                            {
                                continue;
                            }
                            if (Universe.Player.LegacyEspionageEnabled)
                                Universe.ScreenManager.AddScreen(new EspionageScreen(Universe));
                            else
                                Universe.ScreenManager.AddScreen(new InfiltrationScreen(Universe));

                            GameAudio.EchoAffirmative();
                        }
                        else if (b.launches == "Empire")
                        {
                            if (caller is EmpireManagementScreen)
                            {
                                continue;
                            }
                            Universe.ScreenManager.AddScreen(new EmpireManagementScreen(Universe, this));
                            GameAudio.EchoAffirmative();
                        }
                        else if (b.launches == "Diplomacy")
                        {
                            if (caller is MainDiplomacyScreen)
                            {
                                continue;
                            }
                            Universe.ScreenManager.AddScreen(new MainDiplomacyScreen(Universe));
                            GameAudio.EchoAffirmative();
                        }
                        else if (b.launches == "?")
                        {
                            GameAudio.TacticalPause();
                            Universe.ScreenManager.AddScreen(new Codex.CodexScreen(Universe));
                        }
                        return true; // input captured
                    }
                }
            }
            return false;
        }

        public void Update(float elapsedTime)
        {
        }

        public class Button
        {
            public Rectangle Rect;
            public PressState State;
            public SubTexture NormalTexture;
            public SubTexture HoverTexture;
            public SubTexture PressedTexture;
            public string Text = "";
            public Color Tint = Color.White; // Ludoal fork: group tinting
            public string launches;
        }

        public enum PressState
        {
            Normal,
            Hover,
            Pressed
        }
    }
}
