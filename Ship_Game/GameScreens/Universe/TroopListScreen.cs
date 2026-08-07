using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Ship_Game.Universe;
using Ship_Game.UI; // UITable: the shared table charte
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
        public readonly UITable Table; // the shared table charte owns the geometry
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

            // Ludoal fork: the Troops tab of the Empire group, content-sized on the shared
            // table charte (UITable): the text columns SIZE THEMSELVES on the data they are
            // about to show, the troop-group count sets the height - this page is allowed
            // UNDER the 900p floor when the roster is short.
            Table = new UITable(new[]
            {
                new UITable.Column { Title = "System" },
                new UITable.Column { Title = "Location" },
                new UITable.Column { Title = "Status",   Align = TableAlign.Center, Sorted = true, Ascending = true },
                new UITable.Column { Title = "Troop", Foldable = true }, // repli si la table dépasse 1680 (rarissime)
                new UITable.Column { Title = "#",        Width = 60, Align = TableAlign.Number },
                // the offense icon with its tooltip in place of the word (bench 305)
                new UITable.Column { Icon = ResourceManager.Texture("UI/icon_offense"), Width = 80,
                                     Align = TableAlign.Number, Tip = Localizer.Token(GameText.Strength) },
            });
            int rows = CountTroopGroups(out Array<string> systems, out Array<string> locations, out Array<string> troops);
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, systems);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial12Bold, locations);
            UITable.AutoSize(Table.Columns[2], Fonts.Arial12Bold, Statuses);
            UITable.AutoSize(Table.Columns[3], Fonts.Arial12Bold, troops);
            // Ludoal fork (maintainer feedback): the table caps at 1680 like the rest of the group -
            // the Troop column folds if it ever overflows (in practice it never will).
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);
            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // bench 343: capped at 1080p
            // 118 = tab strip + the filter/info lane + headers + a line at the bottom
            float contentH = UITable.ContentHeightFor(119, Math.Max(3, rows), 28, fullAvail);
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.EmpireTabTitles, 2,
                                                    OnEmpireTabChanged, Table.ContentWidth, contentH);
            RectF client = EmpireTabs.ClientArea;
            // one lane: the filter, then the two figures on the same line (maintainer bench 288)
            Table.RowPitch = 28;
            Table.Layout(client, client.Y + 30, client.Bottom - 5);
            TroopSL = Add(new ScrollList<TroopListScreenItem>(Table.ListRect, 24));
            TroopSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(TroopSL);
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
        // BEFORE any UI exists - gathering on the way the names the text columns will
        // show, so they can size themselves on the data. Unfiltered on purpose: the
        // frame keeps one size for the screen's life, a filter just shortens the list.
        int CountTroopGroups(out Array<string> systems, out Array<string> locations, out Array<string> troops)
        {
            var keys = new Map<(object, string), bool>();
            var sys = new Array<string>(); var locs = new Array<string>(); var names = new Array<string>();
            void Add(object location, string sysName, string locName, Troop t)
            {
                keys[(location, t.Name)] = true;
                sys.Add(sysName); locs.Add(locName); names.Add(t.Name);
            }
            foreach (SolarSystem system in Universe.UState.Systems)
                foreach (Planet p in system.PlanetList)
                    foreach (Troop t in p.Troops.GetTroopsOf(Player))
                        Add(p, system.Name, p.Name, t);
            foreach (Ship s in Player.OwnedShips)
                if (s.TroopCount > 0)
                    foreach (Troop t in s.GetOurTroops())
                        Add(s, s.System?.Name ?? "Deep Space", s.Name, t);
            systems = sys; locations = locs; troops = names;
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
                {
                    Universe.ReturnToListScreen = () => Universe.ScreenManager.AddScreen(new TroopListScreen(Universe, EmpireUI));
                    Universe.ReturnToListTabs   = EmpireTabs; // the dimmed silhouette behind the colony
                    Universe.ReturnToListGroup  = ScreenGroups.GroupOf(this); // keep the group button lit (maintainer)
                }
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
                    groups.Add(key, new TroopListScreenItem(Table, sysName, locName, status, statusColor, t, p, s));
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

            // the standing sort: Status, Deployed first (maintainer bench 305) - the
            // fights you are IN outrank the garrisons at home
            int Rank(string st) => st == "Deployed" ? 0 : st == "Garrison" ? 1
                                 : st == "Transport" ? 2 : 3;
            foreach (TroopListScreenItem item in groups.Values.OrderBy(v => Rank(v.StatusText)))
                TroopSL.AddItem(item);

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
            // the canonical fill rect - ClientArea stops short of the frame border and let the
            // map bleed through the rim (maintainer bench, Economy)
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            // the two figures ride the FILTER line (maintainer bench 288): labels vanilla,
            // the count white, the food bill in pink - troops eat Troop.Consumption each
            Graphics.Font font = Fonts.Arial12Bold;
            RectF client = EmpireTabs.ClientArea;
            float infoX = client.X + 190; // right of the filter dropdown
            float infoY = client.Y + 8;
            string totalLbl = "Total Troops: ";
            batch.DrawString(font, totalLbl, new Vector2(infoX, infoY), UITable.Vanilla);
            infoX += font.TextWidth(totalLbl);
            string totalVal = NumTroops.ToString();
            batch.DrawString(font, totalVal, new Vector2(infoX, infoY), Color.White);
            infoX += font.TextWidth(totalVal) + 24;
            string foodLbl = "Food: ";
            batch.DrawString(font, foodLbl, new Vector2(infoX, infoY), UITable.Vanilla);
            batch.DrawString(font, $"-{(NumTroops * Troop.Consumption).String(1)}",
                             new Vector2(infoX + font.TextWidth(foodLbl), infoY), Color.LightPink);

            // maintainer bench 336: with no troops there is nothing to tabulate - skip the empty
            // table chrome (headers, column rules) and show only the note.
            if (TroopSL.NumEntries == 0)
            {
                var msgPos = new Vector2(Table.TableRect.X + 30, Table.TableRect.Y + 30);
                batch.DrawString(font, "No troops anywhere. Recruit some before the neighbours visit.",
                                 msgPos, Color.Gray);
            }
            else
            {
                Table.DrawChrome(batch);
            }
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
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
        readonly string SystemName;
        readonly string Location;
        readonly string Status;
        public string StatusText => Status; // the screen's standing sort reads it
        readonly Color StatusColor;
        readonly string TroopName;
        public readonly Planet Planet;   // set for garrison/deployed rows
        public readonly Ship Ship;       // set for transport/stationed rows
        public int Count { get; private set; }
        public float Strength { get; private set; }

        // the shared table charte owns the columns - the row only knows its data
        readonly UITable Table;

        public TroopListScreenItem(UITable table, string systemName, string location, string status,
                                   Color statusColor, Troop troop, Planet planet, Ship ship)
        {
            Table = table;
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
            RemoveAll();

            // cells read the shared column geometry; the row only supplies its Y band
            Cell(0, SystemName, Colors.Cream);
            Cell(1, Location, Colors.Cream);
            Cell(2, Status, StatusColor);
            Cell(3, TroopName, Colors.Cream);
            // numeric colours through the shared charte: every zero reads gray
            Cell(4, Count.ToString(), UITable.ValueColor(TableColor.Plain, Count));
            Cell(5, ((int)Strength).ToString(), UITable.ValueColor(TableColor.Plain, Strength));
            base.PerformLayout();
        }

        void Cell(int col, string text, Color color)
        {
            UITable.Column c = Table.Columns[col];
            Graphics.Font font = c.Align == TableAlign.Number ? Fonts.Arial12
                : Fonts.Arial12Bold.MeasureString(text).X <= c.Width - 2 * UITable.PadX
                    ? Fonts.Arial12Bold : Fonts.Arial8Bold;
            Label(UITable.CellPos(font, c.Rect, Y, Height, text, c.Align), text, font, color);
        }
    }
}
