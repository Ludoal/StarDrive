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
using System.Collections;
using System.Web;

namespace Ship_Game
{
    public sealed class ShipListScreenItem : ScrollListItem<ShipListScreenItem>
    {
        public Ship Ship;

        public Rectangle TotalEntrySize;
        public Rectangle SysNameRect;
        public Rectangle ShipNameRect;
        public Rectangle RoleRect;
        public Rectangle FleetRect;
        public Rectangle OrdersRect;
        public Rectangle RefitRect;
        public Rectangle StrRect;
        public Rectangle MaintRect;
        public Rectangle TroopRect;
        public Rectangle FTLRect;
        public Rectangle STLRect;
        public Rectangle RemainderRect;

        Rectangle ShipIconRect;
        readonly UITextEntry ShipNameEntry ;
        readonly TexturedButton RefitButton;
        readonly TexturedButton ScrapButton;
        readonly TexturedButton ExploreButton; //Auto-explore button for ShipListScreen
        readonly TexturedButton PatrolButton;  // patrol is a FLEET mechanic: shown on fleet ships, opens the fleet's patrol-plan picker

        public ShipListScreen Screen;
        public string StatusText;
        readonly bool IsScuttle;
        readonly bool IsCombat;  //fbedard
        public bool Selected = false;  //fbedard: for multi-select
        private readonly string SystemName;
        private readonly Graphics.Font Font12 = Fonts.Arial12Bold;
        private readonly Graphics.Font Font8  = Fonts.Arial8Bold;

        public ShipListScreenItem(Ship s, int x, int y, int width1, int height, ShipListScreen caller)
        {
            Screen = caller;
            Ship = s;
            TotalEntrySize = new Rectangle(x, y, width1 - 60, height);
            SysNameRect = new Rectangle(x, y, (int)(TotalEntrySize.Width * 0.10f), height);
            ShipNameRect = new Rectangle(x + SysNameRect.Width, y, (int)(TotalEntrySize.Width * 0.175f), height);
            RoleRect = new Rectangle(x + SysNameRect.Width + ShipNameRect.Width, y, (int)(TotalEntrySize.Width * 0.05f), height);
            FleetRect = new Rectangle(x + SysNameRect.Width + ShipNameRect.Width, y, (int)(TotalEntrySize.Width * 0.075f), height);
            OrdersRect = new Rectangle(x + SysNameRect.Width + ShipNameRect.Width + RoleRect.Width + FleetRect.Width, y, (int)(TotalEntrySize.Width * 0.24f), height);
            RefitRect = new Rectangle(OrdersRect.X + OrdersRect.Width, y, 125, height);
            StrRect = new Rectangle(RefitRect.X + RefitRect.Width, y, 60, height);
            MaintRect = new Rectangle(StrRect.X + StrRect.Width, y, 60, height);
            TroopRect = new Rectangle(MaintRect.X + MaintRect.Width, y, 60, height);
            FTLRect = new Rectangle(TroopRect.X + TroopRect.Width, y, 60, height);
            STLRect = new Rectangle(FTLRect.X + FTLRect.Width, y, 60, height);
            StatusText = GetStatusText(Ship);
            ShipIconRect = new Rectangle(ShipNameRect.X + 5, ShipNameRect.Y + 2, 28, 28);
            SystemName = Ship.System?.Name ?? Localizer.Token(GameText.DeepSpace);

            ShipNameEntry = new UITextEntry(new Vector2(ShipIconRect.Right + 10, 2 + SysNameRect.CenterY() - Fonts.Arial12Bold.LineSpacing / 2),
                                            Fonts.Arial12Bold, Ship.ShipName);
            ShipNameEntry.Color = Colors.Cream;
            ShipNameEntry.OnTextChanged = (text) => Ship.VanityName = text;

            float width = (int)(OrdersRect.Width * 0.8f);
            while (width % 10f != 0f)
                width += 1f;

            if (!Ship.IsPlatformOrStation && !Ship.IsHangarShip 
                                          && Ship.ShipData.Role != RoleName.troop 
                                          && Ship.AI.State != AIState.Colonize 
                                          && Ship.ShipData.Role != RoleName.freighter 
                                          && Ship.ShipData.ShipCategory != ShipCategory.Civilian)
                IsCombat = true;

            Rectangle refit = new Rectangle(RefitRect.X + RefitRect.Width / 2 - 5 - ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover1").Width, RefitRect.Y + RefitRect.Height / 2 - ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover2").Height / 2, ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover2").Width, ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover2").Height);

            if (IsCombat)
            {
                ExploreButton = new TexturedButton(refit, "NewUI/icon_order_explore", "NewUI/icon_order_explore_hover1", "NewUI/icon_order_explore_hover2");
            }
            // built for every row - fleet membership changes at runtime, visibility is
            // decided at draw time (maintainer feedback: wire the disabled upstream button)
            PatrolButton = new TexturedButton(refit, "NewUI/icon_order_patrol", "NewUI/icon_order_patrol_hover1", "NewUI/icon_order_patrol_hover2");
            RefitButton = new TexturedButton(refit, "NewUI/icon_queue_rushconstruction", "NewUI/icon_queue_rushconstruction_hover1", "NewUI/icon_queue_rushconstruction_hover2");			
            ScrapButton = new TexturedButton(refit, "NewUI/icon_queue_delete", "NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2");

            if (Ship.IsPlatformOrStation || Ship.Stats.Thrust <= 0f)
            {
                IsScuttle = true;
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            SetNewPos((int)X, (int)Y);

            if (Selected)
            {
                batch.FillRectangle(TotalEntrySize, Color.DarkGreen);
            }

            var textColor = Colors.Cream;

            // spec (4 Aug): unique text reads from the left, one character of padding
            var sysNameCursor = new Vector2(SysNameRect.X + 8, 2 + SysNameRect.Y + SysNameRect.Height / 2 - Font12.LineSpacing / 2);
            batch.DrawString(Font12, SystemName, sysNameCursor, textColor);

            batch.Draw(Ship.ShipData.Icon, ShipIconRect, Color.White);
            ShipNameEntry.Draw(batch, elapsed);

            var rolePos = new Vector2(RoleRect.X + RoleRect.Width / 2 - Font12.MeasureString(Localizer.GetRole(Ship.ShipData.Role, Ship.Loyalty)).X / 2f, RoleRect.Y + RoleRect.Height / 2 - Font12.LineSpacing / 2);
            rolePos = rolePos.ToFloored();
            batch.DrawString(Font12, Localizer.GetRole(Ship.ShipData.Role, Ship.Loyalty), rolePos, textColor);

            string fleetName     = Ship.Fleet?.Name ?? "";
            Graphics.Font fleetFont = Font12.MeasureString(fleetName).X > FleetRect.Width - 16 ? Font8 : Font12;
            // a fleet name is unique text: from the left (spec 4 Aug)
            var fleetPos = new Vector2(FleetRect.X + 8, FleetRect.Y + FleetRect.Height / 2 - fleetFont.LineSpacing / 2);
            fleetPos = fleetPos.ToFloored();
            batch.DrawString(fleetFont, fleetName, fleetPos, textColor);

            // orders read from the left (maintainer bench) - a sentence, not a datum
            var statusPos = new Vector2(OrdersRect.X + 8, 2 + SysNameRect.Y + SysNameRect.Height / 2 - Fonts.Arial12.MeasureString(StatusText).Y / 2f);
            statusPos = statusPos.ToFloored();
            batch.DrawString(Fonts.Arial12, StatusText, statusPos, textColor);

            float maint = Ship.GetMaintCost();

            // charte: numbers close right on a shared edge, one character of air (-16);
            // a cost is nature, not a result - neutral cream, gray when zero
            void DrawNum(in Rectangle rect, string s, Color c)
            {
                var pos = new Vector2(rect.Right - 16 - Fonts.Arial12.MeasureString(s).X,
                                      rect.Y + rect.Height / 2 - Fonts.Arial12.LineSpacing / 2);
                batch.DrawString(Fonts.Arial12, s, pos.ToFloored(), c);
            }

            DrawNum(MaintRect, maint.ToString("F2"), maint > 0f ? Colors.Cream : Color.Gray);
            DrawNum(StrRect, Ship.GetStrength().ToString("0"), Colors.Cream);
            DrawNum(TroopRect, string.Concat(Ship.TroopCount, "/", Ship.TroopCapacity), Colors.Cream);
            DrawNum(FTLRect, (Ship.MaxFTLSpeed / 1000f).ToString("0") + "k", Colors.Cream);
            DrawNum(STLRect, Ship.MaxSTLSpeed.ToString("0"), Colors.Cream);

            if (IsCombat)
            {
                ExploreButton.Draw(batch);
            }
            if (Ship.Fleet != null)
                PatrolButton.Draw(batch);
            if (!Ship.IsSubspaceProjector)
                RefitButton.Draw(batch);
            ScrapButton.Draw(batch);

            batch.DrawRectangle(TotalEntrySize, new Color(118, 102, 67, 50).Premultiplied());
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
                        return $"{status} {goodsType} from {last2.Trade?.ExportFrom.Name} to {last2.Trade?.ImportTo?.Name ?? last2.Trade?.TargetStation.Name} {blockade}";
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
            if (IsCombat)
            {
                // Explore button for ship list
                if (ExploreButton.HandleInput(input))
                {
                    if (Ship.AI.State == AIState.Explore)
                    {
                        Ship.AI.ClearOrders();
                    }
                    else
                    {
                        Ship.AI.OrderExplore();
                    }
                    StatusText = GetStatusText(Ship);
                    return true;
                }
            }

            // patrol rides the ship's FLEET: the picker loads a patrol plan for it
            if (Ship.Fleet != null && PatrolButton.HandleInput(input))
            {
                GameAudio.EchoAffirmative();
                Screen.ScreenManager.AddScreen(new ChoosePatrolPlan(Screen.Universe, Ship.Fleet));
                return true;
            }

            if (!Ship.IsSubspaceProjector && RefitButton.HandleInput(input))
            {
                GameAudio.EchoAffirmative();
                Screen.ScreenManager.AddScreen(new RefitToWindow(Screen, this));
                return true;
            }

            if (ScrapButton.HandleInput(input))
            {
                if (!IsScuttle)
                {
                    StatusText = GetStatusText(Ship);
                }
                else
                {
                    StatusText = GetStatusText(Ship);
                }
                GameAudio.EchoAffirmative();
                if (!IsScuttle)
                {
                    if (Ship.AI.State == AIState.Scrap)
                    {
                        Ship.AI.ClearOrders();
                    }
                    else
                    {
                        if (input.IsShiftKeyDown)
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
                    StatusText = GetStatusText(Ship);
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
                    StatusText = GetStatusText(Ship);
                }
                return true;
            }

            return base.HandleInput(input);
        }

        void SetNewPos(int x, int y)
        {
            TotalEntrySize = new Rectangle(x, y, TotalEntrySize.Width, TotalEntrySize.Height);
            SysNameRect = new Rectangle(x, y, (int)(TotalEntrySize.Width * 0.10f), TotalEntrySize.Height);
            ShipNameRect = new Rectangle(x + SysNameRect.Width, y, (int)(TotalEntrySize.Width * 0.2f), TotalEntrySize.Height);
            RoleRect = new Rectangle(x + SysNameRect.Width + ShipNameRect.Width, y, (int)(TotalEntrySize.Width * 0.05f), TotalEntrySize.Height);
            FleetRect = new Rectangle(x + SysNameRect.Width + ShipNameRect.Width + RoleRect.Width, y, (int)(TotalEntrySize.Width * 0.075f), TotalEntrySize.Height);
            // 0.24: a little more room for the order text, which reads from the left now
            // (maintainer bench) - the slack it eats was the old gutter's
            OrdersRect = new Rectangle(x + SysNameRect.Width + ShipNameRect.Width + RoleRect.Width + FleetRect.Width, y, (int)(TotalEntrySize.Width * 0.24f), TotalEntrySize.Height);
            RefitRect = new Rectangle(OrdersRect.X + OrdersRect.Width, y, 125, TotalEntrySize.Height);
            StrRect = new Rectangle(RefitRect.X + RefitRect.Width, y, 60, TotalEntrySize.Height);
            MaintRect = new Rectangle(StrRect.X + StrRect.Width, y, 60, TotalEntrySize.Height);
            TroopRect = new Rectangle(MaintRect.X + MaintRect.Width, y, 60, TotalEntrySize.Height);
            FTLRect = new Rectangle(TroopRect.X + TroopRect.Width, y, 60, TotalEntrySize.Height);
            STLRect = new Rectangle(FTLRect.X + FTLRect.Width, y, 60, TotalEntrySize.Height);
            ShipIconRect = new Rectangle(ShipNameRect.X + 5, ShipNameRect.Y + 2, 28, 28);
            ShipNameEntry.SetPos(ShipIconRect.Right + 10, 2 + SysNameRect.CenterY() - ShipNameEntry.Font.LineSpacing / 2);

            // the icon trio packs tight, centred in its column (maintainer bench): the old
            // layout kept a slot for the disabled Patrol button, which left a hole between
            // the icons. Every row keeps all three slots so the trio never shifts.
            SubTexture exploreTex = ResourceManager.Texture("NewUI/icon_order_explore_hover1");
            SubTexture patrolTex  = ResourceManager.Texture("NewUI/icon_order_patrol_hover1");
            SubTexture refitTex   = ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover2");
            SubTexture scrapTex   = ResourceManager.Texture("NewUI/icon_queue_delete_hover1");
            const int IconGap = 4;
            int iconsW = exploreTex.Width + IconGap + patrolTex.Width + IconGap + refitTex.Width + IconGap + scrapTex.Width;
            int ix = RefitRect.X + (RefitRect.Width - iconsW) / 2;
            Rectangle IconSlot(SubTexture t)
            {
                var r = new Rectangle(ix, RefitRect.Y + RefitRect.Height / 2 - t.Height / 2, t.Width, t.Height);
                ix += t.Width + IconGap;
                return r;
            }

            Rectangle explore = IconSlot(exploreTex);
            if (IsCombat)
            {
                ExploreButton.r = explore;
                ExploreButton.Tooltip = GameText.OrdersThisShipToExplore;
            }
            PatrolButton.r = IconSlot(patrolTex);
            PatrolButton.Tooltip = "Choose a patrol plan for this ship's fleet";
            RefitButton.r = IconSlot(refitTex);
            ScrapButton.r = IconSlot(scrapTex);
            RefitButton.Tooltip = GameText.OpensAMenuAllowingYou;
            ScrapButton.Tooltip = GameText.OrdersTheShipToReturn;
        }
    }
}
