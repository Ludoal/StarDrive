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

        public UITable Table;    // the shared table charte owns geometry, headers and rules
        // static: the sort survives the screen for the session (maintainer bench 307)
        static int SortCol = 6;  // NET by default
        static bool SortDesc = true;
        static bool SortByName;

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

        // one colony = one row. Cells sit on the shared table charte's columns, so
        // rows, headers and totals stay aligned by construction.
        class EconColonyItem : ScrollListItem<EconColonyItem>
        {
            public readonly Planet Planet;
            readonly UITable Table;
            public EconColonyItem(UITable table, Planet p) { Table = table; Planet = p; }

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
                int y = (int)Y;
                RemoveAll();

                UITable.Column[] cols = Table.Columns;
                Rectangle c0 = cols[0].Rect;
                Panel(new Rectangle(c0.X + UITable.PadX, y + 2, 18, 18), ResourceManager.Texture(Planet.IconPath));
                var name = Label(new Vector2(c0.X + UITable.PadX + 24, y + 4), Planet.Name, Fonts.Arial12Bold);
                name.Color = Color.White;

                UILabel Cell(int col, Func<UILabel, string> getText)
                {
                    var l = new UILabel(getText, cols[col].CellFont);
                    Rectangle r = cols[col].Rect;
                    l.Pos = new Vector2(r.X, y + 4);
                    l.Size = new Vector2(r.Width - UITable.PadX, Fonts.Arial12.LineSpacing);
                    l.TextAlign = TextAlign.Right;
                    Add(l);
                    return l;
                }
                // the column's own Coloring decides how the value wears colour
                UILabel ValueCell(int col, Func<float> getValue)
                    => Cell(col, ColorText(cols[col].Coloring, getValue, f => f.MoneyString()));

                Cell(1, l => { l.Color = UITable.ValueColor(TableColor.Plain, Planet.PopulationBillion); return $"{Planet.PopulationBillion:0.00}"; });
                ValueCell(2, () => PopIncome(Planet));
                ValueCell(3, () => BldgIncome(Planet));
                var gross = ValueCell(4, () => Planet.Money.GrossRevenue);
                // the tax rate column moved here (maintainer feedback): the rate is already
                // baked into the income columns, the derivation lives in the tooltip
                float baseRate = Planet.Owner != null ? Planet.Owner.data.TaxRate * 100f : 0f;
                gross.Tooltip = $"pop {PopIncome(Planet).MoneyString()} + buildings {BldgIncome(Planet).MoneyString()}" +
                                $" — effective tax {Planet.Money.TaxRate * 100f:0.#}% (empire {baseRate:0.#}% × local bonus)";
                ValueCell(5, () => -Planet.Money.Maintenance);
                ValueCell(6, () => -Planet.Money.TroopMaint);
                ValueCell(7, () => NetIncome(Planet));
                Cell(8, l => { float v = BudgetAlloc(Planet); l.Color = v > 0f ? Color.Wheat : Color.Gray; return v.MoneyString(); });
                ValueCell(9, () => GovExpense(Planet));
                ValueCell(10, () => BudgetLeft(Planet));

                base.PerformLayout();
            }
        }

        // the value floor a numeric column sizes itself against (plus its own title)
        static readonly string[] MoneyFloor = { "-9999.99" };

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
            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // bench 343: capped at 1080p
            float h900 = 900 - ScreenGroups.TabRowY - ScreenGroups.FrameMargin;
            float rowsNeed = 60 + Player.GetPlanets().Count * 24 + 90; // header lane + rows + footer/margins
            float contentH = fullAvail <= h900 ? fullAvail
                           : Math.Min(fullAvail, Math.Max(h900, rowsNeed));
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), 3,
                                                    OnEmpireTabChanged, contentW, contentH);
            RectF client = EmpireTabs.ClientArea;

            // ---- LEFT: the colony table, on the shared charte (UITable) ----
            Table = new UITable(new[]
            {
                new UITable.Column { Title = "Colony", Sortable = true },
                new UITable.Column { Title = "Pop",       Align = TableAlign.Number, Sortable = true, Tip = "Population, in billions" },
                new UITable.Column { Title = "Pop Inc",   Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "Tax income from the colonists" },
                new UITable.Column { Title = "Bldg Inc",  Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "Tax income from the buildings" },
                new UITable.Column { Title = "Gross",     Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "Gross tax revenue (colonists + buildings)" },
                new UITable.Column { Title = "Bldg Mnt",  Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "Building maintenance paid by the colony" },
                new UITable.Column { Title = "Troop Mnt", Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "Troop maintenance paid by the colony" },
                new UITable.Column { Title = "Net",       Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Signed, Bold = true, Tip = "Net income of the colony" },
                new UITable.Column { Title = "Budget",    Align = TableAlign.Number, Sortable = true, Tip = "Budget allocated by the governor" },
                new UITable.Column { Title = "Gov Exp",   Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "What the governor actually spends: building maintenance plus SPACE defense - the delta against Bldg Mnt is the orbital defense bill" },
                new UITable.Column { Title = "Left",      Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Signed, Tip = "Budget left after the governor's spending" },
            });
            // widths from the data: the planet names size the Colony column (plus its icon
            // lane); a numeric column takes its own title or a money figure, whichever is wider
            var names = new Array<string>();
            foreach (Planet p in Player.GetPlanets())
                names.Add(p.Name);
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, names);
            Table.Columns[0].Width += 28; // the planet icon rides ahead of the name
            for (int i = 1; i < Table.Columns.Length; ++i)
                UITable.AutoSize(Table.Columns[i], Fonts.Arial12Bold, MoneyFloor);
            // the sorted column reads orange from the first frame (spec)
            if (SortByName) Table.Columns[0].Sorted = true;
            else            Table.Columns[1 + SortCol].Sorted = true;

            // back to one lane under the note: the class breathes around its own header
            // rule now, the extra empty line doubled it (maintainer, 4 Aug)
            int headerY = (int)client.Y + 24;
            Table.RowPitch = 28; // the 24px econ row plus the list's item padding
            // Ludoal fork (maintainer feedback): the table runs down to 10px off the frame's foot,
            // like the Ships list that falls cleanly - the old value left a gap.
            Table.Layout(client, headerY, client.Bottom - 10);
            // ONE frame, two halves: the synthesis column takes what the table leaves
            float split = Table.ListRect.Right + 10;
            LeftMenu  = new Rectangle((int)client.X, (int)client.Y, (int)(split - client.X), (int)client.H);
            RightMenu = new Rectangle((int)split, (int)client.Y, (int)(client.Right - split), (int)client.H);

            // the unit note of the money charte, centred over the table's reserved first line
            string unitNote = "(all money values are per turn)";
            Label(new Vector2(Table.TableRect.X + (Table.TableRect.Width - Fonts.Arial12.TextWidth(unitNote)) / 2, client.Y + 4),
                  unitNote, Fonts.Arial12, Color.Gray);

            // the TOTAL row keeps the table's last lane. Ludoal fork (maintainer feedback): the
            // scrolling list stops ONE row-pitch above the table foot, leaving exactly that lane
            // for the TOTAL footer just below it - copied from the clean Ships list rather than
            // reserving a hand-guessed band.
            var listRect = Table.ListRect;
            listRect.H -= Table.RowPitch;
            ColonySL = Add(new ScrollList<EconColonyItem>(listRect, 24));
            ColonySL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(ColonySL);
            FillList();

            // TOTAL footer sits in the lane freed just below the list - CENTRED in that lane so it
            // rides near the frame's foot rather than hugging the list top (maintainer bench 343: it
            // read too high off the bottom edge). The lane runs from listRect.Bottom to the table
            // foot (client.Bottom - 10), one RowPitch tall.
            int totalY = (int)listRect.Bottom + (Table.RowPitch - Fonts.Arial12Bold.LineSpacing) / 2;
            var footerLabels = new Array<UILabel>(); // bench 361: the whole row nudges onto the net line below
            var totalLbl = Label(new Vector2(Table.Columns[0].Rect.X + UITable.PadX + 28, totalY), Localizer.Token(GameText.Total2).ToUpper(), Fonts.Arial12Bold);
            totalLbl.Color = Color.Wheat;
            footerLabels.Add(totalLbl);
            UILabel FooterCell(int col, Func<UILabel, string> getText)
            {
                var l = new UILabel(getText, Fonts.Arial12Bold);
                Rectangle r = Table.Columns[col].Rect;
                l.Pos = new Vector2(r.X, totalY);
                l.Size = new Vector2(r.Width - UITable.PadX, Fonts.Arial12Bold.LineSpacing);
                l.TextAlign = TextAlign.Right;
                Add(l);
                footerLabels.Add(l);
                return l;
            }
            void FooterMoney(int col, Func<float> getValue) => FooterCell(col, DynamicText(getValue, f => f.MoneyString()));
            void FooterPlain(int col, Func<float> getValue) => FooterCell(col, NeutralText(getValue, f => f.MoneyString()));

            FooterCell(1, l => { l.Color = Color.White; return $"{Player.GetPlanets().Sum(p => p.PopulationBillion):0.00}"; });
            FooterPlain(2, () => Player.GetPlanets().Sum(EconColonyItem.PopIncome));
            FooterPlain(3, () => Player.GetPlanets().Sum(EconColonyItem.BldgIncome));
            FooterPlain(4, () => Player.GetPlanets().Sum(p => p.Money.GrossRevenue));
            FooterPlain(5, () => -Player.GetPlanets().Sum(p => p.Money.Maintenance));
            FooterPlain(6, () => -Player.GetPlanets().Sum(p => p.Money.TroopMaint));
            FooterMoney(7, () => Player.GetPlanets().Sum(EconColonyItem.NetIncome));
            var budgetTot = FooterCell(8, l => { l.Color = Color.Wheat; return Player.GetPlanets().Sum(EconColonyItem.BudgetAlloc).MoneyString(); });
            budgetTot.Tooltip = "Per-planet allocations are EMA-smoothed slices of the empire pots, plus each colony's" +
                                " initial tolerance and terraform budget — so this sum drifts a few BC from the pots panel by design.";
            FooterPlain(9, () => Player.GetPlanets().Sum(EconColonyItem.GovExpense));
            FooterMoney(10, () => Player.GetPlanets().Sum(EconColonyItem.BudgetLeft));

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

            // bench 361 (maintainer): while the table is SHORT (not yet stretched), its TOTAL lane
            // sits a few px above the synthesis' Net Gain/Loss line - nudge the whole footer row
            // down onto netY so the two bottom lines read as one. Never up (a stretched table's
            // TOTAL belongs to its own lane), and never further than one lane (geometry sanity).
            int footerNudge = netY - totalY;
            if (footerNudge > 0 && footerNudge <= Table.RowPitch)
                foreach (UILabel l in footerLabels)
                    l.Pos = new Vector2(l.Pos.X, l.Pos.Y + footerNudge);

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
                ColonySL.AddItem(new EconColonyItem(Table, p));
        }

        void OnColonyClicked(EconColonyItem item)
        {
            // the economy screen is the door into the diagnosis: a red row → why?
            GameAudio.AcceptClick();
            // Ludoal fork (spec: colony-as-tab): armed before the panel - the colony wears the
            // EMPIRE row, Economy (3) as the Esc origin. Replaces the ReturnToList trio.
            Universe.HostColonyTab(item.Planet, ScreenGroups.Group.Empire, 3);
            // a stacked page like every tab (migration, bench 386): exit + open in the same
            // frame, the fresh ctor claims the pause before any tick can run
            ExitScreen();
            Universe.ScreenManager.AddScreen(new ColonyScreen(Universe, item.Planet, Universe.EmpireUI));
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

        // Dynamic Text label; invoked every time the label draws - the colour comes
        // from the shared charte (UITable.ValueColor), one source for every table
        static Func<UILabel, string> ColorText(TableColor kind, Func<float> getValue,
                                               Func<float, string> stringify)
        {
            return (label) =>
            {
                float f = getValue();
                if (f > -0.005f && f < 0.005f) f = 0f; // kill the "-0.00" display
                label.Color = UITable.ValueColor(kind, f);
                return stringify(f);
            };
        }

        static Func<UILabel, string> DynamicText(Func<float> getValue, Func<float, string> stringify)
            => ColorText(TableColor.Signed, getValue, stringify);

        // charte (Lek, étape 3): the NEUTRAL twin - a value whose nature never changes
        // (a cost is a cost) carries no colour; colour is reserved for results
        static Func<UILabel, string> NeutralText(Func<float> getValue, Func<float, string> stringify)
            => ColorText(TableColor.Neutral, getValue, stringify);

        // Ludoal fork: the other tabs live in their own screen, so leaving Economy hands over to it.
        // Its own index is a no-op: we are already here.
        void OnEmpireTabChanged(int index)
        {
            // one factory for the whole group (ScreenGroups) - this screen only says which tab it is
            ScreenGroups.SwitchEmpireTab(index, self: 3, Universe, this);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first, then ONE rule between the colony table
            // and the treasury column - the two halves share the group's frame rather than carrying
            // a border each.
            // the canonical fill rect - ClientArea stops short of the frame border and let the
            // map bleed through the rim (maintainer bench)
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);
            // no rule on the split either (maintainer bench): the gap is the separator
            base.Draw(batch, elapsed);
            // the shared charte draws the headers, the rule and the column separators
            Table.DrawChrome(batch);
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        // header clicks come from the shared table charte: column 0 sorts by name,
        // the numeric columns map to ColValue (their table index minus one)
        void OnHeaderClicked(int col)
        {
            bool byName = col == 0;
            int numCol = byName ? SortCol : col - 1;
            if (SortByName == byName && (byName || SortCol == numCol))
            {
                SortDesc = !SortDesc; // same header again: flip the direction
            }
            else
            {
                SortByName = byName;
                SortCol = numCol;
                SortDesc = !byName; // numbers biggest-first, names A-Z
            }
            foreach (UITable.Column c in Table.Columns)
                c.Sorted = false;
            Table.Columns[col].Sorted = true;
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

            // headers - tooltips, hover and sort clicks - through the shared charte
            int clicked = Table.HandleInput(input);
            if (clicked >= 0)
            {
                GameAudio.AcceptClick();
                OnHeaderClicked(clicked);
                return true;
            }

            if (input.Escaped || input.RightMouseClick)
            {
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        float LastSortedDate;

        public override void Update(float fixedDeltaTime)
        {
            TreasuryGoal.Text = $"{Localizer.Token(GameText.TreasuryGoal)} : {Player.AI.ProjectedMoney:0.00}";
            // the cells are LIVE but the ORDER was a snapshot of the click (maintainer
            // bench 307: a few turns in, the Net column read shuffled) - re-apply the
            // standing sort each new star date
            if (Player.Universe.StarDate != LastSortedDate)
            {
                LastSortedDate = Player.Universe.StarDate;
                FillList();
            }
            base.Update(fixedDeltaTime);
        }
    }
}
