using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics.Input;
using SDGraphics;
using Ship_Game.GameScreens; // ReworkScreens: the group geometry
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;
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

        readonly SortButton SbPatrolName;
        readonly SortButton SbNumWaypoints;
        readonly SortButton SbNumFleetsAssigned;
        readonly SortButton SbFleetsAssigned;


        RectF ERect;
        SortButton LastSorted;

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
            Rectangle frame = ReworkScreens.GroupFrame(ScreenWidth, ScreenHeight);
            GalaxyTabs = Add(new Submenu(new RectF(frame.X, frame.Y, frame.Width, frame.Height),
                                         ReworkScreens.GalaxyTabTitles));
            GalaxyTabs.OnTabChange = OnGalaxyTabChanged;
            GalaxyTabs.PerformLayout();
            GalaxyTabs.SelectedIndex = 2;

            Vector2 closePos = ReworkScreens.GroupClosePos(GalaxyTabs.ClientArea);
            Add(new CloseButton(closePos.X, closePos.Y));

            RectF client = GalaxyTabs.ClientArea;
            ERect = ReworkScreens.GalaxyTable(client);
            RectF slRect = new(ERect.X, ERect.Y - 10, ERect.W, ERect.H + 10);
            PatrolsSL = Add(new ScrollList<EmpirePatrolsScreenListItem>(slRect));
            PatrolsSL.EnableItemHighlight = true;
            foreach (FleetPatrol patrol in player.FleetPatrols)
            {
                PatrolsSL.AddItem(new EmpirePatrolsScreenListItem(this, patrol, player));
            }

            SbPatrolName = new SortButton(Player.data.PLSort, Localizer.Token(GameText.PatrolPlanName));
            SbNumWaypoints = new SortButton(Player.data.PLSort, Localizer.Token(GameText.NumWayPoints));
            SbNumFleetsAssigned = new SortButton(Player.data.PLSort, Localizer.Token(GameText.PatrolNumAssignedFleets));
            SbFleetsAssigned = new SortButton(Player.data.PLSort, Localizer.Token(GameText.PatrolAssignedFleets));
        }

        // Ludoal fork: a column's rect from its two edges, so a header can be centred without a
        // list item to read the columns off.
        static Rectangle ColumnRect(float left, float right)
            => new((int)left, 0, (int)(right - left), 0);

        // Ludoal fork: the other two tabs live in their own screen, so leaving Patrols hands over to
        // it. This tab is a no-op: we are already here.
        void OnGalaxyTabChanged(int index)
            => ReworkScreens.SwitchGalaxyTab(index, self: 2, Universe, this);

        Vector2 GetCenteredTextOffset(Rectangle rect, GameText text)
        {
            return new Vector2(rect.X + rect.Width / 2 - Fonts.Arial20Bold.MeasureString(Localizer.Token(text)).X / 2f,
                               ERect.Y - Fonts.Arial20Bold.LineSpacing);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first - as a Submenu background it would be
            // drawn among the children, after everything below it.
            batch.FillRectangle(GalaxyTabs.ClientArea, ReworkScreens.GroupFrameFill);
            base.Draw(batch, elapsed);

            // Ludoal fork: the header, separators and border are drawn whether or not there is a
            // patrol to list - an empire with no patrol plans showed a blank panel, which reads as
            // a broken screen rather than an empty table. Column edges come from ERect and the same
            // fractions the list items use, so they no longer need a row to exist to be known.
            Graphics.Font fontStyle = Fonts.Arial20Bold;
            float w = ERect.W;
            float nameX = ERect.X;
            float waypointsX = nameX + w * 0.15f;
            float fleetsNumX = waypointsX + w * 0.14f;
            float fleetsX = fleetsNumX + w * 0.14f;
            float fleetsRight = fleetsX + w * 0.28f;

            var textCursor = GetCenteredTextOffset(ColumnRect(nameX, waypointsX), GameText.PatrolPlanName);
            SbPatrolName.Update(textCursor);
            SbPatrolName.Draw(ScreenManager);

            textCursor = GetCenteredTextOffset(ColumnRect(waypointsX, fleetsNumX), GameText.NumWayPoints);
            SbNumWaypoints.Update(textCursor);
            SbNumWaypoints.Draw(ScreenManager, fontStyle);

            textCursor = GetCenteredTextOffset(ColumnRect(fleetsNumX, fleetsX), GameText.PatrolNumAssignedFleets);
            SbNumFleetsAssigned.Update(textCursor);
            SbNumFleetsAssigned.Draw(ScreenManager, fontStyle);

            textCursor = GetCenteredTextOffset(ColumnRect(fleetsX, fleetsRight), GameText.PatrolAssignedFleets);
            SbFleetsAssigned.Update(textCursor);
            SbFleetsAssigned.Draw(ScreenManager, fontStyle);

            Color lineColor = new Color(118, 102, 67, 255);
            float columnTop = ERect.Y + 15;
            float columnBot = ERect.Y + ERect.H - 20;
            foreach (float lineX in new[] { waypointsX, fleetsNumX, fleetsX, fleetsRight + 5 })
                batch.DrawLine(new Vector2(lineX, columnTop), new Vector2(lineX, columnBot), lineColor);

            batch.DrawRectangle(PatrolsSL.ItemsHousing, lineColor); // items housing border
            ReworkScreens.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        void InitSortedItems(SortButton button)
        {
            LastSorted = button;
            GameAudio.BlipClick();
            button.Ascending = !button.Ascending;
            PatrolsSL.Reset();
        }

        void Sort<T>(SortButton button, Func<FleetPatrol, T> sortPredicate)
        {
            InitSortedItems(button);
            FleetPatrol[] patrols = Player.FleetPatrols.Sorted(button.Ascending, sortPredicate);
            foreach (FleetPatrol patrol in patrols)
            {
                PatrolsSL.AddItem(new EmpirePatrolsScreenListItem(this, patrol, Player));
            }
        }

        void HandleButton<T>(InputState input, SortButton button, Func<FleetPatrol, T> sortPredicate)
        {
            if (button.HandleInput(input))
                Sort(button, sortPredicate);
        }

        void ResetButton<T>(SortButton button, Func<FleetPatrol, T> sortPredicate)
        {
            if (LastSorted.Text == button.Text)
                Sort(button, sortPredicate);
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (PatrolsSL.NumEntries == 0)
                ResetList();

            HandleButton(input, SbPatrolName, p => p.Name);
            HandleButton(input, SbNumWaypoints, p => p.WayPoints.Count);
            HandleButton(input, SbNumFleetsAssigned, p => Player.AllFleets.Count(fleet => fleet.HasPatrolPlan && fleet.Patrol == p));

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
            PatrolsSL.Reset();

            if (LastSorted == null)
            {
                foreach (FleetPatrol patrol in Player.FleetPatrols)
                {
                    PatrolsSL.AddItem(new EmpirePatrolsScreenListItem(this, patrol, Player));
                }
            }
            else
            {
                ResetButton(SbPatrolName, p => p.Name);
                ResetButton(SbNumWaypoints, p => p.WayPoints.Count);
                ResetButton(SbNumFleetsAssigned, p => Player.AllFleets.Count(fleet => fleet.HasPatrolPlan && fleet.Patrol.Name == p.Name));
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
