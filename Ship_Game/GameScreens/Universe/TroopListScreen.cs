using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
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
        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row, this screen being one tab

        public UniverseScreen Universe;
        Empire Player => Universe.Player;
        readonly ScrollList<TroopListScreenItem> TroopSL;
        readonly EmpireUIOverlay EmpireUI;
        RectF ERect;
        int NumTroops;

        // Ludoal fork: status filter, the same shape as the Ships Array's role dropdown - to the
        // right of the title, and it remembers the last pick for the session the way that one
        // does. The statuses are the four PopulateList assigns.
        DropOptions<string> ShowStatus;
        static string LastStatus = "";   // "" = all
        static readonly string[] Statuses = { "Garrison", "Deployed", "Transport", "Stationed" };

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

            // Ludoal fork: the Troops tab of the Empire group - title and brass surround give way to
            // the group's tab row, and the first line inside the frame carries the status filter.
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.EmpireTabTitles, 2,
                                                    OnEmpireTabChanged, out Rectangle frame);
            RectF client = EmpireTabs.ClientArea;
            ERect = ScreenGroups.GalaxyTable(client, ScreenGroups.GalaxyHeaderH);
            RectF slRect = new(ERect.X, ERect.Y - 10, ERect.W, ERect.H + 10);
            TroopSL = Add(new ScrollList<TroopListScreenItem>(slRect, 40));
            TroopSL.EnableItemHighlight = true;
            TroopSL.OnDoubleClick = OnRowClicked; // Ludoal fork: double-click everywhere, like Ships/Empire

            ShowStatus = Add(new DropOptions<string>(
                new Rectangle((int)client.X + 10, (int)client.Y + 6, 160, 18)));
            ShowStatus.AddOption("All Troops", "");
            foreach (string s in Statuses)
                ShowStatus.AddOption(s, s);
            ShowStatus.ActiveValue = LastStatus;   // setter finds the index, defaults to "All"
            ShowStatus.OnValueChange = _ => PopulateList();

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
                // Ludoal fork (bench 191): closing that colony comes back HERE (maintainer feedback).
                // ⚠ Colony view only: the deployed path opens the Ground Assault view instead,
                // which never reaches the close handler that consumes this, so a hook set there
                // would sit and fire on some later, unrelated close.
                // ⚠ And AFTER the snap, which clears the hook on its way in.
                if (!deployed)
                    Universe.ReturnToListScreen = () => Universe.ScreenManager.AddScreen(new TroopListScreen(Universe, EmpireUI));
            }
        }

        void PopulateList()
        {
            // Ludoal fork: called again on every filter change, so the rows have to go first
            TroopSL.Reset();
            LastStatus = ShowStatus?.ActiveValue ?? "";
            string wanted = LastStatus;

            // group rows by (location, troop type) — accumulate count and strength
            var groups = new Map<(object Location, string TroopName), TroopListScreenItem>();

            void Accumulate(object location, string sysName, string locName, string status,
                            Color statusColor, Troop t, Planet p, Ship s)
            {
                if (wanted.NotEmpty() && status != wanted)
                    return;
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


        // Ludoal fork: the other tabs live in their own screen, so leaving this one hands over to
        // it. Its own index is a no-op: we are already here.
        void OnEmpireTabChanged(int index)
        {
            // one factory for the whole group (ScreenGroups) - this screen only says which tab it is
            ScreenGroups.SwitchEmpireTab(index, self: 2, Universe, this);
        }
        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first - as a Submenu background it would be
            // drawn among the children, after everything below it. The troop total moves onto the
            // reserved line beside the filter, where the title used to carry it.
            RectF client = EmpireTabs.ClientArea;
            batch.FillRectangle(client, ScreenGroups.GroupFrameFill);
            batch.DrawString(Fonts.Arial20Bold, $"Total Troops: {NumTroops}",
                             new Vector2(client.X + 190, client.Y + 4), Colors.Cream);
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
                // text stays because the other five are text (maintainer feedback).
                DrawHeader(batch, font, e1.StrRect, "Strength");

                Color lineColor = new Color(118, 102, 67, 255);
                float columnTop = ERect.Y + 15;
                float columnBot = ERect.Y + ERect.H - 20;
                // Ludoal fork (bench): the loop drew each column's LEFT edge, so the last column
                // had no line closing it and Strength bled into the empty gutter (maintainer feedback). Its
                // right edge closes the table.
                foreach (int colX in new[] { e1.LocationRect.X, e1.StatusRect.X, e1.TroopRect.X,
                                             e1.NumRect.X, e1.StrRect.X, e1.StrRect.Right })
                    batch.DrawLine(new Vector2(colX, columnTop), new Vector2(colX, columnBot), lineColor);
                batch.DrawRectangle(TroopSL.ItemsHousing, lineColor);
            }
            else
            {
                var msgPos = new Vector2(ERect.X + 30, ERect.Y + 30);
                batch.DrawString(Fonts.Arial20Bold, "No troops anywhere — recruit some before the neighbours visit.",
                                 msgPos, Color.Gray);
            }
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
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

            // Ludoal fork: laid out the way the Ships list and the Planet Array do it (maintainer feedback),
            // which took three benches to copy rather than approximate.
            //
            // THE RULE, read off both of them: a column holding FREE TEXT (a name, a place) takes
            // a share of the row; a column holding a DATUM (a count, a strength, a status) takes
            // a FIXED pixel width, because a number needs the same room whatever the window does.
            // Planet Array is 30px per stat, Ships is 60px per trailing column — neither ever
            // divides the whole row between everything it shows.
            //
            // That is also what produces the empty column for free: the fixed part does not grow,
            // so the gutter is a floor rather than a share. 46.158 took 60px off before applying
            // the fractions, which could not leave a gap at all (six fractions summing to 1.0
            // just redistribute whatever total they are handed); 46.159 made them sum to 0.90,
            // which left a gutter that SHRANK on a small window, exactly when space is tightest.
            const int DataCol = 90;     // Status, Num, Strength — a number's room is its own
            const int MinGutter = 150;  // the empty column, wide enough for buttons later

            // ⚠ and a CEILING on the text columns (maintainer feedback): an uncapped share gave
            // Location ~620px at 1920 for names like "Terran-Prototype". A name needs the room a
            // name needs; past that the width is spread, not used. The text columns take their
            // share UP TO a maximum, and everything beyond falls into the gutter — which is
            // where growing space belongs anyway.
            // 850 puts Location near 375px and the whole table near 1100 at 1920, leaving the
            // gutter roomy without turning the screen into two thirds of nothing.
            const int MaxText = 850;    // the three text columns together, at their widest

            int fixedPart = DataCol * 3 + MinGutter;
            int textPart = w > fixedPart ? w - fixedPart : w / 2;
            if (textPart > MaxText)
                textPart = MaxText;

            int nextX = x;
            Rectangle NextRect(float width)
            {
                int next = nextX;
                nextX += (int)width;
                return new Rectangle(next, y, (int)width, h);
            }

            // the three text columns share what is left, keeping the proportions they had
            // (0.14 : 0.28 : 0.22 of the old row, renormalised over the three of them)
            SysNameRect  = NextRect(textPart * 0.22f);
            LocationRect = NextRect(textPart * 0.44f);
            StatusRect   = NextRect(DataCol);
            TroopRect    = NextRect(textPart * 0.34f);
            NumRect      = NextRect(DataCol);
            StrRect      = NextRect(DataCol);

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
