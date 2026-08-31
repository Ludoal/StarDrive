using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics.Input;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
using Ship_Game.UI;          // UITable: the shared table charte
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (maintainer feedback): the Trade tab of the Galaxy group - the empire's trade
    // zones, each a named list of colonies its freighters serve as a set. The zone is the object;
    // this page is where it is made, named and unmade.
    public sealed class TradeZonesScreen : GameScreen
    {
        Submenu GalaxyTabs; // the Galaxy group's tab row, this screen being one tab
        public override Rectangle PageFrame => GalaxyTabs?.Rect ?? base.PageFrame;

        public UniverseScreen Universe;
        public UniverseState UState => Universe.UState;
        public readonly Empire Player;

        ScrollList<TradeZonesScreenListItem> ZonesSL; // rebuilt with the page, so not readonly
        public readonly UITable Table;

        static int LastSortCol = -1;   // session-persistent, like the other Galaxy tables
        static bool LastSortAsc = true;
        RectF Client; // the tab frame's content area, kept so the table can be laid out again

        // the New button sits under the table: a page that lists things must be able to make one
        const float ActionsLineH = 34f;

        public TradeZonesScreen(UniverseScreen parent, Empire player)
            : base(parent, toPause: parent)
        {
            Universe = parent;
            Player = player;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;

            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.TzZoneName), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.TzNumColonies), Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.TzServedColonies), Foldable = true },
                // the instrument, in reading order: what it could use, what you granted, what is
                // actually on its way - a zone asking more than it receives shows at a glance
                new UITable.Column { Title = Localizer.Token(GameText.TzRequired), Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.TzAssigned), Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.TzActive), Align = TableAlign.Number, Sortable = true },
                // and what it OWNS, which only a strict zone does - the requisition made visible
                new UITable.Column { Title = Localizer.Token(GameText.Freighters), Align = TableAlign.Number, Sortable = true },
                // four icons now: the two arrows that order the zones, then edit and delete
                new UITable.Column { Title = "", Width = 110, Align = TableAlign.Center },
            });

            Build();
        }

        // Ludoal fork (bench 544): the whole page is rebuilt when its content changes, frame
        // included. Measuring the columns again was not enough: the tab frame is sized from the
        // table's width at build time, so a wider table spilled past a frame that never grew -
        // only reopening the page fixed it. This page MAKES its own rows, so it must be able to
        // rebuild itself the way reopening does.
        void Build()
        {
            RemoveAll();
            MeasureColumns();

            // no column is sorted by default here, unlike the other tables: the LIST order is
            // the priority the player arranged, so it is the meaningful default. Sorting a
            // column is a way to LOOK at the zones; it drops the moment one is moved by hand.
            if (LastSortCol >= 0)
            {
                Table.Columns[LastSortCol].Sorted = true;
                Table.Columns[LastSortCol].Ascending = LastSortAsc;
            }

            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight);
            float contentH = UITable.ContentHeightFor(99, Math.Max(3, Player.TradeZones.Count), 38, fullAvail) + ActionsLineH;
            GalaxyTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Galaxy, Universe), ScreenGroups.TabIndexOf(this),
                                                   OnGalaxyTabChanged, Table.ContentWidth, contentH);
            Client = GalaxyTabs.ClientArea;
            Table.RowPitch = 38;
            // the table stops one line short: that line belongs to the New button, and it is
            // reserved from the frame rather than taken out of what happens to be left
            Table.Layout(Client, Client.Y + 10, Client.Bottom - 5 - ActionsLineH);

            ZonesSL = Add(new ScrollList<TradeZonesScreenListItem>(Table.ListRect, 34));
            ZonesSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(ZonesSL);

            var newZone = Button(ButtonStyle.Default, Table.TableRect.X, Client.Bottom - ActionsLineH,
                                 GameText.TzNewZone, click: _ => NewZone());
            newZone.SetAbsSize((int)Fonts.Arial12Bold.TextWidth(Localizer.Token(GameText.TzNewZone)) + 34, 26);
            newZone.Tooltip = GameText.TzNewZoneTip;

            FillList();
        }

        public string ColonyNames(TradeZone zone)
        {
            var names = new Array<string>();
            foreach (int id in zone.Colonies)
            {
                Planet p = UState.GetPlanet(id);
                if (p != null)
                    names.Add(p.Name);
            }
            return string.Join(", ", names);
        }

        void NewZone()
        {
            GameAudio.AcceptClick();
            // a zone is born from the colonies it serves: an empty one would read as "everywhere"
            ScreenManager.AddScreen(new TradeZoneColoniesScreen(this, null));
        }

        // centring the map on a colony without leaving the page: the same single-click pan the
        // patrol list uses, at the zoom the player already chose.
        public void PanTo(Planet colony)
        {
            GameAudio.AcceptClick();
            Universe.PanToKeepZoom(colony.Position);
        }

        // the order is the priority, so moving a zone is a game decision - it goes through
        // the empire, and the page rebuilds to show the new order
        public void MoveZone(TradeZone zone, bool up)
        {
            GameAudio.AcceptClick();
            Player.MoveTradeZone(zone, up);
            LastSortCol = -1; // a hand-made order outranks a sorted column
            ResetList();
        }

        public void EditColonies(TradeZone zone)
        {
            GameAudio.AcceptClick();
            ScreenManager.AddScreen(new TradeZoneColoniesScreen(this, zone));
        }

        // Called back by the picker. A zone with no colony is never kept.
        public void ApplyColonies(TradeZone zone, Array<Planet> chosen, int quota, string name,
                                  bool strict, CargoPriority priority)
        {
            if (chosen.IsEmpty)
            {
                if (zone != null)
                    DeleteZone(zone);
                return;
            }

            if (zone == null)
                zone = Player.AddTradeZone(chosen[0]);

            zone.Colonies.Clear();
            foreach (Planet p in chosen)
                zone.Add(p);

            zone.Quota = quota;
            zone.Strict = strict;
            zone.Priority = priority;
            // an empty box keeps the name the zone already had, rather than leaving it nameless
            if (name.NotEmpty() && name != zone.Name)
                zone.ChangeName(name);

            GameAudio.EchoAffirmative();
            ResetList();
        }

        public void DeleteZone(TradeZone zone)
        {
            Player.RemoveTradeZone(zone);
            GameAudio.EchoAffirmative();
            ResetList();
        }

        void OnGalaxyTabChanged(int index)
            => ScreenGroups.SwitchGalaxyTab(index, self: ScreenGroups.TabIndexOf(this), Universe, this);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GalaxyTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            if (ZonesSL.NumEntries > 0)
                Table.DrawChrome(batch);
            else
            {
                string hint = Localizer.Token(GameText.TzEmptyHint);
                Graphics.Font font = Fonts.Arial12Bold;
                var pos = new Vector2(Table.TableRect.X + (Table.TableRect.Width - font.TextWidth(hint)) / 2f,
                                      Table.ListRect.Y + 40);
                batch.DrawString(font, hint, pos.Rounded(), Color.Gray);
            }

            ScreenGroups.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // live top bar on every full-screen panel
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this))
                return true;

            int clicked = Table.HandleInput(input);
            if (clicked >= 0)
            {
                GameAudio.BlipClick();
                bool asc = Table.SetSorted(clicked);
                LastSortCol = clicked;
                LastSortAsc = asc;
                Refill(clicked, asc);
                return true;
            }
            return base.HandleInput(input);
        }

        void Refill(int col, bool ascending)
        {
            ZonesSL.Reset();
            TradeZone[] zones;
            switch (col)
            {
                case 1:  zones = Player.TradeZones.Sorted(ascending, z => z.NumColonies); break;
                case 3:  zones = Player.TradeZones.Sorted(ascending, z => z.RequiredFreighters(Player)); break;
                case 4:  zones = Player.TradeZones.Sorted(ascending, z => z.Quota); break;
                case 5:  zones = Player.TradeZones.Sorted(ascending, z => z.ActiveFreighters(Player)); break;
                case 6:  zones = Player.TradeZones.Sorted(ascending, z => z.MemberFreighters(Player).Count); break;
                default: zones = Player.TradeZones.Sorted(ascending, z => z.Name); break;
            }
            foreach (TradeZone zone in zones)
                ZonesSL.AddItem(new TradeZonesScreenListItem(this, zone, Player));
        }

        // Ludoal fork (bench 542): unlike the other Galaxy tables, this page CREATES its own
        // rows - so the columns, measured on an empty list at construction, have to be
        // measured again when a zone appears. Without this the first zone lands in columns
        // sized for nothing, and only reopening the page fixed it.
        void MeasureColumns()
        {
            var names = new Array<string>(); var counts = new Array<string>();
            var served = new Array<string>(); var quotas = new Array<string>();
            var required = new Array<string>(); var active = new Array<string>();
            var owned = new Array<string>();
            foreach (TradeZone z in Player.TradeZones)
            {
                names.Add(z.Name);
                counts.Add(z.NumColonies.ToString());
                served.Add(ColonyNames(z));
                required.Add(z.RequiredFreighters(Player).ToString());
                quotas.Add(z.Quota <= 0 ? Localizer.Token(GameText.PolFreighterRefitAuto) : z.Quota.ToString());
                active.Add(z.ActiveFreighters(Player).ToString());
                owned.Add(z.MemberFreighters(Player).Count.ToString());
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, names);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial12Bold, counts);
            UITable.AutoSize(Table.Columns[2], Fonts.Arial12Bold, served);
            UITable.AutoSize(Table.Columns[3], Fonts.Arial12Bold, required);
            UITable.AutoSize(Table.Columns[4], Fonts.Arial12Bold, quotas);
            UITable.AutoSize(Table.Columns[5], Fonts.Arial12Bold, active);
            UITable.AutoSize(Table.Columns[6], Fonts.Arial12Bold, owned);
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);
        }

        bool NeedsRebuild;

        // asked for from a button's own click handler, so the rebuild waits for the next update:
        // tearing the tree down while an item is still handling its press is how a click ends in
        // a null reference.
        public void ResetList() => NeedsRebuild = true;

        float LiveTimer;
        int LiveHash = -1;

        public override void Update(float fixedDeltaTime)
        {
            if (NeedsRebuild)
            {
                NeedsRebuild = false;
                Build();
            }
            else
            {
                // Required and Active move with the turn, so the page reads them again on a
                // throttled beat - free while nothing changes. The rows are refilled in place;
                // only a change of measured width costs a full rebuild, since the frame is
                // sized from the table and has to follow it.
                LiveTimer -= fixedDeltaTime;
                if (LiveTimer <= 0f)
                {
                    LiveTimer = 1f;
                    int h = ComputeLiveHash();
                    if (LiveHash != -1 && h != LiveHash)
                    {
                        float wasWidth = Table.ContentWidth;
                        MeasureColumns();
                        if (Table.ContentWidth != wasWidth)
                        {
                            Build();
                        }
                        else
                        {
                            Table.Layout(Client, Client.Y + 10, Client.Bottom - 5 - ActionsLineH);
                            FillList();
                        }
                    }
                    LiveHash = h;
                }
            }
            base.Update(fixedDeltaTime);
        }

        // the identity of what the table shows: a change here means the rows are stale
        int ComputeLiveHash()
        {
            int h = 17;
            foreach (TradeZone zone in Player.TradeZones)
            {
                h = h * 31 + (zone.Name?.GetHashCode() ?? 0);
                h = h * 31 + zone.NumColonies;
                h = h * 31 + zone.Quota;
                h = h * 31 + zone.RequiredFreighters(Player);
                h = h * 31 + zone.ActiveFreighters(Player);
            }
            return h;
        }

        void FillList()
        {
            if (LastSortCol < 0)
            {
                ZonesSL.Reset();
                foreach (TradeZone zone in Player.TradeZones)
                    ZonesSL.AddItem(new TradeZonesScreenListItem(this, zone, Player));
            }
            else
            {
                Refill(LastSortCol, Table.Columns[LastSortCol].Ascending);
            }
        }
    }
}
