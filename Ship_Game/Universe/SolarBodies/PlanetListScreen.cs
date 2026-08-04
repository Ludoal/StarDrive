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
        int LastSortCol = -1;

        private UICheckBox cb_hideOwned;
        private UICheckBox cb_hideUninhabitable;

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
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.System), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Planet), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Proximity), Width = 90, Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_food"), Width = 60,
                                     Align = TableAlign.Number, Sortable = true, Tip = Localizer.Token(GameText.Fertility) },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_production"), Width = 60,
                                     Align = TableAlign.Number, Sortable = true, Tip = Localizer.Token(GameText.Richness) },
                new UITable.Column { Title = "Features", Width = 130 },
                new UITable.Column { Icon = ResourceManager.Texture("UI/icon_pop_22"), Width = 90,
                                     Align = TableAlign.Number, Sortable = true, Tip = Localizer.Token(GameText.MaxPopulation) },
                new UITable.Column { Title = "Ratio", Width = 60, Align = TableAlign.Number, Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Owner), Sortable = true },
                new UITable.Column { Width = 2 * UITable.PadX + 2 * OrdersSlotW + 6, Align = TableAlign.Center },
            });
            var sys = new Array<string>(); var names = new Array<string>();
            var pops = new Array<string>(); var owners = new Array<string>();
            foreach (Planet p in ExploredPlanets)
            {
                sys.Add(p.System.Name);
                names.Add(p.Name);
                string ps = p.PopulationStringForPlayer;
                int paren = ps.IndexOf(" (");
                pops.Add(paren < 0 ? ps : ps.Substring(0, paren));
                owners.Add(p.GetOwnerName());
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, sys);
            Table.Columns[0].Width += 24; // the hostile-warning icon lane
            UITable.AutoSize(Table.Columns[1], Fonts.Arial14Bold, names);
            Table.Columns[1].Width += 46 + 40; // planet icon ahead, status icons behind
            UITable.AutoSize(Table.Columns[6], Fonts.Arial12Bold, pops);
            UITable.AutoSize(Table.Columns[8], Fonts.Arial12Bold, owners);
            Table.FitToWidth((int)(Math.Min(ScreenWidth, 1920) - 2 * ScreenGroups.FrameMargin) - 66);

            float fullAvail = ScreenHeight - ScreenGroups.TabRowY - ScreenGroups.FrameMargin;
            float contentH = Math.Min(fullAvail, 96 + Math.Max(3, ExploredPlanets.Count) * 44);
            GalaxyTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.GalaxyTabTitles, 0,
                                                   OnGalaxyTabChanged, Table.ContentWidth, contentH);
            RectF client = GalaxyTabs.ClientArea;
            // the filter lane, then the table
            Table.Layout(client, client.Y + 30, client.Bottom - 5);
            PlanetSL = Add(new ScrollList<PlanetListScreenItem>(Table.ListRect, 44));
            PlanetSL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(PlanetSL);

            float lineY = client.Y + 8;
            cb_hideOwned = Add(new UICheckBox(Table.TableRect.X, lineY,
                () => HideOwned,
                x => { HideOwned = x; ResetList(); }, Fonts.Arial12Bold, "Hide Owned", ""));

            cb_hideUninhabitable = Add(new UICheckBox(Table.TableRect.X + 130, lineY,
                () => HideUninhab,
                x => { HideUninhab = x; ResetList(); }, Fonts.Arial12Bold, "Hide Uninhabitable", ""));
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

            // "Available Troops: N" rides the header band of the buttons column
            // (maintainer, 4 Aug): label vanilla, the count white - gray when dry
            Graphics.Font font = Fonts.Arial12Bold;
            Rectangle actions = Table.Columns[9].Rect;
            string lbl = "Available Troops: ";
            string val = NumAvailableTroops.ToString();
            float tw = font.TextWidth(lbl) + font.TextWidth(val);
            var pos = new Vector2(actions.X + actions.Width / 2f - tw / 2f, Table.HeaderY);
            batch.DrawString(font, lbl, pos.Rounded(), UITable.Vanilla);
            batch.DrawString(font, val, new Vector2(pos.X + font.TextWidth(lbl), pos.Y).Rounded(),
                             NumAvailableTroops == 0 ? Color.Gray : Color.White);

            ScreenGroups.DrawGalaxyTabTip(GalaxyTabs, Input.CursorPosition);
            EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
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
                case 2:  planets = ExploredPlanets.Sorted(ascending, GetShortestDistance); break;
                case 3:  planets = ExploredPlanets.Sorted(ascending, p => p.FertilityFor(Player)); break;
                case 4:  planets = ExploredPlanets.Sorted(ascending, p => p.MineralRichness); break;
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
