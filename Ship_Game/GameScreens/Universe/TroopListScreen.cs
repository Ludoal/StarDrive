using System;
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
        float StrengthTotal;

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

            // Ludoal fork: the Troops tab of the Empire group, content-sized (maintainer bench,
            // the Economy pattern): fixed columns set the width, the troop-group count sets the
            // height - this page is allowed UNDER the 900p floor when the roster is short.
            int rows = CountTroopGroups();
            float contentW = TroopListScreenItem.TableW + 10;
            float fullAvail = ScreenHeight - ScreenGroups.TabRowY - ScreenGroups.FrameMargin;
            // 122 = tab strip + filter lane + column titles + the TOTAL footer's lane
            float contentH = Math.Min(fullAvail, 122 + Math.Max(3, rows) * 28);
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.EmpireTabTitles, 2,
                                                    OnEmpireTabChanged, contentW, contentH);
            RectF client = EmpireTabs.ClientArea;
            ERect = ScreenGroups.GalaxyTable(client, ScreenGroups.GalaxyHeaderH);
            // the last lane of the frame belongs to the TOTAL footer, not the list
            RectF slRect = new(ERect.X, ERect.Y - 10, ERect.W, ERect.H + 10 - 26);
            TroopSL = Add(new ScrollList<TroopListScreenItem>(slRect, 24));
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

        // dry count of (location, troop type) groups - the frame height derives from it,
        // BEFORE any UI exists. Unfiltered on purpose: the frame keeps one size for the
        // screen's life, a filter just shortens the list inside it.
        int CountTroopGroups()
        {
            var keys = new Map<(object, string), bool>();
            foreach (SolarSystem system in Universe.UState.Systems)
                foreach (Planet p in system.PlanetList)
                    foreach (Troop t in p.Troops.GetTroopsOf(Player))
                        keys[(p, t.Name)] = true;
            foreach (Ship s in Player.OwnedShips)
                if (s.TroopCount > 0)
                    foreach (Troop t in s.GetOurTroops())
                        keys[(s, t.Name)] = true;
            return keys.Count;
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
            StrengthTotal = 0f;
            foreach (TroopListScreenItem item in TroopSL.AllEntries)
            {
                NumTroops += item.Count;
                StrengthTotal += item.Strength;
            }
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
            // the canonical fill rect - ClientArea stops short of the frame border and let the
            // map bleed through the rim (maintainer bench, Economy)
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            if (TroopSL.NumEntries > 0)
            {
                TroopListScreenItem e1 = TroopSL.ItemAtTop;
                // charte (Lek): a header is never bigger than the body - weight, not size
                Graphics.Font font = Fonts.Arial12Bold;

                DrawHeader(batch, font, e1.SysNameRect, "System");
                DrawHeader(batch, font, e1.LocationRect, "Location");
                DrawHeader(batch, font, e1.StatusRect, "Status");
                DrawHeader(batch, font, e1.TroopRect, "Troop");
                // the numeric pair centres on its VALUE lane, whose numbers end 16px short
                DrawHeader(batch, font, e1.NumRect, "Num", rightInset: 16);
                // Ludoal fork: the word alone. This header carried "Strength" AND a fist icon
                // saying the same thing, and it was the only column of the six to do so — the
                // text stays because the other five are text (maintainer feedback).
                DrawHeader(batch, font, e1.StrRect, "Strength", rightInset: 16);

                Color lineColor = new Color(118, 102, 67, 255);
                float footY = TroopSL.ItemsHousing.Bottom + 6;
                // the Economy grammar (maintainer bench 285): separators BETWEEN columns only -
                // the frame closes the extremities - running top to bottom through the footer,
                // and no horizontal rules at all
                float columnTop = ERect.Y - font.LineSpacing - 2;
                float columnBot = ERect.Bottom - 15;
                foreach (int colX in new[] { e1.LocationRect.X, e1.StatusRect.X, e1.TroopRect.X,
                                             e1.NumRect.X, e1.StrRect.X })
                    batch.DrawLine(new Vector2(colX, columnTop), new Vector2(colX, columnBot), lineColor);

                // TOTAL footer (maintainer bench): the troop total moves to the table's foot,
                // each sum under the column it closes - the Economy pattern
                batch.DrawString(font, "TOTAL", new Vector2(ERect.X + 8, footY), Color.Wheat);
                string num = NumTroops.ToString();
                batch.DrawString(font, num, new Vector2(e1.NumRect.Right - 16 - font.TextWidth(num), footY), Colors.Cream);
                string str = ((int)StrengthTotal).ToString();
                batch.DrawString(font, str, new Vector2(e1.StrRect.Right - 16 - font.TextWidth(str), footY), Colors.Cream);
            }
            else
            {
                var msgPos = new Vector2(ERect.X + 30, ERect.Y + 30);
                batch.DrawString(Fonts.Arial12Bold, "No troops anywhere — recruit some before the neighbours visit.",
                                 msgPos, Color.Gray);
            }
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        void DrawHeader(SpriteBatch batch, Graphics.Font font, Rectangle rect, string text, int rightInset = 0)
        {
            var pos = new Vector2(rect.X + (rect.Width - rightInset) / 2 - font.MeasureString(text).X / 2f,
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
        public float Strength { get; private set; }

        // fixed pixel columns (the column doctrine): free text gets the room a name
        // needs, a datum gets a number's room - and the frame is sized FROM the sum,
        // so there is no gutter to feed anymore
        public const int SysW = 150, LocW = 290, StatusW = 90, TroopW = 240, NumW = 70, StrW = 90;
        public const int TableW = SysW + LocW + StatusW + TroopW + NumW + StrW;

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
            int h = (int)Height;
            RemoveAll();

            int nextX = x;
            Rectangle NextRect(int width)
            {
                int next = nextX;
                nextX += width;
                return new Rectangle(next, y, width, h);
            }

            SysNameRect  = NextRect(SysW);
            LocationRect = NextRect(LocW);
            StatusRect   = NextRect(StatusW);
            TroopRect    = NextRect(TroopW);
            NumRect      = NextRect(NumW);
            StrRect      = NextRect(StrW);

            // charte (maintainer bench, the Economy pattern): names read from the left,
            // numbers close right on a shared edge; the status stays a centred tag with
            // its colour - a state, not a quantity
            AddLeft(SysNameRect, SystemName, Colors.Cream);
            AddLeft(LocationRect, Location, Colors.Cream);
            AddCentered(StatusRect, Status, StatusColor);
            AddLeft(TroopRect, TroopName, Colors.Cream);
            AddRight(NumRect, Count.ToString(), Colors.Cream);
            AddRight(StrRect, ((int)Strength).ToString(), Colors.Cream);
            base.PerformLayout();
        }

        Graphics.Font FitFont(string text, int room) =>
            Fonts.Arial12Bold.MeasureString(text).X <= room ? Fonts.Arial12Bold : Fonts.Arial8Bold;

        void AddLeft(Rectangle rect, string text, Color color)
        {
            Graphics.Font font = FitFont(text, rect.Width - 12);
            Label(new Vector2(rect.X + 8, rect.Y + rect.Height / 2 - font.LineSpacing / 2), text, font, color);
        }

        void AddRight(Rectangle rect, string text, Color color)
        {
            // -16: one character of air right of the number, the Economy lane
            Graphics.Font font = Fonts.Arial12;
            Label(new Vector2(rect.Right - 16 - font.MeasureString(text).X,
                              rect.Y + rect.Height / 2 - font.LineSpacing / 2), text, font, color);
        }

        void AddCentered(Rectangle rect, string text, Color color)
        {
            Graphics.Font font = FitFont(text, rect.Width);
            var pos = new Vector2(rect.X + rect.Width / 2 - font.MeasureString(text).X / 2f,
                                  rect.Y + rect.Height / 2 - font.LineSpacing / 2);
            Label(pos, text, font, color);
        }
    }
}
