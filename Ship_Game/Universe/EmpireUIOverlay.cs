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
        Array<Button> Buttons = new Array<Button>();
        bool LowRes;
        UniverseScreen Universe;

        // Ludoal fork: the bar's own geometry. The readouts change every turn, so each is drawn
        // on a RESERVED width rather than measured - otherwise the icon after it would shift as
        // the treasury gained a digit.
        Rectangle MoneyIcon, ResearchIcon;
        int MoneyTextX, ResearchTextX, StarDateRight;
        Rectangle PauseRect, SpeedFaster, SpeedSlower;
        // Ludoal fork: the bar's own height, and the height of a Dan button inside it. The tab
        // rows below read BarH so they sit a fixed 10px under the bar rather than at a constant
        // that would drift the day this changes.
        public const int BarTop = 10;   // clear of the window's own edge
        public const int BarH = 34;
        const int DanH = 25;

        // Measured in Arial12Bold on the worst realistic case, not eyeballed: the treasury peaks
        // around 100px ("999.9k (+999.9)"), the research line around 240 with a long topic name.
        const int MoneyRoom = 110;
        const int ResearchRoom = 250;
        const int StarDateRoom = 120;   // "StarDate: 1202.8"
        const int SpeedRoom = 46;       // "0.25x" - reserved, so the cluster never shifts
        int SpeedTextRight;             // the factor is right-aligned here, left of "<<"

        // Ludoal fork: the bar is laid out in three zones and drawn flat - the military plating,
        // the five resource cartouches and the ten-button row are gone. Left: what the empire HAS
        // (treasury, research). Centre: the four groups of the unified bar. Right: the session
        // controls (menu, help, speed, stardate).
        //
        // Every rect is built here, once, from the screen width; Draw only paints and HandleInput
        // only tests. Nothing downstream recomputes a position.
        public EmpireUIOverlay(Empire playerEmpire, GraphicsDevice device, UniverseScreen universe)
        {
            Player = playerEmpire;
            Universe = universe;
            LowRes = universe.ScreenWidth <= 1366;

            // Ludoal fork: the bar stands 34 tall so it reads as a band of its own, and its
            // buttons are Dan buttons (25px textures) rather than painted plates.
            // the same 10 the bar keeps at the top, on both sides
            const int pad = BarTop, gap = 6, btnH = DanH;
            const int y = BarTop + (BarH - DanH) / 2;
            int w = universe.ScreenWidth;

            // ── left: treasury, then research ────────────────────────────────────────────────
            // The readouts are drawn by Draw (their text changes every turn); what lives here is
            // the CLICKABLE icon in front of each, wired to its tab.
            int x = pad;
            MoneyIcon = new Rectangle(x, y + (DanH - 20) / 2, 20, 20);
            Buttons.Add(new Button
            {
                Rect = MoneyIcon, Flat = true, Icon = ResourceManager.Texture("NewUI/icon_money"),
                launches = "Budget", Tip = "Treasury and taxes",
            });
            x += 20 + gap;
            MoneyTextX = x;

            // the treasury readout runs before the research icon; Draw measures it, so the icon
            // is placed on a reserved width rather than on the text of this particular turn
            x += MoneyRoom;
            ResearchIcon = new Rectangle(x, y + (DanH - 20) / 2, 20, 20);
            Buttons.Add(new Button
            {
                Rect = ResearchIcon, Flat = true, Icon = ResourceManager.Texture("NewUI/icon_science"),
                launches = "Research", Tip = "Research and the current topic",
            });
            ResearchTextX = x + 20 + gap;

            // ── right, laid out from the screen edge inwards ─────────────────────────────────
            // Stardate first (it is the rightmost), then the speed cluster, then help and menu.
            int rx = w - pad;
            StarDateRight = rx;
            rx -= StarDateRoom + gap * 2;

            int speedW = 26;
            SpeedFaster = new Rectangle(rx - speedW, y, speedW, btnH);
            Buttons.Add(new Button { Rect = SpeedFaster, Flat = true, Text = ">>", launches = "SpeedUp", Tip = "Speed up" });
            rx -= speedW + 2;

            int pauseW = 54;
            PauseRect = new Rectangle(rx - pauseW, y, pauseW, btnH);
            Buttons.Add(new Button { Rect = PauseRect, Flat = true, Text = "PAUSE", launches = "Pause", Tip = "Pause / resume" });
            rx -= pauseW + 2;

            SpeedSlower = new Rectangle(rx - speedW, y, speedW, btnH);
            Buttons.Add(new Button { Rect = SpeedSlower, Flat = true, Text = "<<", launches = "SpeedDown", Tip = "Slow down" });
            rx -= speedW + gap;

            // the factor sits LEFT of the cluster and is right-aligned on it: growing from "2x"
            // to "0.25x" then stays put, where on the right it would push the stardate about
            SpeedTextRight = rx;
            rx -= SpeedRoom + gap;

            // ── centre: the four groups, then MENU and ? riding with them ───────────────────
            // The two session buttons sit with the groups rather than off at the edge, separated
            // by a DOUBLE gap: near enough to read as one cluster, far enough to read as a
            // different kind of thing.
            (string launch, string text, ReworkScreens.Group group)[] groups =
            {
                ("Planets",   "GALAXY",    ReworkScreens.Group.Galaxy),
                ("Empire",    "EMPIRE",    ReworkScreens.Group.Empire),
                ("Diplomacy", "DIPLOMACY", ReworkScreens.Group.Diplomacy),
                ("Fleets",    "DESIGN",    ReworkScreens.Group.Design),
            };
            int groupW = LowRes ? 96 : 116;
            int menuW = 56, helpW = 30;
            int clusterW = groups.Length * groupW + (groups.Length - 1) * gap
                         + gap * 2 + menuW + gap + helpW;

            // centred in what the two sides leave, never overlapping either
            int freeLeft = ResearchTextX + ResearchRoom + gap;
            int freeRight = rx - gap;
            int gx = freeLeft + ((freeRight - freeLeft) - clusterW) / 2;
            if (gx < freeLeft) gx = freeLeft;

            foreach ((string launch, string text, ReworkScreens.Group group) in groups)
            {
                Buttons.Add(new Button
                {
                    Rect = new Rectangle(gx, y, groupW, btnH), Flat = true,
                    Text = text, launches = launch, Group = group,
                });
                gx += groupW + gap;
            }

            gx += gap;  // the double margin that separates the two kinds
            Buttons.Add(new Button
            {
                Rect = new Rectangle(gx, y, menuW, btnH), Flat = true, Text = "MENU",
                launches = "Main Menu", Tip = "Open the main menu",
            });
            gx += menuW + gap;
            Buttons.Add(new Button
            {
                Rect = new Rectangle(gx, y, helpW, btnH), Flat = true, Text = "?",
                launches = "?", Tip = "Open the codex",
            });
        }

        // Ludoal fork: the bar draws itself flat, in the reworked screens' grammar - a dark plate
        // with a brass rule, no plating textures. Colour is decided HERE from live state (which
        // group is open, whether the game is paused), never stored on the button.
        static readonly Color PlateBlue  = new Color(38, 56, 84);
        static readonly Color PlateBrown = new Color(84, 64, 38);
        static readonly Color PlateRed   = new Color(96, 34, 34);
        static readonly Color TextCream  = new Color(255, 240, 189);

        public void Draw(SpriteBatch batch)
        {
            if (Universe.IsExiting || Universe.IsDisposed)
                return;

            // Which group is open is read from the screen stack rather than passed in: fifteen
            // screens draw this bar, and a parameter is a parameter one of them will forget.
            ReworkScreens.Group open = ReworkScreens.GroupOf(Universe.ScreenManager.Current);
            Graphics.Font font = Fonts.Arial12Bold;

            foreach (Button b in Buttons)
            {
                Color fill = PlateBlue;
                if (b.Group != ReworkScreens.Group.None && b.Group == open)
                    fill = PlateBrown;                       // the group you are inside
                else if (b.launches == "Pause" && Universe.UState.Paused)
                    fill = PlateRed;                         // paused, whatever paused it

                if (b.Icon != null)
                {
                    // an icon button carries no plate: the icon IS the button
                    Color tint = b.State == PressState.Normal ? Color.White : Color.Orange;
                    batch.Draw(b.Icon, b.Rect, tint);
                    continue;
                }

                // the Dan button textures the reworked screens use, stretched to the rect: blue
                // normally, red for a live pause, and the plain plate for the open group
                string tex = fill == PlateBrown ? "NewUI/dan_button_clear"
                           : fill == PlateRed   ? "NewUI/dan_button_red_clear"
                                                : "NewUI/dan_button_blue_clear";
                Color tone = b.State == PressState.Hover   ? Color.White
                           : b.State == PressState.Pressed ? new Color(180, 180, 180)
                                                           : new Color(225, 225, 225);
                batch.Draw(ResourceManager.Texture(tex), b.Rect, tone);

                if (!string.IsNullOrEmpty(b.Text))
                {
                    var at = new Vector2(b.Rect.X + b.Rect.Width / 2 - font.TextWidth(b.Text) / 2f,
                                         b.Rect.Y + b.Rect.Height / 2 - font.LineSpacing / 2f);
                    batch.DrawString(font, b.Text, at, TextCream);
                }
            }

            float textY = MoneyIcon.Y + MoneyIcon.Height / 2f - font.LineSpacing / 2f;

            // treasury, on its reserved width
            float income = Player.EstimateNetIncomeAtTaxRate(Player.data.TaxRate);
            string money = $"{Player.Money.GetNumberString(compact: true)} ({(income > 0f ? "+" : "")}{income.String(1)})";
            batch.DrawString(font, money, new Vector2(MoneyTextX, textY), TextCream);

            // research: progress, net gain, and what is being researched
            if (Player.Research.NoTopic)
            {
                batch.DrawString(font, Localizer.Token(GameText.Choose) + "...",
                                 new Vector2(ResearchTextX, textY), TextCream);
            }
            else
            {
                string progress = Player.Research.Current.Progress.GetNumberString(compact: true);
                string cost = Player.Research.Current.TechCost.GetNumberString(compact: true);
                string res = $"{progress}/{cost} (+{Player.Research.NetResearch.String(1)})";
                batch.DrawString(font, res, new Vector2(ResearchTextX, textY), TextCream);

                // the topic itself, after the numbers - dimmer, it is context rather than a value
                float topicX = ResearchTextX + font.TextWidth(res) + 8;
                string topic = Player.Research.TopicLocText.Text;
                bool disrupted = Player.Research.DisruptionMultiplier < 1f;
                batch.DrawString(font, topic, new Vector2(topicX, textY),
                                 disrupted ? new Color(255, 96, 96) : TextCream.Alpha(0.7f));
            }

            // the speed factor, right-aligned on its reserved width. Same reading as the floating
            // one it replaces: hidden at 1x, red at the extremes.
            if (Universe.UState.GameSpeed.NotEqual(1))
            {
                string speed = Universe.UState.GameSpeed.ToString("0.0##") + "x";
                bool extreme = Universe.UState.GameSpeed is > 3 or < 0.25f;
                batch.DrawString(font, speed, new Vector2(SpeedTextRight - font.TextWidth(speed), textY),
                                 extreme ? Color.Red : Color.LightGreen);
            }

            // stardate, right-aligned on the screen edge
            string stardate = LowRes ? Universe.StarDateString : "StarDate: " + Universe.StarDateString;
            batch.DrawString(font, stardate,
                             new Vector2(StarDateRight - font.TextWidth(stardate), textY), TextCream);
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

            // Ludoal fork: a button that carries its own tip wins. The group buttons open a GROUP
            // rather than the single screen their launch key names, so the cases below - written
            // for the old per-screen row - would describe the wrong thing.
            if (b.Group != ReworkScreens.Group.None)
            {
                ToolTip.CreateTooltip($"Open the {b.Text} group", "", tipPos);
                return;
            }
            if (b.Tip != null)
            {
                ToolTip.CreateTooltip(b.Tip, "", tipPos);
                return;
            }

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
                // Ludoal fork: not while Ctrl is held — Ctrl+Alt+R is the resolution test tool,
                // and a key consumed earlier in the frame is still visible to the screens.
                if (input.KeyPressed(Keys.R) && !input.IsCtrlKeyDown)
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(new ResearchScreenNew(Universe, Universe, this));
                    return true;
                }
                if (input.KeyPressed(Keys.T))
                {
                    GameAudio.EchoAffirmative();
                    Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Economy(Universe));
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
                    Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Diplomacy(Universe));
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
                        Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Espionage(Universe));
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
                        if (b.launches == "Pause")
                        {
                            GameAudio.AcceptClick();
                            Universe.UState.Paused = !Universe.UState.Paused;
                            return true;
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
                            Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Economy(Universe));
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
                            Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Diplomacy(Universe));
                            GameAudio.EchoAffirmative();
                        }
                        else if (b.launches == "Espionage")
                        {
                            if (Universe.Player.LegacyEspionageEnabled)
                                Universe.ScreenManager.AddScreen(new EspionageScreen(Universe));
                            else
                                Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Espionage(Universe));

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
                        if (b.launches == "Pause")
                        {
                            GameAudio.AcceptClick();
                            Universe.UState.Paused = !Universe.UState.Paused;
                            return true;
                        }

                        if (b.launches == "SpeedUp" || b.launches == "SpeedDown")
                        {
                            GameAudio.AcceptClick();
                            Universe.AdjustGameSpeed(b.launches == "SpeedUp");
                            return true;
                        }

                        // Shipyard keeps its dedicated exit (unsaved-design prompt);
                        // its LaunchScreen() then opens the requested target — and its
                        // own button simply closes it (toggle), like every other panel.
                        if (caller is ShipDesignScreen shipDesigner)
                        {
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
                            // Ludoal fork: both regimes, or the reworked screen never
                            // recognises itself and the bar stacks a second copy
                            if (!GameScreens.ReworkScreens.IsEconomy(caller))
                            {
                                Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Economy(Universe));
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
                            // Ludoal fork: both regimes — EspionageScreen is the legacy one,
                            // and IsEspionage covers the stock and reworked infiltration screens
                            if (caller is EspionageScreen || GameScreens.ReworkScreens.IsEspionage(caller))
                            {
                                continue;
                            }
                            if (Universe.Player.LegacyEspionageEnabled)
                                Universe.ScreenManager.AddScreen(new EspionageScreen(Universe));
                            else
                                Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Espionage(Universe));

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
                            if (GameScreens.ReworkScreens.IsDiplomacy(caller))
                            {
                                continue;
                            }
                            Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Diplomacy(Universe));
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

            // Ludoal fork: a FLAT button carries no texture - it is drawn as a filled plate with
            // a rule, in the grammar the reworked screens use. Colour is decided at draw time
            // from what the button represents (which group is open, whether the game is paused),
            // never stored, so it cannot go stale.
            public bool Flat;
            public SubTexture Icon;      // drawn left of the text, vertically centred
            public string Tip;           // tooltip line for a flat button
            public ReworkScreens.Group Group; // set on the four group buttons, None elsewhere
        }

        public enum PressState
        {
            Normal,
            Hover,
            Pressed
        }
    }
}
