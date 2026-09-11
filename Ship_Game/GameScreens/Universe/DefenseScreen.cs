using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
using Ship_Game.UI;          // UITable: the shared table charte
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (wishlist): the Defense tab of the Empire group - every colony on one page with
    // what defends it. Setting a world's defence one colony screen at a time is the chore this
    // replaces; the point is to see at a glance which world is held and which is bare.
    public sealed class DefenseScreen : GameScreen
    {
        Submenu EmpireTabs; // the Empire group's tab row, this screen being one tab
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;

        public readonly UniverseScreen Universe;
        public readonly Empire Player;
        public readonly UITable Table;

        ScrollList<DefenseListItem> ColoniesSL; // rebuilt with the page, so not readonly
        RectF Client; // the tab frame's content area, kept so the table can be laid out again

        static int LastSortCol = 0;    // session-persistent, like the other Empire tables
        static bool LastSortAsc = true;

        // two rails stacked in the budget cell set the row's height: one line each, plus air
        const int RowH = 42;
        const int RowPitch = 46;

        public DefenseScreen(UniverseScreen parent)
            : base(parent, toPause: parent)
        {
            Universe = parent;
            Player = parent.Player;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;

            // the muted in-block separator, the same gray the Colonies tab uses to sub-group its
            // columns: a warm rule says "new subject", a gray one says "same subject, next figure"
            Color MutedSep = new Color(70, 70, 70);
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.System), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Planet), MinWidth = 150, Sortable = true },
                // the three counts share ONE reading: what stands here, and in brackets what is
                // on its way to it - so the column tip is the same for all three
                new UITable.Column { Title = Localizer.Token(GameText.DvDefenseGarrison), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.DvDefenseHereAndComingTip) },
                // the switch says WHO decides the garrison, the rail WHAT it aims for; a fixed
                // width because it carries controls rather than a figure
                new UITable.Column { Title = "Auto-train", Width = 200, Align = TableAlign.Center,
                                     SepColor = MutedSep },
                // ONE column for one question: who runs this colony, and does he run its orbit
                // too (maintainer feedback). The governor's type in a bold letter of its own
                // colour - the mark the Colonies tab uses - and beside it the switch, which is
                // simply ABSENT where there is no governor: there would be nobody to honour it,
                // and a greyed box still asks to be read.
                new UITable.Column { Title = Localizer.Token(GameText.DvDefenseSpaceDef), Width = 90,
                                     Align = TableAlign.Center, Sortable = true,
                                     Tip = Localizer.Token(GameText.DvDefenseSpaceDefTip) },
                // ⚠ the shared tokens carry a trailing colon for their own screens; a table header
                // wears none, and trimming beats a second token saying the same word
                new UITable.Column { Title = Localizer.Token(GameText.Platforms).TrimEnd(':', ' '), Align = TableAlign.Number,
                                     Sortable = true, SepColor = MutedSep,
                                     Tip = Localizer.Token(GameText.DvDefenseHereAndComingTip) },
                new UITable.Column { Title = Localizer.Token(GameText.Stations).TrimEnd(':', ' '), Align = TableAlign.Number,
                                     Sortable = true, SepColor = MutedSep,
                                     Tip = Localizer.Token(GameText.DvDefenseHereAndComingTip) },
                // the two purses that pay for what this page shows: the ground troops above,
                // the orbitals below - stacked, because they answer the same question at two
                // altitudes and a player reads them together (maintainer feedback)
                new UITable.Column { Title = Localizer.Token(GameText.Budget), Width = 260,
                                     Align = TableAlign.Center, SepColor = MutedSep },
                // WHICH buildings, not how many: the page answers "what is missing here"
                new UITable.Column { Title = Localizer.Token(GameText.Defense), Width = 220, Align = TableAlign.Center },
            });

            Build();
        }

        void Build()
        {
            RemoveAll();
            MeasureColumns();

            Table.Columns[LastSortCol].Sorted = true;
            Table.Columns[LastSortCol].Ascending = LastSortAsc;

            int numColonies = Player.GetPlanets().Count;
            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight);
            float contentH = UITable.ContentHeightFor(99, Math.Max(3, numColonies), RowPitch, fullAvail);
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe),
                                                   ScreenGroups.TabIndexOf(this), OnEmpireTabChanged,
                                                   Table.ContentWidth, contentH);
            Client = EmpireTabs.ClientArea;
            Table.RowPitch = RowPitch;
            Table.Layout(Client, Client.Y + 10, Client.Bottom - 5);

            ColoniesSL = Add(new ScrollList<DefenseListItem>(Table.ListRect, RowH));
            ColoniesSL.EnableItemHighlight = true;
            ColoniesSL.OnClick = OnColonyClicked;
            ColoniesSL.OnDoubleClick = OnColonyDoubleClicked;
            Table.ApplyHighlightTo(ColoniesSL);

            FillList();
        }

        // a single click centres the map on the colony at the zoom already chosen, the way the
        // Colonies tab and the patrol list both behave (maintainer feedback)
        void OnColonyClicked(DefenseListItem item) => Universe.PanToPlanetKeepZoom(item.P);

        // Ludoal fork: the colony a row names opens on a double click, as it does on the Colonies
        // tab - the seat is armed BEFORE the snap so the colony wears this tab as its Esc origin.
        void OnColonyDoubleClicked(DefenseListItem item)
        {
            Universe.HostColonyTab(item.P, ScreenGroups.Group.Empire, ScreenGroups.TabIndexOf(this), govTab: 2);
            Universe.SnapViewColony(item.P, combatView: false);
            // the colony inherits this page's automatic pause: consulting a colony from a paused
            // list must not restart the simulation
            if (Universe.LookingAtPlanet)
                HandOverUniversePause(Universe.workersPanel);
            ExitScreen();
        }

        void OnEmpireTabChanged(int index)
            => ScreenGroups.SwitchEmpireTab(index, self: ScreenGroups.TabIndexOf(this), Universe, this);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            if (ColoniesSL.NumEntries > 0)
                Table.DrawChrome(batch);

            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
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
                FillList();
                return true;
            }
            return base.HandleInput(input);
        }

        Planet[] SortedPlanets(int col, bool ascending)
        {
            var planets = new Array<Planet>(Player.GetPlanets());
            switch (col)
            {
                case 2:  return planets.Sorted(ascending, p => p.CountEmpireTroops(Player));
                case 4:  return planets.Sorted(ascending, p => (int)p.CType);
                case 5:  return planets.Sorted(ascending, p => p.NumPlatforms);
                case 6:  return planets.Sorted(ascending, p => p.NumStations);
                case 1:  return planets.Sorted(ascending, p => p.Name);
                default: return planets.Sorted(ascending, p => p.System.Name);
            }
        }

        void FillList()
        {
            ColoniesSL.Reset();
            foreach (Planet p in SortedPlanets(LastSortCol, LastSortAsc))
                ColoniesSL.AddItem(new DefenseListItem(this, p, Player));
        }

        // measured on the strings the rows will actually DRAW, never on the raw values - a column
        // sized on "3" while the row shows "3 (+2)" overruns by the width of the brackets
        void MeasureColumns()
        {
            var sys = new Array<string>(); var names = new Array<string>();
            var garrison = new Array<string>(); var platforms = new Array<string>();
            var stations = new Array<string>();
            foreach (Planet p in Player.GetPlanets())
            {
                sys.Add(p.System.Name);
                names.Add(p.Name);
                garrison.Add(DefenseListItem.GarrisonText(p, Player));
                platforms.Add(DefenseListItem.PlatformsText(p));
                stations.Add(DefenseListItem.StationsText(p));
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, sys);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial12Bold, names);
            UITable.AutoSize(Table.Columns[2], Fonts.Arial12Bold, garrison);
            UITable.AutoSize(Table.Columns[5], Fonts.Arial12Bold, platforms);
            UITable.AutoSize(Table.Columns[6], Fonts.Arial12Bold, stations);
            // ⚠ each of the three figure columns carries a button at its right end, and AutoSize
            // only ever saw the figures: the lane is added here, from the row's own constant, or
            // the button would sit on the number (the same lane the trade zones' padlock takes)
            Table.Columns[2].Width += DefenseListItem.ActionLane;
            Table.Columns[5].Width += DefenseListItem.ActionLane;
            Table.Columns[6].Width += DefenseListItem.ActionLane;
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);
        }

        float LiveTimer;
        int LiveHash = -1;

        public override void Update(float fixedDeltaTime)
        {
            // the counts move with the turn, so the page reads them again on a throttled beat -
            // free while nothing changes, and a change of measured width costs a full rebuild
            // since the frame is sized from the table
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
                        Build();
                    else
                    {
                        Table.Layout(Client, Client.Y + 10, Client.Bottom - 5);
                        FillList();
                    }
                }
                LiveHash = h;
            }
            base.Update(fixedDeltaTime);
        }

        // the identity of what the table shows: a change here means the rows are stale
        int ComputeLiveHash()
        {
            int h = 17;
            foreach (Planet p in Player.GetPlanets())
            {
                h = h * 31 + p.CountEmpireTroops(Player);
                h = h * 31 + p.NumTroopsInTheWorks;
                h = h * 31 + p.NumPlatforms;
                h = h * 31 + p.NumStations;
                h = h * 31 + p.GarrisonSize;
                h = h * 31 + (p.AutoBuildTroops ? 1 : 0);
                h = h * 31 + p.NumBuildings;
                h = h * 31 + (int)p.CType;
                h = h * 31 + (p.GovOrbitals ? 1 : 0);
                h = h * 31 + (p.IsBudgetManual(BudgetArea.GroundDef) ? 1 : 0);
                h = h * 31 + (p.IsBudgetManual(BudgetArea.SpaceDef) ? 1 : 0);
            }
            return h;
        }
    }
}
