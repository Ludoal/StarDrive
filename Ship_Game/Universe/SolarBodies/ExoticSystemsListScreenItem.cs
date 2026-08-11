using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.AI;
using Ship_Game.Audio;
using Ship_Game.Commands.Goals;
using Ship_Game.Ships;
using System.Linq;
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Graphics;
using Ship_Game.Universe.SolarBodies;
using Ship_Game.Universe;
using Ship_Game.UI; // UITable: the shared table charte
using System;

namespace Ship_Game
{
    public sealed class ExoticSystemsListScreenItem : ScrollListItem<ExoticSystemsListScreenItem> // Moved to UI V2
    {
        public readonly Planet Planet;
        public readonly SolarSystem System;

        // the shared table charte (Screen.Table) owns the columns
        readonly ExoticSystemsListScreen Screen;
        Rectangle OrdersRect; // the Actions column band of this row, for the deploy widgets

        Empire Player => Universe.Player;
        readonly Color Cream = Colors.Cream;
        // a body's NAME reads a step larger than the body text, its class a plain regular
        // (maintainer, 4 Aug - down from the old Arial20)
        readonly Graphics.Font NameFont = Fonts.Arial14Bold;
        readonly Graphics.Font SmallFont = Fonts.Arial12Bold;
        readonly Graphics.Font ClassFont = Fonts.Arial12;
        readonly Color TextColor = new Color(255, 239, 208);

        Rectangle PlanetIconRect;
        Rectangle ResourceIconRect;
        UIButton DeployButton;
        readonly float Distance;
        bool MarkedForResearch;
        bool DysonSwarmActiveByPlayer;
        readonly UniverseState Universe;

        UILabel Owner;
        // bench 399 (maintainer): the deploy buttons are a fixed-slot icon lane now, the
        // Planets-list convention. Research: white = deploy, RED = abort. Mining: left-click
        // adds, right-click cancels one deploying; gone once every station is deployed.
        // Icons show only with their tech. The Stations column carries the state as text.
        Rectangle ResearchIconRect;
        Rectangle MiningIconRect;
        UIPanel ResearchPanel;
        UIPanel MiningPanel;
        UILabel StationsLabel;
        bool IsPlanet => Planet != null;
        public bool IsStar => Planet == null;
        public bool IsForResearch => IsStar && System.IsResearchable || Planet?.IsResearchable == true;
        public bool IsForMining => !IsStar && Planet.IsMineable;
        public bool IsForDysonSwarm => IsStar && System.DysonSwarmType > 0;
        ExplorableGameObject SolarBody;

        public ExoticSystemsListScreenItem(ExoticSystemsListScreen screen, ExplorableGameObject solarBody, float distance)
        {
            Screen = screen;
            SolarBody = solarBody;
            if (solarBody is Planet planet) 
            {
                Planet   = planet;
                System   = planet.System;
                Universe = planet.Universe;
            }
            else
            {
                SolarSystem system = solarBody as SolarSystem;
                System   = system;
                Universe = system.Universe;
            }

            Distance = distance / 1000; // Distance from nearest player colony


            if (solarBody.IsResearchable && !solarBody.IsResearchStationDeployedBy(Player))
            {
                foreach (Goal g in Player.AI.Goals)
                {
                    if (g.IsResearchStationGoal(solarBody))
                    {
                        MarkedForResearch = true;
                        break;
                    }
                }
            }
            else if (Planet?.IsMineable == true)
            {
                // a mining row: its deploy/abort state is read live from the goal count in
                // SetMiningVisibility, so nothing is cached here - the branch keeps mining rows
                // out of the Dyson case below.
            }
            else if (Player.CanBuildDysonSwarmIn(System))
            {
                DysonSwarmActiveByPlayer = System.HasDysonSwarm && System.DysonSwarm.Owner == Player;
            }
        }

        public override void PerformLayout()
        {
            int y = (int)Y;
            int h = (int)Height;
            RemoveAll();

            // cells read the shared column geometry; Actions is col 6, Stations col 7
            UITable.Column[] cols = Screen.Table.Columns;
            Rectangle actionsCol = cols[6].Rect;
            OrdersRect = new Rectangle(cols[7].Rect.X, y, cols[7].Rect.Width, h);
            PlanetIconRect = new Rectangle(cols[1].Rect.X + UITable.PadX, y + h / 2 - 16, 32, 32);
            ResourceIconRect = new Rectangle(cols[3].Rect.X + UITable.PadX, y + h / 2 - 10, 20, 20);

            // fixed icon slots, centred as a pair (research left, mining right) - a row uses
            // the slots its nature calls for, and an icon only shows with its tech
            const int IconSize = 22, IconGap = 8;
            int laneW = 2 * IconSize + IconGap;
            int slotX = actionsCol.X + (actionsCol.Width - laneW) / 2;
            int iy = y + h / 2 - IconSize / 2;
            ResearchIconRect = new Rectangle(slotX, iy, IconSize, IconSize);
            MiningIconRect   = new Rectangle(slotX + IconSize + IconGap, iy, IconSize, IconSize);

            // the state text, centred in the Stations column, refreshed live
            StationsLabel = Add(new UILabel(SmallFont) { Color = Color.White, TextAlign = TextAlign.HorizontalCenter });
            StationsLabel.Pos  = new Vector2(OrdersRect.X, (int)(Y + Height / 2f - SmallFont.LineSpacing / 2f));
            StationsLabel.Size = new Vector2(OrdersRect.Width, SmallFont.LineSpacing);

            if (IsForResearch)
            {
                ResearchPanel = Panel(ResearchIconRect, Color.White, ResourceManager.Texture("NewUI/icon_science"));
                RefreshResearchState();
            }
            else if (IsForMining)
            {
                MiningPanel = Panel(MiningIconRect, Color.White, ResourceManager.Texture("Buildings/icon_fission_mine_48x48"));
                RefreshMiningState();
            }
            else
            {
                // the Dyson swarm keeps its wide button, riding the Stations column
                ButtonStyle dysonStyle = DysonSwarmActiveByPlayer ? ButtonStyle.DefaultHostile : ButtonStyle.Default;
                LocalizedText dysonText = !DysonSwarmActiveByPlayer ? GameText.BuildDysonSwarm : GameText.KillDysonSwarm;
                DeployButton = Button(dysonStyle, dysonText, OnDysonSwarmClicked);
                var btn = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_168px");
                int btnY = OrdersRect.Y + OrdersRect.Height / 2 - btn.Height / 2;
                DeployButton.Rect = new Rectangle(OrdersRect.X + 10, btnY, 168, btn.Height);
                SetDysonSwarmVisibility();
            }

            AddSystemName();
            AddHostileWarning();
            AddTextureAndStatus();
            AddDistanceStats();
            AddPlanetName();
            AddResourceName();
            AddRichnessStat();
            AddOwner();
            base.PerformLayout();
        }

        void SetDysonSwarmVisibility()
        {
            if (!IsForDysonSwarm)
                return;

            if (System.HasDysonSwarm && System.DysonSwarm.Owner != Player
                || Player.data.Traits.DysonSwarmType < System.DysonSwarmType
                || !System.HasPlanetsOwnedBy(Player))
            {
                DeployButton.Visible = false;
            }
        }

        public override bool HandleInput(InputState input)
        {
            if (ResearchPanel is { Visible: true } && ResearchIconRect.HitTest(input.CursorPosition)
                && input.LeftMouseClick)
            {
                OnResearchIconClicked();
                return true;
            }
            if (HandleMiningClick(input))
                return true;
            return base.HandleInput(input);
        }

        float LiveRefreshTimer;
        int LiveStateHash = -1;

        int ComputeLiveHash()
        {
            int h = 17;
            if (IsForResearch)
            {
                h = h * 31 + (SolarBody.IsResearchStationDeployedBy(Player) ? 1 : 0);
                h = h * 31 + (Player.CanBuildResearchStations ? 1 : 0);
            }
            if (IsForMining)
            {
                h = h * 31 + Planet.OrbitalStations.Count(st => st.Loyalty.isPlayer && st.IsMiningStation);
                h = h * 31 + Player.AI.CountGoals(g => g.IsMiningOpsGoal(Planet) && g.TargetShip == null);
                // the branches only distinguish nobody / us / someone else
                h = h * 31 + (Planet.Mining.Owner == Player ? 1 : Planet.Mining.Owner != null ? 2 : 0);
                h = h * 31 + (Player.CanBuildMiningStations ? 1 : 0);
            }
            if (IsForDysonSwarm)
            {
                h = h * 31 + (System.HasDysonSwarm ? 1 : 0);
                h = h * 31 + (System.HasPlanetsOwnedBy(Player) ? 1 : 0);
            }
            return h;
        }

        void AddSystemName()
        {
            UITable.Column c = Screen.Table.Columns[0];
            Label(UITable.CellPos(SmallFont, c.Rect, Y, Height, System.Name, c.Align), System.Name, SmallFont, Cream);
        }

        void AddPlanetName()
        {
            // two lines: the NAME a step larger (owner-coloured for a claimed body), the
            // CLASS under it in plain regular, without the richness - it has its own
            // column now (maintainer, 4 Aug)
            var namePos = new Vector2(PlanetIconRect.Right + 8, Y + Height / 2 - (NameFont.LineSpacing + ClassFont.LineSpacing + 2) / 2);
            if (IsStar)
            {
                Label(namePos, System.Name, NameFont, TextColor);
                namePos.Y += NameFont.LineSpacing + 2;
                Label(namePos, StarClassName(System.Sun.Id), ClassFont, Color.Gray);
                return;
            }

            Color nameColor = Planet.Mining?.HasOpsOwner == true ? Planet.Mining.Owner.EmpireColor : TextColor;
            Label(namePos, Planet.Name, NameFont, nameColor);
            namePos.Y += NameFont.LineSpacing + 2;
            // class with its richness WORD - the mineable variant appends " (8.2)" and
            // that number lives in its own column (bench 293)
            string cls = Planet.LocalizedRichness;
            int par = cls.IndexOf(" (");
            if (par >= 0) cls = cls.Substring(0, par);
            Label(namePos, cls, ClassFont, Color.Gray);
        }

        // "star_red3" -> "Red", "Blue_giant" -> "Blue Giant": the sun ids ARE the game's star
        // taxonomy, trailing digits being art variants of the same class
        static string StarClassName(string sunId)
        {
            string s = sunId.TrimEnd('0','1','2','3','4','5','6','7','8','9');
            if (s.StartsWith("star_"))
                s = s.Substring(5);
            string[] words = s.Split('_');
            for (int i = 0; i < words.Length; i++)
                if (words[i].Length > 0)
                    words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            return string.Join(" ", words);
        }

        void AddDistanceStats()
        {
            UITable.Column c = Screen.Table.Columns[2];
            DistanceDisplay dd = new DistanceDisplay(Distance);
            if (Distance > 0)
                Label(UITable.CellPos(SmallFont, c.Rect, Y, Height, dd.Text, c.Align), dd.Text, SmallFont, dd.Color);
        }

        void AddResourceName()
        {
            bool researchable = IsForResearch;
            bool mineable = IsForMining;
            var namePos = new Vector2(ResourceIconRect.Right + 8, Y + Height / 2 - SmallFont.LineSpacing / 2);
            Color labelColor = researchable ? Color.CornflowerBlue
                                            : mineable ? Color.White 
                                                       : Color.Gold; // Dyson Swarm
            var resourceName = Label(namePos, GetResourceLabel(), SmallFont, labelColor);

            resourceName.Tooltip = researchable ? new LocalizedText(GameText.ResearchPointsAreAddedInto) 
                                                : mineable ? Planet.Mining.ResourceDescription
                                                           : "";

            Panel(ResourceIconRect, IsForDysonSwarm ? Color.Yellow : Color.White, researchable 
                ? ResourceManager.Texture("NewUI/icon_science") 
                : mineable ? Planet.Mining.ExoticResourceIcon
                           : ResourceManager.Texture("NewUI/icon_projection"));
        }

        string GetResourceLabel()
        {
            return IsForResearch ? "Research"
                                 : IsForMining ? Planet.Mining.TranslatedResourceName.Text
                                               : $"{Localizer.Token(GameText.DysonSwarm)} {System.DysonSwarmType}";
        }

        void AddRichnessStat()
        {
            string richness = IsStar || Planet.IsResearchable ? "" : Planet.Mining.Richness.String(0);
            UITable.Column c = Screen.Table.Columns[4];
            Label(UITable.CellPos(SmallFont, c.Rect, Y, Height, richness, c.Align), richness, SmallFont, Cream);
        }

        void AddOwner()
        {
            // an unclaimed body shows NOTHING (maintainer bench 291: "None" read like a
            // white-named race)
            UITable.Column c = Screen.Table.Columns[5];
            if (IsForDysonSwarm && System.HasDysonSwarm)
            {
                string owner = System.DysonSwarm.Owner.data.Traits.Singular;
                Owner = Label(UITable.CellPos(SmallFont, c.Rect, Y, Height, owner, c.Align), owner, SmallFont,
                              System.DysonSwarm.Owner.EmpireColor);
            }
            else if (Planet?.IsMineable == true && Planet.Mining.HasOpsOwner)
            {
                string owner = Planet.Mining.Owner.data.Traits.Singular;
                Owner = Label(UITable.CellPos(SmallFont, c.Rect, Y, Height, owner, c.Align), owner, SmallFont,
                              Planet.Mining.Owner.EmpireColor);
            }
        }

        void AddHostileWarning()
        {
            if (Player.KnownEnemyStrengthIn(System) > 0)
            {
                Rectangle c0 = Screen.Table.Columns[0].Rect;
                SubTexture flash = ResourceManager.Texture("Ground_UI/EnemyHere");
                UIPanel enemyHere = Panel(c0.Right - 22, (int)Y + 5, flash);
                enemyHere.Tooltip = GameText.IndicatesThatHostileForcesWere;
            }
        }

        void AddTextureAndStatus()
        {
            Add(new UIPanel(PlanetIconRect, ResourceManager.Texture(IsStar ? System.Sun.IconPath : Planet.IconPath))
            {
                Tooltip = GameText.PlanetTypeAndRichnessThe
            });
        }

        void OnResearchIconClicked()
        {
            if (SolarBody.IsResearchStationDeployedBy(Player))
            {
                GameAudio.NegativeClick(); // already built
                return;
            }
            GameAudio.EchoAffirmative();
            if (!MarkedForResearch)
            {
                if (IsStar)
                    Player.AI.AddGoalAndEvaluate(new ProcessResearchStation(Player, System, System.SelectStarResearchStationPos()));
                else
                    Player.AI.AddGoalAndEvaluate(new ProcessResearchStation(Player, Planet));
            }
            else
            {
                // ⚠ per-nature overloads: the old code cancelled with the Planet overload on
                // STAR rows too (a null planet), so the abort silently did nothing there -
                // the table/cartouche desync the bench reported
                if (IsStar) Player.AI.CancelResearchStation(System);
                else        Player.AI.CancelResearchStation(Planet);
            }
            RefreshResearchState(); // the icon flips white/red immediately
        }

        // ⚠ the mining widgets refresh EVERY FRAME (like the Planet Info cartouche, which recomputes
        // its state in Draw), NOT only on click. Refreshing at click-time alone showed the previous
        // state until the NEXT click: AddGoalAndEvaluate takes effect after HandleInput, so the
        // click-time count was stale by one. Reading it per-frame in Update always shows current.
        public override void Update(float fixedDeltaTime)
        {
            if (IsForMining)
                RefreshMiningState();
            if (IsForResearch)
                RefreshResearchState(); // per-frame like the star cartouche - never out of sync
            // the OTHER live states (rig owner flip, dyson) rebuild the row on change -
            // throttled, free while nothing moves.
            LiveRefreshTimer -= fixedDeltaTime;
            if (LiveRefreshTimer <= 0f)
            {
                LiveRefreshTimer = 0.5f;
                int h = ComputeLiveHash();
                if (h != LiveStateHash)
                {
                    LiveStateHash = h;
                    PerformLayout();
                }
            }
            base.Update(fixedDeltaTime);
        }

        // left = add a station, right = cancel one deploying. Returns true if it consumed the click.
        // left = add a station, right = cancel one deploying - on the mining ICON now
        bool HandleMiningClick(InputState input)
        {
            if (MiningPanel is not { Visible: true } || !MiningIconRect.HitTest(input.CursorPosition))
                return false;

            if (input.LeftMouseClick)
            {
                if (Planet.Mining.CanAddMiningStationFor(Player))
                {
                    Player.AI.AddGoalAndEvaluate(new MiningOps(Player, Planet));
                    GameAudio.EchoAffirmative();
                }
                else GameAudio.NegativeClick();
                return true;
            }
            if (input.RightMouseClick)
            {
                if (Player.AI.CountGoals(g => g.IsMiningOpsGoal(Planet) && g.TargetShip == null) > 0)
                {
                    Player.AI.CancelMiningStation(Planet);
                    GameAudio.EchoAffirmative();
                }
                else GameAudio.NegativeClick();
                return true;
            }
            return false;
        }

        // drive the mining icon and the Stations text on the EXISTING widgets - no rebuild
        void RefreshMiningState()
        {
            if (MiningPanel == null)
                return;
            int numDeployed   = Planet.OrbitalStations.Count(st => st.Loyalty.isPlayer && st.IsMiningStation);
            int numInProgress = Player.AI.CountGoals(g => g.IsMiningOpsGoal(Planet) && g.TargetShip == null);
            int max = Mineable.MaximumMiningStations;
            bool rigOk = Planet.Mining.Owner == null || Planet.Mining.Owner == Player;

            // the icon needs its tech, a free (or our) rig, and room left to deploy
            MiningPanel.Visible = Player.CanBuildMiningStations && rigOk && numDeployed < max;
            MiningPanel.Tooltip = new LocalizedText(GameText.DeployMiningStation);

            StationsLabel.Text = numInProgress > 0 || numDeployed > 0
                               ? $"Deploying: {numInProgress} - Deployed: {numDeployed}/{max}" : "";
        }

        // drive the research icon (white = deploy, red = abort) and the Stations text
        void RefreshResearchState()
        {
            if (ResearchPanel == null)
                return;
            bool deployed = SolarBody.IsResearchStationDeployedBy(Player);
            MarkedForResearch = !deployed && Player.AI.HasGoal(g => g.IsResearchStationGoal(SolarBody));

            ResearchPanel.Visible = Player.CanBuildResearchStations && !deployed;
            ResearchPanel.Color   = MarkedForResearch ? Color.Red : Color.White;
            ResearchPanel.Tooltip = MarkedForResearch ? new LocalizedText(GameText.AbortDeployent)
                                                      : new LocalizedText(GameText.DeployResearchStation);

            StationsLabel.Text = deployed ? "Research Station" : "";
        }

        void OnDysonSwarmClicked(UIButton b)
        {
            if (System.HasDysonSwarm)
            {
                System.KillDysonSwarm();
                DeployButton.Text = GameText.BuildDysonSwarm;
                DeployButton.Style = ButtonStyle.Default;
                DysonSwarmActiveByPlayer = false;
                if (Owner != null)
                    Owner.Text = ""; // unclaimed shows nothing
            }
            else
            {
                System.ActivateDysonSwarm(Player);
                DeployButton.Text = GameText.KillDysonSwarm;
                DeployButton.Style = ButtonStyle.DefaultHostile;
                DysonSwarmActiveByPlayer = true;
                if (Owner != null)
                {
                    Owner.Text = Player.data.Traits.Singular;
                    Owner.Color = Player.EmpireColor;
                }
                else
                {
                    RequiresLayout = true; // the row had no owner label - rebuild creates it
                }
            }
        }
    }
}
