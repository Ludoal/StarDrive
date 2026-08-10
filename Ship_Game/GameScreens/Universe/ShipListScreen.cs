using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;
using Ship_Game.Universe.SolarBodies; // DistanceDisplay
using Ship_Game.UI; // UITable: the shared table charte

namespace Ship_Game
{
    public sealed class ShipListScreen : GameScreen
    {
        public readonly UniverseScreen Universe;
        public UniverseState UState => Universe.UState;
        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row, this screen being one tab
        // Ludoal fork (bench 387): this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;
        private Ship SelectedShip;
        private readonly ScrollList<ShipListScreenItem> ShipSL;
        public EmpireUIOverlay EmpireUi;
        private readonly DropOptions<int> ShowRoles;
        public readonly UITable Table; // the shared table charte owns geometry, headers and rules

        private bool PlayerDesignsOnly
        {
            get => UState.P.ShipListFilterPlayerShipsOnly;
            set => UState.P.ShipListFilterPlayerShipsOnly = value;
        }
        private bool InFleetsOnly
        {
            get => UState.P.ShipListFilterInFleetsOnly;
            set
            {
                UState.P.ShipListFilterInFleetsOnly = value;
                if (UState.P.ShipListFilterInFleetsOnly && UState.P.ShipListFilterNotInFleets)
                    UState.P.ShipListFilterNotInFleets = false;
            }

        }

        private bool NotInFleets
        {
            get => UState.P.ShipListFilterNotInFleets;
            set
            {
                UState.P.ShipListFilterNotInFleets = value;
                if (UState.P.ShipListFilterNotInFleets && UState.P.ShipListFilterInFleetsOnly)
                    UState.P.ShipListFilterInFleetsOnly = false;
            }
        }

        private static int IndexLast;

        public ShipListScreen(UniverseScreen parent, EmpireUIOverlay empUi, string audioCue = "")
            : base(parent, toPause: parent)
        {
            Universe = parent;
            if (!string.IsNullOrEmpty(audioCue))
                GameAudio.PlaySfxAsync(audioCue);

            EmpireUi = empUi;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;
            // Ludoal fork: the Ships tab of the Empire group on the shared table charte
            // (UITable, spec 4 Aug): fixed columns except Orders, which takes what the
            // screen offers within bounds - so the frame HUGS the table and stops after
            // the slider lane. Height follows the unfiltered fleet count.
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.System), Width = 110, Sortable = true },
                // distance to the nearest colony - live for every ship, Deep Space included
                new UITable.Column { Title = Localizer.Token(GameText.Proximity), Align = TableAlign.Center, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Ship),   Width = 240, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Role),   Width = 80,  Align = TableAlign.Center, Sortable = true },
                new UITable.Column { Title = "Fleet",  Width = 110, Sortable = true },
                new UITable.Column { Title = "Patrol", Sortable = true }, // the fleet's patrol plan, if any
                new UITable.Column { Title = Localizer.Token(GameText.Orders), Sortable = true },
                new UITable.Column { Width = 110, Align = TableAlign.Center }, // the order/refit/scrap icon lane
                new UITable.Column { Icon = ResourceManager.Texture("UI/icon_fighting_small"), Width = 60,
                                     Align = TableAlign.Number, Sortable = true, Tip = "Indicates Ship Strength; sortable" },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_money"), Width = 60,
                                     Align = TableAlign.Number, Sortable = true, Tip = "Maintenance Cost of Ship; sortable" },
                new UITable.Column { Icon = ResourceManager.Texture("UI/icon_troop"), Width = 60,
                                     Align = TableAlign.Number, Sortable = true, Tip = "Indicates Troops on board, friendly or hostile; sortable" },
                new UITable.Column { Title = "FTL", Width = 60, Align = TableAlign.Number, Sortable = true, Tip = "Faster Than Light Speed of Ship" },
                new UITable.Column { Title = "STL", Width = 60, Align = TableAlign.Number, Sortable = true, Tip = "Sublight Speed of Ship" },
            });
            // EVERY column sizes itself on the fleet's DATA, header (or icon) included
            // (maintainer, 4 Aug); Orders is FOLDABLE - if the natural widths exceed the
            // resolution, its text cuts to a tooltip instead of pushing the frame off-screen
            var vals = new Array<string>[13];
            for (int i = 0; i < vals.Length; ++i)
                vals[i] = new Array<string>();
            foreach (Ship s in Universe.Player.OwnedShips)
            {
                vals[0].Add(s.System?.Name ?? Localizer.Token(GameText.DeepSpace));
                vals[1].Add(new DistanceDisplay(DistanceToNearestColony(s.Position) / 1000).Text);
                vals[2].Add(s.ShipName);
                vals[3].Add(Localizer.GetRole(s.ShipData.Role, s.Loyalty));
                vals[4].Add(s.Fleet?.Name ?? "");
                vals[5].Add(s.Fleet?.Patrol?.Name ?? "");
                vals[6].Add(ShipListScreenItem.GetStatusText(s));
                vals[8].Add(s.GetStrength().ToString("0"));
                vals[9].Add(s.GetMaintCost().ToString("F2"));
                vals[10].Add(string.Concat(s.TroopCount, "/", s.TroopCapacity));
                vals[11].Add((s.MaxFTLSpeed / 1000f).ToString("0") + "k");
                vals[12].Add(s.MaxSTLSpeed.ToString("0"));
            }
            // measure with the font each column actually DRAWS: sizing Orders in bold left
            // a bold-vs-regular slack after its longest text (maintainer bench 290)
            for (int i = 0; i < vals.Length; ++i)
                if (i != 7) // the icon lane keeps its fixed width
                    UITable.AutoSize(Table.Columns[i], i <= 5 ? Fonts.Arial12Bold : Fonts.Arial12, vals[i]);
            Table.Columns[2].Width += 34; // the ship icon rides ahead of the name
            // capped (maintainer bench 305): a fleet's longest name was sizing the lane for
            // everyone, and every pixel it hoards is a pixel Orders has to fold away
            Table.Columns[2].Width = Math.Min(Table.Columns[2].Width, 210);
            Table.Columns[6].Foldable = true;
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);

            // Proximity ascending is the factory default; the standing sort survives the
            // screen for the session (maintainer bench 307) and ResetList re-applies it
            Table.Columns[StandingCol].Sorted = true;
            Table.Columns[StandingCol].Ascending = StandingAsc;

            int shipRows = Universe.Player.OwnedShips.Count;
            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // bench 389: the one floor (cartouche + order rows)
            // 119, measured: frame->client 41, filter line + headers 43, foot 5, paddings 30
            float contentH = UITable.ContentHeightFor(119, Math.Max(5, shipRows), 34, fullAvail);
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), 1,
                                                    OnEmpireTabChanged, Table.ContentWidth, contentH);

            // Ludoal fork: the reserved first line carries the three filters and the role dropdown,
            // side by side where they used to be stacked beside the title. The table takes the rest.
            RectF client = EmpireTabs.ClientArea;
            Table.RowPitch = 34;
            Table.Layout(client, client.Y + 30, client.Bottom - 5);

            ShipSL = Add(new ScrollList<ShipListScreenItem>(Table.ListRect, 30));
            ShipSL.OnDoubleClick = OnShipListScreenItemClicked;
            ShipSL.OnClick = OnShipRowSingleClicked; // bench 388 (maintainer): single-click = select on the map and pan at current zoom
            ShipSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(ShipSL);

            float lineY = client.Y + 6;
            Add(new UICheckBox(client.X + 10, lineY,
                () => PlayerDesignsOnly,
                (x) => {
                    PlayerDesignsOnly = x;
                    ResetList(ShowRoles.ActiveValue);
                }, Fonts.Arial12Bold, title: GameText.PlayerDesignsOnly, tooltip: GameText.ShowPlayerDesignsOnly));

            Add(new UICheckBox(client.X + 170, lineY,
                () => InFleetsOnly,
                (x) => {
                    InFleetsOnly = x;
                    ResetList(ShowRoles.ActiveValue);
                }, Fonts.Arial12Bold, title: GameText.InFleetsOnly, tooltip: GameText.ShowOnlyShipsWhichAre));

            Add(new UICheckBox(client.X + 300, lineY,
                () => NotInFleets,
                (x) => {
                    NotInFleets = x;
                    ResetList(ShowRoles.ActiveValue);
                }, Fonts.Arial12Bold, title: GameText.NotInFleets, tooltip: GameText.ShowOnlyShipsWhichAre2));

            ShowRoles = new DropOptions<int>(new Rectangle((int)client.X + 440, (int)lineY, 175, 18));
            ShowRoles.AddOption("All Ships", 1);
            ShowRoles.AddOption("Fighters", 2);
            ShowRoles.AddOption("Corvettes", 3);
            ShowRoles.AddOption("Frigates", 4);
            ShowRoles.AddOption("Cruisers", 5);
            ShowRoles.AddOption("Battleships", 6);
            ShowRoles.AddOption("Titans", 7);
            ShowRoles.AddOption("Carriers", 8);
            ShowRoles.AddOption("Bombers", 9);
            ShowRoles.AddOption("Military Ships", 14);
            ShowRoles.AddOption("Troopships", 10);
            ShowRoles.AddOption("Support Ships", 11);
            ShowRoles.AddOption("All Structures", 12);
            ShowRoles.AddOption("Civilian", 13);

            ShowRoles.ActiveIndex = IndexLast;  //fbedard: remember last filter
            ResetList(ShowRoles.ActiveValue);
        }


        // Ludoal fork: the other tabs live in their own screen, so leaving this one hands over to
        // it. Its own index is a no-op: we are already here.
        void OnEmpireTabChanged(int index)
        {
            // one factory for the whole group (ScreenGroups) - this screen only says which tab it is
            ScreenGroups.SwitchEmpireTab(index, self: 1, Universe, this);
        }
        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first - as a Submenu background it would be
            // drawn among the children, after everything below it.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);

            base.Draw(batch, elapsed);

            // the shared charte draws the headers, the rule and the column separators
            Table.DrawChrome(batch);
            ShowRoles.Draw(batch, elapsed);
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            EmpireUi.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        // bench 388 (maintainer): single-click = select on the map and pan at current zoom -
        // the double-click still exits and chases the ship
        void OnShipRowSingleClicked(ShipListScreenItem item)
        {
            Universe.PanToShipKeepZoom(item.Ship);
        }

        void OnShipListScreenItemClicked(ShipListScreenItem item)
        {
            ExitScreen();
            UniverseScreen u = Universe;
            u.ViewToShip(item.Ship);
            u.returnToShip = true;
        }

        public override bool HandleInput(InputState input)
        {
            if (!IsActive)
                return false;

            if (EmpireUi.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (ShowRoles.HandleInput(input))
                return true;

            if (ShowRoles.ActiveIndex != IndexLast)
            {
                ResetList(ShowRoles.ActiveValue);
                IndexLast = ShowRoles.ActiveIndex;
                return true;
            }

            if (base.HandleInput(input))
                return true;

            if (HandleShipListSortButtonClick(input))
                return true;

            if (input.KeyPressed(Keys.K) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                ResetUniverseShipSelectionMessy(Universe);
                return true;
            }

            if (input.Escaped || input.RightMouseClick)
            {
                ExitScreen();
                ResetUniverseShipSelectionMessy(Universe);
                return true;
            }

            return false;
        }

        // distance to the player's nearest colony - live for every ship, Deep Space
        // included: a ship always has galactic coordinates, a system or not
        public float DistanceToNearestColony(Vector2 pos)
        {
            var planets = Universe.Player.GetPlanets();
            float best = float.MaxValue;
            for (int i = 0; i < planets.Count; ++i)
                best = Math.Min(best, planets[i].Position.Distance(pos));
            return best == float.MaxValue ? 0f : best;
        }

        // header clicks come from the shared table charte; this maps a column to its sort
        bool HandleShipListSortButtonClick(InputState input)
        {
            int col = Table.HandleInput(input);
            if (col < 0)
                return false;

            bool asc = Table.SetSorted(col);
            StandingCol = col;
            StandingAsc = asc;
            GameAudio.AcceptClick();
            return ApplySort(col, asc);
        }

        static int StandingCol = 1;    // session-persistent (bench 307)
        static bool StandingAsc = true;

        bool ApplySort(int col, bool asc)
        {
            void Sort<T>(Func<ShipListScreenItem, T> key)
            {
                if (asc) ShipSL.Sort(key);
                else     ShipSL.SortDescending(key);
            }

            switch (col)
            {
                case 0:  Sort(sl => sl.Ship.SystemName); break;
                case 1:  Sort(sl => DistanceToNearestColony(sl.Ship.Position)); break;
                case 2:  Sort(sl => sl.Ship.VanityName); break;
                case 3:  Sort(sl => sl.Ship.ShipData.Role); break;
                case 4:  Sort(sl => sl.Ship.Fleet?.Name ?? "None"); break;
                case 5:  Sort(sl => sl.Ship.Fleet?.Patrol?.Name ?? ""); break;
                case 6:  Sort(sl => ShipListScreenItem.GetStatusText(sl.Ship)); break;
                case 8:  Sort(sl => sl.Ship.GetStrength()); break;
                case 9:  Sort(sl => sl.Ship.GetMaintCost()); break;
                case 10: Sort(sl => sl.Ship.TroopCount); break;
                case 11: Sort(sl => sl.Ship.MaxFTLSpeed); break;
                case 12: Sort(sl => sl.Ship.MaxSTLSpeed); break;
                default: return false;
            }
            return true;
        }

        void ResetUniverseShipSelectionMessy(UniverseScreen u)
        {
            if (SelectedShip != null)
            {
                Array<Ship> selected = new();
                foreach (ShipListScreenItem sel in ShipSL.AllEntries)
                    if (sel.Selected) selected.AddUnique(sel.Ship);
                u.SetSelectedShipList(selected, fleet: null);
            }
        }

        public void ResetList(int category)
        {
            ShipSL.Reset();
            IReadOnlyList<Ship> ships = Universe.Player.OwnedShips;
            if (ships.Count <= 0)
                return;

            bool ShouldAddForCategory(Ship ship, int forCategory)
            {
                if (ship.IsHangarShip
                    || ship.IsHomeDefense
                    || (PlayerDesignsOnly && !ship.ShipData.IsPlayerDesign)
                    || (InFleetsOnly && ship.Fleet == null)
                    || (NotInFleets && ship.Fleet != null))
                {
                    return false;
                }

                switch (forCategory)
                {
                    case 1:  return ship.DesignRole > RoleName.station;
                    case 2:  return ship.DesignRole == RoleName.fighter || ship.DesignRole == RoleName.scout;
                    case 3:  return ship.DesignRole == RoleName.corvette || ship.DesignRole == RoleName.gunboat;
                    case 4:  return ship.DesignRole == RoleName.frigate || ship.DesignRole == RoleName.destroyer;
                    case 5:  return ship.DesignRole == RoleName.cruiser;
                    case 6:  return ship.DesignRole == RoleName.battleship;
                    case 7:  return ship.DesignRole == RoleName.capital;
                    case 8:  return ship.DesignRole == RoleName.carrier;
                    case 9:  return ship.DesignRole == RoleName.bomber;
                    case 10: return ship.DesignRole == RoleName.troopShip || ship.DesignRole == RoleName.troop;
                    case 11: return ship.DesignRole == RoleName.support;
                    case 12: return ship.DesignRole <= RoleName.platform || ship.DesignRole == RoleName.station;
                    case 13: return ship.IsConstructor || ship.DesignRole == RoleName.freighter || ship.ShipData.ShipCategory == ShipCategory.Civilian;
                    // #348: everything from troopShip upward in RoleName is the military family
                    // (troopShip, support, bomber, carrier, fighter, scout, gunboat, drone,
                    // corvette..capital) — no way to list only military ships before this.
                    case 14: return ship.DesignRole >= RoleName.troopShip;
                }

                return false;
            }

            foreach (Ship ship in ships)
            {
                if (ShouldAddForCategory(ship, category))
                {
                    ShipSL.AddItem(new ShipListScreenItem(ship, this));
                }
            }

            // the orange header stays truthful (bench 293: it announced a sort the list
            // never had): re-apply the standing sort after every refill
            for (int i = 0; i < Table.Columns.Length; ++i)
            {
                if (Table.Columns[i].Sorted)
                {
                    ApplySort(i, Table.Columns[i].Ascending);
                    break;
                }
            }

            SelectedShip = null;
        }

        public void ResetStatus()
        {
            foreach (ShipListScreenItem sel in ShipSL.AllEntries)
                sel.StatusText = ShipListScreenItem.GetStatusText(sel.Ship);
        }

    }
}
