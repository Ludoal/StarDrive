using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    // Ludoal fork: the hotkey reference, opened from the in-game menu.
    // The bindings live in one flat data table (category -> action -> keys).
    public sealed class HotkeysScreen : PopupWindow
    {
        struct Hotkey
        {
            public readonly string Keys;
            public readonly string Action;
            public Hotkey(string keys, string action) { Keys = keys; Action = action; }
        }

        // display order = table order. Keys are the CURRENT bindings (InputState.cs);
        // until remapping lands, this table mirrors them by hand.
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
                new Hotkey("F11", "Cinematic mode"),
                new Hotkey("Ctrl+Middle-click", "Chase camera on selected ship"),
            }),
            ("OVERLAYS", new[]
            {
                new Hotkey("F2", "Influence zones"),
                new Hotkey("F3", "Vision / sensor coverage"),
                new Hotkey("F4", "Subspace projection"),
                new Hotkey("F5", "Gravity wells"),
                new Hotkey("F6", "Weapons range"),
                new Hotkey("Shift+F5", "Realistic lights"),
            }),
            ("SCREENS", new[]
            {
                new Hotkey("R", "Research"),
                new Hotkey("Y", "Shipyard"),
                new Hotkey("J", "Fleets"),
                new Hotkey("F", "Blueprints"),
                new Hotkey("K", "Ships"),
                new Hotkey("E", "Espionage"),
                new Hotkey("I", "Intelligence"),
                new Hotkey("T", "Economy"),
                new Hotkey("U", "Colonies"),
                new Hotkey("L", "Planets"),
                new Hotkey("C", "Troops"),
                new Hotkey("P", "Patrols"),
                new Hotkey("G", "Exotic Systems"),
                new Hotkey("H", "Automation"),
                new Hotkey("B", "Deep Space Build"),
                new Hotkey("M", "Exotic Bonuses"),
                new Hotkey("N", "Freighter Utilization"),
                new Hotkey("F7", "Important Events log"),
                new Hotkey("F8", "Colony overview (the Empire group's colony tab)"),
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
                new Hotkey("Q", "Ship pie menu"),
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
                new Hotkey("F9", "Quicksave"),
            }),
        };

        // categories per display column, balanced by hand for the fixed table above
        static readonly int[][] Columns =
        {
            new[] { 0, 1, 2 },       // time, map, overlays
            new[] { 3, 8 },          // screens, misc
            new[] { 4, 5, 6, 7 },    // fleets, selection, shipyard, fleet design
        };

        public HotkeysScreen(GameScreen parent) : base(parent, 1200, 620)
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
                        Add(new UILabel(new Vector2(x, y), k.Keys, Fonts.Arial12Bold, Color.Wheat));
                        Add(new UILabel(new Vector2(x + keyW, y), k.Action, Fonts.Arial12, Color.White));
                        y += lineH;
                    }
                    y += lineH; // a breath between categories
                }
            }

            string note = "Key remapping is planned for a future update.";
            var pos = new Vector2(inner.X + (inner.Width - Fonts.Arial12.TextWidth(note)) / 2f,
                                  inner.Bottom - Fonts.Arial12.LineSpacing - 10);
            Add(new UILabel(pos, note, Fonts.Arial12, Color.Gray));
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            base.Draw(batch, elapsed);
        }
    }
}
