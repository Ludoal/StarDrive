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
    // table on the left, the synthesis in the 1/3 right block).
    // Spec: Lek, economic-overview-spec.md (v1 + Ludo's bench remarks v2/v3).
    // Left table: one row per colony, sortable columns, NET highlighted after the
    // upkeep columns (the order of the calculation is the order of reading),
    // deficits red, click opens the Colony Overview.
    public sealed class BudgetScreen : GameScreen
    {
        readonly Empire Player;
        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row, this screen being one tab
        // NOT Add()ed: geometry only. The group's frame is the border now, so drawing these would
        // double it - the two halves are separated by a single rule instead.
        Rectangle LeftMenu;
        Rectangle RightMenu;

        FloatSlider TaxSlider;
        FloatSlider TreasuryGoal;
        UILabel EmpireNetIncome;
        ScrollList<EconColonyItem> ColonySL;

        SortButton[] SortButtons;
        SortButton SbColony;
        int SortCol = 6;         // NET by default
        bool SortDesc = true;
        bool SortByName;

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
                Padding     = new Vector2(4f, 1f); // tighter rows (maintainer bench)
                LayoutStyle = ListLayoutStyle.Fill;
            }

            public void AddItem(LocalizedText text, Func<float> getValue) => AddItem(text, getValue, Color.White);
            public void AddItem(LocalizedText text, Func<float> getValue, Color keyColor)
            {
                // charte (Lek, étape 3): line items are pure nature - neutral; the TOTALS
                // below are results and keep the sign colours
                AddSplit(new UILabel(text.Text, keyColor),
                         new UILabel(NeutralText(getValue, f => f.MoneyString())) );
            }

            public void Spacer() => Add(new UILabel(" ", Fonts.Arial8Bold)); // half-height breath (maintainer: tighter lines)

            // totals are regular rows, not the UIList Footer — the Footer pins to the
            // rect bottom and breaks the even row pitch (maintainer feedback)
            public void AddTotal(Func<float> getValue)
            {
                AddSplit(new UILabel(Localizer.Token(GameText.Total2), Colors.Cream),
                         new UILabel(DynamicText(getValue, f => f.MoneyString())) );
            }

            public FloatSlider AddSlider(LocalizedText title, float value)
            {
                return Add(new FloatSlider(SliderStyle.Percent, new Vector2(100,42), title, 0f, 1f, value));
            }

            // the coloured background hugs its rows: UIList only enforces width, so a
            // fixed height either clips the total or leaves dead colour - lay the rows
            // out, then take the height from the deepest one plus a breath of padding
            public void FitHeightToRows(float bottomPad = 6f)
            {
                PerformLayout();
                float bottom = Pos.Y;
                if (Header != null)
                    bottom = Header.Bottom;
                for (int i = 0; i < Count; ++i)
                    bottom = Math.Max(bottom, this[i].Bottom);
                Height = (bottom + bottomPad) - Pos.Y;
            }
        }

        // one colony = one row. Numeric cells sit on the shared pixel geometry
        // (TableXpx/TableWpx) so rows, headers and totals stay aligned even with
        // the scrollbar eating into the list width.
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

            public static float BudgetAlloc(Planet p) => p.Budget?.TotalAlloc ?? 0f;
            public static float BudgetLeft(Planet p) => p.Budget == null ? 0f
                : p.Budget.RemainingCivilian + p.Budget.RemainingSpaceDef + p.Budget.RemainingGroundDef;
            // what the governor actually pays, from the source maintenances (Budget.cs
            // derives Remaining from these but rounds to tenths — Alloc − Left showed
            // rounding ghosts, the raw sum doesn't). Negative: it is an expense.
            public static float GovExpense(Planet p) =>
                -(p.GroundDefMaintenance + p.SpaceDefMaintenance + p.CivilianBuildingsMaintenance);

            public override void PerformLayout()
            {
                int x = (int)X, y = (int)Y;
                RemoveAll();

                Panel(new Rectangle(x + 4, y + 2, 18, 18), ResourceManager.Texture(Planet.IconPath));
                var name = Label(new Vector2(x + 28, y + 4), Planet.Name, Fonts.Arial12Bold);
                name.Color = Color.White;

                UILabel Cell(int col, Func<UILabel, string> getText, Graphics.Font font = null)
                {
                    var l = new UILabel(getText, font ?? Fonts.Arial12);
                    l.Pos = new Vector2(TableXpx + TableWpx * ColStart(col), y + 4);
                    l.Size = new Vector2(TableWpx * NumColW - 16, Fonts.Arial12.LineSpacing); // -16: one character of air right of the number (maintainer)
                    l.TextAlign = TextAlign.Right;
                    Add(l);
                    return l;
                }
                UILabel MoneyCell(int col, Func<float> getValue, Graphics.Font font = null)
                    => Cell(col, DynamicText(getValue, f => f.MoneyString()), font);
                // charte (Lek, étape 3): a pure-nature column reads NEUTRAL - colour is for
                // RESULTS (Net, Left) and the anomalous, not for "this is a cost"
                UILabel PlainCell(int col, Func<float> getValue)
                    => Cell(col, NeutralText(getValue, f => f.MoneyString()));

                Cell(0, l => { l.Color = Color.White; return $"{Planet.PopulationBillion:0.00}"; });
                PlainCell(1, () => PopIncome(Planet));
                PlainCell(2, () => BldgIncome(Planet));
                var gross = PlainCell(3, () => Planet.Money.GrossRevenue);
                // the tax rate column moved here (maintainer feedback): the rate is already
                // baked into the income columns, the derivation lives in the tooltip
                float baseRate = Planet.Owner != null ? Planet.Owner.data.TaxRate * 100f : 0f;
                gross.Tooltip = $"pop {PopIncome(Planet).MoneyString()} + buildings {BldgIncome(Planet).MoneyString()}" +
                                $" — effective tax {Planet.Money.TaxRate * 100f:0.#}% (empire {baseRate:0.#}% × local bonus)";
                PlainCell(4, () => -Planet.Money.Maintenance);
                PlainCell(5, () => -Planet.Money.TroopMaint);
                MoneyCell(6, () => NetIncome(Planet), Fonts.Arial12Bold);
                Cell(7, l => { l.Color = Color.Wheat; return BudgetAlloc(Planet).MoneyString(); });
                PlainCell(8, () => GovExpense(Planet));
                MoneyCell(9, () => BudgetLeft(Planet));

                base.PerformLayout();
            }
        }

        // table geometry, shared by the header buttons, the rows and the total footer:
        // the name column, then the numeric columns of equal width, right-aligned.
        // TableWpx excludes the scrollbar so the totals stay under the rows.
        const float NameColW = 0.20f;
        const int NumCols = 10;
        const float NumColW = (1f - NameColW) / NumCols;
        static float ColStart(int i) => NameColW + i * NumColW;
        static int TableXpx;
        static int TableWpx;

        // Header labels follow the grammar of the game's other tables (Ship Array:
        // "System", "Ship", "Role") — Title Case and short, not long capitals. Each
        // column carries a shorter fallback used when the full label no longer fits
        // its column: the labels were what spilled into the neighbouring column
        // below 1080p, never the values.
        static readonly string[] Headers = { "Pop", "Pop Inc", "Bldg Inc", "Gross",
                                             "Bldg Mnt", "Troop Mnt", "Net", "Budget", "Gov Exp", "Left" };
        static readonly string[] HeadersShort = { "Pop", "P.Inc", "B.Inc", "Gross",
                                                  "B.Mnt", "T.Mnt", "Net", "Bdgt", "G.Exp", "Left" };
        // hover tooltips (maintainer bench): every abbreviated or non-obvious header explains itself
        static readonly string[] HeaderTips =
        {
            "Population, in billions",
            "Tax income from the colonists",
            "Tax income from the buildings",
            "Gross tax revenue (colonists + buildings)",
            "Building maintenance paid by the colony",
            "Troop maintenance paid by the colony",
            "Net income of the colony",
            "Budget allocated by the governor",
            "What the governor actually spends",
            "Budget left after the governor's spending",
        };
        static readonly Func<Planet, float>[] ColValue =
        {
            p => p.PopulationBillion,
            EconColonyItem.PopIncome,
            EconColonyItem.BldgIncome,
            p => p.Money.GrossRevenue,
            p => -p.Money.Maintenance,
            p => -p.Money.TroopMaint,
            EconColonyItem.NetIncome,
            EconColonyItem.BudgetAlloc,
            EconColonyItem.GovExpense,
            EconColonyItem.BudgetLeft,
        };

        public override void LoadContent()
        {
            // Ludoal fork: the Economy tab of the Empire group. ONE frame for the whole page rather
            // than two side by side: the colony table and the treasury column are two halves of one
            // view - you read a colony against its budget - so they share the frame and a single
            // vertical rule separates them. Two thirds / one third, as before.
            // the Automation pattern (maintainer bench): a content-sized frame, anchored bar
            // and left. Width is the 900p width at EVERY resolution - which is what makes all
            // the columns fixed; height fills 900p, and past it grows only as the planet list
            // needs, capped by the screen.
            float contentW = 1440 - 2 * ScreenGroups.FrameMargin;
            float fullAvail = ScreenHeight - ScreenGroups.TabRowY - ScreenGroups.FrameMargin;
            float h900 = 900 - ScreenGroups.TabRowY - ScreenGroups.FrameMargin;
            float rowsNeed = 60 + Player.GetPlanets().Count * 24 + 90; // header lane + rows + footer/margins
            float contentH = fullAvail <= h900 ? fullAvail
                           : Math.Min(fullAvail, Math.Max(h900, rowsNeed));
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.EmpireTabTitles, 3,
                                                    OnEmpireTabChanged, contentW, contentH);
            RectF client = EmpireTabs.ClientArea;
            // +20 to the table, -20 to the synthesis (maintainer bench)
            float split = client.X + client.W * 2f / 3f + 20;
            LeftMenu  = new Rectangle((int)client.X, (int)client.Y, (int)(split - client.X), (int)client.H);
            RightMenu = new Rectangle((int)split, (int)client.Y, (int)(client.Right - split), (int)client.H);

            // the unit note of the money charte, on the reserved first line
            string unitNote = "(all money values are per turn)";
            // centred over the table, on its reserved first line (maintainer bench)
            Label(new Vector2(client.X + (split - client.X - Fonts.Arial12.TextWidth(unitNote)) / 2, client.Y + 4),
                  unitNote, Fonts.Arial12, Color.Gray);

            // ---- LEFT 2/3: the colony table ----
            TableXpx = (int)LeftMenu.X + 10; // tighter left margin (maintainer bench)
            TableWpx = (int)LeftMenu.Width - 20 - 24; // reserve the scrollbar lane
            int headerY = (int)LeftMenu.Y + 24; // air under the unit note (maintainer bench)
            int sepBottom = (int)LeftMenu.Bottom - 14;

            // no NET column band (maintainer bench): the bold figures carry the column alone

            // headers centered over their columns, every one of them in the body's own
            // Arial12Bold - charte (Lek): a header distinguishes itself by weight, not size
            SbColony = new SortButton { Text = "Colony" };
            int nameW = (int)(TableWpx * NameColW);
            // charte (Lek): a header is never bigger than the body - weight, not size
            int nameTxtW = (int)Fonts.Arial12Bold.TextWidth(SbColony.Text);
            SbColony.rect = new Rectangle(TableXpx + (nameW - nameTxtW) / 2, headerY,
                                          nameTxtW, Fonts.Arial12Bold.LineSpacing);
            SortButtons = new SortButton[NumCols];
            for (int i = 0; i < NumCols; ++i)
            {
                int colLeft = TableXpx + (int)(TableWpx * ColStart(i));
                int colWpx = (int)(TableWpx * NumColW);
                // full label if it fits the column (2px of air each side), else the short form
                string label = Headers[i];
                int wTxt = (int)Fonts.Arial12Bold.TextWidth(label);
                if (wTxt > colWpx - 16)
                {
                    label = HeadersShort[i];
                    wTxt = (int)Fonts.Arial12Bold.TextWidth(label);
                }
                var sb = new SortButton { Text = label };
                // centred on the VALUE lane, not the raw column: the lane runs from the
                // separator (colLeft-4) to where the right-aligned numbers end (colLeft+colWpx-16)
                sb.rect = new Rectangle(colLeft - 4 + (colWpx - 12 - wTxt) / 2, headerY, wTxt, Fonts.Arial12Bold.LineSpacing);
                SortButtons[i] = sb;
            }

            var listRect = new RectF(TableXpx, headerY + 20, (int)LeftMenu.Width - 40, (int)LeftMenu.Height - 36 - 48);
            ColonySL = Add(new ScrollList<EconColonyItem>(listRect, 24));
            ColonySL.EnableItemHighlight = true; // the same row selector as the game's other tables
            FillList();

            // vertical separators BETWEEN the numeric columns only - no closing bar at the
            // table's end, the frame is the delimiter (maintainer bench)
            for (int i = 0; i < NumCols; ++i)
            {
                int sepX = TableXpx + (int)(TableWpx * ColStart(i)) - 4;
                // same warm line colour the game's other tables use (ShipListScreen)
                Panel(new Rectangle(sepX, headerY, 1, sepBottom - headerY), new Color(118, 102, 67, 255).Premultiplied());
            }

            // TOTAL footer
            int totalY = (int)listRect.Bottom + 10;
            var totalLbl = Label(new Vector2(TableXpx + 4, totalY), Localizer.Token(GameText.Total2).ToUpper(), Fonts.Arial12Bold);
            totalLbl.Color = Color.Wheat;
            void FooterCell(int col, Func<UILabel, string> getText)
            {
                var l = new UILabel(getText, Fonts.Arial12Bold);
                l.Pos = new Vector2(TableXpx + TableWpx * ColStart(col), totalY);
                l.Size = new Vector2(TableWpx * NumColW - 16, Fonts.Arial12Bold.LineSpacing);
                l.TextAlign = TextAlign.Right;
                Add(l);
            }
            void FooterMoney(int col, Func<float> getValue) => FooterCell(col, DynamicText(getValue, f => f.MoneyString()));
            void FooterPlain(int col, Func<float> getValue) => FooterCell(col, NeutralText(getValue, f => f.MoneyString()));
            UILabel FooterCellL(int col, Func<UILabel, string> getText)
            {
                var l = new UILabel(getText, Fonts.Arial12Bold);
                l.Pos = new Vector2(TableXpx + TableWpx * ColStart(col), totalY);
                l.Size = new Vector2(TableWpx * NumColW - 16, Fonts.Arial12Bold.LineSpacing);
                l.TextAlign = TextAlign.Right;
                Add(l);
                return l;
            }

            FooterCell(0, l => { l.Color = Color.White; return $"{Player.GetPlanets().Sum(p => p.PopulationBillion):0.00}"; });
            FooterPlain(1, () => Player.GetPlanets().Sum(EconColonyItem.PopIncome));
            FooterPlain(2, () => Player.GetPlanets().Sum(EconColonyItem.BldgIncome));
            FooterPlain(3, () => Player.GetPlanets().Sum(p => p.Money.GrossRevenue));
            FooterPlain(4, () => -Player.GetPlanets().Sum(p => p.Money.Maintenance));
            FooterPlain(5, () => -Player.GetPlanets().Sum(p => p.Money.TroopMaint));
            FooterMoney(6, () => Player.GetPlanets().Sum(EconColonyItem.NetIncome));
            var budgetTot = FooterCellL(7, l => { l.Color = Color.Wheat; return Player.GetPlanets().Sum(EconColonyItem.BudgetAlloc).MoneyString(); });
            budgetTot.Tooltip = "Per-planet allocations are EMA-smoothed slices of the empire pots, plus each colony's" +
                                " initial tolerance and terraform budget — so this sum drifts a few BC from the pots panel by design.";
            FooterPlain(8, () => Player.GetPlanets().Sum(EconColonyItem.GovExpense));
            FooterMoney(9, () => Player.GetPlanets().Sum(EconColonyItem.BudgetLeft));

            // ---- RIGHT 1/3: the synthesis, causal order (maintainer feedback) ----
            // auto-tax mode + sliders → governor budget (derived from the treasury
            // goal) → vertical arithmetic Income − Expenditure = Net Gain
            int rx = (int)RightMenu.X + 12; // tighter margins (maintainer bench)
            int rw = (int)RightMenu.Width - 24;
            var taxRect    = new Rectangle(rx, (int)RightMenu.Y + 42, rw, 104); // top rhythm = the left table's headerY, checkbox first

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
            AutoTaxCheckBox(new Rectangle(rx, (int)RightMenu.Y + 16, rw, 20));

            // each panel lays out its rows and takes its height from them, and the next
            // panel hangs 8px below the fitted bottom - heights are content, not constants
            SummaryPanel budget = BudgetTab(new Rectangle(rx, taxRect.Bottom + 8, rw, 170));
            budget.FitHeightToRows();
            SummaryPanel income = IncomesTab(new Rectangle(rx, (int)budget.Bottom + 8, rw, 155));
            income.FitHeightToRows();
            SummaryPanel costs = CostsTab(new Rectangle(rx, (int)income.Bottom + 8, rw, 195));
            costs.FitHeightToRows();

            // the net verdict: the word at the panel labels' own left edge, the figure in
            // the values' lane - one breath of air above (maintainer bench)
            float NetGainNow() => Player.NetIncome - Player.MoneySpendOnProductionNow;
            const int NetValueW = 110;
            int netY = (int)costs.Bottom + 20;
            var netWord = Label(new Vector2(rx + 4, netY), "", Fonts.Arial12Bold);
            netWord.DropShadow = true;
            netWord.Color = Colors.Cream;
            netWord.DynamicText = l => NetGainNow() >= 0f ? Localizer.Token(GameText.NetGain) : Localizer.Token(GameText.NetLoss);
            // -4: closes on the same right edge as the panel values above it (maintainer bench)
            EmpireNetIncome = Label(new Vector2(rx + rw - NetValueW - 4, netY), "", Fonts.Arial12Bold);
            EmpireNetIncome.Size = new Vector2(NetValueW, Fonts.Arial12Bold.LineSpacing);
            EmpireNetIncome.TextAlign = TextAlign.Right;
            EmpireNetIncome.DropShadow  = true;
            EmpireNetIncome.DynamicText = DynamicText(NetGainNow, f => f.MoneyString());

            base.LoadContent();
        }

        void FillList()
        {
            ColonySL.Reset();
            // Ludoal fork (bench 190): DOUBLE click, not single (maintainer feedback). Opening a colony
            // tears down this screen, so a stray click while reading the table threw you out
            // of it. Same gesture as the Empire screen's colony list.
            // ⚠ re-armed here rather than at construction: Reset drops the handlers.
            ColonySL.OnDoubleClick = OnColonyClicked;
            var planets = Player.GetPlanets();
            var sorted = SortByName
                ? (SortDesc ? planets.OrderByDescending(p => p.Name) : planets.OrderBy(p => p.Name))
                : (SortDesc ? planets.OrderByDescending(ColValue[SortCol]) : planets.OrderBy(ColValue[SortCol]));
            foreach (Planet p in sorted)
                ColonySL.AddItem(new EconColonyItem(p));
        }

        void OnColonyClicked(EconColonyItem item)
        {
            // the economy screen is the door into the diagnosis: a red row → why?
            GameAudio.AcceptClick();
            ExitScreen();
            // Ludoal fork (bench 191): right-click in the colony comes back HERE, not to the
            // map (maintainer feedback). The universe screen calls this once, when that colony closes.
            Universe.ReturnToListScreen = () => Universe.ScreenManager.AddScreen(new BudgetScreen(Universe));
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
        // outside the per-turn arithmetic. Two sub-sections (maintainer feedback): what the
        // planets receive (Colony + Defense), then Space Roads, then the total.
        // Shares sit LEFT of the values; share = of the allocated total.
        private SummaryPanel BudgetTab(Rectangle budgetRect)
        {
            // the note rides the title line (maintainer bench: one row back to the synthesis)
            SummaryPanel budget = Add(new SummaryPanel("Governor Budget  (allocated on treasury goal)", budgetRect, new Color(30, 26, 19)));
            budget.Spacer();
            float Pots() => Player.AI.ColonyBudget + Player.AI.SSPBudget + Player.AI.DefenseBudget;
            void PotItem(string name, Func<float> pot, Color keyColor)
            {
                // the share rides the name label, white like it — left of the value
                var key = new UILabel(l => { float t = Pots(); return t > 0f ? $"{name} ({pot() / t * 100:0}%)" : name; });
                key.Color = keyColor;
                budget.AddSplit(key, new UILabel(NeutralText(pot, f => f.MoneyString()))); // pots are nature, not results (charte)
            }
            PotItem("Colony", () => Player.AI.ColonyBudget, Color.White);
            PotItem("Defense", () => Player.AI.DefenseBudget, Color.White);
            budget.Spacer();
            budget.AddSplit(new UILabel("Planets subtotal", Color.Wheat),
                            new UILabel(DynamicText(() => Player.AI.ColonyBudget + Player.AI.DefenseBudget, f => f.MoneyString())));
            budget.Spacer();
            PotItem("Space Roads", () => Player.AI.SSPBudget, Color.White);
            budget.Spacer();
            budget.AddTotal(Pots);
            return budget;
        }

        private SummaryPanel CostsTab(Rectangle costRect)
        {
            SummaryPanel costs = Add(new SummaryPanel(GameText.Expenditure, costRect, new Color(27, 22, 25)));

            // planet-side lines first, then their subtotal, then the off-planet lines.
            // Building line = Gross − Net (the true maintenance sum, matches the table);
            // upstream's TotalBuildingMaintenance subtracts troop cost from it — the
            // 0.50 gap of the bench. Troop line = TroopCostOnPlanets, the figure the
            // treasury actually debits (and the table column sum), not just our own.
            float PlanetsExpense() => -(Player.GrossPlanetIncome - Player.NetPlanetIncomes
                                        + Player.TroopCostOnPlanets
                                        + Player.MoneySpendOnProductionThisTurn + Player.MoneySpendOnProductionNow);
            costs.Spacer();
            costs.AddItem("Building Maintenance", () => -(Player.GrossPlanetIncome - Player.NetPlanetIncomes));
            costs.AddItem("Troop Maintenance", () => -Player.TroopCostOnPlanets);
            costs.AddItem(GameText.ProductionFees, () => -(Player.MoneySpendOnProductionThisTurn+Player.MoneySpendOnProductionNow)); // "production costs."
            costs.Spacer();
            costs.AddSplit(new UILabel("Planets subtotal", Color.Wheat),
                           new UILabel(DynamicText(PlanetsExpense, f => f.MoneyString())));
            costs.Spacer();
            costs.AddItem("Ship Maintenance", () => -Player.TotalShipMaintenance);
            costs.AddItem("Espionage", () => -Player.EspionageCostLastTurn);
            costs.Spacer();

            costs.AddTotal(() => -(Player.AllSpending+Player.MoneySpendOnProductionNow));
            return costs;
        }

        private SummaryPanel IncomesTab(Rectangle incomeRect)
        {
            SummaryPanel income = Add(new SummaryPanel(GameText.Income, incomeRect, new Color(18, 29, 29)));

            income.Spacer();
            income.AddItem(GameText.PlanetaryTaxes, () => Player.GrossPlanetIncome); // "Planetary Taxes"
            income.AddItem("Trade (cargo + treaties)", () => Player.TotalTradeMoneyAddedThisTurn);
            income.AddItem("Excess Goods", () => Player.ExcessGoodsMoneyAddedThisTurn);
            income.AddItem("Money Leeched", () => Player.TotalMoneyLeechedLastTurn);
            income.AddItem(GameText.Other, () => Player.data.FlatMoneyBonus);
            income.Spacer();
            income.AddTotal(() => Player.GrossIncome);
            return income;
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

        // charte (Lek, étape 3): the NEUTRAL twin - a value whose nature never changes
        // (a cost is a cost) carries no colour; colour is reserved for results
        static Func<UILabel, string> NeutralText(Func<float> getValue, Func<float, string> stringify)
        {
            return (label) =>
            {
                float f = getValue();
                if (f > -0.005f && f < 0.005f) f = 0f; // kill the "-0.00" display
                // zeros in gray (maintainer taste): what produces or consumes nothing recedes
                label.Color = f == 0f ? Color.Gray : Colors.Cream;
                return stringify(f);
            };
        }

        // Ludoal fork: the other tabs live in their own screen, so leaving Economy hands over to it.
        // Its own index is a no-op: we are already here.
        void OnEmpireTabChanged(int index)
        {
            // one factory for the whole group (ScreenGroups) - this screen only says which tab it is
            ScreenGroups.SwitchEmpireTab(index, self: 3, Universe, this);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first, then ONE rule between the colony table
            // and the treasury column - the two halves share the group's frame rather than carrying
            // a border each.
            // the canonical fill rect - ClientArea stops short of the frame border and let the
            // map bleed through the rim (maintainer bench)
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);
            // no rule on the split either (maintainer bench): the gap is the separator
            base.Draw(batch, elapsed);
            SbColony.Draw(ScreenManager, Fonts.Arial12Bold);
            for (int i = 0; i < SortButtons.Length; ++i)
                SortButtons[i].Draw(ScreenManager, Fonts.Arial12Bold);
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        void OnSortClicked(int col, bool byName)
        {
            if (SortByName == byName && (byName || SortCol == col))
            {
                SortDesc = !SortDesc; // same header again: flip the direction
            }
            else
            {
                SortByName = byName;
                SortCol = col;
                SortDesc = !byName; // numbers biggest-first, names A-Z
            }
            SbColony.Selected = byName;
            for (int i = 0; i < SortButtons.Length; ++i)
                SortButtons[i].Selected = !byName && i == SortCol;
            FillList();
        }

        public override bool HandleInput(InputState input)
        {
            // Ludoal fork (bench 46.173): the closing key is tested BEFORE the top bar, not
            // after. The bar reads the same key to OPEN this screen and returns true, so with the
            // bar first the key never reached the line below and the screen would not close on
            // its own hotkey (maintainer feedback). The stock screen has no bar, which is why it never showed.
            if (input.KeyPressed(Keys.T) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            // every abbreviated or non-obvious header explains itself on hover (maintainer)
            for (int i = 0; i < SortButtons.Length; ++i)
                if (SortButtons[i].rect.HitTest(input.CursorPosition))
                    ToolTip.CreateTooltip(HeaderTips[i]);

            if (SbColony.HandleInput(input))
            {
                OnSortClicked(0, byName: true);
                return true;
            }
            for (int i = 0; i < SortButtons.Length; ++i)
            {
                if (SortButtons[i].HandleInput(input))
                {
                    OnSortClicked(i, byName: false);
                    return true;
                }
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
