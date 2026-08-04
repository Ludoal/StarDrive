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

        public UniverseScreen Universe;
        public UniverseState UState => Universe.UState;
        public readonly Empire Player;

        public Planet SelectedPlanet { get; private set; }
        readonly ScrollList<EmpirePatrolsScreenListItem> PatrolsSL;

        public readonly UITable Table; // the shared table charte owns geometry, headers and rules
        // the Actions lane: both buttons sized to their own text (maintainer, 4 Aug)
        public readonly int RenameBtnW;
        public readonly int DeleteBtnW;
        int LastSortCol = -1;

        public EmpirePatrolsScreen(UniverseScreen parent, Empire player)
            : base(parent, toPause: parent)
        {
            Universe = parent;
            Player = player;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;

            // Ludoal fork: the Patrols tab of the Galaxy group - the title cartouche and its brass
            // surround give way to the group's tab row.
            Rectangle frame = ScreenGroups.GroupFrame(ScreenWidth, ScreenHeight);
            GalaxyTabs = Add(new Submenu(new RectF(frame.X, frame.Y, frame.Width, frame.Height),
                                         ScreenGroups.GalaxyTabTitles));
            GalaxyTabs.OnTabChange = OnGalaxyTabChanged;
            GalaxyTabs.PerformLayout();
            GalaxyTabs.SelectedIndex = 2;

            Vector2 closePos = ScreenGroups.GroupClosePos(GalaxyTabs.ClientArea);
            Add(new CloseButton(closePos.X, closePos.Y));

            // the table on the shared charte: every column sizes itself on the data,
            // the Assigned Fleets column folds if the sums pass the resolution
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.PatrolPlanName), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.NumWayPoints), Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = "# Fleets", Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.PatrolAssignedFleets), Foldable = true },
                new UITable.Column { Title = "Actions", Align = TableAlign.Center },
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
            RenameBtnW = (int)Fonts.Arial12Bold.TextWidth(Localizer.Token(GameText.RenamePatrol)) + 24;
            DeleteBtnW = (int)Fonts.Arial12Bold.TextWidth(Localizer.Token(GameText.DeletePatrol)) + 24;
            Table.Columns[4].Width = RenameBtnW + 8 + DeleteBtnW + 2 * UITable.PadX;

            RectF client = GalaxyTabs.ClientArea;
            Table.FitToWidth((int)client.W - 2 * (UITable.SideMargin - 9) - UITable.SliderLane);
            Table.Layout(client, client.Y + 10, client.Bottom - 5);

            PatrolsSL = Add(new ScrollList<EmpirePatrolsScreenListItem>(Table.ListRect, 34));
            PatrolsSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(PatrolsSL);
            foreach (FleetPatrol patrol in player.FleetPatrols)
            {
                PatrolsSL.AddItem(new EmpirePatrolsScreenListItem(this, patrol, player));
            }
        }

        // Ludoal fork: the other two tabs live in their own screen, so leaving Patrols hands over to
        // it. This tab is a no-op: we are already here.
        void OnGalaxyTabChanged(int index)
            => ScreenGroups.SwitchGalaxyTab(index, self: 2, Universe, this);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first - as a Submenu background it would be
            // drawn among the children, after everything below it.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GalaxyTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            // the shared charte draws the headers, the rule and the separators - with or
            // without a patrol to list, so an empty empire still reads as an empty TABLE
            Table.DrawChrome(batch);
            ScreenGroups.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        void Refill(int col, bool ascending)
        {
            PatrolsSL.Reset();
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
