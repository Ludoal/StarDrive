using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    // Ludoal fork: the hotkey reference IS the editor (wishlist). Rows backed by the
    // KeyBindings table are clickable: click puts the row in listening mode (gold),
    // the next key pressed becomes the binding, Esc cancels. A conflict swaps: the
    // previous holder goes unbound (yellow) until rebound. Right-click resets a row
    // to its shipped default; the button below resets them all. Every change writes
    // Hotkeys.yaml on the spot - the edit is the save.
    public sealed class HotkeysScreen : PopupWindow
    {
        struct Hotkey
        {
            public readonly string Keys;
            public readonly string Action;
            public readonly string Bind; // KeyBindings field name; null = fixed row
            public Hotkey(string keys, string action, string bind = null) { Keys = keys; Action = action; Bind = bind; }
        }

        // display order = table order. Fixed rows keep their literal key text; bound
        // rows read the live KeyBindings value and never show a literal.
        static readonly (string Category, Hotkey[] Keys)[] Bindings =
        {
            (Localizer.Token(GameText.HkCatTimeSpeed), new[]
            {
                new Hotkey(Localizer.Token(GameText.HkKeySpace), Localizer.Token(GameText.HkPause)),
                new Hotkey(Localizer.Token(GameText.HkKeyShiftSpace), Localizer.Token(GameText.HkResetSpeedToX1)),
                new Hotkey("+ / -", Localizer.Token(GameText.HkSpeedUpSlowDown)),
            }),
            (Localizer.Token(GameText.HkCatMapCamera), new[]
            {
                new Hotkey(Localizer.Token(GameText.HkKeyArrowsWasd), Localizer.Token(GameText.HkPanTheCamera)),
                new Hotkey(null, Localizer.Token(GameText.HkZoomToSelection), nameof(KeyBindings.ZoomToSelection)),
                new Hotkey(null, Localizer.Token(GameText.HkZoomOut), nameof(KeyBindings.ZoomOut)),
                new Hotkey(Localizer.Token(GameText.HkKeyAltHold), Localizer.Token(GameText.HkTacticalIconsAtCloseZoom)),
                new Hotkey(Localizer.Token(GameText.HkKeyCtrlMiddle), Localizer.Token(GameText.HkChaseCameraOnSelectedShip)),
                new Hotkey(null, Localizer.Token(GameText.HkPreviousColony), nameof(KeyBindings.PrevColony)),
                new Hotkey(null, Localizer.Token(GameText.HkNextColony), nameof(KeyBindings.NextColony)),
                new Hotkey(null, Localizer.Token(GameText.HkGoToCapital), nameof(KeyBindings.GoToCapital)),
            }),
            (Localizer.Token(GameText.HkCatOverlays), new[]
            {
                new Hotkey(null, Localizer.Token(GameText.HkInfluenceZones), nameof(KeyBindings.InfluenceOverlay)),
                new Hotkey(null, Localizer.Token(GameText.HkVisionSensorCoverage), nameof(KeyBindings.VisionOverlay)),
                new Hotkey(null, Localizer.Token(GameText.HkSubspaceProjection), nameof(KeyBindings.FTLOverlay)),
                new Hotkey(null, Localizer.Token(GameText.HkGravityWells), nameof(KeyBindings.GravityWellOverlay)),
                new Hotkey(null, Localizer.Token(GameText.HkWeaponsRange), nameof(KeyBindings.RangeOverlay)),
                // bench 434 (maintainer decision): Cinematic changes what you SEE, not
                // where the camera is - its family is the overlays
                new Hotkey(null, Localizer.Token(GameText.HkCinematicMode), nameof(KeyBindings.CinematicMode)),
                new Hotkey(null, Localizer.Token(GameText.HkRealisticLights), nameof(KeyBindings.RealisticLights)),
            }),
            (Localizer.Token(GameText.HkCatScreens), new[]
            {
                new Hotkey(null, Localizer.Token(GameText.HkResearch), nameof(KeyBindings.OpenResearch)),
                new Hotkey(null, Localizer.Token(GameText.HkShipyard), nameof(KeyBindings.OpenShipyard)),
                new Hotkey(null, Localizer.Token(GameText.HkFleets), nameof(KeyBindings.FleetDesignScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkBlueprints), nameof(KeyBindings.BlueprintsScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkShips), nameof(KeyBindings.ShipListScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkEspionage), nameof(KeyBindings.OpenEspionage)),
                new Hotkey(null, Localizer.Token(GameText.HkIntelligence), nameof(KeyBindings.OpenDiplomacy)),
                new Hotkey(null, Localizer.Token(GameText.HkEconomy), nameof(KeyBindings.OpenEconomy)),
                new Hotkey(null, Localizer.Token(GameText.HkColonies), nameof(KeyBindings.OpenEmpire)),
                new Hotkey(null, Localizer.Token(GameText.HkPlanets), nameof(KeyBindings.PlanetListScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkTroops), nameof(KeyBindings.TroopListScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkPatrols), nameof(KeyBindings.EmpirePatrolsScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkExoticSystems), nameof(KeyBindings.ExoticListScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkAutomation), nameof(KeyBindings.AutomationWindow)),
                new Hotkey(null, Localizer.Token(GameText.HkDeepSpaceBuild), nameof(KeyBindings.DeepSpaceBuildWindow)),
                new Hotkey(null, Localizer.Token(GameText.HkExoticBonuses), nameof(KeyBindings.ExoticBonusesWindow)),
                new Hotkey(null, Localizer.Token(GameText.HkFreighterUtilization), nameof(KeyBindings.FreighterUtilWindow)),
                new Hotkey(null, Localizer.Token(GameText.HkImportantEventsLog), nameof(KeyBindings.ImportantEventsScreen)),
                new Hotkey(null, Localizer.Token(GameText.HkLastViewedColony), nameof(KeyBindings.ColonyOverviewScreen)),
                new Hotkey("F1", Localizer.Token(GameText.HkHelp)),
                new Hotkey(Localizer.Token(GameText.HkKeyEsc), Localizer.Token(GameText.HkCloseScreen)),
            }),
            (Localizer.Token(GameText.HkCatFleets), new[]
            {
                new Hotkey("1-0", Localizer.Token(GameText.HkSelectFleet110)),
                new Hotkey("Alt+1-0", Localizer.Token(GameText.HkSelectFleet1120)),
                new Hotkey(Localizer.Token(GameText.HkKeyCtrlDigit), Localizer.Token(GameText.HkCreateReplaceFleet)),
                new Hotkey(Localizer.Token(GameText.HkKeyCtrlShiftDigit), Localizer.Token(GameText.HkAddSelectionToFleet)),
            }),
            (Localizer.Token(GameText.HkCatSelectionOrders), new[]
            {
                new Hotkey(null, Localizer.Token(GameText.HkShipPieMenu), nameof(KeyBindings.ShipPieMenu)),
                new Hotkey(Localizer.Token(GameText.HkKeyDelBackspace), Localizer.Token(GameText.HkScrapShip)),
                new Hotkey(Localizer.Token(GameText.HkKeyAltClick), Localizer.Token(GameText.HkSelectSameHull)),
                new Hotkey(Localizer.Token(GameText.HkKeyCtrlClick), Localizer.Token(GameText.HkSelectSameRoleAndHull)),
                new Hotkey(Localizer.Token(GameText.HkKeyCtrlAltClick), Localizer.Token(GameText.HkSelectSameDesign)),
                new Hotkey(Localizer.Token(GameText.HkKeyMouseBack), Localizer.Token(GameText.HkPreviousTarget)),
            }),
            (Localizer.Token(GameText.HkCatShipyard), new[]
            {
                new Hotkey(Localizer.Token(GameText.HkKeyArrows), Localizer.Token(GameText.HkRotateModuleInHand)),
                new Hotkey("Tab", Localizer.Token(GameText.HkShowAllFiringArcs)),
                new Hotkey(null, Localizer.Token(GameText.HkDesignIssues), nameof(KeyBindings.DesignIssues)), // bench 435: the OLD fixed row shadowed the live one
                new Hotkey("Ctrl+Z / Ctrl+Y", Localizer.Token(GameText.HkUndoRedo)),
                new Hotkey(Localizer.Token(GameText.HkKeyHoldLeft), Localizer.Token(GameText.HkSetFiringArc)),
                new Hotkey(Localizer.Token(GameText.HkKeyRightClick), Localizer.Token(GameText.HkCancelRemoveModuleOutsideClose)),
            }),
            (Localizer.Token(GameText.HkCatFleetDesign), new[]
            {
                new Hotkey(Localizer.Token(GameText.HkKeyDelBackspace), Localizer.Token(GameText.HkRemoveSquad)),
                new Hotkey(Localizer.Token(GameText.HkKeyWasdEdges), Localizer.Token(GameText.HkScrollTheGrid)),
            }),
            (Localizer.Token(GameText.HkCatMisc), new[]
            {
                new Hotkey(null, Localizer.Token(GameText.HkQuicksave), nameof(KeyBindings.QuickSave)),
            }),
        };

        // categories per display column, balanced by hand for the fixed table above
        static readonly int[][] Columns =
        {
            new[] { 0, 1, 2 },       // time, map, overlays
            new[] { 3, 8 },          // screens, misc
            new[] { 4, 5, 6, 7 },    // fleets, selection, shipyard, fleet design
        };

        // a live editable row: its bind name, its key label, and its click zone
        class BindRow
        {
            public string Bind;
            public UILabel KeyLabel;
            public RectF Hit;
            public float ConflictFlash; // seconds of yellow left after a swap landed here
        }

        readonly Array<BindRow> Rows = new();
        string Listening; // bind name currently capturing, null = idle
        UILabel Footer;

        // 660 high (bench 408): the footer needs air under the SCREENS column
        public HotkeysScreen(GameScreen parent) : base(parent, 1200, 660)
        {
            TransitionOnTime = 0.25f;
        }

        public override void LoadContent()
        {
            // the window names itself in its own title bar; frame and close cross are PopupWindow's
            TitleText = Localizer.Token(GameText.MmHotkeysBtn);
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            int colW = (inner.Width - 32) / Columns.Length;
            int keyW = 150; // the fixed keys lane inside a column
            int lineH = Fonts.Arial12Bold.LineSpacing + 4;

            for (int c = 0; c < Columns.Length; ++c)
            {
                float x = inner.X + 16 + c * colW;
                float y = inner.Y + 12;
                foreach (int cat in Columns[c])
                {
                    (string title, Hotkey[] keys) = Bindings[cat];
                    Add(new UILabel(new Vector2(x, y), title, Fonts.Arial14Bold, Colors.Cream));
                    y += Fonts.Arial14Bold.LineSpacing + 6;
                    foreach (Hotkey k in keys)
                    {
                        // bindable keys read wheat and answer the mouse; fixed ones are
                        // dimmed gray - the color IS the affordance (bench 426)
                        string keyText = k.Bind == null ? k.Keys : KeyBindings.Name(KeyBindings.Get(k.Bind));
                        UILabel keyLabel = Add(new UILabel(new Vector2(x, y), keyText, Fonts.Arial12Bold,
                                                           k.Bind == null ? Color.Gray : Color.Wheat));
                        keyLabel.Tooltip = k.Bind == null ? Localizer.Token(GameText.HkNotRemappable)
                                                          : Localizer.Token(GameText.HkRebindTooltip);
                        Add(new UILabel(new Vector2(x + keyW, y), k.Action, Fonts.Arial12, Color.White));
                        if (k.Bind != null)
                            Rows.Add(new BindRow { Bind = k.Bind, KeyLabel = keyLabel,
                                                   Hit = new RectF(x, y, keyW - 8, lineH) });
                        y += lineH;
                    }
                    y += lineH; // a breath between categories
                }
            }

            string note = Localizer.Token(GameText.HkFooter);
            var pos = new Vector2(inner.X + (inner.Width - Fonts.Arial12.TextWidth(note)) / 2f,
                                  inner.Bottom - Fonts.Arial12.LineSpacing - 10);
            Footer = Add(new UILabel(pos, note, Fonts.Arial12, Color.Gray));

            UIButton resetAll = Button(ButtonStyle.WideHostile, 0f, 0f, Localizer.Token(GameText.HkResetAll), b =>
            {
                KeyBindings.ResetAll();
                Listening = null;
                GameAudio.AcceptClick();
            });
            resetAll.SetAbsSize(170, 24);
            resetAll.SetAbsPos(inner.X + 16, inner.Bottom - 34); // bottom-left (bench 426)
        }

        public override bool HandleInput(InputState input)
        {
            if (Listening != null)
            {
                if (input.KeyPressed(Keys.Escape))
                {
                    Listening = null; // Esc stays unbindable by construction
                    GameAudio.NegativeClick();
                    return true;
                }
                foreach (Keys k in input.GetKeysDown())
                {
                    if (!input.KeyPressed(k) || k == Keys.Escape)
                        continue;
                    string holder = KeyBindings.HolderOf(k, except: Listening);
                    if (holder != null)
                    {
                        // the swap: the key moves here, the previous holder goes unbound
                        // (yellow) until the player rebinds it - visible, never silent
                        KeyBindings.Set(holder, Keys.None);
                        BindRow other = Rows.Find(r => r.Bind == holder);
                        if (other != null) other.ConflictFlash = 2.5f;
                    }
                    KeyBindings.Set(Listening, k);
                    Listening = null;
                    GameAudio.AcceptClick();
                    return true;
                }
                return true; // while listening, the window swallows input
            }

            foreach (BindRow r in Rows)
            {
                if (!r.Hit.HitTest(input.CursorPosition))
                    continue;
                if (input.LeftMouseClick)
                {
                    Listening = r.Bind;
                    GameAudio.ButtonMouseOver();
                    return true;
                }
                if (input.RightMouseClick)
                {
                    KeyBindings.Set(r.Bind, KeyBindings.DefaultOf(r.Bind));
                    GameAudio.AcceptClick();
                    return true;
                }
            }

            return base.HandleInput(input);
        }

        public override void Update(float fixedDeltaTime)
        {
            foreach (BindRow r in Rows)
            {
                if (r.ConflictFlash > 0f) r.ConflictFlash -= fixedDeltaTime;
                Keys cur = KeyBindings.Get(r.Bind);
                bool listening = Listening == r.Bind;
                r.KeyLabel.Text = listening ? Localizer.Token(GameText.HkListening) : KeyBindings.Name(cur);
                r.KeyLabel.Color = listening ? Color.Gold
                                 : cur == Keys.None || r.ConflictFlash > 0f ? Color.Yellow
                                 : Color.Wheat;
            }
            base.Update(fixedDeltaTime);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            base.Draw(batch, elapsed);
        }
    }
}
