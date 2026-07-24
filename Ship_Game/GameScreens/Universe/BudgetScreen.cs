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

        // one colony = one row: name | net | pop | pop income | building income | gross |
        // building upkeep | troop upkeep | effective tax rate — Stats+ grammar, the split
        // pop/building income is a direct display of the two terms of GrossRevenue
        class EconColonyItem : ScrollListItem<EconColonyItem>
        {
            public readonly Planet Planet;
            public EconColonyItem(Planet p) { Planet = p; }

            public static float NetIncome(Planet p) => p.Money.NetRevenue - p.Money.TroopMaint;

            // GrossRevenue = (Pop×IncomePerColonist + IncomeFromBuildings) × TaxRate —
            // shares are proportional so the two columns always sum to GROSS
            public static float PopIncome(Planet p)
            {
                float pop = p.PopulationBillion * p.Money.IncomePerColonist;
                float baseSum = pop + p.Money.IncomeFromBuildings;
                return baseSum > 0f ? p.Money.GrossRevenue * (pop / baseSum) : 0f;
            }
            public static float BldgIncome(Planet p) => p.Money.GrossRevenue - PopIncome(p);

            public static float BudgetLeft(Planet p) => p.Budget == null ? 0f
                : p.Budget.RemainingCivilian + p.Budget.RemainingSpaceDef + p.Budget.RemainingGroundDef;

            public override void PerformLayout()
            {
                int x = (int)X, y = (int)Y, w = (int)Width;
                RemoveAll();

                Panel(new Rectangle(x + 4, y + 2, 18, 18), ResourceManager.Texture(Planet.IconPath));
                var name = Label(new Vector2(x + 28, y + 4), Planet.Name, Fonts.Arial12Bold);
                name.Color = Color.White;

                void Cell(int col, Func<UILabel, string> getText)
                {
                    var l = new UILabel(getText);
                    l.Pos = new Vector2(x + w * ColStart(col), y + 4);
                    l.Size = new Vector2(w * NumColW - 8, Fonts.Arial12.LineSpacing);
                    l.TextAlign = TextAlign.Right;
                    Add(l);
                }
                void MoneyCell(int col, Func<float> getValue) => Cell(col, DynamicText(getValue, f => f.MoneyString()));

                MoneyCell(0, () => NetIncome(Planet));
                Cell(1, l => { l.Color = Color.White; return $"{Planet.PopulationBillion:0.00}"; });
                MoneyCell(2, () => PopIncome(Planet));
                MoneyCell(3, () => BldgIncome(Planet));
                MoneyCell(4, () => Planet.Money.GrossRevenue);
                MoneyCell(5, () => -Planet.Money.Maintenance);
                MoneyCell(6, () => -Planet.Money.TroopMaint);
                Cell(7, l => { l.Color = Color.Wheat; return $"{Planet.Money.TaxRate * 100:0.#}%"; });
                // governor budget: treasury ALLOCATION (wheat, outside the per-turn
                // arithmetic) and what the governor has left of it (red = overspent)
                Cell(8, l => { l.Color = Color.Wheat; return (Planet.Budget?.TotalAlloc ?? 0f).MoneyString(); });
                MoneyCell(9, () => BudgetLeft(Planet));

                base.PerformLayout();
            }
        }

        // table geometry, shared by the header labels, the rows and the total footer:
        // the name column, then the numeric columns of equal width, right-aligned
        const float NameColW = 0.14f;
        const int NumCols = 10;
        const float NumColW = (1f - NameColW) / NumCols;
        static float ColStart(int i) => NameColW + i * NumColW;

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
            void HeaderCell(int col, string text)
            {
                var l = Label(new Vector2(tableX + tableW * ColStart(col), headerY), text, Fonts.Arial12Bold);
                l.Size = new Vector2(tableW * NumColW - 8, Fonts.Arial12Bold.LineSpacing);
                l.TextAlign = TextAlign.Right;
                l.Color = Color.Wheat;
            }
            var colonyHdr = Label(new Vector2(tableX + 4, headerY), "COLONY", Fonts.Arial12Bold);
            colonyHdr.Color = Color.Wheat;
            string[] headers = { "NET", "POP", "POP INC", "BLDG INC", "GROSS", "BLDG MAINT", "TROOP MAINT", "TAX RATE", "BUDGET", "BDGT LEFT" };
            for (int i = 0; i < headers.Length; ++i)
                HeaderCell(i, headers[i]);

            var listRect = new RectF(tableX, headerY + 20, tableW, (int)LeftMenu.Height - 36 - 78);
            ColonySL = Add(new ScrollList<EconColonyItem>(listRect, 24));
            ColonySL.OnClick = OnColonyClicked;
            foreach (Planet p in Player.GetPlanets().OrderByDescending(EconColonyItem.NetIncome))
                ColonySL.AddItem(new EconColonyItem(p));

            // vertical separators between the numeric columns
            int sepBottom = (int)LeftMenu.Bottom - 14;
            for (int i = 0; i <= NumCols; ++i)
            {
                int sepX = tableX + (int)(tableW * ColStart(i)) - 4;
                Panel(new Rectangle(sepX, headerY, 1, sepBottom - headerY), new Color(255, 255, 255, 25));
            }

            // TOTAL footer + the off-planet reconciliation down to the empire Net Gain
            float NetGainNow() => Player.NetIncome - Player.MoneySpendOnProductionNow;
            float PlanetsNet() => Player.GetPlanets().Sum(EconColonyItem.NetIncome);
            int totalY = (int)listRect.Bottom + 8;
            void FooterCell(int col, int row, Func<float> getValue)
            {
                var l = new UILabel(DynamicText(getValue, f => f.MoneyString()), Fonts.Arial12Bold);
                l.Pos = new Vector2(tableX + tableW * ColStart(col), totalY + row * 17);
                l.Size = new Vector2(tableW * NumColW - 8, Fonts.Arial12Bold.LineSpacing);
                l.TextAlign = TextAlign.Right;
                Add(l);
            }
            var totalLbl = Label(new Vector2(tableX + 4, totalY), Localizer.Token(GameText.Total2).ToUpper(), Fonts.Arial12Bold);
            totalLbl.Color = Color.Wheat;
            FooterCell(0, 0, PlanetsNet);
            var popTot = new UILabel(l => $"{Player.GetPlanets().Sum(p => p.PopulationBillion):0.00}", Fonts.Arial12Bold);
            popTot.Pos = new Vector2(tableX + tableW * ColStart(1), totalY);
            popTot.Size = new Vector2(tableW * NumColW - 8, Fonts.Arial12Bold.LineSpacing);
            popTot.TextAlign = TextAlign.Right;
            popTot.Color = Color.White;
            Add(popTot);
            FooterCell(2, 0, () => Player.GetPlanets().Sum(EconColonyItem.PopIncome));
            FooterCell(3, 0, () => Player.GetPlanets().Sum(EconColonyItem.BldgIncome));
            FooterCell(4, 0, () => Player.GetPlanets().Sum(p => p.Money.GrossRevenue));
            FooterCell(5, 0, () => -Player.GetPlanets().Sum(p => p.Money.Maintenance));
            FooterCell(6, 0, () => -Player.GetPlanets().Sum(p => p.Money.TroopMaint));
            var allocTot = new UILabel(l => Player.GetPlanets().Sum(p => p.Budget?.TotalAlloc ?? 0f).MoneyString(), Fonts.Arial12Bold);
            allocTot.Pos = new Vector2(tableX + tableW * ColStart(8), totalY);
            allocTot.Size = new Vector2(tableW * NumColW - 8, Fonts.Arial12Bold.LineSpacing);
            allocTot.TextAlign = TextAlign.Right;
            allocTot.Color = Color.Wheat;
            Add(allocTot);
            FooterCell(9, 0, () => Player.GetPlanets().Sum(EconColonyItem.BudgetLeft));

            var offLbl = Label(new Vector2(tableX + 4, totalY + 17), "off-planet (trade, fleet, espionage, production)", Fonts.Arial12);
            offLbl.Color = Color.Gray;
            FooterCell(0, 1, () => NetGainNow() - PlanetsNet());

            var gainLbl = Label(new Vector2(tableX + 4, totalY + 34), "= NET GAIN", Fonts.Arial12Bold);
            gainLbl.Color = Color.Wheat;
            FooterCell(0, 2, NetGainNow);

            // ---- RIGHT 1/3: the synthesis, causal order (Ludo, 23 Jul 18:03) ----
            // auto-tax mode + sliders → governor budget (derived from the treasury
            // goal) → vertical arithmetic Income − Expenditure = Net Gain
            int rx = (int)RightMenu.X + 20;
            int rw = (int)RightMenu.Width - 40;
            var taxRect    = new Rectangle(rx, (int)RightMenu.Y + 74, rw, 84);
            var budgetRect = new Rectangle(rx, taxRect.Bottom + 8, rw, 128);
            var incomeRect = new Rectangle(rx, budgetRect.Bottom + 8, rw, 150);
            var costRect   = new Rectangle(rx, incomeRect.Bottom + 8, rw, 140);

            SummaryPanel tax = Add(new SummaryPanel("", taxRect, new Color(17, 21, 28)));

            TaxSlider = tax.AddSlider(Player.AutoTaxes ? "Tax Rate (auto)" : Localizer.Token(GameText.TaxRate), Player.data.TaxRate);
            TaxSlider.Tip = GameText.TaxesAreCollectedFromYour;
            TaxSlider.OnChange = TaxSliderOnChange;

            TreasuryGoal          = tax.AddSlider(GameText.TreasuryGoal, Player.data.treasuryGoal);
            TreasuryGoal.Tip      = GameText.TreasuryGoalIsTheTarget;
            TreasuryGoal.OnChange = TreasurySliderOnChange;

            TreasuryGoal.RelativeValue = Player.data.treasuryGoal; // trigger updates
            TaxSlider.RelativeValue    = Player.data.TaxRate;

            // the checkbox is a MODE switch — it sits on top of the slider it drives
            AutoTaxCheckBox(new Rectangle(rx, (int)RightMenu.Y + 48, rw, 20));

            BudgetTab(budgetRect);
            IncomesTab(incomeRect);
            CostsTab(costRect);

            EmpireNetIncome = Label(new Vector2(rx, costRect.Bottom + 12),
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
                TaxSlider.Text = Player.AutoTaxes ? "Tax Rate (auto)" : Localizer.Token(GameText.TaxRate);
            };
            TaxSlider.Enabled = !autoTax.Checked;
            return autoTax;
        }

        // pots = EMA(treasury goal × weights) — a treasury ALLOCATION, deliberately
        // outside the per-turn arithmetic; each pot shows its share of the total
        private void BudgetTab(Rectangle budgetRect)
        {
            SummaryPanel budget = Add(new SummaryPanel("Governor Budget — allocated on treasury goal", budgetRect, new Color(30, 26, 19)));
            float Pots() => Player.AI.ColonyBudget + Player.AI.SSPBudget + Player.AI.DefenseBudget;
            void PotItem(string name, Func<float> pot)
            {
                budget.AddSplit(new UILabel(name + ":", Color.White),
                                new UILabel(l =>
                                {
                                    float v = pot(); float t = Pots();
                                    l.Color = v > 0f ? Color.ForestGreen : Color.Gray;
                                    return t > 0f ? $"{v.MoneyString()} ({v / t * 100:0}%)" : v.MoneyString();
                                }));
            }
            PotItem("Colony", () => Player.AI.ColonyBudget);
            PotItem("SpaceRoad", () => Player.AI.SSPBudget);
            PotItem("Defense", () => Player.AI.DefenseBudget);
            budget.SetTotalFooter(Pots);
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
            income.AddItem("Trade (cargo + treaties)", () => Player.TotalTradeMoneyAddedThisTurn);
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
                if (f > -0.005f && f < 0.005f) f = 0f; // kill the "-0.00" display
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
