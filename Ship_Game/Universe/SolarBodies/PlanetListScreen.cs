using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
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
using Ship_Game.Ships; // Ship, for the rebasing-troops count
using Ship_Game.AI;    // ShipAI.Plan.Rebase

namespace Ship_Game
{
    public sealed class PlanetListScreen : GameScreen
    {
        Submenu GalaxyTabs; // Ludoal fork: the Galaxy group's tab row, this screen being one tab

        public UniverseScreen Universe;
        public UniverseState UState => Universe.UState;
        public EmpireUIOverlay EmpireUI;
        Empire Player => Universe.Player;

        public Planet SelectedPlanet { get; private set; }
        readonly ScrollList<PlanetListScreenItem> PlanetSL;

        public readonly UITable Table; // the shared table charte owns geometry, headers and rules
        // one slot width for the row buttons, from the widest text either slot can wear
        public readonly int OrdersSlotW;
        static int LastSortCol = -1;   // session-persistent (bench 307)
        static bool LastSortAsc = true;

        private UICheckBox cb_hideOwned;
        private UICheckBox cb_hideUninhabitable;
        float FilterLineY; // line 1, where the filters live - the troops count rides it too
        private DropOptions<string> ProximityFilter;
        private DropOptions<string> OwnerFilter;

        bool HideOwned
        {
            get => UState.P.PlanetScreenHideOwned;
            set => UState.P.PlanetScreenHideOwned = value;
        }

        bool HideUninhab
        {
            get => UState.P.PlanetsScreenHideInhospitable;
            set => UState.P.PlanetsScreenHideInhospitable = value;
        }

        private int NumAvailableTroops;
        private int NumRebasingTroops;   // Ludoal fork: troops rebasing to our own worlds, shown on line 1
        readonly Array<Planet> ExploredPlanets = new Array<Planet>();

        // FB - this will store each planet and it's distance to the closest player colony. If the planet is owned
        // by the player - the distance will be 0, logically.
        readonly Map<Planet, float> PlanetDistanceToClosestColony = new Map<Planet, float>();

        public PlanetListScreen(UniverseScreen parent, EmpireUIOverlay empireUi, string audioCue = "")
            : base(parent, toPause: parent)
        {
            if(!string.IsNullOrEmpty(audioCue))
                GameAudio.PlaySfxAsync(audioCue);

            Universe = parent;
            EmpireUI = empireUi;

            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;

            foreach (SolarSystem system in Universe.UState.Systems.OrderBy(s => s.Position.Distance(Universe.Player.WeightedCenter)))
            {
                foreach (Planet p in system.PlanetList)
                {
                    if (p.IsExploredBy(Player))
                    {
                        p.UpdateMaxPopulation();
                        ExploredPlanets.Add(p);
                    }
                }
            }

            CalcPlanetsDistances();

            // Ludoal fork: the Planets tab of the Galaxy group, content-sized on the shared
            // table charte - every column sizes itself on the data it will show. The button
            // slots size from the widest text either can wear (Colonize from its Cancel
            // Colonize toggle - maintainer, 4 Aug).
            OrdersSlotW = 24 + (int)new[] { "Colonize", "Cancel Colonize", "Send Troops",
                                            "Recall Troops (99)", "Invading: 99" }
                                   .Max(t => Fonts.Arial12Bold.TextWidth(t));
            // Features rides right after Planet (maintainer bench 291); Proximity and
            // Owner read centred
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.System), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Planet), Sortable = true, MinWidth = 180 },
                new UITable.Column { Title = "Features" },
                new UITable.Column { Title = Localizer.Token(GameText.Proximity), Align = TableAlign.Center, Sortable = true },
                // biospheres/crystal, not food/production: the INTRINSIC stats wear their
                // own icons so they never read as the net-income pair (maintainer bench 294)
                // the Planet Info cartouche's own pair (Lek's review, bench 305)
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_food"),
                                     Align = TableAlign.Number, Sortable = true, Tip = Localizer.Token(GameText.Fertility) },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_production"),
                                     Align = TableAlign.Number, Sortable = true, Tip = Localizer.Token(GameText.Richness) },
                new UITable.Column { Icon = ResourceManager.Texture("UI/icon_pop_22"),
                                     Align = TableAlign.Number, Sortable = true, Tip = Localizer.Token(GameText.MaxPopulation) },
                new UITable.Column { Title = "Fill", Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Owner), Align = TableAlign.Center, Sortable = true },
                new UITable.Column { Width = 2 * UITable.PadX + 2 * OrdersSlotW + 6, Align = TableAlign.Center },
            });
            var sys = new Array<string>(); var names = new Array<string>();
            var feats = new Array<string>(); var prox = new Array<string>();
            var ferts = new Array<string>(); var richs = new Array<string>();
            var pops = new Array<string>(); var ratios = new Array<string>();
            var owners = new Array<string>();
            foreach (Planet p in ExploredPlanets)
            {
                sys.Add(p.System.Name);
                names.Add(p.Name);
                feats.Add(PlanetListScreenItem.FeaturesMeasure(p));
                prox.Add(new DistanceDisplay(GetShortestDistance(p) / 1000).Text);
                ferts.Add(p.FertilityFor(Player).ToString("0.0", CultureInfo.InvariantCulture));
                richs.Add(p.MineralRichness.ToString("0.0", CultureInfo.InvariantCulture));
                string ps = p.PopulationStringForPlayer;
                int paren = ps.IndexOf(" (");
                pops.Add(paren < 0 ? ps : ps.Substring(0, paren));
                ratios.Add(paren < 0 ? "" : ps.Substring(paren + 2).TrimEnd(')'));
                owners.Add(p.GetOwnerName());
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, sys);
            Table.Columns[0].Width += 24; // the hostile-warning icon lane
            UITable.AutoSize(Table.Columns[1], Fonts.Arial14Bold, names);
            Table.Columns[1].Width += 46 + 40; // planet icon ahead, status icons behind
            UITable.AutoSize(Table.Columns[2], Fonts.Arial12Bold, feats);
            UITable.AutoSize(Table.Columns[3], Fonts.Arial12Bold, prox);
            UITable.AutoSize(Table.Columns[4], Fonts.Arial12Bold, ferts);
            UITable.AutoSize(Table.Columns[5], Fonts.Arial12Bold, richs);
            UITable.AutoSize(Table.Columns[6], Fonts.Arial12Bold, pops);
            UITable.AutoSize(Table.Columns[7], Fonts.Arial12Bold, ratios);
            UITable.AutoSize(Table.Columns[8], Fonts.Arial12Bold, owners);
            Table.FitToWidth((int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66);
            // the standing sort survives the screen for the session (maintainer bench 307)
            if (LastSortCol < 0) { LastSortCol = 0; LastSortAsc = true; }
            Table.Columns[LastSortCol].Sorted = true;
            Table.Columns[LastSortCol].Ascending = LastSortAsc;

            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // bench 343: capped at 1080p
            // 48 = the 44px row plus the list's 4px item padding
            float contentH = UITable.ContentHeightFor(119, Math.Max(3, ExploredPlanets.Count), 48, fullAvail);
            GalaxyTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Galaxy, Universe), 0,
                                                   OnGalaxyTabChanged, Table.ContentWidth, contentH);
            RectF client = GalaxyTabs.ClientArea;
            // the filter lane, then the table
            Table.RowPitch = 48;
            Table.Layout(client, client.Y + 30, client.Bottom - 5);
            PlanetSL = Add(new ScrollList<PlanetListScreenItem>(Table.ListRect, 44));
            PlanetSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(PlanetSL);

            float lineY = client.Y + 8;
            FilterLineY = lineY;
            cb_hideOwned = Add(new UICheckBox(Table.TableRect.X, lineY,
                () => HideOwned,
                x => { HideOwned = x; ResetList(); }, Fonts.Arial12Bold, "Hide Owned", ""));

            cb_hideUninhabitable = Add(new UICheckBox(Table.TableRect.X + 130, lineY,
                () => HideUninhab,
                x => { HideUninhab = x; ResetList(); }, Fonts.Arial12Bold, "Hide Uninhabitable", ""));

            // proximity and owner filters on the same line (maintainer bench 291)
            ProximityFilter = Add(new DropOptions<string>(new Rectangle((int)Table.TableRect.X + 290, (int)lineY, 110, 18)));
            ProximityFilter.AddOption("All Distances", "");
            foreach (string cat in new[] { "Local", "Near", "Midway", "Distant", "Beyond" })
                ProximityFilter.AddOption(cat, cat);
            ProximityFilter.OnValueChange = _ => ResetList();

            OwnerFilter = Add(new DropOptions<string>(new Rectangle((int)Table.TableRect.X + 410, (int)lineY, 130, 18)));
            OwnerFilter.AddOption("All Owners", "");
            OwnerFilter.AddOption("Unowned", "-");
            var seenOwners = new Array<string>();
            foreach (Planet p in ExploredPlanets)
            {
                string o = p.Owner?.data.Traits.Singular ?? "";
                if (o.NotEmpty() && !seenOwners.Contains(o))
                {
                    seenOwners.Add(o);
                    OwnerFilter.AddOption(o, o);
                }
            }
            OwnerFilter.OnValueChange = _ => ResetList();
        }

        void CalcPlanetsDistances()
        {
            var playerPlanets = Player.GetPlanets();
            foreach (Planet planet in ExploredPlanets)
            {
                if (planet.Owner != Player)
                {
                    float shortestDistance = playerPlanets.Min(p => p.Position.Distance(planet.Position));
                    PlanetDistanceToClosestColony.Add(planet, shortestDistance);
                }
                else
                {
                    PlanetDistanceToClosestColony.Add(planet, 0f);
                }
            }
        }

        float GetShortestDistance(Planet p)
        {
            return PlanetDistanceToClosestColony.TryGetValue(p, out float distance) ?  distance : 0;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first - as a Submenu background it would be
            // drawn among the children, after everything below it.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GalaxyTabs), ScreenGroups.GroupFrameFill);
            base.Draw(batch, elapsed);

            // the shared charte draws the headers, the rule and the separators
            Table.DrawChrome(batch);

            // "Available Troops: N" rides LINE 1 - the filter row - centred over the Send Troops
            // column (Lek's review, bench 305); label vanilla, count white, gray when dry. Ludoal
            // fork (maintainer feedback): "Rebasing: N" follows it (same convention), and the pair
            // is centred as a whole - it carries the count the Homeworld button used to show.
            Graphics.Font font = Fonts.Arial12Bold;
            Rectangle actions = Table.Columns[9].Rect;
            NumRebasingTroops = CountRebasingTroops();

            string availLbl = "Available Troops: ";
            string availVal = NumAvailableTroops.ToString();
            string rebLbl   = "   Rebasing: ";
            string rebVal   = NumRebasingTroops.ToString();
            bool showReb    = NumRebasingTroops > 0;

            float tw = font.TextWidth(availLbl) + font.TextWidth(availVal)
                     + (showReb ? font.TextWidth(rebLbl) + font.TextWidth(rebVal) : 0f);
            float x  = actions.X + actions.Width / 2f - tw / 2f;
            var pos  = new Vector2(x, FilterLineY);

            batch.DrawString(font, availLbl, pos.Rounded(), UITable.Vanilla);
            x += font.TextWidth(availLbl);
            batch.DrawString(font, availVal, new Vector2(x, pos.Y).Rounded(),
                             NumAvailableTroops == 0 ? Color.Gray : Color.White);
            if (showReb)
            {
                x += font.TextWidth(availVal);
                batch.DrawString(font, rebLbl, new Vector2(x, pos.Y).Rounded(), UITable.Vanilla);
                x += font.TextWidth(rebLbl);
                batch.DrawString(font, rebVal, new Vector2(x, pos.Y).Rounded(), Color.White);
            }

            ScreenGroups.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        // Ludoal fork (maintainer feedback): total troops aboard ships rebasing to one of our own
        // worlds - the count the Homeworld's inert "Rebasing" button used to carry.
        int CountRebasingTroops()
        {
            int total = 0;
            foreach (Ship s in Player.OwnedShips)
            {
                if (s?.AI == null || !s.HasOurTroops)
                    continue;
                if (s.AI.OrderQueue.Any(g => g.Plan == ShipAI.Plan.Rebase
                                             && g.TargetPlanet != null && g.TargetPlanet.Owner == Player))
                    total += s.TroopCount;
            }
            return total;
        }

        void Refill(int col, bool ascending)
        {
            PlanetSL.Reset();
            PlanetSL.OnDoubleClick = OnPlanetListItemClicked;
            NumAvailableTroops = Player.NumFreeTroops();
            Planet[] planets;
            switch (col)
            {
                case 1:  planets = ExploredPlanets.Sorted(ascending, p => p.Name); break;
                case 3:  planets = ExploredPlanets.Sorted(ascending, GetShortestDistance); break;
                case 4:  planets = ExploredPlanets.Sorted(ascending, p => p.FertilityFor(Player)); break;
                case 5:  planets = ExploredPlanets.Sorted(ascending, p => p.MineralRichness); break;
                case 6:  planets = ExploredPlanets.Sorted(ascending, p => p.MaxPopulationFor(Player)); break;
                case 7:  planets = ExploredPlanets.Sorted(ascending, p => p.PopulationRatio); break;
                case 8:  planets = ExploredPlanets.Sorted(ascending, p => p.GetOwnerName()); break;
                default: planets = ExploredPlanets.Sorted(ascending, p => p.System.Name); break;
            }
            foreach (Planet p in planets)
            {
                if (ShouldAddItem(p))
                    PlanetSL.AddItem(new PlanetListScreenItem(this, p, GetShortestDistance(p), NumAvailableTroops > 0));
            }
        }

        public override bool HandleInput(InputState input)
        {
            if (EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (PlanetSL.NumEntries == 0)
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

            if (input.KeyPressed(Keys.L) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        void OnPlanetListItemClicked(PlanetListScreenItem item)
        {
            ExitScreen();
            GameAudio.AcceptClick();
            Universe.SetSelectedPlanet(item.Planet);
            Universe.CamDestination = new Vector3d(item.Planet.Position, 10000);
        }

        public void ResetList()
        {
            if (LastSortCol < 0)
            {
                PlanetSL.Reset();
                PlanetSL.OnDoubleClick = OnPlanetListItemClicked; // Ludoal fork: double-click everywhere
                NumAvailableTroops = Player.NumFreeTroops();
                foreach (Planet p in ExploredPlanets)
                {
                    if (ShouldAddItem(p))
                        PlanetSL.AddItem(new PlanetListScreenItem(this, p, GetShortestDistance(p), NumAvailableTroops > 0));
                }
            }
            else
            {
                // re-apply the standing sort with its CURRENT direction
                Refill(LastSortCol, Table.Columns[LastSortCol].Ascending);
            }

            SelectedPlanet = PlanetSL.NumEntries > 0 ? PlanetSL.AllEntries[0].Planet : null;
        }

        public void RefreshSendTroopButtonsVisibility()
        {
            NumAvailableTroops = Player.NumFreeTroops();
            foreach (PlanetListScreenItem item in PlanetSL.AllEntries)
            {
                item.SetCanSendTroops(NumAvailableTroops > 0);
            }
        }

        public bool ShouldAddItem(Planet p)
        {
            // the two dropdown filters (maintainer bench 291)
            string wantProx = ProximityFilter?.ActiveValue ?? "";
            if (wantProx.NotEmpty() && new DistanceDisplay(GetShortestDistance(p) / 1000).Text != wantProx)
                return false;
            string wantOwner = OwnerFilter?.ActiveValue ?? "";
            if (wantOwner == "-" && p.Owner != null)
                return false;
            if (wantOwner.NotEmpty() && wantOwner != "-" && (p.Owner?.data.Traits.Singular ?? "") != wantOwner)
                return false;

            if (!HideOwned && !HideUninhab)                                 return true;
            if (HideOwned && HideUninhab && p.Habitable && p.Owner == null) return true;
            if (HideOwned && !HideUninhab && p.Owner == null)               return true;
            if (!HideOwned && HideUninhab && p.Habitable)                   return true;
            return false;
        }

        // Ludoal fork: the other two tabs live in their own screen, so leaving Planets hands over
        // to it. Planets itself is a no-op: we are already here.
        void OnGalaxyTabChanged(int index)
            => ScreenGroups.SwitchGalaxyTab(index, self: 0, Universe, this);
    }
}
