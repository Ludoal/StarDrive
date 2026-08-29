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

        readonly ScrollList<TradeZonesScreenListItem> ZonesSL;
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
                new UITable.Column { Title = Localizer.Token(GameText.TzAssigned), Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = "", Width = 60, Align = TableAlign.Center },
            });

            MeasureColumns();

            if (LastSortCol < 0) { LastSortCol = 0; LastSortAsc = true; }
            Table.Columns[LastSortCol].Sorted = true;
            Table.Columns[LastSortCol].Ascending = LastSortAsc;

            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight);
            float contentH = UITable.ContentHeightFor(99, Math.Max(3, player.TradeZones.Count), 38, fullAvail) + ActionsLineH;
            GalaxyTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Galaxy, Universe), 2,
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

            ResetList();
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

        public void EditColonies(TradeZone zone)
        {
            GameAudio.AcceptClick();
            ScreenManager.AddScreen(new TradeZoneColoniesScreen(this, zone));
        }

        // Called back by the picker. A zone with no colony is never kept.
        public void ApplyColonies(TradeZone zone, Array<Planet> chosen, int quota)
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
            => ScreenGroups.SwitchGalaxyTab(index, self: 2, Universe, this);

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
                case 3:  zones = Player.TradeZones.Sorted(ascending, z => z.Quota); break;
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
            foreach (TradeZone z in Player.TradeZones)
            {
                names.Add(z.Name);
                counts.Add(z.NumColonies.ToString());
                served.Add(ColonyNames(z));
                quotas.Add(z.Quota <= 0 ? Localizer.Token(GameText.PolFreighterRefitAuto) : z.Quota.ToString());
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, names);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial12Bold, counts);
            UITable.AutoSize(Table.Columns[2], Fonts.Arial12Bold, served);
            UITable.AutoSize(Table.Columns[3], Fonts.Arial12Bold, quotas);
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);
        }

        public void ResetList()
        {
            if (Client.W > 0) // not during construction: the frame does not exist yet
            {
                MeasureColumns();
                Table.Layout(Client, Client.Y + 10, Client.Bottom - 5 - ActionsLineH);
                // the rows read their cell rects from the columns, so re-measuring is enough:
                // the list keeps the geometry it was built with.
            }
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
