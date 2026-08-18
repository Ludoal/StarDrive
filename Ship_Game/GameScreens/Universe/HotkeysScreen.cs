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
            ("TIME & SPEED", new[]
            {
                new Hotkey("Space", "Pause"),
                new Hotkey("Shift+Space", "Reset speed to x1"),
                new Hotkey("+ / -", "Speed up / slow down"),
            }),
            ("MAP & CAMERA", new[]
            {
                new Hotkey("PageUp", "Zoom to selection"),
                new Hotkey("PageDown", "Zoom out"),
                new Hotkey("Alt (hold)", "Tactical icons at close zoom"),
                new Hotkey(null, "Cinematic mode", nameof(KeyBindings.CinematicMode)),
                new Hotkey("Ctrl+Middle-click", "Chase camera on selected ship"),
            }),
            ("OVERLAYS", new[]
            {
                new Hotkey(null, "Influence zones", nameof(KeyBindings.InfluenceOverlay)),
                new Hotkey(null, "Vision / sensor coverage", nameof(KeyBindings.VisionOverlay)),
                new Hotkey(null, "Subspace projection", nameof(KeyBindings.FTLOverlay)),
                new Hotkey(null, "Gravity wells", nameof(KeyBindings.GravityWellOverlay)),
                new Hotkey(null, "Weapons range", nameof(KeyBindings.RangeOverlay)),
                new Hotkey("Shift+F5", "Realistic lights"),
            }),
            ("SCREENS", new[]
            {
                new Hotkey("R", "Research"),
                new Hotkey("Y", "Shipyard"),
                new Hotkey(null, "Fleets", nameof(KeyBindings.FleetDesignScreen)),
                new Hotkey(null, "Blueprints", nameof(KeyBindings.BlueprintsScreen)),
                new Hotkey(null, "Ships", nameof(KeyBindings.ShipListScreen)),
                new Hotkey("E", "Espionage"),
                new Hotkey("I", "Intelligence"),
                new Hotkey("T", "Economy"),
                new Hotkey("U", "Colonies"),
                new Hotkey(null, "Planets", nameof(KeyBindings.PlanetListScreen)),
                new Hotkey(null, "Troops", nameof(KeyBindings.TroopListScreen)),
                new Hotkey(null, "Patrols", nameof(KeyBindings.EmpirePatrolsScreen)),
                new Hotkey(null, "Exotic Systems", nameof(KeyBindings.ExoticListScreen)),
                new Hotkey(null, "Automation", nameof(KeyBindings.AutomationWindow)),
                new Hotkey(null, "Deep Space Build", nameof(KeyBindings.DeepSpaceBuildWindow)),
                new Hotkey(null, "Exotic Bonuses", nameof(KeyBindings.ExoticBonusesWindow)),
                new Hotkey(null, "Freighter Utilization", nameof(KeyBindings.FreighterUtilWindow)),
                new Hotkey(null, "Important Events log", nameof(KeyBindings.ImportantEventsScreen)),
                new Hotkey(null, "Last viewed colony", nameof(KeyBindings.ColonyOverviewScreen)),
                new Hotkey("F1", "Help"),
                new Hotkey("Esc", "Close screen"),
            }),
            ("FLEETS", new[]
            {
                new Hotkey("1-0", "Select fleet 1-10"),
                new Hotkey("Alt+1-0", "Select fleet 11-20"),
                new Hotkey("Ctrl+digit", "Create / replace fleet"),
                new Hotkey("Ctrl+Shift+digit", "Add selection to fleet"),
            }),
            ("SELECTION & ORDERS", new[]
            {
                new Hotkey(null, "Ship pie menu", nameof(KeyBindings.ShipPieMenu)),
                new Hotkey("Del / Backspace", "Scrap ship"),
                new Hotkey("Alt+Click", "Select same hull"),
                new Hotkey("Ctrl+Click", "Select same role and hull"),
                new Hotkey("Ctrl+Alt+Click", "Select same design"),
                new Hotkey("Mouse Back", "Previous target"),
            }),
            ("SHIPYARD", new[]
            {
                new Hotkey("Arrows", "Rotate module in hand"),
                new Hotkey("Tab", "Show all firing arcs"),
                new Hotkey("T", "Design issues"),
                new Hotkey("Ctrl+Z / Ctrl+Y", "Undo / redo"),
                new Hotkey("Hold Left-click", "Set firing arc"),
                new Hotkey("Right-click", "Cancel / remove module; outside: close"),
            }),
            ("FLEET DESIGN", new[]
            {
                new Hotkey("Del / Backspace", "Remove squad"),
                new Hotkey("WASD / edges", "Scroll the grid"),
            }),
            ("MISC", new[]
            {
                new Hotkey(null, "Quicksave", nameof(KeyBindings.QuickSave)),
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
            TitleText = "Hotkeys";
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
                        string keyText = k.Bind == null ? k.Keys : KeyBindings.Name(KeyBindings.Get(k.Bind));
                        UILabel keyLabel = Add(new UILabel(new Vector2(x, y), keyText, Fonts.Arial12Bold, Color.Wheat));
                        Add(new UILabel(new Vector2(x + keyW, y), k.Action, Fonts.Arial12, Color.White));
                        if (k.Bind != null)
                            Rows.Add(new BindRow { Bind = k.Bind, KeyLabel = keyLabel,
                                                   Hit = new RectF(x, y, keyW - 8, lineH) });
                        y += lineH;
                    }
                    y += lineH; // a breath between categories
                }
            }

            string note = "Click a key to rebind it - right-click resets a row.";
            var pos = new Vector2(inner.X + (inner.Width - Fonts.Arial12.TextWidth(note)) / 2f,
                                  inner.Bottom - Fonts.Arial12.LineSpacing - 10);
            Footer = Add(new UILabel(pos, note, Fonts.Arial12, Color.Gray));

            UIButton resetAll = Button(ButtonStyle.WideHostile, 0f, 0f, "Reset all to defaults", b =>
            {
                KeyBindings.ResetAll();
                Listening = null;
                GameAudio.AcceptClick();
            });
            resetAll.SetAbsSize(170, 24);
            resetAll.SetAbsPos(inner.Right - 186, inner.Bottom - 34);
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
                r.KeyLabel.Text = listening ? "press a key..." : KeyBindings.Name(cur);
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
