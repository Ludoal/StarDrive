using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (backlog #3, v2): Troops Array — where are all our ground troops?
    // Grouped by (location, troop type): System | Location | Status | Troop | Num | Strength.
    // Status: Garrison (own planet) / Deployed (planet we don't own) /
    //         Transport (aboard a troopship) / Stationed (aboard any other ship).
    // Click a row: ship rows snap the camera to the ship; planet rows open the
    // colony view (own) or the planet view (not ours) via SnapViewColony.
    public sealed class TroopListScreen : GameScreen
    {
        readonly Menu2 TitleBar;
        readonly Vector2 TitlePos;
        readonly Menu2 EMenu;

        public UniverseScreen Universe;
        Empire Player => Universe.Player;
        readonly ScrollList<TroopListScreenItem> TroopSL;
        readonly EmpireUIOverlay EmpireUI;
        RectF ERect;
        int NumTroops;

        public TroopListScreen(UniverseScreen parent, EmpireUIOverlay empireUi, string audioCue = "")
            : base(parent, toPause: parent)
        {
            if (!string.IsNullOrEmpty(audioCue))
                GameAudio.PlaySfxAsync(audioCue);

            Universe = parent;
            EmpireUI = empireUi;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;

            Rectangle titleRect = new Rectangle(2, 44, ScreenWidth * 2 / 3, 80);
            TitleBar = new Menu2(titleRect);
            TitlePos = new Vector2(titleRect.X + titleRect.Width / 2 - Fonts.Laserian14.MeasureString("Troops Array").X / 2f,
                                   titleRect.Y + titleRect.Height / 2 - Fonts.Laserian14.LineSpacing / 2);
            Rectangle leftRect = new Rectangle(2, titleRect.Y + titleRect.Height + 5, ScreenWidth - 10,
                                               ScreenHeight - titleRect.Bottom - 7);
            EMenu = new Menu2(leftRect);
            Add(new CloseButton(leftRect.Right - 40, leftRect.Y + 20));
            ERect = new(leftRect.X + 20, titleRect.Bottom + 50, ScreenWidth - 40,
                        leftRect.Bottom - (titleRect.Bottom + 46) - 31);
            RectF slRect = new(ERect.X, ERect.Y - 10, ERect.W, ERect.H + 10);
            TroopSL = Add(new ScrollList<TroopListScreenItem>(slRect, 40));
            TroopSL.EnableItemHighlight = true;
            TroopSL.OnDoubleClick = OnRowClicked; // Ludoal fork: double-click everywhere, like Ships/Empire

            PopulateList();
        }

        void OnRowClicked(TroopListScreenItem item)
        {
            GameAudio.AcceptClick();
            ExitScreen();
            if (item.Ship != null)
            {
                // same gentle zoom as the Ships Array (SnapViewShip dives way too deep)
                Universe.ViewToShip(item.Ship);
                Universe.returnToShip = true;
            }
            else if (item.Planet != null)
            {
                // Garrison: colony view. Deployed (planet not ours): combatView=true
                // routes to the Ground Assault View via OpenCombatMenu.
                bool deployed = item.Planet.Owner != Player;
                Universe.SnapViewColony(item.Planet, deployed);
            }
        }

        void PopulateList()
        {
            // group rows by (location, troop type) — accumulate count and strength
            var groups = new Map<(object Location, string TroopName), TroopListScreenItem>();

            void Accumulate(object location, string sysName, string locName, string status,
                            Color statusColor, Troop t, Planet p, Ship s)
            {
                var key = (location, t.Name);
                if (groups.TryGetValue(key, out TroopListScreenItem item))
                    item.Accumulate(t);
                else
                    groups.Add(key, TroopSL.AddItem(
                        new TroopListScreenItem(sysName, locName, status, statusColor, t, p, s)));
            }

            foreach (SolarSystem system in Universe.UState.Systems)
            {
                foreach (Planet p in system.PlanetList)
                {
                    bool ours = p.Owner == Player;
                    foreach (Troop t in p.Troops.GetTroopsOf(Player))
                        Accumulate(p, system.Name, p.Name,
                                   ours ? "Garrison" : "Deployed",
                                   ours ? Color.LightGreen : Color.Orange, t, p, null);
                }
            }

            foreach (Ship s in Player.OwnedShips)
            {
                if (s.TroopCount == 0)
                    continue;
                bool transport = s.DesignRole == RoleName.troopShip || s.DesignRole == RoleName.troop;
                string sysName = s.System?.Name ?? "Deep Space";
                foreach (Troop t in s.GetOurTroops())
                    Accumulate(s, sysName, s.Name,
                               transport ? "Transport" : "Stationed",
                               transport ? Color.LightSkyBlue : Color.SteelBlue, t, null, s);
            }

            NumTroops = 0;
            foreach (TroopListScreenItem item in TroopSL.AllEntries)
                NumTroops += item.Count;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            TitleBar.Draw(batch, elapsed);
            batch.DrawString(Fonts.Laserian14, $"Troops Array ({NumTroops})", TitlePos, Colors.Cream);
            EMenu.Draw(batch, elapsed);
            base.Draw(batch, elapsed);

            if (TroopSL.NumEntries > 0)
            {
                TroopListScreenItem e1 = TroopSL.ItemAtTop;
                Graphics.Font font = Fonts.Arial20Bold;

                DrawHeader(batch, font, e1.SysNameRect, "System");
                DrawHeader(batch, font, e1.LocationRect, "Location");
                DrawHeader(batch, font, e1.StatusRect, "Status");
                DrawHeader(batch, font, e1.TroopRect, "Troop");
                DrawHeader(batch, font, e1.NumRect, "Num");
                // Ludoal fork: the word alone. This header carried "Strength" AND a fist icon
                // saying the same thing, and it was the only column of the six to do so — the
                // text stays because the other five are text (Ludo).
                DrawHeader(batch, font, e1.StrRect, "Strength");

                Color lineColor = new Color(118, 102, 67, 255);
                float columnTop = ERect.Y + 15;
                float columnBot = ERect.Y + ERect.H - 20;
                foreach (int colX in new[] { e1.LocationRect.X, e1.StatusRect.X, e1.TroopRect.X, e1.NumRect.X, e1.StrRect.X })
                    batch.DrawLine(new Vector2(colX, columnTop), new Vector2(colX, columnBot), lineColor);
                batch.DrawRectangle(TroopSL.ItemsHousing, lineColor);
            }
            else
            {
                var msgPos = new Vector2(ERect.X + 30, ERect.Y + 30);
                batch.DrawString(Fonts.Arial20Bold, "No troops anywhere — recruit some before the neighbours visit.",
                                 msgPos, Color.Gray);
            }
            EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        void DrawHeader(SpriteBatch batch, Graphics.Font font, Rectangle rect, string text)
        {
            var pos = new Vector2(rect.X + rect.Width / 2 - font.MeasureString(text).X / 2f,
                                  ERect.Y - font.LineSpacing);
            batch.DrawString(font, text, pos, Colors.Cream);
        }

        public override bool HandleInput(InputState input)
        {
            if (EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;
            if (base.HandleInput(input))
                return true;
            if (input.Escaped || input.RightMouseClick
                || (input.TroopListScreen && !GlobalStats.TakingInput))
            {
                ExitScreen();
                return true;
            }
            return false;
        }
    }

    public sealed class TroopListScreenItem : ScrollListItem<TroopListScreenItem>
    {
        public Rectangle SysNameRect;
        public Rectangle LocationRect;
        public Rectangle StatusRect;
        public Rectangle TroopRect;
        public Rectangle NumRect;
        public Rectangle StrRect;

        readonly string SystemName;
        readonly string Location;
        readonly string Status;
        readonly Color StatusColor;
        readonly string TroopName;
        public readonly Planet Planet;   // set for garrison/deployed rows
        public readonly Ship Ship;       // set for transport/stationed rows
        public int Count { get; private set; }
        float Strength;

        public TroopListScreenItem(string systemName, string location, string status, Color statusColor,
                                   Troop troop, Planet planet, Ship ship)
        {
            SystemName = systemName;
            Location = location;
            Status = status;
            StatusColor = statusColor;
            TroopName = troop.Name;
            Planet = planet;
            Ship = ship;
            Count = 1;
            Strength = troop.Strength;
        }

        public void Accumulate(Troop t)
        {
            Count += 1;
            Strength += t.Strength;
            RequiresLayout = true;
        }

        public override void PerformLayout()
        {
            int x = (int)X;
            int y = (int)Y;
            int w = (int)Width;
            int h = (int)Height;
            RemoveAll();

            // Ludoal fork: an empty column is kept on the right, the way the Ships list does it
            // (Ludo). There, the trailing columns are fixed 60px each and the layout simply
            // stops, so whatever the window is wider than the table becomes breathing room that
            // grows with the screen. This list divided a full 100% of the width between its six
            // columns (0.14+0.28+0.12+0.22+0.08+0.16), so the last one ran into the scrollbar
            // and nothing ever had air to its right.
            //
            // Taken off the width BEFORE the fractions are applied rather than left as a
            // seventh fraction: a reservation belongs to the object, not to a percentage that
            // would shrink exactly when the window gets small and the space matters most.
            const int RightGutter = 60;   // the Ships list's own trailing column width
            int cols = w > RightGutter ? w - RightGutter : w;

            int nextX = x;
            Rectangle NextRect(float width)
            {
                int next = nextX;
                nextX += (int)width;
                return new Rectangle(next, y, (int)width, h);
            }

            SysNameRect  = NextRect(cols * 0.14f);
            LocationRect = NextRect(cols * 0.28f);
            StatusRect   = NextRect(cols * 0.12f);
            TroopRect    = NextRect(cols * 0.22f);
            NumRect      = NextRect(cols * 0.08f);
            StrRect      = NextRect(cols * 0.16f);

            AddCentered(SysNameRect, SystemName, Colors.Cream);
            AddCentered(LocationRect, Location, Colors.Cream);
            AddCentered(StatusRect, Status, StatusColor); // the status carries the color, like other panels
            AddCentered(TroopRect, TroopName, Colors.Cream);
            AddCentered(NumRect, Count.ToString(), Colors.Cream);
            AddCentered(StrRect, ((int)Strength).ToString(), Colors.Cream);
            base.PerformLayout();
        }

        void AddCentered(Rectangle rect, string text, Color color)
        {
            Graphics.Font font = Fonts.Arial12Bold.MeasureString(text).X <= rect.Width ? Fonts.Arial12Bold : Fonts.Arial8Bold;
            var pos = new Vector2(rect.X + rect.Width / 2 - font.MeasureString(text).X / 2f,
                                  rect.Y + rect.Height / 2 - font.LineSpacing / 2);
            Label(pos, text, font, color);
        }
    }
}
