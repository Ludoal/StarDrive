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
    // Left table: one row per colony, sortable columns, NET highlighted after the
    // upkeep columns (the order of the calculation is the order of reading),
    // deficits red, click opens the Colony Overview.
    public sealed class BudgetScreen : GameScreen
    {
        readonly Empire Player;
        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row, this screen being one tab
        // Ludoal fork: this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;
        // NOT Add()ed: geometry only. The group's frame is the border now, so drawing these would
        // double it - the two halves are separated by a single rule instead.
        Rectangle LeftMenu;
        Rectangle RightMenu;

        FloatSlider TaxSlider;
        FloatSlider TreasuryGoal;
        FloatSlider GovSpendingSlider;   // the governors' tap
        FloatSlider ColonyPotSlider;     // per-area ratios, titles carry the live pot values
        FloatSlider DefensePotSlider;
        FloatSlider SSPPotSlider;
        UILabel EmpireNetIncome;
        ScrollList<EconColonyItem> ColonySL;

        public UITable Table;    // the shared table charte owns geometry, headers and rules
        // static: the sort survives the screen for the session
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
            public SummaryPanel(LocalizedText title, in Rectangle rect, Color c, LocalizedText titleTip = default)
                : base(rect, c)
            {
                if (title.NotEmpty)
                {
                    Header = new UILabel(title, Fonts.Arial14Bold, Color.Wheat)
                    {
                        DropShadow = true,
                        Tooltip = titleTip
                    };
                }
                Padding     = new Vector2(4f, 1f); // tighter rows
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

            // a line break inside a block is a fixed SEPARATION, not a blank line whose height
            // happens to be a font's. The gap between the two rows it parts measures
            // LineBreakH whatever the row font is, so one number governs every block.
            const float LineBreakH = 20f;
            public void Spacer(float extra = 0f)
                => Add(new UISpacer(0f, Math.Max(0f, LineBreakH + extra - (Fonts.Arial12Bold.LineSpacing + Padding.Y))));

            // totals are regular rows, not the UIList Footer — the Footer pins to the
            // rect bottom and breaks the even row pitch
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
                // the tax rate is already baked into the income columns; the derivation
                // lives in the tooltip
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
            // vertical rule separates them. Two thirds / one third.
            // Content-sized frame, anchored bar and left. Width is the 900p width at EVERY
            // resolution - which is what makes all the columns fixed; height fills 900p, and
            // past it grows only as the planet list needs, capped by the screen.
            float contentW = 1440 - 2 * ScreenGroups.FrameMargin;
            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // floor = the info cartouche
            float h900 = 900 - ScreenGroups.TabRowY - ScreenGroups.FrameMargin;
            float rowsNeed = 60 + Player.GetPlanets().Count * 24 + 90; // header lane + rows + footer/margins
            float contentH = fullAvail <= h900 ? fullAvail
                           : Math.Min(fullAvail, Math.Max(h900, rowsNeed));
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), ScreenGroups.TabIndexOf(this),
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
                new UITable.Column { Title = "Bldg Upk",  Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "Building upkeep paid by the colony" },
                new UITable.Column { Title = "Troop Upk", Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "Troop upkeep paid by the colony" },
                new UITable.Column { Title = "Net",       Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Signed, Bold = true, Tip = "Net income of the colony" },
                new UITable.Column { Title = "Budget",    Align = TableAlign.Number, Sortable = true, Tip = "Budget allocated by the governor" },
                new UITable.Column { Title = "Gov Exp",   Align = TableAlign.Number, Sortable = true, Coloring = TableColor.Neutral, Tip = "What the governor actually spends: building upkeep plus SPACE defense - the delta against Bldg Mnt is the orbital defense bill" },
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

            int headerY = (int)client.Y + 24;
            Table.RowPitch = 28; // the 24px econ row plus the list's item padding
            // Ludoal fork: the table runs down to 10px off the frame's foot,
            // like the Ships list that falls cleanly.
            Table.Layout(client, headerY, client.Bottom - 10);
            // ONE frame, two halves: the synthesis column takes what the table leaves
            float split = Table.ListRect.Right + 10;
            LeftMenu  = new Rectangle((int)client.X, (int)client.Y, (int)(split - client.X), (int)client.H);
            RightMenu = new Rectangle((int)split, (int)client.Y, (int)(client.Right - split), (int)client.H);

            // the unit note of the money charte, centred over the table's reserved first line
            string unitNote = "(all money values are per turn)";
            Label(new Vector2(Table.TableRect.X + (Table.TableRect.Width - Fonts.Arial12.TextWidth(unitNote)) / 2, client.Y + 4),
                  unitNote, Fonts.Arial12, Color.Gray);

            // the TOTAL row keeps the table's last lane. Ludoal fork: the scrolling list stops
            // ONE row-pitch above the table foot, leaving exactly that lane for the TOTAL
            // footer just below it.
            var listRect = Table.ListRect;
            listRect.H -= Table.RowPitch;
            ColonySL = Add(new ScrollList<EconColonyItem>(listRect, 24));
            ColonySL.EnableItemHighlight = true;
            Table.ApplyHighlightTo(ColonySL);
            FillList();

            // TOTAL footer sits in the lane freed just below the list - CENTRED in that lane.
            // The lane runs from listRect.Bottom to the table foot (client.Bottom - 10),
            // one RowPitch tall.
            int totalY = (int)listRect.Bottom + (Table.RowPitch - Fonts.Arial12Bold.LineSpacing) / 2;
            var footerLabels = new Array<UILabel>(); // the whole row can nudge onto the net line below
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

            // ---- RIGHT 1/3: the synthesis, causal order ----
            // auto-tax mode + sliders → governor budget (derived from the treasury
            // goal) → vertical arithmetic Income − Expenditure = Net Gain
            int rx = (int)RightMenu.X + 12; // tighter margins
            int rw = (int)RightMenu.Width - 24;
            var taxRect    = new Rectangle(rx, (int)RightMenu.Y + 42, rw, 96); // top rhythm = the left table's headerY, checkbox first; the height is trimmed under the goal slider so Net Gain breathes at the foot, and the three blocks below hang off this bottom

            SummaryPanel tax = Add(new SummaryPanel("", taxRect, new Color(17, 21, 28)));

            TaxSlider = tax.AddSlider(Player.AutoTaxes ? "Tax Rate (auto)" : Localizer.Token(GameText.TaxRate), Player.data.TaxRate);
            TaxSlider.Tip = GameText.TaxesAreCollectedFromYour;
            TaxSlider.OnChange = TaxSliderOnChange;

            TreasuryGoal          = tax.AddSlider(GameText.BgtTargetName, Player.data.treasuryGoal);
            TreasuryGoal.Tip      = GameText.BgtTargetTip;
            TreasuryGoal.OnChange = TreasurySliderOnChange;
            // the amount rides its own line under the rail: a slider title carrying a figure
            // and a duration reads as a sentence, not as a setting
            TreasuryGoalValue = tax.Add(new UILabel(l => BudgetTargetLine(), Fonts.Arial12Bold));
            TreasuryGoalValue.Color = Color.White;

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
            // the values' lane - one breath of air above
            float NetGainNow() => Player.NetIncome - Player.MoneySpendOnProductionNow;
            const int NetValueW = 110;
            int netY = (int)costs.Bottom + 20;
            var netWord = Label(new Vector2(rx + 4, netY), "", Fonts.Arial12Bold);
            netWord.DropShadow = true;
            netWord.Color = Colors.Cream;
            netWord.DynamicText = l => NetGainNow() >= 0f ? Localizer.Token(GameText.NetGain) : Localizer.Token(GameText.NetLoss);
            // -4: closes on the same right edge as the panel values above it
            EmpireNetIncome = Label(new Vector2(rx + rw - NetValueW - 4, netY), "", Fonts.Arial12Bold);
            EmpireNetIncome.Size = new Vector2(NetValueW, Fonts.Arial12Bold.LineSpacing);
            EmpireNetIncome.TextAlign = TextAlign.Right;
            EmpireNetIncome.DropShadow  = true;
            EmpireNetIncome.DynamicText = DynamicText(NetGainNow, f => f.MoneyString());

            // the table's TOTAL lane always drops onto the synthesis' Net Gain/Loss line so
            // the two bottom rows read as one. Never up (a stretched table's TOTAL belongs to
            // its own lane); the two-lane bound is sanity against a degenerate layout, not a
            // tuning knob.
            int footerNudge = netY - totalY;
            if (footerNudge > 0 && footerNudge <= 2 * Table.RowPitch)
                foreach (UILabel l in footerLabels)
                    l.Pos = new Vector2(l.Pos.X, l.Pos.Y + footerNudge);

            base.LoadContent();
        }

        void FillList()
        {
            ColonySL.Reset();
            // Ludoal fork: DOUBLE click, not single. Opening a colony tears down this screen,
            // so a stray click while reading the table would throw you out of it. Same
            // gesture as the Empire screen's colony list.
            // ⚠ re-armed here rather than at construction: Reset drops the handlers.
            ColonySL.OnDoubleClick = OnColonyClicked;
            ColonySL.OnClick = item => Universe.PanToPlanetKeepZoom(item.Planet); // single-click = select on the map and pan at current zoom
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
            // Ludoal fork: armed before the panel - the colony wears the EMPIRE row,
            // Economy (3) as the Esc origin.
            Universe.HostColonyTab(item.Planet, ScreenGroups.Group.Empire, ScreenGroups.TabIndexOf(this));
            // a stacked page like every tab: exit + open in the same frame, the fresh ctor
            // claims the pause before any tick can run
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
        // outside the per-turn arithmetic. Two sub-sections: what the planets receive
        // (Colony + Defense), then Space Roads, then the total.
        // Shares sit LEFT of the values; share = of the allocated total.
        private SummaryPanel BudgetTab(Rectangle budgetRect)
        {
            // the note rides the title line
            SummaryPanel budget = Add(new SummaryPanel(Localizer.Token(GameText.BgtDistribution), budgetRect,
                                                       new Color(30, 26, 19), GameText.BgtDistributionTip));
            budget.Spacer();
            float Pots() => Player.AI.ColonyBudget + Player.AI.SSPBudget + Player.AI.DefenseBudget;
            var up = Universe.UState.P;
            // Ludoal fork: the governors' tap leads the panel - what share of
            // their AUTO allocations the governors may spend; the treasury keeps the rest.
            // Manual per-colony overrides bypass it.
            // the three LINKED shares as one-line rows [name][slider][lock][value] -
            // no percent text, the money value rides the right edge.
            // A LOCK pins a share: renormalization spreads over the unlocked ones only.
            // The Auto toggle pins the split on the default 55/25/20.
            var autoShares = budget.AddCheckbox(() => Universe.UState.P.AutoBudgetShares,
                title: "Auto split (55/25/20)",
                tooltip: "Locks the split on the default 55% Civil / 25% Defense / 20% Projectors. Untick to divide the pool yourself.");
            string civilName  = Localizer.Token(GameText.BgtCivilBuildings);
            string defName    = Localizer.Token(GameText.BgtDefenseStations);
            string projName   = Localizer.Token(GameText.BgtProjectors);
            // the name lane is measured on the longest of the three, never a fixed number:
            // the labels say who spends the share, so they are long and they are localized.
            float nameW = Math.Max(Fonts.Arial12Bold.TextWidth(civilName),
                          Math.Max(Fonts.Arial12Bold.TextWidth(defName),
                                   Fonts.Arial12Bold.TextWidth(projName))) + 8;
            ShareRows[0] = budget.Add(new ShareRow(civilName, up.ColonyBudgetShare,
                                                   () => Player.AI.ColonyBudget, sl => OnShareChanged(0),
                                                   nameW, GameText.BgtCivilBuildingsTip));
            ShareRows[1] = budget.Add(new ShareRow(defName, up.DefenseBudgetShare,
                                                   () => Player.AI.DefenseBudget, sl => OnShareChanged(1),
                                                   nameW, GameText.BgtDefenseStationsTip));
            ShareRows[2] = budget.Add(new ShareRow(projName, up.SSPBudgetShare,
                                                   () => Player.AI.SSPBudget, sl => OnShareChanged(2),
                                                   nameW, GameText.BgtProjectorsTip));
            ColonyPotSlider  = ShareRows[0].ShareSlider;
            DefensePotSlider = ShareRows[1].ShareSlider;
            SSPPotSlider     = ShareRows[2].ShareSlider;
            autoShares.OnChange = cb => ApplyAutoShares(cb.Checked);
            ApplyAutoShares(up.AutoBudgetShares); // initial lock state
            budget.Spacer(extra: 2f); // the total stands off its group by a hair more
            budget.AddTotal(Pots);
            AllowanceBlock(budget, up);
            return budget;
        }

        readonly ShareRow[] ShareRows = new ShareRow[3];
        UILabel TreasuryGoalValue;

        // the allowance closes the block: it governs what the three shares above become in
        // the colonies' hands, so it reads after them rather than before.
        void AllowanceBlock(SummaryPanel budget, Ship_Game.Universe.UniverseParams up)
        {
            budget.Spacer();
            GovSpendingSlider = budget.AddSlider(GameText.BgtGovAllowance, up.GovernorSpendingRatio);
            GovSpendingSlider.Tip = GameText.BgtGovAllowanceTip;
            // the allowance bites one step below this panel, on each colony's share - so the
            // two figures come from the colonies themselves. Snapping on change is what makes
            // the move visible: the allocations are smoothed and would drift there over turns.
            GovSpendingSlider.OnChange = sl =>
            {
                up.GovernorSpendingRatio = sl.RelativeValue;
                // this screen pauses the universe, so no turn will consume a snap flag:
                // the colonies are recomputed here and now, on the thread that owns them
                Universe.RunOnSimThread(() =>
                {
                    foreach (Planet p in Player.GetPlanets())
                    {
                        p.Budget?.SnapToTarget();
                        p.Budget?.Update();
                    }
                });
            };
            budget.AddItem(GameText.BgtMaySpend, GovernorsMaySpend);
            budget.AddItem(GameText.BgtWithheld, WithheldByAllowance);
        }

        // the figure under the goal rail: the amount first, the duration that set it after
        string BudgetTargetLine()
        {
            int turns = (int)(Player.data.treasuryGoal * Ship_Game.AI.EmpireAI.TreasuryGoalTurns);
            return $"{Player.AI.ProjectedMoney:0} BC ({turns} {Localizer.Token(GameText.BgtTurnsOfRevenue)})";
        }

        // the allowance acts one step below this panel: it trims each colony's own share.
        // Summing the colonies is therefore the only figure that matches what happens -
        // a product taken from the rows above would miss terraforming, the new-colony
        // tolerance and the colonies set to manual, which are not throttled at all.
        // read the colonies, not EmpireAI.PlanetBudgets: that list is filled only under
        // the debug flag, so off the debug build it is empty and both figures read zero.
        float GovernorsMaySpend()
        {
            float total = 0;
            foreach (Planet p in Player.GetPlanets())
                if (p.Budget != null) total += p.Budget.TotalAlloc;
            return total;
        }

        float WithheldByAllowance()
        {
            float total = 0;
            foreach (Planet p in Player.GetPlanets())
                if (p.Budget != null) total += p.Budget.WithheldByAllowance;
            return total;
        }

        // one line: [name][slider][padlock][live money value] - the compact grammar for
        // the linked shares
        class ShareRow : UIElementContainer
        {
            public readonly FloatSlider ShareSlider;
            public bool Locked;
            public bool AutoMode; // on Auto split the whole row is read-only - solid padlock
            readonly UILabel NameLbl;
            readonly UIButton LockBtn;
            readonly UILabel Value;

            // one refresher, called from every state change - the tint must not depend
            // solely on layout time, or a padlock click gives no visible feedback.
            public void RefreshLockState()
            {
                LockBtn.IconTint = (AutoMode || Locked) ? Color.White : Color.White.Alpha(0.35f);
                ShareSlider.Enabled = !AutoMode && !Locked;
                LockBtn.Enabled = !AutoMode;
            }

            readonly float NameW;

            public ShareRow(string name, float value, Func<float> livePot, Action<FloatSlider> onChange,
                            float nameW, LocalizedText tooltip)
                : base(Vector2.Zero, new Vector2(100, 20))
            {
                NameW = nameW;
                NameLbl = base.Add(new UILabel(Vector2.Zero, name, Fonts.Arial12Bold, Color.White)
                                   { Tooltip = tooltip });
                ShareSlider = base.Add(new FloatSlider(SliderStyle.Percent, new Vector2(80, 12), "", 0f, 1f, value)
                {
                    DrawValueText = false,
                    OnChange = onChange,
                });
                LockBtn = base.Add(new UIButton(new UIButton.StyleTextures("NewUI/icon_lock", "NewUI/icon_lock"),
                                                Vector2.Zero, "")
                {
                    Tooltip = GameText.LockShareTooltip,
                });
                LockBtn.OnClick = b =>
                {
                    Locked = !Locked;
                    RefreshLockState();
                };
                Value = base.Add(new UILabel(l => livePot().MoneyString(), Fonts.Arial12Bold));
                Value.Color = Color.White;
            }

            public override void PerformLayout()
            {
                // the text lane rides 4px BELOW the slider's seat so text, padlock and value
                // centre on the track - the slider itself keeps its Y
                const int LockW = 18, ValueW = 52, Gap = 6;
                float cy = Y + 6;
                NameLbl.Pos = new Vector2(X, cy);
                ShareSlider.Pos  = new Vector2(X + NameW, Y + 1);
                ShareSlider.Size = new Vector2(Width - NameW - LockW - ValueW - 2 * Gap + 32, 12); // +32: the track is Width-32
                LockBtn.Rect = new Rectangle((int)(Right - ValueW - Gap - LockW), (int)Y + 5, 16, 16);
                Value.Pos = new Vector2(Right - ValueW + 6, cy);
                Value.TextAlign = TextAlign.Right;
                Value.Size = new Vector2(ValueW - 6, Fonts.Arial12Bold.LineSpacing);
                RefreshLockState();
                base.PerformLayout();
            }
        }

        // Auto = the split pinned on 55/25/20 and the sliders frozen; untick to take over
        void ApplyAutoShares(bool auto)
        {
            var up = Universe.UState.P;
            if (auto)
            {
                LinkingShares = true;
                up.ColonyBudgetShare  = 0.55f;
                up.DefenseBudgetShare = 0.25f;
                up.SSPBudgetShare     = 0.20f;
                ColonyPotSlider.RelativeValue  = 0.55f;
                DefensePotSlider.RelativeValue = 0.25f;
                SSPPotSlider.RelativeValue     = 0.20f;
                LinkingShares = false;
            }
            // enable/tint per row, not a flat "!auto" - that would re-enable LOCKED
            // sliders, defeating the padlock. Auto also releases the pins.
            for (int i = 0; i < 3; i++)
            {
                if (auto) ShareRows[i].Locked = false;
                ShareRows[i].AutoMode = auto;
                ShareRows[i].RefreshLockState();
            }
        }

        bool LinkingShares; // guard: renormalizing the others re-fires their OnChange

        void OnShareChanged(int which)
        {
            if (LinkingShares)
                return;
            LinkingShares = true;
            float[] v = { ColonyPotSlider.RelativeValue, DefensePotSlider.RelativeValue, SSPPotSlider.RelativeValue };
            // locked shares hold their value: the mover is clamped to what the locks leave,
            // and the remainder spreads over the UNLOCKED others by their mutual ratio
            float lockedSum = 0f;
            float unlockedSum = 0f;
            for (int i = 0; i < 3; i++)
            {
                if (i == which) continue;
                if (ShareRows[i].Locked) lockedSum += v[i];
                else unlockedSum += v[i];
            }
            v[which] = v[which].Clamped(0f, (1f - lockedSum).LowerBound(0f));
            float rest = 1f - lockedSum - v[which];
            for (int i = 0; i < 3; i++)
            {
                if (i == which || ShareRows[i].Locked) continue;
                v[i] = unlockedSum > 0.0001f ? rest * v[i] / unlockedSum : rest; // one unlocked: it takes it all
            }
            var up = Universe.UState.P;
            up.ColonyBudgetShare = v[0];
            up.DefenseBudgetShare = v[1];
            up.SSPBudgetShare = v[2];
            ColonyPotSlider.RelativeValue = v[0];
            DefensePotSlider.RelativeValue = v[1];
            SSPPotSlider.RelativeValue = v[2];
            LinkingShares = false;
        }

        private SummaryPanel CostsTab(Rectangle costRect)
        {
            SummaryPanel costs = Add(new SummaryPanel(GameText.Expenditure, costRect, new Color(27, 22, 25)));

            // planet-side lines first, then their subtotal, then the off-planet lines.
            // Building line = Gross − Net (the true maintenance sum, matches the table);
            // upstream's TotalBuildingMaintenance subtracts troop cost from it, which
            // would understate this line. Troop line = TroopCostOnPlanets, the figure the
            // treasury actually debits (and the table column sum), not just our own.
            float PlanetsExpense() => -(Player.GrossPlanetIncome - Player.NetPlanetIncomes
                                        + Player.TroopCostOnPlanets
                                        + Player.MoneySpendOnProductionThisTurn + Player.MoneySpendOnProductionNow);
            costs.Spacer();
            costs.AddItem("Building Upkeep", () => -(Player.GrossPlanetIncome - Player.NetPlanetIncomes));
            costs.AddItem("Troop Upkeep", () => -Player.TroopCostOnPlanets);
            costs.AddItem(GameText.ProductionFees, () => -(Player.MoneySpendOnProductionThisTurn+Player.MoneySpendOnProductionNow)); // "production costs."
            costs.Spacer();
            costs.AddSplit(new UILabel("Planets subtotal", Color.Wheat),
                           new UILabel(DynamicText(PlanetsExpense, f => f.MoneyString())));
            costs.Spacer();
            costs.AddItem("Ship Upkeep", () => -Player.TotalShipMaintenance);
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

            Player.AI.RunEconomicPlanner(); // Update() rewrites the label from the fresh figure

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
            ScreenGroups.SwitchEmpireTab(index, self: ScreenGroups.TabIndexOf(this), Universe, this);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();
            // Ludoal fork: the frame fill by hand and first, then ONE rule between the colony table
            // and the treasury column - the two halves share the group's frame rather than carrying
            // a border each.
            // the canonical fill rect - ClientArea stops short of the frame border and lets the
            // map bleed through the rim
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);
            // no rule on the split either: the gap is the separator
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
            // Ludoal fork: the closing key is tested BEFORE the top bar, not after. The bar
            // reads the same key to OPEN this screen and returns true, so if the bar ran
            // first the key would never reach the line below and the screen would not
            // close on its own hotkey.
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
            TreasuryGoal.Text = Localizer.Token(GameText.BgtTargetName);
            // in auto the rate is not the player's to read as a setting, so the line says so
            TaxSlider.Text = Player.AutoTaxes
                ? Localizer.Token(GameText.BgtAutoTaxLine)
                : Localizer.Token(GameText.TaxRate);
            // the cells are LIVE but the ORDER is a snapshot of the click - re-apply the
            // standing sort each new star date, or values drift out of sorted order
            if (Player.Universe.StarDate != LastSortedDate)
            {
                LastSortedDate = Player.Universe.StarDate;
                FillList();
            }
            base.Update(fixedDeltaTime);
        }
    }
}
