using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.AI;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Commands.Goals;
using Ship_Game.Universe.SolarBodies; // DistanceDisplay
using Ship_Game.UI; // UITable: the shared table charte
using System.Collections;
using System.Web;

namespace Ship_Game
{
    public sealed class ShipListScreenItem : ScrollListItem<ShipListScreenItem>
    {
        public Ship Ship;

        // the shared table charte (Screen.Table) owns the columns; the row only carries
        // its data and its widgets
        Rectangle ShipIconRect;
        readonly UITextEntry ShipNameEntry;
        readonly UIButton RefitButton;
        readonly UIButton ScrapButton;
        readonly UIButton ExploreButton; //Auto-explore button for ShipListScreen
        readonly UIButton PatrolButton;  // patrol is a FLEET mechanic: shown on fleet ships, opens the fleet's patrol-plan picker

        // icon buttons driven by the row: sized by their Rect each layout, actions on OnClick
        static UIButton IconButton(string normal, string hover, string pressed, Action<UIButton> onClick)
            => new(new UIButton.StyleTextures(normal, hover, pressed), Vector2.Zero, "") { OnClick = onClick };

        public ShipListScreen Screen;
        public string StatusText;
        readonly bool IsScuttle;
        readonly bool IsCombat;  //fbedard
        public bool Selected = false;  //fbedard: for multi-select
        private readonly string SystemName;

        public ShipListScreenItem(Ship s, ShipListScreen caller)
        {
            Screen = caller;
            Ship = s;
            StatusText = GetStatusText(Ship);
            SystemName = Ship.System?.Name ?? Localizer.Token(GameText.DeepSpace);

            ShipNameEntry = new UITextEntry(Vector2.Zero, Fonts.Arial12Bold, Ship.ShipName);
            ShipNameEntry.Color = Colors.Cream;
            ShipNameEntry.OnTextChanged = (text) => Ship.VanityName = text;

            if (!Ship.IsPlatformOrStation && !Ship.IsHangarShip
                                          && Ship.ShipData.Role != RoleName.troop
                                          && Ship.AI.State != AIState.Colonize
                                          && Ship.ShipData.Role != RoleName.freighter
                                          && Ship.ShipData.ShipCategory != ShipCategory.Civilian)
                IsCombat = true;

            // widget rects are positioned every frame from the table's columns
            if (IsCombat)
            {
                ExploreButton = IconButton("NewUI/icon_order_explore", "NewUI/icon_order_explore_hover1", "NewUI/icon_order_explore_hover2", OnExploreClicked);
            }
            // built for every row - fleet membership changes at runtime, visibility is
            // decided at draw time (maintainer feedback: wire the disabled upstream button)
            PatrolButton = IconButton("NewUI/icon_order_patrol", "NewUI/icon_order_patrol_hover1", "NewUI/icon_order_patrol_hover2", OnPatrolClicked);
            RefitButton = IconButton("NewUI/icon_queue_rushconstruction", "NewUI/icon_queue_rushconstruction_hover1", "NewUI/icon_queue_rushconstruction_hover2", OnRefitClicked);
            ScrapButton = IconButton("NewUI/icon_queue_delete", "NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2", OnScrapClicked);
            ScrapButton.IconTint = Color.Red; // destruction reads red (maintainer bench 305)

            if (Ship.IsPlatformOrStation || Ship.Stats.Thrust <= 0f)
            {
                IsScuttle = true;
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            UITable table = Screen.Table;
            UITable.Column[] cols = table.Columns;
            int y = (int)Y, h = (int)Height;
            PositionWidgets(cols, y, h);
            StatusText = GetStatusText(Ship); // live: the order changes without a tab rebuild

            if (Selected)
                batch.FillRectangle(new Rectangle(table.TableRect.X, y, table.TableRect.Width, h), Color.DarkGreen);

            // every cell through the shared charte: alignment and padding come from the column
            void Cell(int i, string text, Color color, Graphics.Font font)
                => UITable.DrawCell(batch, font, cols[i].Rect, y, h, text, color, cols[i].Align);

            Cell(0, SystemName, Colors.Cream, Fonts.Arial12Bold);
            // proximity to the nearest colony, live - Deep Space ships have coordinates too
            DistanceDisplay dd = new DistanceDisplay(Screen.DistanceToNearestColony(Ship.Position) / 1000);
            Cell(1, dd.Text, dd.Color, Fonts.Arial12Bold);
            batch.Draw(Ship.ShipData.Icon, ShipIconRect, Color.White);
            ShipNameEntry.Draw(batch, elapsed);
            // the role wears its family's colour: combat purple, troops orange with
            // construction, colony green, scout blue, freighter white (the majority)
            Color roleColor = Ship.IsConstructor || Ship.DesignRole == RoleName.construction ? Color.Orange
                            : Ship.DesignRole == RoleName.troop || Ship.DesignRole == RoleName.troopShip ? Color.IndianRed
                            : Ship.DesignRole == RoleName.scout ? Color.CornflowerBlue
                            : Ship.DesignRole == RoleName.freighter ? Color.White
                            : Ship.DesignRole == RoleName.colony ? Color.LightGreen
                            : IsCombat ? Color.MediumPurple
                            : Colors.Cream;
            Cell(3, Localizer.GetRole(Ship.ShipData.Role, Ship.Loyalty), roleColor, Fonts.Arial12Bold);
            string fleetName = Ship.Fleet?.Name ?? "";
            Graphics.Font fleetFont = Fonts.Arial12Bold.TextWidth(fleetName) > cols[4].Width - 2 * UITable.PadX
                                    ? Fonts.Arial8Bold : Fonts.Arial12Bold;
            Cell(4, fleetName, Colors.Cream, fleetFont);
            Cell(5, Ship.Fleet?.Patrol?.Name ?? "", Colors.Cream, Fonts.Arial12Bold);
            // Orders is foldable: cut to the column with an ellipsis, the tooltip (in
            // HandleInput) carries the whole sentence
            Cell(6, UITable.FitText(Fonts.Arial12, StatusText, cols[6].Width - 2 * UITable.PadX),
                 Colors.Cream, Fonts.Arial12);

            // numeric colours through the shared charte: every zero reads gray
            float maint = Ship.GetMaintCost();
            float str = Ship.GetStrength();
            Cell(8, str.ToString("0"), UITable.ValueColor(TableColor.Plain, str), Fonts.Arial12);
            Cell(9, maint.ToString("F2"), UITable.ValueColor(TableColor.Neutral, maint), Fonts.Arial12);
            Cell(10, string.Concat(Ship.TroopCount, "/", Ship.TroopCapacity),
                 UITable.ValueColor(TableColor.Plain, Ship.TroopCount), Fonts.Arial12);
            Cell(11, (Ship.MaxFTLSpeed / 1000f).ToString("0") + "k",
                 UITable.ValueColor(TableColor.Plain, Ship.MaxFTLSpeed), Fonts.Arial12);
            Cell(12, Ship.MaxSTLSpeed.ToString("0"),
                 UITable.ValueColor(TableColor.Plain, Ship.MaxSTLSpeed), Fonts.Arial12);

            if (IsCombat)
                ExploreButton.Draw(batch, elapsed);
            if (Ship.Fleet != null)
                PatrolButton.Draw(batch, elapsed);
            if (!Ship.IsSubspaceProjector)
                RefitButton.Draw(batch, elapsed);
            ScrapButton.Draw(batch, elapsed);
        }

        // widgets (ship icon, name entry, the order/refit/scrap icon lane) follow the
        // table's columns; every row keeps all four icon slots so nothing shifts
        // between combat and civilian rows
        void PositionWidgets(UITable.Column[] cols, int y, int h)
        {
            Rectangle shipCol = cols[2].Rect;
            ShipIconRect = new Rectangle(shipCol.X + UITable.PadX, y + h / 2 - 14, 28, 28);
            ShipNameEntry.SetPos(ShipIconRect.Right + 6, y + h / 2 - ShipNameEntry.Font.LineSpacing / 2);

            SubTexture exploreTex = ResourceManager.Texture("NewUI/icon_order_explore_hover1");
            SubTexture patrolTex  = ResourceManager.Texture("NewUI/icon_order_patrol_hover1");
            SubTexture refitTex   = ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover2");
            SubTexture scrapTex   = ResourceManager.Texture("NewUI/icon_queue_delete_hover1");
            const int IconGap = 4;
            int iconsW = exploreTex.Width + IconGap + patrolTex.Width + IconGap + refitTex.Width + IconGap + scrapTex.Width;
            Rectangle lane = cols[7].Rect;
            int ix = lane.X + (lane.Width - iconsW) / 2;
            Rectangle IconSlot(SubTexture t)
            {
                var r = new Rectangle(ix, y + h / 2 - t.Height / 2, t.Width, t.Height);
                ix += t.Width + IconGap;
                return r;
            }

            Rectangle explore = IconSlot(exploreTex);
            if (IsCombat)
            {
                ExploreButton.Rect = explore;
                ExploreButton.Tooltip = GameText.OrdersThisShipToExplore;
            }
            PatrolButton.Rect = IconSlot(patrolTex);
            PatrolButton.Tooltip = "Choose a patrol plan for this ship's fleet";
            RefitButton.Rect = IconSlot(refitTex);
            ScrapButton.Rect = IconSlot(scrapTex);
            RefitButton.Tooltip = GameText.OpensAMenuAllowingYou;
            ScrapButton.Tooltip = GameText.OrdersTheShipToReturn;
        }

        void OnExploreClicked(UIButton b)
        {
            if (Ship.AI.State == AIState.Explore)
                Ship.AI.ClearOrders();
            else
                Ship.AI.OrderExplore();
            StatusText = GetStatusText(Ship);
        }

        void OnPatrolClicked(UIButton b)
        {
            Screen.ScreenManager.AddScreen(new ChoosePatrolPlan(Screen.Universe, Ship.Fleet));
        }

        void OnRefitClicked(UIButton b)
        {
            Screen.ScreenManager.AddScreen(new RefitToWindow(Screen, this));
        }

        void OnScrapClicked(UIButton b)
        {
            if (!IsScuttle)
            {
                if (Ship.AI.State == AIState.Scrap)
                {
                    Ship.AI.ClearOrders();
                }
                else if (Screen.Input.IsShiftKeyDown)
                {
                    Screen.Universe.RunOnSimThread(() => Ship.Loyalty.MassScrap(Ship));
                    Screen.Universe.RunOnSimThread(() => Screen.ResetStatus());
                }
                else
                {
                    // OrderScrapShip defers the ScrapShip goal to the sim thread,
                    // so refresh the status only after the goal has actually run
                    Ship.AI.OrderScrapShip();
                    Screen.Universe.RunOnSimThread(() => Screen.ResetStatus());
                }
            }
            else
            {
                if (Ship.ScuttleTimer != -1f)
                {
                    Ship.ScuttleTimer = -1f;
                    Ship.AI.ClearOrders();
                }
                else
                {
                    Ship.ScuttleTimer = 10f;
                    Ship.AI.ClearOrders(AIState.Scuttle, priority:true);
                }
            }
            StatusText = GetStatusText(Ship);
        }

        public static string GetStatusText(Ship ship)
        {
            if (ship.AI == null)  //fbedard: prevent crash ?
                return "";
            switch (ship.AI.State)
            {
                default:
                case AIState.PirateRaiderCarrier:
                case AIState.MineAsteroids:
                case AIState.Intercept:
                case AIState.AssaultPlanet:
                case AIState.Exterminate:
                    if (ship.AI.OrderQueue.TryPeekFirst(out ShipAI.ShipGoal first))
                    {
                        if (first.TargetPlanet == null)
                            return first.Plan.ToString();

                        if (first.Plan == ShipAI.Plan.LandTroop)
                            return $"{Localizer.Token(GameText.LandingTroopsOn)} {first.TargetPlanet.Name}";

                        return first.Plan + " to " + first.TargetPlanet.Name;
                    }
                    return ship.AI.State.ToString();

                case AIState.Combat:
                {
                    if (ship.AI.Intercepting)
                    {
                        if (ship.AI.Target == null)
                            return "";
                        return string.Concat(Localizer.Token(GameText.Intercepting), " ", (ship.AI.Target as Ship).VanityName);
                    }

                    if (ship.AI.Target == null)
                        return string.Concat(Localizer.Token(GameText.InCombat), "\n", Localizer.Token(GameText.SearchingForTargets));

                    return string.Concat(Localizer.Token(GameText.InCombatWith), " ", (ship.AI.Target as Ship).Loyalty.data.Traits.Name);
                }
                case AIState.HoldPosition:   return Localizer.Token(GameText.HoldingPosition);
                case AIState.AwaitingOrders: return Localizer.Token(GameText.AwaitingOrders);
                case AIState.AttackTarget:
                    if (ship.AI.Target == null)
                        return string.Concat(Localizer.Token(GameText.InCombat), "\n", Localizer.Token(GameText.SearchingForTargets));
                    return string.Concat(Localizer.Token(GameText.Attacking), " ", (ship.AI.Target as Ship).VanityName);
                case AIState.Escort:
                    if (ship.AI.EscortTarget == null)
                        return "";
                    return string.Concat(Localizer.Token(GameText.Escorting), " ", ship.AI.EscortTarget.Name);
                case AIState.SystemTrader:
                    if (ship.AI.OrderQueue.TryPeekLast(out ShipAI.ShipGoal last2))
                    {
                        string goodsType = last2.Trade?.Goods.ToString();
                        string blockade  = last2.Trade?.BlockadeTimer < 120 ? Localizer.Token(GameText.Blockade) : "";
                        string status    = "";
                        switch (last2.Plan)
                        {
                            case ShipAI.Plan.PickupGoods:  status = Localizer.Token(GameText.PickingUp); break;
                            case ShipAI.Plan.DropOffGoods: status = Localizer.Token(GameText.Delivering); break;
                        }
                        // status is empty outside Pickup/DropOff - build without the stray
                        // leading space it used to leave ahead of "Production from ..."
                        string head = status.IsEmpty() ? goodsType : $"{status} {goodsType}";
                        return $"{head} from {last2.Trade?.ExportFrom.Name} to {last2.Trade?.ImportTo?.Name ?? last2.Trade?.TargetStation.Name} {blockade}".TrimEnd();
                    }
                    return $"{Localizer.Token(GameText.TradingGoods)} \n {Localizer.Token(GameText.SeekingRoute)}";
                case AIState.Research:
                    if (ship.AI.OrderQueue.TryPeekLast(out ShipAI.ShipGoal researchOrder))
                    {
                        GameText researchStatus;
                        switch (researchOrder.Plan) 
                        {
                            default:
                            case ShipAI.Plan.ResearchStationResearching: researchStatus = GameText.ResearchPlanResearching; break;
                            case ShipAI.Plan.ResearchStationIdle:        researchStatus = GameText.ResearchPlanIdle;        break;
                            case ShipAI.Plan.ExoticStationNoSupply:      researchStatus = GameText.ExoticPlanNoSupply;      break;
                        }
                        return Localizer.Token(researchStatus);
                    }
                    return "";
                case AIState.Mining:
                    if (ship.AI.OrderQueue.TryPeekLast(out ShipAI.ShipGoal mineOrder))
                    {
                        GameText miningStatus;
                        switch (mineOrder.Plan)
                        {
                            default:
                            case ShipAI.Plan.MiningStationRefining:    miningStatus = GameText.MiningPlanRefining; break;
                            case ShipAI.Plan.MiningStationIdle:        miningStatus = GameText.MiningPlanIdle;     break;
                            case ShipAI.Plan.ExoticStationNoSupply:    miningStatus = GameText.ExoticPlanNoSupply; break;
                            case ShipAI.Plan.MinePlanet:               miningStatus = GameText.MiningPlanetStatus; break;
                            case ShipAI.Plan.MiningStationNotOpsOwner: miningStatus = GameText.MiningPlanetStatus; break;
                        }
                        return Localizer.Token(miningStatus);
                    }
                    return "";
                case AIState.AttackRunner:
                case AIState.PatrolSystem:
                case AIState.Flee:                
                    if (ship.AI.OrbitTarget == null)
                        return Localizer.Token(GameText.Orbiting);
                    return string.Concat("Fleeing to", " ", ship.AI.OrbitTarget.Name);
                case AIState.Orbit:
                    if (ship.AI.OrbitTarget == null)
                        return Localizer.Token(GameText.Orbiting);

                    Planet planet    = ship.AI.OrbitTarget;
                    string orbitText = $"{Localizer.Token(GameText.Orbiting)} ";
                    if (!ship.AI.HasPriorityOrder && ship.Position.Distance(planet.Position) > planet.Radius * 3)
                        orbitText = $"{Localizer.Token(GameText.Offensively)} {orbitText}"; // offensive move to orbit

                    return $"{orbitText} {planet.Name}";
                case AIState.Colonize:
                    if (ship.AI.ColonizeTarget == null)
                        return "";

                    return string.Concat(Localizer.Token(GameText.EnRouteToColonize), " ", ship.AI.ColonizeTarget.Name);
                case AIState.MoveTo:
                    if (ship.Velocity.NotZero())
                    {
                        string moveText = $"{Localizer.Token(GameText.MovingTo)} ";
                        if (!ship.AI.HasPriorityOrder)
                            moveText = $"{Localizer.Token(GameText.Offensively)} {moveText}"; // offensive move

                        if (!ship.AI.OrderQueue.TryPeekLast(out ShipAI.ShipGoal last))
                        {
                            // Ludoal fork: FindClosestSystem is null in a system-less
                            // universe (battle simulator arena) — NullRef froze the UI
                            SolarSystem system = ship.Universe.FindClosestSystem(ship.AI.MovePosition);
                            if (system != null && system.IsExploredBy(ship.Universe.Player))
                                return string.Concat(moveText, Localizer.Token(GameText.DeepSpaceNear), " ", system.Name);
                            return Localizer.Token(GameText.ExploringTheGalaxy);
                        }
                        if (last.Plan == ShipAI.Plan.DeployStructure || last.Plan == ShipAI.Plan.DeployOrbital)
                        {
                            moveText += Localizer.Token(GameText.Deploy);
                            if (last.Goal is DeepSpaceBuildGoal b)
                                moveText += " " + b.ToBuild.Name;
                            return moveText;
                        }
                        else
                        {
                            SolarSystem system = ship.Universe.FindClosestSystem(ship.AI.MovePosition);
                            if (system != null && system.IsExploredBy(ship.Universe.Player))
                                return moveText + system.Name;
                            return Localizer.Token(GameText.ExploringTheGalaxy);
                        }
                    }
                    return Localizer.Token(GameText.HoldingPosition);
                case AIState.Explore:        return Localizer.Token(GameText.ExploringTheGalaxy);
                case AIState.Resupply:
                    if (ship.AI.ResupplyTarget == null)
                        return Localizer.Token(GameText.ReturningToBaseForResupply);
                    return string.Concat(Localizer.Token(GameText.ResupplyingAt), " ", ship.AI.ResupplyTarget.Name);
                case AIState.Rebase:
                    var planetName = ship.AI.OrderQueue.PeekLast?.TargetPlanet.Name;                    
                    return Localizer.Token(GameText.TransferringTroops) + $" to {planetName ?? "ERROR" }";  //fbedard
                case AIState.RebaseToShip:
                    return Localizer.Token(GameText.TransferringTroops) + $" to {ship.AI.EscortTarget?.VanityName ?? "ERROR" }";  
                case AIState.Bombard:
                    if (ship.AI.OrderQueue.IsEmpty || ship.AI.OrderQueue.PeekFirst.TargetPlanet == null)
                        return "";
                    if (ship.Position.Distance(ship.AI.OrderQueue.PeekFirst.TargetPlanet.Position) >= 2500f)
                        return string.Concat(Localizer.Token(GameText.EnRouteToBombard), " ", ship.AI.OrderQueue.PeekFirst.TargetPlanet.Name);
                    return string.Concat(Localizer.Token(GameText.Bombarding), " ", ship.AI.OrderQueue.PeekFirst.TargetPlanet.Name);
                case AIState.Boarding:         return Localizer.Token(GameText.ExecutingBoardingAssaultAction);
                case AIState.ReturnToHangar:   return Localizer.Token(GameText.ReturningToHangar);
                case AIState.Ferrying:
                    if (!ship.AI.OrderQueue.TryPeekFirst(out ShipAI.ShipGoal goal) || goal.Plan != ShipAI.Plan.BuildOrbital)
                        return Localizer.Token(GameText.FerryingOrdnance);
                    return "Building Structure";
                case AIState.Refit:            return ship.IsPlatformOrStation ? Localizer.Token(GameText.WaitingForRefitShip) : Localizer.Token(GameText.MovingToShipyardForRefit);
                case AIState.FormationMoveTo:    return "Moving in Formation";
                case AIState.Scuttle:          return "Self Destruct: " + ship.ScuttleTimer.ToString("#");
                case AIState.ReturnHome:       return "Defense Ship Returning Home";
                case AIState.SupplyReturnHome: return "Supply Ship Returning Home";
                case AIState.Scrap:
                    string scrapInPlanet = ship.AI.OrbitTarget != null ? $" in {ship.AI.OrbitTarget.Name}" : "";
                    return Localizer.Token(GameText.ScrappingShip) + scrapInPlanet;
            }
        }

        public override bool HandleInput(InputState input)
        {
            // a folded Orders cell explains itself: full sentence on hover when it was cut
            UITable.Column orders = Screen.Table.Columns[6];
            if (orders.Folded && StatusText.NotEmpty()
                && new Rectangle(orders.Rect.X, (int)Y, orders.Rect.Width, (int)Height).HitTest(input.CursorPosition)
                && Fonts.Arial12.TextWidth(StatusText) > orders.Rect.Width - 2 * UITable.PadX)
            {
                ToolTip.CreateTooltip(StatusText);
            }

            // actions live on the buttons' OnClick now; the guards still gate whether a
            // button takes input at all, and a consumed press stops the row underneath
            if (IsCombat && ExploreButton.HandleInput(input))
                return true;

            // patrol rides the ship's FLEET: the picker loads a patrol plan for it
            if (Ship.Fleet != null && PatrolButton.HandleInput(input))
                return true;

            if (!Ship.IsSubspaceProjector && RefitButton.HandleInput(input))
                return true;

            if (ScrapButton.HandleInput(input))
                return true;

            return base.HandleInput(input);
        }


    }
}
