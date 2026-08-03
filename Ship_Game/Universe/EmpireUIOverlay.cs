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
        // The figures in front of the research topic, on their own reserved width so the topic
        // name keeps ONE position instead of sliding as the numbers grow. Bound, not eyeballed:
        // GetNumberString(compact) tops out at six glyphs ("999.9M"), so the worst realistic
        // string is "999.9k/999.9M (+999.9)" - about 150px in Arial12Bold.
        const int ResearchNumbersRoom = 155;
        // ⚠ MEASURED, not eyeballed: StarDateString is "####.0", so "StarDate: 9999.9" is the
        // widest this can ever be - 94px in Arial12Bold, and it does not vary with the date.
        // The old 120 left 27px of unused reserve, and since the text is right-aligned on
        // StarDateRight that slack all fell on the LEFT, opening a gap before the speed cluster
        // roughly three times the one after the menu icon (maintainer feedback).
        const int StarDateRoom = 94;
        const int SpeedRoom = 46;       // "0.25x" - reserved, so the cluster never shifts
        const int PauseRoom = 62;       // "PAUSED", the longer of the two words it shows
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
            // The two session buttons take the edge; the stardate sits just left of them, close
            // to the speed cluster it belongs with rather than pushed out to the corner.
            int rx = w - pad;

            int helpW = 26;
            Buttons.Add(new Button
            {
                Rect = new Rectangle(rx - helpW, y, helpW, btnH), Flat = true, Bare = true,
                Text = "?", launches = "?", Tip = "Open the codex",
            });
            rx -= helpW + gap;

            int menuW = 26;
            Buttons.Add(new Button
            {
                Rect = new Rectangle(rx - menuW, y, menuW, btnH), Flat = true,
                Icon = ResourceManager.Texture("NewUI/icon_exotic_Bonuses_big"),
                launches = "Main Menu", Tip = "Open the main menu",
            });
            rx -= menuW + gap * 2;

            StarDateRight = rx;
            // the SAME gap on both sides of the stardate: gap*2 was taken off before it (above),
            // so the speed cluster gets gap*2 too rather than a bare gap (maintainer feedback).
            rx -= StarDateRoom + gap * 2;

            // The session controls are bare text: they act in place and open nothing, so a plate
            // would give them the weight of the group buttons beside them. PauseRoom is reserved
            // for the LONGER of the two words - "PAUSED" must not push "<<" when the game stops.
            int speedW = 26;
            SpeedFaster = new Rectangle(rx - speedW, y, speedW, btnH);
            Buttons.Add(new Button { Rect = SpeedFaster, Flat = true, Bare = true, Text = ">>", launches = "SpeedUp", Tip = "Speed up" });
            rx -= speedW + 2;

            PauseRect = new Rectangle(rx - PauseRoom, y, PauseRoom, btnH);
            Buttons.Add(new Button { Rect = PauseRect, Flat = true, Bare = true, Text = "PAUSE", launches = "Pause", Tip = "Pause / resume" });
            rx -= PauseRoom + 2;

            SpeedSlower = new Rectangle(rx - speedW, y, speedW, btnH);
            Buttons.Add(new Button { Rect = SpeedSlower, Flat = true, Bare = true, Text = "<<", launches = "SpeedDown", Tip = "Slow down" });
            rx -= speedW + gap;

            // the factor sits LEFT of the cluster and is right-aligned on it: growing from "2x"
            // to "0.25x" then stays put, where on the right it would push the stardate about
            SpeedTextRight = rx;
            rx -= SpeedRoom + gap;

            // ── centre: the four groups ─────────────────────────────────────────────────────
            (string launch, string text, ReworkScreens.Group group)[] groups =
            {
                ("Planets",   "GALAXY",    ReworkScreens.Group.Galaxy),
                ("Empire",    "EMPIRE",    ReworkScreens.Group.Empire),
                ("Diplomacy", "DIPLOMACY", ReworkScreens.Group.Diplomacy),
                ("Fleets",    "DESIGN",    ReworkScreens.Group.Design),
            };
            int groupW = LowRes ? 96 : 116;
            int clusterW = groups.Length * groupW + (groups.Length - 1) * gap;

            // Centred on the SCREEN, not in the gap the two sides leave: the groups are the bar's
            // spine and read as off-centre when the left readouts are wider than the right cluster.
            // The free span still bounds them, so a narrow window pushes rather than overlaps.
            int freeLeft = ResearchTextX + ResearchRoom + gap;
            int freeRight = rx - gap;
            int gx = (w - clusterW) / 2;
            if (gx < freeLeft) gx = freeLeft;
            if (gx + clusterW > freeRight) gx = freeRight - clusterW;

            foreach ((string launch, string text, ReworkScreens.Group group) in groups)
            {
                Buttons.Add(new Button
                {
                    Rect = new Rectangle(gx, y, groupW, btnH), Flat = true,
                    Text = text, launches = launch, Group = group,
                });
                gx += groupW + gap;
            }

        }

        // Ludoal fork: the bar draws itself flat, in the reworked screens' grammar - a dark plate
        // with a brass rule, no plating textures. Colour is decided HERE from live state (which
        // group is open, whether the game is paused), never stored on the button.
        // ⚠ These are TINTS now, not fills: the plate multiplies them, so they set how bright its
        // rule comes out. The old near-black pair left the group tabs almost invisible over the
        // map. Brown is the group you are inside, blue the rest.
        static readonly Color PlateBlue  = new Color(120, 150, 200);
        static readonly Color PlateBrown = new Color(193, 113, 26);
        static readonly Color TextCream  = new Color(255, 240, 189);
        // an ink, bright enough to read over the map - a plate-fill red would sink into it
        static readonly Color PausedRed  = new Color(255, 92, 92);

        public void Draw(SpriteBatch batch)
        {
            if (Universe.IsExiting || Universe.IsDisposed)
                return;

            // Ludoal fork: the veil under the bar. It dims the stars along with the foreground -
            // keeping the starfield while hiding world icons would mean scissor-clipping the
            // world-overlay draw pass, and the maintainer judged the flat band acceptable until
            // then. Top band only: it covers 0..veilBottom, under everything the screens draw,
            // so unlike the late minimap ground it cannot land on their content.
            int veilBottom = BarTop + BarH + 10;
            batch.FillRectangle(new Rectangle(0, 0, Universe.ScreenWidth, veilBottom),
                                new Color(6, 8, 12).Alpha(0.82f));

            // Which group is open is read from the screen stack rather than passed in: fifteen
            // screens draw this bar, and a parameter is a parameter one of them will forget.
            ReworkScreens.Group open = ReworkScreens.GroupOf(Universe.ScreenManager.Current);
            Graphics.Font font = Fonts.Arial12Bold;

            foreach (Button b in Buttons)
            {
                Color fill = PlateBlue;
                if (b.Group != ReworkScreens.Group.None && b.Group == open)
                    fill = PlateBrown;                       // the group you are inside

                if (b.Icon != null)
                {
                    // An icon button carries no plate: the icon IS the button. Drawn at its own
                    // size, centred in the rect - stretched to the hit area it would smear.
                    Color tint = b.State == PressState.Normal ? Color.White : Color.Orange;
                    var at = new Rectangle(b.Rect.X + (b.Rect.Width - b.Icon.Width) / 2,
                                           b.Rect.Y + (b.Rect.Height - b.Icon.Height) / 2,
                                           b.Icon.Width, b.Icon.Height);
                    batch.Draw(b.Icon, at, tint);
                    continue;
                }

                if (b.State == PressState.Hover)   fill = fill.LerpTo(Color.White, 0.18f);
                if (b.State == PressState.Pressed) fill = fill.LerpTo(Color.Black, 0.25f);

                // A bare button is its text and nothing else - the plate would say "control"
                // where the glyph already says it.
                if (!b.Bare)
                {
                    // Ludoal fork: the same nine-sliced plate every button in the game draws, so
                    // the four group tabs belong to the set rather than being flat rectangles
                    // beside it. Translucent still: the bar sits over the map, where a solid
                    // plate reads as a hole punched in it, and the group you are inside carries
                    // more weight than the rest.
                    // Ludoal fork: full strength. The plate already carries its own translucency
                    // in its body, so fading the tint on top of that left the group tabs barely
                    // visible over the map (maintainer feedback).
                    UIButton.DrawPlate(batch, b.Rect, fill);
                }

                if (!string.IsNullOrEmpty(b.Text))
                {
                    // The pause control says what the game IS, not what the click would do, and
                    // says it in red - it now carries the paused state on its own, the plate that
                    // used to shout it having gone with the rest of the trim.
                    bool paused = b.launches == "Pause" && Universe.UState.Paused;
                    // A pause held by an open screen is not the player's to lift, so the control
                    // reads as inert: dimmed, and it does not light up under the cursor either.
                    bool locked = paused && PauseIsAutomatic;
                    string label = paused ? "PAUSED" : b.Text;
                    // A BARE control is its glyph and nothing else, so the glyph itself has to
                    // answer the cursor - the same orange the icons already use, rather than a
                    // brightened cream that read as nothing at all. A plated button keeps its
                    // own answer in the plate and leaves its label alone.
                    // ⚠ the hover BRIGHTENS the state colour, never replaces it: red means the
                    // game is stopped, and that must not vanish under the cursor.
                    bool hot = b.Bare && b.State != PressState.Normal;
                    // dimmed enough to say "not yours to lift", not so much that the stopped
                    // state stops reading - it is the one thing on the bar that must be seen
                    Color ink = locked ? PausedRed.Alpha(0.8f)
                              : paused ? (hot ? PausedRed.LerpTo(Color.White, 0.35f) : PausedRed)
                              : hot    ? Color.Orange
                              : b.State == PressState.Normal ? TextCream
                              : TextCream.LerpTo(Color.White, 0.5f);

                    var at = new Vector2(b.Rect.X + b.Rect.Width / 2 - font.TextWidth(label) / 2f,
                                         b.Rect.Y + b.Rect.Height / 2 - font.LineSpacing / 2f);
                    batch.DrawString(font, label, at, ink);
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

                // the topic itself, after the numbers - dimmer, it is context rather than a value.
                // On a RESERVED width, never measured off this turn's figures: the numbers change
                // every turn, and a topic placed behind them slid left and right as they did.
                float topicX = ResearchTextX + ResearchNumbersRoom;
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
                            TogglePause();
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

        // Ludoal fork: which screen each hotkey asks for, by the same name the buttons use.
        // The universe reads these keys itself; a screen sitting on top of it never saw them,
        // so every hotkey but the open screen's own was dead once a tab was up.
        static string HotkeyTarget(InputState input) =>
              input.PlanetListScreen    ? "Planets"
            : input.ExoticListScreen    ? "Exotic"
            : input.EmpirePatrolsScreen ? "Patrols"
            : input.ShipListScreen      ? "ShipList"
            : input.TroopListScreen     ? "Troops"
            : input.FleetDesignScreen   ? "Fleets"
            : input.BlueprintsSceen     ? "Blueprints"
            : input.ImportantEventsScreen ? "Events"
            // the bar's own keys: reachable from the universe through the other overload,
            // dead from inside a screen until they came through here too
            : input.KeyPressed(Keys.R) && !input.IsCtrlKeyDown ? "Research"  // Ctrl+Alt+R is the resolution tool
            : input.KeyPressed(Keys.T) ? "Budget"
            : input.KeyPressed(Keys.Y) ? "Shipyard"
            : input.KeyPressed(Keys.U) ? "Empire"
            : input.KeyPressed(Keys.I) ? "Diplomacy"
            : input.KeyPressed(Keys.E) ? "Espionage"
            : null;

        // TODO: This is utterly retarded, needs a complete rewrite
        public bool HandleInput(InputState input, GameScreen caller)
        {
            // Ludoal fork: the hotkeys switch tabs from inside a screen, through the very path a
            // click takes - the group guard, the Shipyard's save prompt and the self-guards all
            // apply unchanged. Text fields still win: TakingInput means the letter is being typed.
            if (!GlobalStats.TakingInput)
            {
                string target = HotkeyTarget(input);
                if (target != null)
                    return SwitchTo(target, ReworkScreens.Group.None, caller);
            }

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
                            TogglePause();
                            return true;
                        }

                        if (b.launches == "SpeedUp" || b.launches == "SpeedDown")
                        {
                            GameAudio.AcceptClick();
                            Universe.AdjustGameSpeed(b.launches == "SpeedUp");
                            return true;
                        }

                        return SwitchTo(b.launches, b.Group, caller);
                    }
                }
            }
            return false;
        }

        // Ludoal fork: a pause a SCREEN owns is not the player's to lift. Clicking PAUSED while
        // an open screen holds the simulation used to flip the label back to PAUSE while the game
        // stayed stopped - the button lied about a state it did not control. The screen releases
        // its own pause when it closes, so the control simply refuses here.
        bool PauseIsAutomatic => Universe.ScreenManager.AnyScreenHoldsUniversePause();

        void TogglePause()
        {
            if (PauseIsAutomatic)
            {
                GameAudio.NegativeClick();
                return;
            }
            GameAudio.AcceptClick();
            Universe.UState.Paused = !Universe.UState.Paused;
        }

        // Ludoal fork: one place decides what "go to screen X while screen Y is open" does.
        // The mouse path and the keyboard path both land here, so a tab reachable by click is
        // reachable by its hotkey and cannot drift apart from it.
        bool SwitchTo(string launches, ReworkScreens.Group group, GameScreen caller)
        {
            // Ludoal fork: a GROUP button whose group is already open just closes it,
            // whichever of its tabs you are on. The per-class guards below only know
            // the group's FIRST screen, so pressing DESIGN from the Shipyard used to
            // close it and reopen Fleets rather than leave the group.
            if (group != ReworkScreens.Group.None &&
                group == ReworkScreens.GroupOf(caller))
            {
                GameAudio.EchoAffirmative();
                caller.ExitScreen();  // virtual - the Shipyard's override still prompts
                return true;
            }

            // Shipyard keeps its dedicated exit (unsaved-design prompt);
            // its LaunchScreen() then opens the requested target — and its
            // own button simply closes it (toggle), like every other panel.
            if (caller is ShipDesignScreen shipDesigner)
            {
                shipDesigner.ExitToMenu(launches);
                return true;
            }

            // Everyone else (FleetDesign included): close the caller, then
            // the dispatch below opens the target. Clicking a screen's own
            // button just closes it (toggle) via the per-branch self-guards.
            caller.ExitScreen();

            if (launches == "Research")
            {
                GameAudio.EchoAffirmative();
                if (!(caller is ResearchScreenNew))
                {
                    Universe.ScreenManager.AddScreen(new ResearchScreenNew(Universe, Universe, this));
                }
            }
            else if (launches == "Budget")
            {
                GameAudio.EchoAffirmative();
                // Ludoal fork: both regimes, or the reworked screen never
                // recognises itself and the bar stacks a second copy
                if (!GameScreens.ReworkScreens.IsEconomy(caller))
                {
                    Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Economy(Universe));
                }
            }
            else if (launches == "Main Menu")
            {
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new GamePlayMenuScreen(Universe));
            }
            else if (launches == "Shipyard")
            {
                if (caller is ShipDesignScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new ShipDesignScreen(Universe, this));
            }
            else if (launches == "Fleets")
            {
                if (caller is FleetDesignScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new FleetDesignScreen(Universe, this));
            }
            // Ludoal fork: ShipList and Espionage were missing from the caller
            // dispatch — harmless while only Shipyard/Fleets kept the bar live,
            // a dead button once every full-screen does (top-bar standard).
            else if (launches == "ShipList")
            {
                if (caller is ShipListScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new ShipListScreen(Universe, this));
            }
            // Ludoal fork: provisional second-row buttons (self-click = toggle close)
            else if (launches == "Planets")
            {
                if (caller is PlanetListScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new PlanetListScreen(Universe, this));
            }
            else if (launches == "Exotic")
            {
                if (caller is ExoticSystemsListScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new ExoticSystemsListScreen(Universe, this));
            }
            else if (launches == "Patrols")
            {
                if (caller is EmpirePatrolsScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new EmpirePatrolsScreen(Universe, Universe.Player));
            }
            else if (launches == "Events")
            {
                if (caller is ImportantEventsScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new ImportantEventsScreen(Universe));
            }
            else if (launches == "Blueprints")
            {
                if (caller is BlueprintsScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new BlueprintsScreen(Universe, Universe.Player));
            }
            else if (launches == "Troops")
            {
                if (caller is TroopListScreen)
                {
                    return true;
                }
                GameAudio.EchoAffirmative();
                Universe.ScreenManager.AddScreen(new TroopListScreen(Universe, this));
            }
            else if (launches == "Espionage")
            {
                // Ludoal fork: both regimes — EspionageScreen is the legacy one,
                // and IsEspionage covers the stock and reworked infiltration screens
                if (caller is EspionageScreen || GameScreens.ReworkScreens.IsEspionage(caller))
                {
                    return true;
                }
                if (Universe.Player.LegacyEspionageEnabled)
                    Universe.ScreenManager.AddScreen(new EspionageScreen(Universe));
                else
                    Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Espionage(Universe));

                GameAudio.EchoAffirmative();
            }
            else if (launches == "Empire")
            {
                if (caller is EmpireManagementScreen)
                {
                    return true;
                }
                Universe.ScreenManager.AddScreen(new EmpireManagementScreen(Universe, this));
                GameAudio.EchoAffirmative();
            }
            else if (launches == "Diplomacy")
            {
                if (GameScreens.ReworkScreens.IsDiplomacy(caller))
                {
                    return true;
                }
                Universe.ScreenManager.AddScreen(GameScreens.ReworkScreens.Diplomacy(Universe));
                GameAudio.EchoAffirmative();
            }
            else if (launches == "?")
            {
                GameAudio.TacticalPause();
                Universe.ScreenManager.AddScreen(new Codex.CodexScreen(Universe));
            }
            return true; // handled
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
            public bool Bare;            // text only, no plate behind it
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
