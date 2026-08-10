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
using Ship_Game.Universe.SolarBodies; // DistanceDisplay
using Ship_Game.UI; // UITable: the shared table charte

namespace Ship_Game
{
    public sealed class ExoticSystemsListScreen : GameScreen
    {
        Submenu GalaxyTabs; // Ludoal fork: the Galaxy group's tab row, this screen being one tab
        // Ludoal fork (bench 387): this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => GalaxyTabs?.Rect ?? base.PageFrame;

        public UniverseScreen Universe;
        public UniverseState UState => Universe.UState;
        public EmpireUIOverlay EmpireUI;
        Empire Player => Universe.Player;

        public Planet SelectedPlanet { get; private set; }
        readonly ScrollList<ExoticSystemsListScreenItem> ExoticSL;

        public readonly UITable Table; // the shared table charte owns geometry, headers and rules
        readonly Array<ExplorableGameObject> ExploredSolarBodies = new();
        static int LastSortCol = -1;   // session-persistent (bench 307)
        static bool LastSortAsc = true;

        // FB - this will store each planet or system and it's distance to the closest player colony. 
        readonly Map<ExplorableGameObject, float> DistancesToClosestColony = new();

        public ExoticSystemsListScreen(UniverseScreen parent, EmpireUIOverlay empireUi, string audioCue = "")
            : base(parent, toPause: parent)
        {
            if (!string.IsNullOrEmpty(audioCue))
                GameAudio.PlaySfxAsync(audioCue);

            Universe = parent;
            EmpireUI = empireUi;

            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;

            foreach (SolarSystem system in Universe.UState.Systems.OrderBy(s => s.Position.Distance(Universe.Player.WeightedCenter)))
            {
                if (system.IsExploredBy(Player) && Player.CanBuildDysonSwarmIn(system))
                    ExploredSolarBodies.Add(system);
                if (system.IsResearchable && system.IsExploredBy(Player))
                    ExploredSolarBodies.Add(system);

                foreach (Planet p in system.PlanetList)
                {
                    if (p.IsExploredBy(Player)&& (p.IsResearchable || p.IsMineable))
                        ExploredSolarBodies.Add(p);
                }
            }

            CalcPlanetsDistances();

            // Ludoal fork: the Exotic Systems tab of the Galaxy group, content-sized on the
            // shared table charte - every column sizes itself on the data it will show
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.System), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.StarOrPlanet), Sortable = true, MinWidth = 180 },
                new UITable.Column { Title = Localizer.Token(GameText.Proximity), Align = TableAlign.Center, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.ResourceName), Sortable = true },
                // back to the production hammer (Lek's review, bench 305) - the crystal
                // doubled the Resource column's own icon right beside it
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_production"), Width = 40,
                                     Align = TableAlign.Number, Sortable = true, Tip = Localizer.Token(GameText.Richness) },
                new UITable.Column { Title = Localizer.Token(GameText.Owner), Align = TableAlign.Center, Sortable = true },
                // sized on the mining row's worst case: the 168px deploy button plus its two
                // wide enough for the single deploy button (168) plus the "N/M Deployed" count
                // beside it - back down from 420 now that the two-button layout is gone
                new UITable.Column { Title = "Stations", Width = 290, Align = TableAlign.Center },
            });
            var sys = new Array<string>(); var names = new Array<string>();
            var prox = new Array<string>();
            var res = new Array<string>(); var owners = new Array<string>();
            foreach (ExplorableGameObject sb in ExploredSolarBodies)
            {
                Planet p = sb as Planet;
                sys.Add(p?.System.Name ?? (sb as SolarSystem)?.Name ?? "");
                names.Add(p?.Name ?? (sb as SolarSystem)?.Name ?? "");
                prox.Add(new DistanceDisplay(GetShortestDistance(sb) / 1000).Text);
                res.Add(p != null ? (p.Mining?.TranslatedResourceName.Text ?? "Research") : "Dyson Swarm 0");
                owners.Add(p?.Mining?.Owner?.data.Traits.Singular ?? "");
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, sys);
            Table.Columns[0].Width += 24; // the hostile-warning icon lane
            UITable.AutoSize(Table.Columns[1], Fonts.Arial14Bold, names);
            Table.Columns[1].Width += 46; // the body icon rides ahead of the two-line name
            UITable.AutoSize(Table.Columns[2], Fonts.Arial12Bold, prox);
            UITable.AutoSize(Table.Columns[3], Fonts.Arial12Bold, res);
            Table.Columns[3].Width += 30; // the resource icon
            UITable.AutoSize(Table.Columns[5], Fonts.Arial12Bold, owners);
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);
            // System is the standing sort from the first frame (the list arrives system-ordered)
            // the standing sort survives the screen for the session (maintainer bench 307)
            if (LastSortCol < 0) { LastSortCol = 0; LastSortAsc = true; }
            Table.Columns[LastSortCol].Sorted = true;
            Table.Columns[LastSortCol].Ascending = LastSortAsc;

            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // bench 343: capped at 1080p
            // 48 = the 44px row plus the list's 4px item padding - counting 44 alone kept
            // a scrollbar alive with room to spare (maintainer bench 291)
            float contentH = UITable.ContentHeightFor(99, Math.Max(3, ExploredSolarBodies.Count), 48, fullAvail);
            GalaxyTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Galaxy, Universe), 1,
                                                   OnGalaxyTabChanged, Table.ContentWidth, contentH);
            RectF client = GalaxyTabs.ClientArea;
            Table.RowPitch = 48;
            Table.Layout(client, client.Y + 10, client.Bottom - 5);

            ExoticSL = Add(new ScrollList<ExoticSystemsListScreenItem>(Table.ListRect, 44));
            ExoticSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(ExoticSL);
        }

        // Ludoal fork: the other two tabs live in their own screen, so leaving Exotic Systems hands
        // over to it. This tab is a no-op: we are already here.
        void OnGalaxyTabChanged(int index)
            => ScreenGroups.SwitchGalaxyTab(index, self: 1, Universe, this);

        void CalcPlanetsDistances()
        {
            var playerPlanets = Player.GetPlanets();
            foreach (ExplorableGameObject solarBody in ExploredSolarBodies)
            {
                if (solarBody is Planet planet)
                {
                    float shortestDistance = playerPlanets.Min(p => p.Position.Distance(planet.Position));
                    DistancesToClosestColony.Add(planet, shortestDistance);
                }
                else if (solarBody is SolarSystem system)
                {
                    float shortestDistance = playerPlanets.Min(p => p.Position.Distance(system.Position));
                    DistancesToClosestColony.Add(system, shortestDistance);
                }
            }
        }

        float GetShortestDistance(ExplorableGameObject solarBody)
        {
            return DistancesToClosestColony.TryGetValue(solarBody, out float distance) ? distance : 0;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first - as a Submenu background it would be
            // drawn among the children, after everything below it.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GalaxyTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            // the shared charte draws the headers, the rule and the separators
            Table.DrawChrome(batch);
            ScreenGroups.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        void Refill(int col, bool ascending)
        {
            ExoticSL.Reset();
            ExoticSL.OnDoubleClick = OnExoticSystemsListItemClicked;
            ExplorableGameObject[] bodies;
            switch (col)
            {
                case 1:  bodies = ExploredSolarBodies.Sorted(ascending, sb => sb is Planet p ? p.Name : ""); break;
                case 2:  bodies = ExploredSolarBodies.Sorted(ascending, GetShortestDistance); break;
                case 3:  bodies = ExploredSolarBodies.Sorted(ascending, sb => sb is Planet p ? (p.Mining?.TranslatedResourceName.Text ?? "") : sb is SolarSystem s && s.DysonSwarmType > 0 ? s.DysonSwarmType.ToString() : ""); break;
                case 4:  bodies = ExploredSolarBodies.Sorted(ascending, sb => sb is Planet p ? (p.Mining?.Richness ?? 0f) : 0f); break;
                case 5:  bodies = ExploredSolarBodies.Sorted(ascending, sb => sb is Planet p && p.IsMineable && p.Mining.HasOpsOwner ? p.Mining.Owner.data.Traits.Singular : ""); break;
                default: bodies = ExploredSolarBodies.Sorted(ascending, sb => sb is Planet p ? p.System.Name : sb is SolarSystem s ? s.Name : ""); break;
            }
            foreach (ExplorableGameObject solarBody in bodies)
                ExoticSL.AddItem(new ExoticSystemsListScreenItem(this, solarBody, GetShortestDistance(solarBody)));
        }

        public override bool HandleInput(InputState input)
        {
            if (EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (ExoticSL.NumEntries == 0)
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

            if (input.KeyPressed(Keys.G) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        void OnExoticSystemsListItemClicked(ExoticSystemsListScreenItem item)
        {
            ExitScreen();
            GameAudio.AcceptClick();
            if (item.IsStar)
            {
                Universe.SetSelectedSystem(item.System);
                Universe.CamDestination = new Vector3d(item.System.Position, item.System.Radius);
            }
            else
            {
                // Ludoal fork (maintainer feedback): select the PLANET (not just its system) and
                // glide to the planet-view zoom, copied from the Planet Info cartouche's arrows -
                // the old Z=10000 zoomed in far too hard and left the planet unselected.
                Universe.SetSelectedPlanet(item.Planet);
                Universe.SnapViewTo(new Vector3d(item.Planet.Position.X, item.Planet.Position.Y,
                    Universe.GetZfromScreenState(UniverseScreen.UnivScreenState.PlanetView)), 5f, 2f);
            }
        }

        public void ResetList()
        {
            if (LastSortCol < 0)
            {
                ExoticSL.Reset();
                ExoticSL.OnDoubleClick = OnExoticSystemsListItemClicked; // Ludoal fork: double-click everywhere
                foreach (ExplorableGameObject solarBody in ExploredSolarBodies)
                    ExoticSL.AddItem(new ExoticSystemsListScreenItem(this, solarBody, GetShortestDistance(solarBody)));
            }
            else
            {
                // re-apply the standing sort with its CURRENT direction
                Refill(LastSortCol, Table.Columns[LastSortCol].Ascending);
            }

            SelectedPlanet = ExoticSL.NumEntries > 0 ? ExoticSL.AllEntries[0].Planet : null;
        }

    }
}
