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
    // Ludoal fork (backlog #3, v1): Troops Array — finally a way to see where all our
    // ground troops are. Columns: System | Location (planet or troopship) | Troop type.
    // v1 is read-only and unsorted (grouped by location by construction order);
    // sorting/filters/actions can come in a later pass.
    public sealed class TroopListScreen : GameScreen
    {
        readonly Menu2 TitleBar;
        readonly Vector2 TitlePos;
        readonly Menu2 EMenu;

        public UniverseScreen Universe;
        Empire Player => Universe.Player;
        readonly ScrollList<TroopListScreenItem> TroopSL;
        RectF ERect;
        int NumTroops;

        public TroopListScreen(UniverseScreen parent, EmpireUIOverlay empireUi, string audioCue = "")
            : base(parent, toPause: parent)
        {
            if (!string.IsNullOrEmpty(audioCue))
                GameAudio.PlaySfxAsync(audioCue);

            Universe = parent;
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

            PopulateList();
        }

        void PopulateList()
        {
            // troops on the ground — any planet can host our troops (garrison or ongoing invasion)
            foreach (SolarSystem system in Universe.UState.Systems)
            {
                foreach (Planet p in system.PlanetList)
                {
                    foreach (Troop t in p.Troops.GetTroopsOf(Player))
                        TroopSL.AddItem(new TroopListScreenItem(system.Name, $"{p.Name} (planet)", t,
                                                                p.Owner == Player ? Color.LightGreen : Color.Orange));
                }
            }

            // troops aboard our ships (troopships, carriers with assault bays, anything that hosts them)
            foreach (Ship s in Player.OwnedShips)
            {
                if (s.TroopCount == 0)
                    continue;
                string sysName = s.System?.Name ?? "Deep Space";
                foreach (Troop t in s.GetOurTroops())
                    TroopSL.AddItem(new TroopListScreenItem(sysName, $"{s.Name} (ship)", t, Color.LightSkyBlue));
            }

            NumTroops = TroopSL.NumEntries;
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
                DrawHeader(batch, font, e1.TroopRect, "Troop");

                Color lineColor = new Color(118, 102, 67, 255);
                float columnTop = ERect.Y + 15;
                float columnBot = ERect.Y + ERect.H - 20;
                batch.DrawLine(new Vector2(e1.LocationRect.X, columnTop), new Vector2(e1.LocationRect.X, columnBot), lineColor);
                batch.DrawLine(new Vector2(e1.TroopRect.X, columnTop), new Vector2(e1.TroopRect.X, columnBot), lineColor);
                batch.DrawRectangle(TroopSL.ItemsHousing, lineColor);
            }
            else
            {
                var msgPos = new Vector2(ERect.X + 30, ERect.Y + 30);
                batch.DrawString(Fonts.Arial20Bold, "No troops anywhere — recruit some before the neighbours visit.",
                                 msgPos, Color.Gray);
            }
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
            if (base.HandleInput(input))
                return true;
            if (input.Escaped || input.RightMouseClick)
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
        public Rectangle TroopRect;

        readonly string SystemName;
        readonly string Location;
        readonly Troop Troop;
        readonly Color LocationColor;

        public TroopListScreenItem(string systemName, string location, Troop troop, Color locationColor)
        {
            SystemName = systemName;
            Location = location;
            Troop = troop;
            LocationColor = locationColor;
        }

        public override void PerformLayout()
        {
            int x = (int)X;
            int y = (int)Y;
            int w = (int)Width;
            int h = (int)Height;
            RemoveAll();

            int nextX = x;
            Rectangle NextRect(float width)
            {
                int next = nextX;
                nextX += (int)width;
                return new Rectangle(next, y, (int)width, h);
            }

            SysNameRect  = NextRect(w * 0.20f);
            LocationRect = NextRect(w * 0.40f);
            TroopRect    = NextRect(w * 0.40f);

            AddCentered(SysNameRect, SystemName, Colors.Cream);
            AddCentered(LocationRect, Location, LocationColor);
            AddCentered(TroopRect, $"{Troop.Name}  (str {(int)Troop.Strength})", Colors.Cream);
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
