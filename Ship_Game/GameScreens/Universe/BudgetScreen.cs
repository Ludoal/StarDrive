using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using Ship_Game.Audio;
using Ship_Game.Gameplay;
using Ship_Game.UI;
using System;
using System.Linq;
using SDUtils;
using Ship_Game.ExtensionMethods;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens
{
    // Ludoal fork: full-screen dashboard (Colony grammar — title bar, 2/3 colony
    // table on the left, the classic synthesis panels in the 1/3 right block).
    // Spec: Lek, economic-overview-spec.md v1. Left table: one row per colony,
    // net income first, deficits in red, click opens the Colony Overview.
    public sealed class BudgetScreen : GameScreen
    {
        readonly Empire Player;
        Menu2 TitleBar;
        Menu1 LeftMenu;
        Menu1 RightMenu;

        FloatSlider TaxSlider;
        FloatSlider TreasuryGoal;
        UILabel EmpireNetIncome;
        ScrollList<EconColonyItem> ColonySL;

        readonly UniverseScreen Universe; // Ludoal fork: for the live top bar
        public BudgetScreen(UniverseScreen screen) : base(screen, toPause: screen)
        {
            Player            = screen.Player;
            IsPopup           = true;
            Universe = screen; // Ludoal fork
            TransitionOnTime  = 0.25f;
            TransitionOffTime = 0.25f;
        }

        class SummaryPanel : UIList
        {
            public SummaryPanel(LocalizedText title, in Rectangle rect, Color c) : base(rect, c)
            {
                if (title.NotEmpty)
                {
                    Header = new UILabel(title, Fonts.Arial14Bold, Color.Wheat)
                    {
                        DropShadow = true
                    };
                }
                Padding     = new Vector2(4f, 2f);
                LayoutStyle = ListLayoutStyle.Fill;
            }

            public void AddItem(LocalizedText text, Func<float> getValue) => AddItem(text, getValue, Color.White);
            public void AddItem(LocalizedText text, Func<float> getValue, Color keyColor)
            {
                AddSplit(new UILabel(text.Text + ":", keyColor),
                         new UILabel(DynamicText(getValue, f => f.MoneyString())) );
            }

            public void SetTotalFooter(Func<float> getValue)
            {
                Footer = new SplitElement(new UILabel(Localizer.Token(GameText.Total2) + ":"),
                                          new UILabel(DynamicText(getValue, f => f.MoneyString())) );
            }

            public FloatSlider AddSlider(LocalizedText title, float value)
            {
                return Add(new FloatSlider(SliderStyle.Percent, new Vector2(100,32), title, 0f, 1f, value));
            }
        }

        // one colony = one row: name | net | gross | building upkeep | troop upkeep | tax rate
        // columns share the fractions of the header labels drawn by the screen
        class EconColonyItem : ScrollListItem<EconColonyItem>
        {
            public readonly Planet Planet;
            public EconColonyItem(Planet p) { Planet = p; }

            public static float NetIncome(Planet p) => p.Money.NetRevenue - p.Money.TroopMaint;

            public override void PerformLayout()
            {
                int x = (int)X, y = (int)Y, w = (int)Width;
                RemoveAll();

                Panel(new Rectangle(x + 4, y + 2, 18, 18), ResourceManager.Texture(Planet.IconPath));
                var name = Label(new Vector2(x + 28, y + 4), Planet.Name, Fonts.Arial12Bold);
                name.Color = Color.White;

                void Value(float fraction, Func<float> getValue)
                {
                    var l = new UILabel(DynamicText(getValue, f => f.MoneyString()));
                    l.Pos = new Vector2(x + w * fraction, y + 4);
                    Add(l);
                }

                Value(ColNet,   () => NetIncome(Planet));
                Value(ColGross, () => Planet.Money.GrossRevenue);
                Value(ColBldg,  () => -Planet.Money.Maintenance);
                Value(ColTroop, () => -Planet.Money.TroopMaint);

                var tax = new UILabel(l => $"{Planet.Money.TaxRate * 100:0.#}%");
                tax.Color = Color.Wheat;
                tax.Pos = new Vector2(x + w * ColTax, y + 4);
                Add(tax);

                base.PerformLayout();
            }
        }

        // column fractions, shared by the header labels, the rows and the total footer
        const float ColNet   = 0.30f;
        const float ColGross = 0.44f;
        const float ColBldg  = 0.58f;
        const float ColTroop = 0.72f;
        const float ColTax   = 0.88f;

        public override void LoadContent()
        {
            var titleRect = new Rectangle(2, 44, ScreenWidth * 2 / 3, 80);
            TitleBar  = Add(new Menu2(titleRect));
            LeftMenu  = Add(new Menu1(2, titleRect.Bottom + 5, titleRect.Width, ScreenHeight - titleRect.Bottom - 7));
            RightMenu = Add(new Menu1(titleRect.Right + 5, titleRect.Bottom + 5, ScreenWidth / 3 - 10, ScreenHeight - titleRect.Bottom - 7));
            CloseButton(RightMenu.Right - 52, RightMenu.Y + 22);

            // title + the unit note of the money charte
            string title = Localizer.Token(GameText.EconomicOverview);
            Label(new Vector2(titleRect.X + titleRect.Width / 2 - Fonts.Laserian14.MeasureString(title).X / 2f,
                              titleRect.Y + titleRect.Height / 2 - Fonts.Laserian14.LineSpacing / 2), title, Fonts.Laserian14, Colors.Cream);
            string unitNote = "(all money values are per turn)";
            Label(new Vector2(titleRect.X + titleRect.Width / 2 - Fonts.Arial12.TextWidth(unitNote) / 2f,
                              titleRect.Bottom - 22), unitNote, Fonts.Arial12, Color.Gray);

            // ---- LEFT 2/3: the colony table ----
            int tableX = (int)LeftMenu.X + 20;
            int tableW = (int)LeftMenu.Width - 40;
            int headerY = (int)LeftMenu.Y + 16;
            void HeaderLabel(float fraction, string text)
            {
                var l = Label(new Vector2(tableX + tableW * fraction, headerY), text, Fonts.Arial12Bold);
                l.Color = Color.Wheat;
            }
            HeaderLabel(0f,       "COLONY");
            HeaderLabel(ColNet,   "NET");
            HeaderLabel(ColGross, "GROSS");
            HeaderLabel(ColBldg,  "BLDG MAINT");
            HeaderLabel(ColTroop, "TROOP MAINT");
            HeaderLabel(ColTax,   "TAX RATE");

            var listRect = new RectF(tableX, headerY + 20, tableW, (int)LeftMenu.Height - 36 - 48);
            ColonySL = Add(new ScrollList<EconColonyItem>(listRect, 24));
            ColonySL.OnClick = OnColonyClicked;
            foreach (Planet p in Player.GetPlanets().OrderByDescending(EconColonyItem.NetIncome))
                ColonySL.AddItem(new EconColonyItem(p));

            // TOTAL footer, reconciling the columns
            int totalY = (int)listRect.Bottom + 10;
            var totalLbl = Label(new Vector2(tableX + 4, totalY), Localizer.Token(GameText.Total2).ToUpper(), Fonts.Arial12Bold);
            totalLbl.Color = Color.Wheat;
            void TotalValue(float fraction, Func<float> getValue)
            {
                var l = new UILabel(DynamicText(getValue, f => f.MoneyString()), Fonts.Arial12Bold);
                l.Pos = new Vector2(tableX + tableW * fraction, totalY);
                Add(l);
            }
            TotalValue(ColNet,   () => Player.GetPlanets().Sum(EconColonyItem.NetIncome));
            TotalValue(ColGross, () => Player.GetPlanets().Sum(p => p.Money.GrossRevenue));
            TotalValue(ColBldg,  () => -Player.GetPlanets().Sum(p => p.Money.Maintenance));
            TotalValue(ColTroop, () => -Player.GetPlanets().Sum(p => p.Money.TroopMaint));

            // ---- RIGHT 1/3: the classic synthesis ----
            int rx = (int)RightMenu.X + 20;
            int rw = (int)RightMenu.Width - 40;
            int colW = (rw - 12) / 2;
            var taxRect    = new Rectangle(rx, (int)RightMenu.Y + 50, rw, 84);
            var incomeRect = new Rectangle(rx, taxRect.Bottom + 6, colW, 150);
            var costRect   = new Rectangle(rx + colW + 12, incomeRect.Y, colW, 150);
            var tradeRect  = new Rectangle(rx, incomeRect.Bottom + 6, colW, 166);
            var budgetRect = new Rectangle(costRect.X, costRect.Bottom + 6, colW, 112);
            var footerRect = new Rectangle(rx, Math.Max(tradeRect.Bottom, budgetRect.Bottom) + 10, rw, 86);

            SummaryPanel tax = Add(new SummaryPanel("", taxRect, new Color(17, 21, 28)));
            var taxTitle = Player.AutoTaxes ? GameText.AutoTaxes : GameText.TaxRate;

            TaxSlider = tax.AddSlider(Localizer.Token(taxTitle), Player.data.TaxRate);
            TaxSlider.Tip = GameText.TaxesAreCollectedFromYour;
            TaxSlider.OnChange = TaxSliderOnChange;

            TreasuryGoal          = tax.AddSlider(GameText.TreasuryGoal, Player.data.treasuryGoal);
            TreasuryGoal.Tip      = GameText.TreasuryGoalIsTheTarget;
            TreasuryGoal.OnChange = TreasurySliderOnChange;

            TreasuryGoal.RelativeValue = Player.data.treasuryGoal; // trigger updates
            TaxSlider.RelativeValue    = Player.data.TaxRate;

            AutoTaxCheckBox(footerRect);

            IncomesTab(incomeRect);
            CostsTab(costRect);
            TradeTab(tradeRect);
            BudgetTab(budgetRect);

            EmpireNetIncome = Label(new Vector2(rx, RightMenu.Bottom - 50),
                                    text:GameText.NetGain, Fonts.Arial20Bold);
            EmpireNetIncome.DropShadow  = true;
            EmpireNetIncome.DynamicText = DynamicText(
                ()   => Player.NetIncome-Player.MoneySpendOnProductionNow,
                (f) => $"{( f >= 0f ? Localizer.Token(GameText.NetGain) : Localizer.Token(GameText.NetLoss) )} : {f.MoneyString()}");

            base.LoadContent();
        }

        void OnColonyClicked(EconColonyItem item)
        {
            // the economy screen is the door into the diagnosis: a red row → why?
            GameAudio.AcceptClick();
            ExitScreen();
            Universe.workersPanel = new ColonyScreen(Universe, item.Planet, Universe.EmpireUI);
            Universe.LookingAtPlanet = true;
            // same anchor as the double-click path: the panel covers the map, no snap,
            // but the close handler restores transitionStartPosition — keep it current
            Universe.transitionStartPosition = Universe.CamPos;
            Universe.CamDestination = Universe.CamPos;
        }

        private UICheckBox AutoTaxCheckBox(Rectangle footerRect)
        {
            var autoTax = Checkbox(new Vector2(footerRect.X, footerRect.Y), () => Player.AutoTaxes,
                                   GameText.AutoTaxes, GameText.YourEmpireWillAutomaticallyManage3);
            autoTax.OnChange = cb =>
            {
                if (cb.Checked)
                {
                    Player.AI.RunEconomicPlanner();
                    TaxSlider.RelativeValue = Player.data.TaxRate;
                }
                TaxSlider.Enabled = !cb.Checked;
                TaxSlider.Text = Player.AutoTaxes ? GameText.AutoTaxes : GameText.TaxRate;
            };
            TaxSlider.Enabled = !autoTax.Checked;
            return autoTax;
        }

        private void BudgetTab(Rectangle budgetRect)
        {
            SummaryPanel budget = Add(new SummaryPanel(GameText.GovernorBudget, budgetRect, new Color(30, 26, 19)));
            budget.AddItem("Colony", () => Player.AI.ColonyBudget);
            budget.AddItem("SpaceRoad", () => Player.AI.SSPBudget);
            budget.AddItem("Defense", () => Player.AI.DefenseBudget);
            budget.SetTotalFooter(() => Player.AI.ColonyBudget + Player.AI.SSPBudget + Player.AI.DefenseBudget);
        }

        private void TradeTab(Rectangle tradeRect)
        {
            SummaryPanel trade = Add(new SummaryPanel(GameText.Trade, tradeRect, new Color(30, 26, 19)));

            trade.AddItem(GameText.MercantilismAvg, () => Player.AverageTradeIncome); // "Mercantilism (Avg)"
            trade.AddItem(GameText.TradeTreaties, () => Player.TotalTradeTreatiesIncome()); // "Trade Treaties"

            foreach (Relationship r in Player.TradeRelations)
                trade.AddItem($"   {r.Them.data.Traits.Plural}", () => r.TradeIncome(Player), r.Them.EmpireColor);

            trade.SetTotalFooter(() => Player.TotalAvgTradeIncome); // "Total"
        }

        private void CostsTab(Rectangle costRect)
        {
            SummaryPanel costs = Add(new SummaryPanel(GameText.Expenditure, costRect, new Color(27, 22, 25)));

            costs.AddItem(GameText.BuildingMaint, () => -Player.TotalBuildingMaintenance); // "Building Maint."
            costs.AddItem(GameText.ShipMaint, () => -Player.TotalShipMaintenance); // "Ship Maint."
            costs.AddItem(GameText.TroopMaint, () => -Player.GetTroopMaintThisTurn()); // "Troop Maint."
            costs.AddItem(GameText.ProductionFees, () => -(Player.MoneySpendOnProductionThisTurn+Player.MoneySpendOnProductionNow)); // "production costs."
            if (Player.NewEspionageEnabled)
                costs.AddItem("Espionage", () => -Player.EspionageCostLastTurn);

            costs.SetTotalFooter(() => -(Player.AllSpending+Player.MoneySpendOnProductionNow)); // "Total"
        }

        private void IncomesTab(Rectangle incomeRect)
        {
            SummaryPanel income = Add(new SummaryPanel(GameText.Income, incomeRect, new Color(18, 29, 29)));

            income.AddItem(GameText.PlanetaryTaxes, () => Player.GrossPlanetIncome); // "Planetary Taxes"
            income.AddItem("Trade Cargo", () => Player.TotalTradeMoneyAddedThisTurn);
            income.AddItem("Excess Goods", () => Player.ExcessGoodsMoneyAddedThisTurn);
            income.AddItem("Money Leeched", () => Player.TotalMoneyLeechedLastTurn);
            income.AddItem(GameText.Other, () => Player.data.FlatMoneyBonus);
            income.SetTotalFooter(() => Player.GrossIncome); // "Total"
        }

        private void TaxSliderOnChange(FloatSlider s)
        {
            Player.data.TaxRate = s.RelativeValue;
            Player.UpdateNetPlanetIncomes();
        }

        private void TreasurySliderOnChange(FloatSlider s)
        {
            Player.data.treasuryGoal = s.RelativeValue;
            Player.data.treasuryGoal = s.AbsoluteValue;

            int goal = (int)Player.AI.TreasuryGoal(Player.Money) / 2;
            s.Text = $"{Localizer.Token(GameText.TreasuryGoal)} : {goal}";
            Player.AI.RunEconomicPlanner();

            if (Player.AutoTaxes)
                TaxSlider.RelativeValue = Player.data.TaxRate;
        }

        // Dynamic Text label; this is invoked every time MoneyLabels are drawn
        static Func<UILabel, string> DynamicText(Func<float> getValue,
                                                 Func<float, string> stringify)
        {
            return (label) =>
            {
                float f = getValue(); // update money color based on value:
                label.Color = f > 0f ? Color.ForestGreen :
                              f < 0f ? Color.Red : Color.Gray;
                return stringify(f);
            };
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            base.Draw(batch, elapsed);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (input.KeyPressed(Keys.T) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }
            if (input.Escaped || input.RightMouseClick)
            {
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        public override void Update(float fixedDeltaTime)
        {
            TreasuryGoal.Text = $"{Localizer.Token(GameText.TreasuryGoal)} : {Player.AI.ProjectedMoney:0.00}";
            base.Update(fixedDeltaTime);
        }
    }
}
