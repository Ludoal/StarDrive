using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.AI.CombatTactics.UI;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using System.Collections.Generic;

namespace Ship_Game
{
    public sealed class ShipListInfoUIElement : UIElement
    {
        // Ludoal fork: the fleet cartouche wears the standard frame (maintainer bench 320)
        // - one plate for every cartouche. The orders strip above docks on the visible
        // frame top, so it re-seats along with it.
        const int FrameShave = PlanetInfoUIElement.FrameShave;
        const int BarsLeft = 45; // the fleet bars' left edge (absolute) - Total Strength shares it
        public readonly UniverseScreen Screen;
        Empire Player => Screen.Player;

        public ShipStanceButtons OrdersButtons;
        readonly Array<TippedItem> ToolTipItems = new Array<TippedItem>();
        public Array<OrdersButton> Orders = new Array<OrdersButton>();

        Array<Ship> ShipList = new Array<Ship>();
        readonly Selector Selector;
        public Rectangle LeftRect;
        public Rectangle RightRect;
        public Rectangle ShipInfoRect;
        ScrollList<SelectedShipListItem> SelectedShipsSL;
        public Rectangle Power;
        public Rectangle Shields;
        public ToggleButton GridButton;
        readonly Rectangle Housing;
        private readonly Rectangle FlagRect;
        readonly Rectangle DefenseRect;
        readonly Rectangle TroopRect;
        bool IsFleet;
        bool AllShipsMine = true;
        bool ShowModules = true;
        public Ship HoveredShip;
        public Ship HoveredShipLast;
        float HoverOff;

        public ShipListInfoUIElement(Rectangle r, ScreenManager sm, UniverseScreen screen)
        {
            Housing = r;
            Screen  = screen;
            ScreenManager = sm;
            ElementRect = r;
            Selector = new Selector(r, Color.Black);
            TransitionOnTime = TimeSpan.FromSeconds(0.25);
            TransitionOffTime = TimeSpan.FromSeconds(0.25);
            LeftRect = new Rectangle(r.X, r.Y + 44, 180, r.Height - 44);
            // the ship cartouche's seats (bench 320): flag's right edge on the bars' end
            // (382 = icon column 197 + 20 + 15 + bar 150), stance right-aligned on it
            FlagRect = new Rectangle(r.X + 382 - 18, r.Y + 71, 18, 18);
            RightRect = new Rectangle(LeftRect.X + LeftRect.Width, LeftRect.Y, 220, LeftRect.Height);
            float spacing = LeftRect.Height - 26 - 96;
            Power = new Rectangle(RightRect.X, LeftRect.Y + 12, 20, 20);
            Shields = new Rectangle(RightRect.X, LeftRect.Y + 12 + 20 + (int)spacing, 20, 20);
            DefenseRect = new Rectangle(Housing.X + 13, Housing.Y + 112, 22, 22);
            TroopRect = new Rectangle(Housing.X + 13, Housing.Y + 137, 22, 22);

            var gridPos = new Vector2(Housing.X + 16f, Screen.Height - 45f);
            GridButton = new ToggleButton(gridPos, ToggleButtonStyle.Grid, "SelectionBox/icon_grid")
            {
                IsToggled = true
            };
            ShipInfoRect = new Rectangle(Housing.X + 60, Housing.Y + 110, 115, 115);

            // the stance block takes the ship cartouche's exact seat (bench 320)
            var ordersBarPos = new Vector2(Housing.X + 382 - StanceButtons.RowWidth, Housing.Y + 184);

            OrdersButtons = new ShipStanceButtons(screen, ordersBarPos);

            // bench 427, the established bound rule: under 1200px of screen height the list
            // runs down to the frame's bottom; at 1200+ it ALSO climbs one button-height
            // into the freed second-row space above. One variable, nothing rearranges.
            int topExtra = screen.ScreenHeight >= 1200 ? 52 : 0;
            int listTop = Housing.Y + 85 - topExtra;
            int listBottom = Housing.Y + Housing.Height - 10;
            RectF selected = new(RightRect.X-10, listTop, RightRect.Width - 5, listBottom - listTop);
            SelectedShipsSL = new ScrollList<SelectedShipListItem>(selected, 24);
        }

        public void ClearShipList()
        {
            ShipList.Clear();
            SelectedShipsSL.Reset();
        }

        public override void Update(UpdateTimes elapsed)
        {
            OrdersButtons.Update(elapsed.RealTime.Seconds);
            base.Update(elapsed);
            SelectedShipsSL.Update(elapsed.RealTime.Seconds);
            OrdersButtons.ResetButtons(ShipList);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (Screen.SelectedShips == null || SelectedShipsSL.NumEntries == 0)
                return;  //fbedard

            // Ludoal fork: the orders ride a visible strip above the cartouche, same as the
            // single-ship cartouche.
            if (AllShipsMine)
            {
                foreach (OrdersButton ob in Orders)
                    ob.Draw(batch, ScreenManager.input.CursorPosition, ob.ClickRect);
            }

            // the minimap's recipe instead of the sculpted unitselmenu texture
            // ⚠ the frame starts 26 under the housing's top (maintainer feedback: avoids empty
            // space at the top of the plate). The housing keeps its size - every inner anchor is
            // an offset from it - only the visible frame shrinks.
            Rectangle frame = Housing;
            frame.Y += FrameShave; frame.Height -= FrameShave;
            frame.Width -= PlanetInfoUIElement.RightTrim;
            Submenu.DrawFrameWithGround(batch, new RectF(frame));
            var namePos = new Vector2(Housing.X + 13, Housing.Y + 71); // the ship cartouche's name seat
            byte alpha  = Screen.CurrentFlashColor.A;

            foreach (SelectedShipListItem item in SelectedShipsSL.AllEntries)
            {
                foreach (SkinnableButton button in item.ShipButtons)
                {
                    Ship s = (Ship)button.ReferenceObject;
                    if (s.HealthPercent < 0.75f)
                        button.UpdateBackGroundTexColor(new Color(Color.Yellow, alpha).Premultiplied());

                    if (s.InternalSlotsHealthPercent < 0.75f)
                        button.UpdateBackGroundTexColor(new Color(Color.Red, alpha).Premultiplied());
                }
            }

            SelectedShipsSL.Draw(batch, elapsed);

            if (HoveredShip == null)
            {
                HoverOff += elapsed.RealTime.Seconds;
                if (HoverOff > 0.5f)
                {
                    string text = (!IsFleet || ShipList.Count <= 0 || ShipList.First.Fleet == null) ? "Multiple Ships" : ShipList.First.Fleet.Name;
                    batch.DrawString(Fonts.Arial20Bold, text, namePos, tColor);
                    namePos.X += Fonts.Arial20Bold.TextWidth(text) + 5;
                    namePos.Y += 3;
                    batch.DrawString(Fonts.Arial14Bold, $" ({ShipList.Count})", namePos, Color.LightBlue);

                    // the order on the name line, at the ship cartouche's bars-start seat
                    var shipStatus = new Vector2(Housing.X + 232,
                                                 Housing.Y + 71 + (Fonts.Arial14Bold.LineSpacing - Fonts.TahomaBold9.LineSpacing) / 2).ToFloored();
                    string statusTxt = Fonts.TahomaBold9.ParseText(ShipListScreenItem.GetStatusText(ShipList[0]), 120);
                    batch.DrawString(Fonts.TahomaBold9, statusTxt, shipStatus, tColor);

                    CalcAndDrawProgressBars(batch);
                }
            }
            else
            {
                HoverOff = 0f;
                HoveredShip.RenderOverlay(batch, ShipInfoRect, ShowModules && HoveredShip.Loyalty.CanBeScannedByPlayer);
                string text = HoveredShip.VanityName;
                Vector2 tpos = new Vector2(Housing.X + 13, Housing.Y + 71); // the ship cartouche's name seat
                string name = (!string.IsNullOrEmpty(HoveredShip.VanityName) ? HoveredShip.VanityName : HoveredShip.Name);
                Graphics.Font TitleFont = Fonts.Arial14Bold;
                Vector2 ShipSuperName = new Vector2(Housing.X + 13, Housing.Y + 87);
                if (Fonts.Arial14Bold.MeasureString(name).X > 180f)
                {
                    TitleFont = Fonts.Arial12Bold;
                    tpos.Y = tpos.Y + 1;
                }
                batch.DrawString(TitleFont, (!string.IsNullOrEmpty(HoveredShip.VanityName) ? HoveredShip.VanityName : HoveredShip.Name), tpos, tColor);
                //Added by Doctor, adds McShooterz' class/hull data to the rollover in the list too:
                //this.batch.DrawString(Fonts.Visitor10, string.Concat(this.HoveredShip.Name, " - ", Localizer.GetRole(this.HoveredShip.shipData.Role, this.HoveredShip.loyalty)), ShipSuperName, Color.Orange);
                string longName = HoveredShip.Name+" - "+HoveredShip.DesignRole;
                if (HoveredShip.ShipData.ShipCategory != ShipCategory.Unclassified)
                    longName += " - "+HoveredShip.ShipData.ShipCategory;
                batch.DrawString(Fonts.Visitor10, longName, ShipSuperName, Color.Orange);
                batch.Draw(ResourceManager.Texture("UI/icon_shield"), DefenseRect, Color.White);
                Vector2 defPos = new Vector2(DefenseRect.X + DefenseRect.Width + 2, DefenseRect.Y + 11 - Fonts.Arial12Bold.LineSpacing / 2);
                SpriteBatch spriteBatch = batch;
                Graphics.Font arial12Bold = Fonts.Arial12Bold;
                float totalBoardingDefense = HoveredShip.CurrentMechanicalBoardingDefense + HoveredShip.TroopBoardingDefense;
                spriteBatch.DrawString(arial12Bold, totalBoardingDefense.String(), defPos, Color.White);
                Vector2 shipStatus = new Vector2(Housing.X + 232,
                                                 Housing.Y + 71 + (Fonts.Arial14Bold.LineSpacing - Fonts.TahomaBold9.LineSpacing) / 2);
                text = Fonts.TahomaBold9.ParseText(ShipListScreenItem.GetStatusText(HoveredShip), 120f);
                shipStatus = shipStatus.ToFloored();
                batch.DrawString(Fonts.TahomaBold9, text, shipStatus, tColor);
                shipStatus.Y = shipStatus.Y + Fonts.Arial12Bold.MeasureString(text).Y;
                batch.Draw(ResourceManager.Texture("UI/icon_troop_shipUI"), TroopRect, Color.White);
                Vector2 troopPos = new Vector2(TroopRect.X + TroopRect.Width + 2, TroopRect.Y + 11 - Fonts.Arial12Bold.LineSpacing / 2);
                batch.DrawString(Fonts.Arial12Bold, HoveredShip.TroopCount+"/"+HoveredShip.TroopCapacity, troopPos, Color.White);

                Rectangle star = new Rectangle(TroopRect.X, TroopRect.Y + 25, 22, 22);
                Vector2 levelPos = new Vector2(star.X + star.Width + 2, star.Y + 11 - Fonts.Arial12Bold.LineSpacing / 2);
                batch.Draw(ResourceManager.Texture("UI/icon_experience_shipUI"), star, Color.White);
                batch.DrawString(Fonts.Arial12Bold, HoveredShip.Level.ToString(), levelPos, Color.White);
            }
            if (ShipList.Count > 0)
                batch.Draw(ResourceManager.Flag(ShipList.First().Loyalty), FlagRect, ShipList.First().Loyalty.EmpireColor);

            OrdersButtons.Draw(batch, elapsed);
            
            if (ShipList.Any(s => s.Loyalty.CanBeScannedByPlayer))
                GridButton.Draw(batch, elapsed);
        }

        public void CalcAndDrawProgressBars(SpriteBatch batch)
        {

            float fleetOrdnance      = 0f;
            float fleetOrdnanceMax   = 0f;
            float fleetShields       = 0f;
            float fleetShieldsMax    = 0f;
            float fleetHealthPercent = 0f;
            float fleetStr           = 0f;

            for (int i = 0; i < ShipList.Count; i++)
            {
                Ship ship = ShipList[i];
                if (ship == null)
                    continue;

                fleetOrdnance      += ship.Ordinance;
                fleetOrdnanceMax   += ship.OrdinanceMax;
                fleetShields       += ship.ShieldPower;
                fleetShieldsMax    += ship.ShieldMax;
                fleetHealthPercent += ship.HealthPercent;
                fleetStr           += ship.GetStrength();
            }

            fleetHealthPercent = (fleetHealthPercent / ShipList.Count * 100).Clamped(0,100);
            int barYPos        = Housing.Y + 115;
            DrawProgressBar(batch, fleetHealthPercent, 100, "green", "StatusIcons/icon_structure", ref barYPos, true);
            DrawProgressBar(batch, fleetOrdnance, fleetOrdnanceMax, "brown", "Modules/Ordnance", ref barYPos);
            DrawProgressBar(batch, fleetShields, fleetShieldsMax, "blue", "Modules/Shield_1KW", ref barYPos);
            // left-aligned on the bars above - ONE seat for both (they sit at absolute
            // BarsLeft, not off the housing; the old Housing.X + 45 was 10px adrift)
            batch.DrawString(Fonts.Arial12, $"Total Strength: {fleetStr.GetNumberString()}", BarsLeft, barYPos, Color.White);
        }

        public void DrawProgressBar(SpriteBatch batch, float value, float maxValue, string color, string texture, ref int yPos, bool percentage = false)
        {
            if (maxValue.LessOrEqual(0))
                return;

            var barRect = new Rectangle(BarsLeft, yPos, 130, 18);
            var bar = new ProgressBar(barRect)
            {
                Max            = maxValue,
                Progress       = value,
                color          = color,
                DrawPercentage = percentage
            };

            bar.Draw(batch);
            Rectangle texRect = new Rectangle(barRect.X - 25, barRect.Y, 20, 20);
            batch.Draw(ResourceManager.Texture(texture), texRect, Color.White);
            yPos += 22;
        }

        public override bool HandleInput(InputState input)
        {
            if (Screen.SelectedShips == null)
                return false;  // fbedard

            foreach (SelectedShipListItem ship in SelectedShipsSL.AllEntries)
            {
                if (!ship.AllButtonsActive)
                {
                    SetShipList(ShipList, IsFleet);
                    break;
                }
            }

            if (ShipList == null || ShipList.Count == 0 || Screen.SelectedShips.Count == 0)
                return false;

            if (GridButton.HandleInput(input))
            {
                GameAudio.AcceptClick();
                ShowModules = !ShowModules;
                GridButton.IsToggled = ShowModules;
                return true;
            }

            if (AllShipsMine)
            {
                if (OrdersButtons.HandleInput(input)) return true;

                // the strip is always live - no drawer to open first
                {
                    bool orderHover = false;
                    foreach (OrdersButton ob in Orders)
                    {
                        if (!ob.HandleInput(input))
                        {
                            continue;
                        }
                        orderHover = true;
                    }
                    if (orderHover)
                    {
                        //this.screen.SelectedFleet.Ships.thisLock.EnterReadLock();      //Enter and Exit lock removed to stop crash -Gretman
                        if (Screen.SelectedFleet != null && Screen.SelectedFleet.Ships.Count >0 && Screen.SelectedFleet.Ships[0] != null)
                        {
                            bool flag = true;                            
                            foreach (Ship ship2 in Screen.SelectedFleet.Ships)
                                if (ship2.AI.State != AIState.Resupply)
                                    flag = false;
                            
                            if (flag)
                                Screen.SelectedFleet.FinalPosition = Screen.SelectedFleet.Ships[0].AI.OrbitTarget.Position;  //fbedard: center fleet on resupply planet
                            
                        }
                        //this.screen.SelectedFleet.Ships.thisLock.ExitReadLock();
                        return true;
                    }                  
                }
            }

            HoveredShipLast = HoveredShip;
            HoveredShip = null;

            if (SelectedShipsSL.HandleInput(input))
                return true;

            foreach (TippedItem ti in ToolTipItems)
            {
                if (ti.Rect.HitTest(input.CursorPosition))
                    ToolTip.CreateTooltip(ti.Tooltip);
            }

            if (ElementRect.HitTest(input.CursorPosition))
                return true;
            return false;
        }

        void OnSelectedShipsListButtonClicked(SkinnableButton button)
        {
            if (Screen.Input.SelectSameDesign)
            {
                FilterShipList(s => s.Name == HoveredShip.Name);
            }
            else if (Screen.Input.SelectSameHull)
            {
                FilterShipList(s => s.BaseHull == HoveredShip.BaseHull);
            }
            else if (Screen.Input.SelectSameRoleAndHull)
            {
                FilterShipList(s => s.DesignRole == HoveredShip.DesignRole && s.BaseHull == HoveredShip.BaseHull);
            }
            else
            {
                Screen.SetSelectedShip(HoveredShip);
            }
        }

        void FilterShipList(Predicate<Ship> predicate)
        {
            Ship[] ships = Screen.SelectedShips.Filter(predicate);
            Screen.SetSelectedShipList(ships, fleet: null);
        }

        public void SetShipList(IReadOnlyList<Ship> shipList, bool isFleet)
        {
            Orders.Clear();
            IsFleet  = isFleet;
            ShipList = new(shipList); // always copy!
            SelectedShipsSL.Reset();
            AllShipsMine        = true;
            bool allResupply    = true;
            bool allFreighters  = true;
            bool allCombat      = true;
            bool carriersHere   = false;
            bool troopShipsHere = false;
            var entry = new SelectedShipListItem(this, OnSelectedShipsListButtonClicked);
            for (int i = 0; i < ShipList.Count; i++)
            {
                Ship ship  = ShipList[i];
                TacticalIcon icon = ship.TacticalIcon();
                var button = new SkinnableButton(new Rectangle(0, 0, 20, 20), 
                    icon.Primary, icon.Secondary, ResourceManager.Texture("TacticalIcons/symbol_status"))
                {
                    IsToggle = false,
                    ReferenceObject = ship,
                    BaseColor = ship.Resupplying ? Color.Gray : ship.Loyalty.EmpireColor,
                };

                if (entry.ShipButtons.Count < 8)
                    entry.ShipButtons.Add(button);

                if (entry.ShipButtons.Count == 8 || i == ShipList.Count - 1)
                {
                    SelectedShipsSL.AddItem(entry);
                    entry = new SelectedShipListItem(this, OnSelectedShipsListButtonClicked);
                }

                if (ship.AI.State != AIState.Resupply) allResupply    = false;
                if (ship.Loyalty != Player)            AllShipsMine   = false;
                if (!ship.IsFreighter)                 allFreighters  = false;
                if (ship.Carrier.HasFighterBays)       carriersHere   = true;
                if (ship.Carrier.HasTroopBays)         troopShipsHere = true;

                if (ship.DesignRole < RoleName.carrier || ship.ShipData.ShipCategory == ShipCategory.Civilian 
                                                       || ship.AI.State == AIState.Colonize 
                                                       || ship.IsHangarShip)
                {
                    allCombat = false;
                }
                OrdersButtons.ResetButtons(ShipList);
            }

            var slRect = new Rectangle(RightRect.X - 10, Housing.Y + 85, RightRect.Width - 5, OrdersButtons.Visible ? 100 : 140);
            SelectedShipsSL.Rect = slRect;

            OrdersButton resupply = new(ShipList, OrderType.OrderResupply, GameText.OrdersSelectedShipOrShips)
            {
                SimpleToggle = true,
                Active = allResupply
            };
            Orders.Add(resupply);

            if (allCombat)
            {  
                OrdersButton explore = new(ShipList, OrderType.Explore, GameText.OrdersThisShipToExplore)
                {
                    SimpleToggle = true,
                    Active = false
                };
                Orders.Add(explore);
            }

            if (carriersHere)
            {
                OrdersButton launchFighters = new(ShipList, OrderType.FighterToggle, GameText.WhenActiveAllAvailableFighters)
                {
                    SimpleToggle = true,
                    Active = false
                };
                Orders.Add(launchFighters);
                OrdersButton waitForFighters = new(ShipList, OrderType.FighterRecall, GameText.ClickToToggleWhetherThis)
                {
                    SimpleToggle = true,
                    Active = true
                };
                Orders.Add(waitForFighters);
            }

            if (troopShipsHere)
            {
                OrdersButton launchTroops = new(ShipList, OrderType.TroopToggle, GameText.TogglesWhetherThisShipsAssault)
                {
                    SimpleToggle = true,
                    Active = true
                };
                Orders.Add(launchTroops);

                OrdersButton sendTroops = new(ShipList, OrderType.SendTroops, GameText.SendTroopsToThisShip)
                {
                    SimpleToggle = true,
                    Active = true
                };
                Orders.Add(sendTroops);

                if (!carriersHere)
                {
                    OrdersButton waitForTroops = new(ShipList, OrderType.FighterRecall, GameText.ClickToToggleWhetherThis)
                    {
                        SimpleToggle = true,
                        Active = true
                    };
                    Orders.Add(waitForTroops);
                }
            }

            if (allFreighters)
            {
                OrdersButton tradeFood = new(ShipList, OrderType.TradeFood, GameText.ManualTradeOrdersThisFreighter2)
                {
                    SimpleToggle = true
                };
                Orders.Add(tradeFood);
                OrdersButton tradeProduction = new(ShipList, OrderType.TradeProduction, GameText.ManualTradeOrdersThisFreighter2)
                {
                    SimpleToggle = true
                };
                Orders.Add(tradeProduction);
                OrdersButton transportColonists = new(ShipList, OrderType.TransportColonists, GameText.OrderTheseShipsToBegin2)
                {
                    SimpleToggle = true
                };
                Orders.Add(transportColonists);
                OrdersButton allowInterEmpireTrade = new(ShipList, OrderType.AllowInterTrade, GameText.ManualTradeAllowSelectedFreighters)
                {
                    SimpleToggle = true
                };
                Orders.Add(allowInterEmpireTrade);
            }

            if (isFleet)
            {
                OrdersButton patrol = new(ShipList, OrderType.Patrol, GameText.OrderFleetPatrol)
                {
                    SimpleToggle = true,
                    Active = false
                };
                Orders.Add(patrol);
            }

            //Added by McShooterz: fleet scrap button
            OrdersButton scrap = new(ShipList, OrderType.Scrap, GameText.OrderShipBackToThe)
            {
                SimpleToggle = true,
                Active = false
            };
            Orders.Add(scrap);

            // bench 427, spec v4.5: the same nature-split the single-ship cartouche wears -
            // generic orders in the fixed right column, specifics on one top row
            int colX = ElementRect.X + ElementRect.Width - PlanetInfoUIElement.RightTrim + 4;
            int colY = ElementRect.Y + FrameShave + 4;
            int rowCol = 0;
            foreach (OrdersButton ob in Orders)
            {
                if (ob.IsGeneric)
                {
                    ob.ClickRect.X = colX;
                    ob.ClickRect.Y = colY;
                    colY += 52;
                }
                else
                {
                    int col = rowCol % 7, row = rowCol / 7;
                    ob.ClickRect.X = ElementRect.X + col * 52;
                    ob.ClickRect.Y = ElementRect.Y + FrameShave - 52 - 4 - row * 52; // docked on the VISIBLE frame top
                    rowCol++;
                }
            }
        }
    }
}