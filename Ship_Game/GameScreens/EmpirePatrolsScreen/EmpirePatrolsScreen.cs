using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics.Input;
using SDGraphics;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;
using Ship_Game.UI; // UITable: the shared table charte
using Ship_Game.Fleets;

namespace Ship_Game
{
    public sealed class EmpirePatrolsScreen : GameScreen
    {
        Submenu GalaxyTabs; // Ludoal fork: the Galaxy group's tab row, this screen being one tab
        // Ludoal fork: this page's real frame is its tab row's rect
        public override Rectangle PageFrame => GalaxyTabs?.Rect ?? base.PageFrame;

        public UniverseScreen Universe;
        public UniverseState UState => Universe.UState;
        public readonly Empire Player;

        public Planet SelectedPlanet { get; private set; }
        readonly ScrollList<EmpirePatrolsScreenListItem> PatrolsSL;

        public readonly UITable Table; // the shared table charte owns geometry, headers and rules
        // the Actions lane: both buttons sized to their own text
        static int LastSortCol = -1;   // session-persistent
        static bool LastSortAsc = true;

        public EmpirePatrolsScreen(UniverseScreen parent, Empire player)
            : base(parent, toPause: parent)
        {
            Universe = parent;
            Player = player;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;

            // the table on the shared charte: every column sizes itself on the data,
            // the Assigned Fleets column folds if the sums pass the resolution
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.PatrolPlanName), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.NumWayPoints), Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = "# Fleets", Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.PatrolAssignedFleets), Foldable = true },
                // Ludoal fork: a dedicated Actions column for the edit/delete icons,
                // so the table lays them out in their own lane at the right end.
                new UITable.Column { Title = "", Width = 60, Align = TableAlign.Center },
            });
            var names = new Array<string>(); var wps = new Array<string>();
            var counts = new Array<string>(); var assigned = new Array<string>();
            foreach (FleetPatrol p in player.FleetPatrols)
            {
                names.Add(p.Name);
                wps.Add(p.WayPoints.Count.ToString());
                counts.Add(player.AllFleets.Count(f => f.HasPatrolPlan && f.Patrol == p).ToString());
                assigned.Add(string.Join(", ", player.AllFleets.Where(f => f.HasPatrolPlan && f.Patrol == p)
                                                               .Select(f => f.Name)));
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, names);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial12Bold, wps);
            UITable.AutoSize(Table.Columns[2], Fonts.Arial12Bold, counts);
            UITable.AutoSize(Table.Columns[3], Fonts.Arial12Bold, assigned);
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);

            // Ludoal fork: the Patrols tab of the Galaxy group is content-sized -
            // the frame hugs the table, the plan count sets the height.
            // The standing sort survives the screen for the session.
            if (LastSortCol < 0) { LastSortCol = 0; LastSortAsc = true; }
            Table.Columns[LastSortCol].Sorted = true;
            Table.Columns[LastSortCol].Ascending = LastSortAsc;

            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // floor = the info cartouche
            // 38 = the 34px row plus the list's 4px item padding
            float contentH = UITable.ContentHeightFor(99, Math.Max(3, player.FleetPatrols.Count), 38, fullAvail);
            GalaxyTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Galaxy, Universe), 2,
                                                   OnGalaxyTabChanged, Table.ContentWidth, contentH);
            RectF client = GalaxyTabs.ClientArea;
            Table.RowPitch = 38;
            Table.Layout(client, client.Y + 10, client.Bottom - 5);

            PatrolsSL = Add(new ScrollList<EmpirePatrolsScreenListItem>(Table.ListRect, 34));
            PatrolsSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(PatrolsSL);
            PatrolsSL.OnDoubleClick = OnPatrolDoubleClicked; // to the plan on the map
            PatrolsSL.OnClick = OnPatrolSingleClicked; // single-click pans at current zoom
            ResetList(); // honors the session's standing sort
        }

        // double-click flies the camera to the patrol's route.
        // single-click pans to the route's midpoint at the CURRENT zoom -
        // the screen stays open, the flight shows in the band.
        void OnPatrolSingleClicked(EmpirePatrolsScreenListItem item)
        {
            var wps = item.FleetPatrol.WayPoints.ToArray();
            if (wps.Length == 0)
                return;
            GameAudio.AcceptClick();
            Vector2 center = Vector2.Zero;
            foreach (var wp in wps)
                center += wp.Position;
            center /= wps.Length;
            Universe.PanToKeepZoom(center);
        }

        void OnPatrolDoubleClicked(EmpirePatrolsScreenListItem item)
        {
            var wps = item.FleetPatrol.WayPoints.ToArray();
            if (wps.Length == 0)
                return;
            GameAudio.AcceptClick();
            ExitScreen();
            // Ludoal fork: centre on the ROUTE's midpoint, not its first waypoint -
            // zooming to wps[0] lands the camera at the corner of the plan, not on it.
            Vector2 center = Vector2.Zero;
            foreach (var wp in wps)
                center += wp.Position;
            center /= wps.Length;
            Universe.CamDestination = new Vector3d(center, 100000);
        }

        // Ludoal fork: the other two tabs live in their own screen, so leaving Patrols hands over to
        // it. This tab is a no-op: we are already here.
        void OnGalaxyTabChanged(int index)
            => ScreenGroups.SwitchGalaxyTab(index, self: 3, Universe, this);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first - as a Submenu background it would be
            // drawn among the children, after everything below it.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GalaxyTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            // no chrome on an empty table: the hint speaks alone
            if (PatrolsSL.NumEntries > 0)
                Table.DrawChrome(batch);

            if (PatrolsSL.NumEntries == 0)
            {
                const string hint = "Assign a patrol from a fleet.";
                Graphics.Font font = Fonts.Arial12Bold;
                var pos = new Vector2(Table.TableRect.X + (Table.TableRect.Width - font.TextWidth(hint)) / 2f,
                                      Table.ListRect.Y + 40);
                batch.DrawString(font, hint, pos.Rounded(), Color.Gray);
            }
            ScreenGroups.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        float LiveRefreshTimer;
        int LivePatrolsHash = -1;

        // the live-data pass: plans made or edited and fleets (un)assigned while the page
        // is open rebuild the rows - a throttled identity walk, free while nothing moves
        public override void Update(float fixedDeltaTime)
        {
            LiveRefreshTimer -= fixedDeltaTime;
            if (LiveRefreshTimer <= 0f)
            {
                LiveRefreshTimer = 1f;
                int h = ComputePatrolsHash();
                if (LivePatrolsHash != -1 && h != LivePatrolsHash)
                    ResetList();
                LivePatrolsHash = h;
            }
            base.Update(fixedDeltaTime);
        }

        int ComputePatrolsHash()
        {
            int h = 17;
            foreach (FleetPatrol p in Player.FleetPatrols)
            {
                h = h * 31 + (p.Name?.GetHashCode() ?? 0);
                h = h * 31 + p.WayPoints.Count;
            }
            foreach (Fleet f in Player.AllFleets)
                if (f.HasPatrolPlan)
                {
                    h = h * 31 + (f.Patrol?.Name?.GetHashCode() ?? 0);
                    h = h * 31 + (f.Name?.GetHashCode() ?? 0);
                }
            return h;
        }

        void Refill(int col, bool ascending)
        {
            PatrolsSL.Reset();
            PatrolsSL.OnDoubleClick = OnPatrolDoubleClicked; // Reset drops the handlers
            PatrolsSL.OnClick = OnPatrolSingleClicked;
            FleetPatrol[] patrols;
            switch (col)
            {
                case 1:  patrols = Player.FleetPatrols.Sorted(ascending, p => p.WayPoints.Count); break;
                case 2:  patrols = Player.FleetPatrols.Sorted(ascending, p => Player.AllFleets.Count(f => f.HasPatrolPlan && f.Patrol == p)); break;
                default: patrols = Player.FleetPatrols.Sorted(ascending, p => p.Name); break;
            }
            foreach (FleetPatrol patrol in patrols)
            {
                PatrolsSL.AddItem(new EmpirePatrolsScreenListItem(this, patrol, Player));
            }
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (PatrolsSL.NumEntries == 0)
                ResetList();

            // headers - tooltips, hover and sort clicks - through the shared charte
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

            // Ludoal fork: close with the key that opens this screen (P) — Keys.L was a
            // copy-paste leftover from PlanetListScreen, so the hotkey felt dead in-game.
            if (input.EmpirePatrolsScreen && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        void ResetList()
        {
            if (LastSortCol < 0)
            {
                PatrolsSL.Reset();
                foreach (FleetPatrol patrol in Player.FleetPatrols)
                {
                    PatrolsSL.AddItem(new EmpirePatrolsScreenListItem(this, patrol, Player));
                }
            }
            else
            {
                // re-apply the standing sort with its CURRENT direction
                Refill(LastSortCol, Table.Columns[LastSortCol].Ascending);
            }
        }

        public void DeletePatrol(FleetPatrol patrol)
        {
            lock (Player.FleetPatrols)
            {
                foreach (Fleet fleet in Player.AllFleets)
                {
                    if (fleet.HasPatrolPlan && fleet.Patrol.Name == patrol.Name)
                        fleet.ClearPatrol();
                }

                Player.FleetPatrols.Remove(patrol);
                GameAudio.EchoAffirmative();
                ResetList();
            }
        }

        public bool RenamePatrol(FleetPatrol patrol, string newName)
        {
            lock (Player.FleetPatrols)
            {
                patrol.ChangeName(newName);
                foreach (Fleet fleet in Player.AllFleets)
                {
                    if (fleet.HasPatrolPlan && fleet.Patrol.Name == newName)
                        fleet.Patrol.ChangeName(newName);
                }
                GameAudio.EchoAffirmative();
                ResetList();
                return true;
            }
        }
    }
}
