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
        UIButton MiningAbortButton; // Ludoal fork: mining has TWO buttons - deploy (amber) + abort (red)
        readonly float Distance;
        bool MarkedForResearch;
        bool MarkedForMining;
        bool DysonSwarmActiveByPlayer;
        readonly UniverseState Universe;

        UILabel DeployTextInfo;
        UILabel MiningDeployedTextInfo;
        UILabel Owner;
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
            else if (Planet?.IsMineable == true && Player.AI.Goals.Any(g => g.IsMiningOpsGoal(Planet) && g.TargetShip == null))
            {
                MarkedForMining = true;
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

            if (Planet?.IsResearchable == true || System.IsResearchable)
            {
                bool deployed = SolarBody.IsResearchStationDeployedBy(Player); // built: neither Deploy nor Abort
                ButtonStyle researchStyle = MarkedForResearch || deployed ? ButtonStyle.Military : ButtonStyle.BigDip;
                LocalizedText researchText = deployed ? new LocalizedText("Station Deployed", LocalizationMethod.RawText)
                                           : !MarkedForResearch ? new LocalizedText(GameText.DeployResearchStation) : new LocalizedText(GameText.AbortDeployent);
                DeployButton = Button(researchStyle, researchText, OnResearchClicked);
            }
            else if (Planet?.IsMineable == true)
            {
                // Ludoal fork (maintainer feedback): TWO buttons like the Planet Info cartouche -
                // Deploy (amber, always present, greyed at max, carrying the in-progress count) and
                // a separate Abort (red) that appears once at least one station is deploying and
                // removes them one by one. No more "In Progress" line beside them.
                DeployButton = Button(ButtonStyle.Default, GameText.DeployMiningStation, OnMiningDeployClicked);
                DeployButton.DefaultColor = UIButton.PlateNeutral;   // the Codex amber
                DeployButton.HoverColor   = UITheme.Hover(UIButton.PlateNeutral);
                DeployButton.PressColor   = UITheme.Press(UIButton.PlateNeutral);
                MiningAbortButton = Button(ButtonStyle.Default, GameText.AbortDeployent, OnMiningAbortClicked);
                MiningAbortButton.DefaultColor = UIButton.PlateHostile;
                MiningAbortButton.HoverColor   = UITheme.Hover(UIButton.PlateHostile);
                MiningAbortButton.PressColor   = UITheme.Press(UIButton.PlateHostile);
            }
            else
            {
                ButtonStyle dysonStyle = DysonSwarmActiveByPlayer ? ButtonStyle.Military : ButtonStyle.Default;
                LocalizedText dysonText = !DysonSwarmActiveByPlayer ? GameText.BuildDysonSwarm : GameText.KillDysonSwarm;
                DeployButton = Button(dysonStyle, dysonText, OnDysonSwarmClicked);
            }

            // cells read the shared column geometry
            UITable.Column[] cols = Screen.Table.Columns;
            OrdersRect = new Rectangle(cols[6].Rect.X, y, cols[6].Rect.Width, h);
            PlanetIconRect = new Rectangle(cols[1].Rect.X + UITable.PadX, y + h / 2 - 16, 32, 32);
            ResourceIconRect = new Rectangle(cols[3].Rect.X + UITable.PadX, y + h / 2 - 10, 20, 20);

            // Ludoal fork (maintainer feedback): the button keeps the default font (the earlier
            // narrowing shrank the text); the plate height is the 168px asset's.
            var btn = ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_168px");
            int btnY = OrdersRect.Y + OrdersRect.Height / 2 - btn.Height / 2;
            DeployButton.Rect = new Rectangle(OrdersRect.X + 10, btnY, 168, btn.Height);
            if (MiningAbortButton != null) // mining: Abort sits just right of Deploy
                MiningAbortButton.Rect = new Rectangle(DeployButton.Rect.Right + 6, btnY, 140, btn.Height);

            AddSystemName();
            AddHostileWarning();
            SetResearchVisibility();
            SetMiningVisibility();
            SetDysonSwarmVisibility();
            AddTextureAndStatus();
            AddDistanceStats();
            AddPlanetName();
            AddResourceName();
            AddRichnessStat();
            AddOwner();
            base.PerformLayout();
        }

        void SetResearchVisibility()
        {
            if (!IsForResearch)
                return;

            Vector2 researchTextBox = new Vector2(DeployButton.Rect.X, DeployButton.Rect.Y + 4);
            DeployTextInfo = Add(new UILabel(researchTextBox, GameText.CannotBuildResearchStationTip2, SmallFont));
            DeployTextInfo.Color = Color.Gray;

            if (!Player.CanBuildResearchStations) 
            {
                DeployButton.Visible = false;
                return;
            }

            if (SolarBody.IsResearchStationDeployedBy(Player))
            {
                DeployButton.Visible = false;
                DeployTextInfo.Text = Localizer.Token(GameText.ResearchStationDeployed);
                DeployTextInfo.Color = Color.LimeGreen; // deployed reads green (maintainer feedback)
            }
            else
            {
                DeployButton.Visible = true;
                DeployTextInfo.Visible= false;
            }
        }

        void SetMiningVisibility()
        {
            if (!IsForMining)
                return;

            // a rig owned by someone else: no controls, no counts
            if (Planet.Mining.Owner != null && Planet.Mining.Owner != Player)
            {
                DeployButton.Visible = false;
                MiningAbortButton.Visible = false;
                return;
            }

            // can't build at all: a single greyed hint where the button would be
            if (!Player.CanBuildMiningStations)
            {
                DeployButton.Visible = false;
                MiningAbortButton.Visible = false;
                DeployTextInfo = Add(new UILabel(new Vector2(DeployButton.Rect.X, DeployButton.Rect.Y + 4),
                                                 Localizer.Token(GameText.CannotBuildMiningStationTip2), SmallFont));
                DeployTextInfo.Color = Color.Gray;
                return;
            }

            int numDeployed   = Planet.OrbitalStations.Count(s => s.Loyalty.isPlayer && s.IsMiningStation);
            int numInProgress = Player.AI.CountGoals(g => g.IsMiningOpsGoal(Planet) && g.TargetShip == null);
            bool atMax        = numDeployed >= Mineable.MaximumMiningStations;

            // Ludoal fork (maintainer feedback): at max, the buttons give way to a green text like
            // the research row does - carrying the station count.
            if (atMax)
            {
                DeployButton.Visible = false;
                MiningAbortButton.Visible = false;
                DeployTextInfo = Add(new UILabel(new Vector2(DeployButton.Rect.X, DeployButton.Rect.Y + 4),
                                     $"{numDeployed}/{Mineable.MaximumMiningStations} Mining Stations Deployed", SmallFont));
                DeployTextInfo.Color = Color.LimeGreen;
                return;
            }

            // Deploy carries the in-progress count; Abort shows only when at least one is deploying
            // and removes them one by one. No separate "In Progress" line.
            DeployButton.Visible = true;
            DeployButton.Text = numInProgress > 0
                ? $"{new LocalizedText(GameText.DeployMiningStation).Text} ({numInProgress})"
                : new LocalizedText(GameText.DeployMiningStation).Text;

            MiningAbortButton.Visible = numInProgress > 0;

            // "N/M Deployed" beside the buttons - the stations actually in orbit
            Vector2 deployedPos = new Vector2(MiningAbortButton.Rect.Right + 8,
                                              OrdersRect.Y + OrdersRect.Height / 2 - SmallFont.LineSpacing / 2);
            MiningDeployedTextInfo = Add(new UILabel(deployedPos, $"{numDeployed}/{Mineable.MaximumMiningStations} Deployed", SmallFont));
            MiningDeployedTextInfo.Color = numDeployed > 0 ? Color.Green : Color.Gray;
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
            return base.HandleInput(input);
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

        void OnResearchClicked(UIButton b)
        {
            if (SolarBody.IsResearchStationDeployedBy(Player))
            {
                GameAudio.NegativeClick(); // already built - clicking Deploy again would queue a duplicate goal
                return;
            }
            GameAudio.EchoAffirmative();
            if (!MarkedForResearch)
            {
                if (IsStar)
                    Player.AI.AddGoalAndEvaluate(new ProcessResearchStation(Player, System, System.SelectStarResearchStationPos()));
                else
                    Player.AI.AddGoalAndEvaluate(new ProcessResearchStation(Player, Planet));

                DeployButton.Text = GameText.AbortDeployent;
                DeployButton.Style = ButtonStyle.Military;
                MarkedForResearch = true;
            }
            else
            {
                Player.AI.CancelResearchStation(Planet);
                DeployButton.Text = GameText.DeployResearchStation;
                DeployButton.Style = ButtonStyle.BigDip;
            }
        }

        // Ludoal fork (maintainer feedback): mining now has two dedicated buttons. Deploy queues one
        // more station; Abort cancels one deploying station at a time. The row rebuilds so the
        // counts, the greyed-at-max state and the Abort button's visibility all refresh.
        void OnMiningDeployClicked(UIButton b)
        {
            if (!Planet.Mining.CanAddMiningStationFor(Player))
            {
                GameAudio.NegativeClick();
                return;
            }
            GameAudio.EchoAffirmative();
            Player.AI.AddGoalAndEvaluate(new MiningOps(Player, Planet));
            RequiresLayout = true;
        }

        void OnMiningAbortClicked(UIButton b)
        {
            GameAudio.EchoAffirmative();
            Player.AI.CancelMiningStation(Planet); // one goal at a time
            RequiresLayout = true;
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
                DeployButton.Style = ButtonStyle.Military;
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
