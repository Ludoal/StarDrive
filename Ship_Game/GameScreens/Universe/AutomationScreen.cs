using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI; // SplitElement (two controls sharing one row)

namespace Ship_Game
{
    // Ludoal fork: the Automation tab of the Empire group. The H shortcut opens this tab.
    //
    // Categories: EMPIRE, COLONIZATION, CONSTRUCTION, TRADE, NOTIFICATIONS. Each wears its own
    // one-tab frame and they are ALL visible at once.
    public sealed class AutomationScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu EmpireTabs;
        // Ludoal fork: this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;

        DropOptions<int> FreighterDropDown, ColonyShipDropDown, ScoutDropDown,
                         ConstructorDropDown, ResearchStationDropDown, MiningStationDropDown;
        DropOptions<CargoPriority> FreighterPriorityDropDown;
        bool ResearchStationsEnabled, MiningOpsEnabled;

        // fixed box geometry - the boxes own their sizes, the columns just stack them.
        // Heights: one-tab strip (~24) + 12 top pad + 26 per row (a checked-dropdown rides
        // its toggle's row now, so it costs the same 26 as a plain checkbox) + 12 bottom pad.
        // BoxW2: the dropdown boxes are WIDER instead of taller - label room + picker.
        const float BoxW = 320f, BoxW2 = 450f, BoxW3 = 300f, BoxGap = 10f;
        const float EmpireBoxH = 160f, ColonizationBoxH = 156f, ConstructionBoxH = 139f, // +26: Auto-explore split adds a row (Send New Explorers)
                    TradeBoxH = 204f, NotificationsBoxH = 476f, PriorityBoxH = 330f; // frame just tall enough for its content: at 456 Inhibition spilled, at 500 it left empty space that stretched the window (bench 487)

        public AutomationScreen(UniverseScreen u) : base(u, toPause: u)
        {
            Universe = u;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        public override void LoadContent()
        {
            RemoveAll();
            // the frame hugs its content, anchored on the bar and the left margin.
            // Two columns: [Empire / Notifications] and [Colonization / Construction / Trade].
            float col1H = EmpireBoxH + BoxGap + NotificationsBoxH;
            float col2H = ColonizationBoxH + BoxGap + ConstructionBoxH + BoxGap + TradeBoxH;
            float contentW = 9 + 10 + BoxW + BoxGap + BoxW2 + BoxGap + BoxW3 + 10 + 9;  // ClientArea insets + gutters
            float contentH = 60 + Math.Max(Math.Max(col1H, col2H), PriorityBoxH) + 22;  // tab strip + cross clearance + pads
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), 5,
                                                    OnEmpireTabChanged, contentW, contentH);
            ResearchStationsEnabled = !Universe.Player.Universe.P.DisableResearchStations;
            MiningOpsEnabled       = !Universe.Player.Universe.P.DisableMiningOps;

            RectF client = EmpireTabs.ClientArea;
            float top = ScreenGroups.GroupContentTop(client);
            float x0 = client.X + 10, x1 = x0 + BoxW + BoxGap;
            Empire player = Universe.Player;

            // ⚠ within a column the LOWER box is added FIRST: an open dropdown's list spills
            // below its own row, and add order is draw order - the spill must land on top
            // of the neighbour, not under it.

            UIList notifications = NewBox(new RectF(x0, top + EmpireBoxH + BoxGap, BoxW, NotificationsBoxH), "Notifications");
            var P = Universe.UState.P;

            // Each category's Auto-clear box greys out when EITHER its category is hidden OR the
            // timer is 0 (a 0 timer means nothing auto-clears, so every box would be a no-op - the
            // global setting mustn't leave live-looking boxes that do nothing).
            var autoClearBoxes = new Array<(NotificationCategory cat, UICheckBox box)>();
            void RefreshAutoClearGrey()
            {
                bool timerOff = GlobalStats.NotificationAutoClearSeconds <= 0f;
                foreach ((NotificationCategory cat, UICheckBox box) in autoClearBoxes)
                    box.Greyed = timerOff || GlobalStats.IsHiddenCategory(cat);
            }

            // Ludoal fork (wishlist): a global switch, above the nine families - it is not a
            // tenth family. The head of the queue keeps its text on screen; the others still
            // show theirs on hover. Off by default: stock conduct unchanged.
            notifications.AddCheckbox(() => GlobalStats.ShowFirstNotificationText,
                                      title: "Show first Notification text",
                                      tooltip: "The oldest notification, the one auto-clear takes next, keeps its text on screen");
            notifications.AddCheckbox(() => GlobalStats.AutoClearFirstOnly,
                                      title: "Auto-clear oldest first",
                                      tooltip: "Only the oldest notification ages out; the ones behind it wait their turn. A family with no auto-clear holds the queue until dismissed");

            // FIRST: the auto-clear timer. Its label titles the whole block - read the duration,
            // then tick which categories it applies to below. 0 = off (nothing auto-clears).
            notifications.Add(new UILabel(GameText.NotificationAutoClear, Fonts.Arial12Bold, Colors.Cream)).Tooltip = GameText.NotificationAutoClearTip;
            // Height must contain the 26px crosshair knob, or it overflows below its declared box
            // and the next row overlaps the handle (bench 485: the 7px height did exactly that).
            var autoClear = notifications.Add(new FloatSlider(SliderStyle.Decimal, new Vector2(BoxW - 40, 28),
                                                              "", 0, 60, GlobalStats.NotificationAutoClearSeconds)
            {
                Step = 1,
                Tip = GameText.NotificationAutoClearTip,
                TrackYOffset = -5, // tuck the rail up close under its title; the box still holds the knob (bench 486)
            });
            autoClear.OnChange = s => { GlobalStats.NotificationAutoClearSeconds = s.AbsoluteValue; RefreshAutoClearGrey(); };

            // One row per notification category (the old scattered Disable*/Suppress* toggles are
            // folded into these nine). POSITIVE voice: the left box checked = you SEE this category
            // (bitmask NotificationHiddenCategories, all shown by default). The right box is the
            // per-category Auto-clear opt-in ("Auto-clear"); it GREYS OUT (read-only, click-refused,
            // state kept) when its category is hidden - a hidden category has nothing to auto-clear.
            // A few categories carry indented SHOW sub-options (the old noisy alerts, kept as fine
            // filters); they grey with their parent category too.
            (NotificationCategory cat, string title, LocalizedText tip)[] cats =
            {
                (NotificationCategory.Exploration,  "Exploration",  GameText.NotifCatExplorationTip),
                (NotificationCategory.Colony,       "Colony",       GameText.NotifCatColonyTip),
                (NotificationCategory.Construction, "Construction", GameText.NotifCatConstructionTip),
                (NotificationCategory.Combat,       "Combat",       GameText.NotifCatCombatTip),
                (NotificationCategory.Diplomacy,    "Diplomacy",    GameText.NotifCatDiplomacyTip),
                (NotificationCategory.Espionage,    "Espionage",    GameText.NotifCatEspionageTip),
                (NotificationCategory.Economy,      "Economy",      GameText.NotifCatEconomyTip),
                (NotificationCategory.Events,       "Events",       GameText.NotifCatEventsTip),
                (NotificationCategory.Threats,      "Threats",      GameText.NotifCatThreatsTip),
            };
            foreach ((NotificationCategory cat, string title, LocalizedText tip) in cats)
            {
                NotificationCategory c = cat; // capture per iteration
                var subBoxes = new Array<UICheckBox>(); // indented Show sub-options that grey with the parent
                var autoClearBox = new UICheckBox(0f, 0f, () => GlobalStats.IsAutoClearCategory(c),
                                                  on => GlobalStats.SetAutoClearCategory(c, on),
                                                  Fonts.Arial12Bold, GameText.AutoClearShort, GameText.NotificationAutoClearTip)
                { Greyed = GlobalStats.NotificationAutoClearSeconds <= 0f || GlobalStats.IsHiddenCategory(c) };
                autoClearBoxes.Add((c, autoClearBox));
                var showBox = new UICheckBox(0f, 0f, () => !GlobalStats.IsHiddenCategory(c),
                                             show =>
                                             {
                                                 GlobalStats.SetHiddenCategory(c, !show);
                                                 RefreshAutoClearGrey(); // timer + category together
                                                 foreach (UICheckBox sub in subBoxes) sub.Greyed = !show;
                                             },
                                             Fonts.Arial12Bold, title, tip);
                notifications.Add(new SplitElement(showBox, autoClearBox) { Split = 150f, Tooltip = tip });

                // indented Show sub-options, checked by default (positive voice on the old flags),
                // greyed when the parent category is hidden - they filter WITHIN the category.
                void AddSub(Func<bool> get, Action<bool> set, string subTitle, LocalizedText subTip)
                {
                    var sub = new UICheckBox(0f, 0f, get, set, Fonts.Arial12Bold, subTitle, subTip)
                        { Indent = 18, Greyed = GlobalStats.IsHiddenCategory(c) };
                    subBoxes.Add(sub);
                    notifications.Add(sub);
                }
                if (c == NotificationCategory.Colony)
                {
                    AddSub(() => !P.DisableVolcanoWarning,    v => P.DisableVolcanoWarning = !v,    "Volcano",    GameText.DisableVolcanoActivationOrDeactivation);
                    AddSub(() => !P.DisableStarvationWarning, v => P.DisableStarvationWarning = !v, "Starvation", GameText.EnableStarvationWarningTip);
                }
                else if (c == NotificationCategory.Combat)
                {
                    AddSub(() => !P.DisableCrashSiteWarning, v => P.DisableCrashSiteWarning = !v, "Crash Sites", GameText.DisableCrashSiteAlertsTip);
                }
            }
            // Inhibition Alerts stays here (Ludo) but out of the auto-clear group: it is a map
            // OVERLAY toggle, not a notification, so it has nothing to auto-clear. The tooltip says so
            // it doesn't read as a forgotten row missing its right-hand box.
            notifications.AddCheckbox(() => !P.DisableInhibitionWarning, v => P.DisableInhibitionWarning = !v,
                                      title: "Inhibition Alerts (map overlay)", tooltip: GameText.InhibitionAlertsAreDisplayedWhen);

            UIList empire = NewBox(new RectF(x0, top, BoxW, EmpireBoxH), "Empire");
            empire.AddCheckbox(() => player.AutoTaxes, title: GameText.AutoTaxes, tooltip: GameText.YourEmpireWillAutomaticallyManage3);
            empire.AddCheckbox(() => player.AutoResearch, title: GameText.AutoResearch, tooltip: GameText.YourEmpireWillAutomaticallySelect);
            empire.AddCheckbox(() => player.AutoBuildTerraformers, title: GameText.AutoBuildTerraformers, tooltip: GameText.AutoBuildTerraformersTip);
            empire.AddCheckbox(() => RushConstruction, title: GameText.RushAllConstruction, tooltip: GameText.RushAllConstructionTip);

            UIList trade = NewBox(new RectF(x1, top + ColonizationBoxH + BoxGap + ConstructionBoxH + BoxGap, BoxW2, TradeBoxH), "Trade");
            // Freighter Priority first (Ludo's call): which cargo is served first under a shortage.
            // The notice (Policies phase 0) spells out that it only bites when cargos are scarce.
            FreighterPriorityDropDown = trade.Add(new LabeledDropdown<CargoPriority>())
                .Create(GameText.FreighterPriority, GameText.FreighterPriorityTip);
            FreighterPriorityDropDown.AddOption(GameText.FreighterPriorityAuto, CargoPriority.Auto);
            FreighterPriorityDropDown.AddOption(GameText.FreighterPriorityProductionFirst, CargoPriority.ProductionFirst);
            FreighterPriorityDropDown.AddOption(GameText.FreighterPriorityColonistsFirst, CargoPriority.ColonistsFirst);
            FreighterPriorityDropDown.ActiveValue = player.CargoPriority;
            FreighterPriorityDropDown.OnValueChange = v => player.CargoPriority = v;

            // The old single "Automatic Trade" toggle is dissected into three checkboxes below.
            // The picker (kept from that control, minus its lead toggle) names the shared Freighter
            // Model that Auto-build and Auto-upgrade both use; its Auto Pick box picks the best
            // model when checked, or reveals the manual list when unchecked.
            FreighterDropDown = trade.Add(new CheckedDropdown())
                .CreateTitled(GameText.FreighterModel, GameText.FreighterModelTip, autoPick: () => player.AutoPickBestFreighter);
            trade.AddCheckbox(() => player.AutoBuildFreighters, title: GameText.AutoBuildFreighters, tooltip: GameText.AutoBuildFreightersTip);
            trade.AddCheckbox(() => player.AutoUpgradeFreighters, title: GameText.AutoUpgradeFreighters, tooltip: GameText.AutoUpgradeFreightersTip);
            trade.AddCheckbox(() => player.AutoScrapIdleFreighters, title: GameText.AutoScrapIdleFreighters, tooltip: GameText.AutoScrapIdleFreightersTip);
            trade.AddCheckbox(() => Universe.UState.P.AllowPlayerInterTrade, title: GameText.AllowPlayerInterTradeTitle, tooltip: GameText.AllowPlayerInterTradeTip);

            trade.ReverseZOrder(); // an open list draws over the rows beneath it

            UIList construction = NewBox(new RectF(x1, top + ColonizationBoxH + BoxGap, BoxW2, ConstructionBoxH), "Construction");
            ConstructorDropDown = construction.Add(new CheckedDropdown())
                .Create(() => player.AutoBuildSpaceRoads, Localizer.Token(GameText.Autobuild) + " Projectors", GameText.YourEmpireWillAutomaticallyCreate2,
                        autoPick: () => player.AutoPickConstructors);
            if (ResearchStationsEnabled)
                ResearchStationDropDown = construction.Add(new CheckedDropdown())
                    .Create(() => player.AutoBuildResearchStations, title: GameText.AutoBuildResearchStation, tooltip: GameText.AutoBuildResearchStationTip,
                            autoPick: () => player.AutoPickBestResearchStation);
            if (MiningOpsEnabled)
                MiningStationDropDown = construction.Add(new CheckedDropdown())
                    .Create(() => player.AutoBuildMiningStations, title: GameText.AutoBuildMiningStation, tooltip: GameText.AutoBuildMiningStationTip,
                            autoPick: () => player.AutoPickBestMiningStation);

            construction.ReverseZOrder(); // an open list draws over the rows beneath it

            UIList colonization = NewBox(new RectF(x1, top, BoxW2, ColonizationBoxH), "Colonization");
            // Auto-explore split into two jobs: build new scouts (keeps the model picker), and
            // send idle scouts out to explore (a plain toggle, checked by default in a new game).
            ScoutDropDown = colonization.Add(new CheckedDropdown())
                .Create(() => player.AutoBuildExplorers, title: GameText.AutoBuildExplorers, tooltip: GameText.AutoBuildExplorersTip,
                        autoPick: () => player.AutoPickBestScout);
            colonization.AddCheckbox(() => player.SendNewExplorersToExplore, title: GameText.SendNewExplorersToExplore,
                                     tooltip: GameText.SendNewExplorersToExploreTip);
            ColonyShipDropDown = colonization.Add(new CheckedDropdown())
                .Create(() => player.AutoColonize, title: GameText.Autocolonize, tooltip: GameText.YourEmpireWillAutomaticallyCreate,
                        autoPick: () => player.AutoPickBestColonizer);
            // ⚠ "Auto Governor" decides whether a new colony gets an ASSESSED governor -
            // see Planet_Colonize.SetupColonyType.
            colonization.AddCheckbox(() => player.AutoCoreGovernor, title: "Auto Governor",
                                     tooltip: GameText.AutoGovernorTip); // Policies phase 0: token, not a bare string

            colonization.ReverseZOrder(); // an open list draws over the rows beneath it

            // ── third column: the construction Prioritization list ──
            // Upper block = the prioritized categories, their ORDER is the hierarchy; arrows
            // reorder, the inhibit glyph demotes. Lower block = the rest; the plus promotes.
            // Acts at queue INSERTION only (SBProduction) - reordering never reshuffles
            // queues already filled.
            float x2 = x1 + BoxW2 + BoxGap;
            NewBox(new RectF(x2, top, BoxW3, PriorityBoxH), "Prioritization");
            PriorityHost = Add(new UIPanel(new Rectangle((int)x2 + 6, (int)top + 30,
                                                         (int)BoxW3 - 12, (int)PriorityBoxH - 36),
                                           new Color(0, 0, 0, 0)));
            RebuildPriorityRows();

            UpdateDropDowns();
            base.LoadContent();
        }

        // the categories the queue insertion knows, in display order (keys match SBProduction)
        static readonly (string Key, string Label)[] PriorityCategories =
        {
            ("Explorers",        "Explorers"),
            ("Colonizers",       "Colonizers"),
            ("Projectors",       "Projectors"),
            ("ResearchStations", "Research Stations"),
            ("MiningStations",   "Mining Stations"),
            ("Freighters",       "Freighters"),
            ("Troops",           "Troops"),
            ("MilitaryShips",    "Military Ships"),
        };

        UIPanel PriorityHost;

        static string LabelOf(string key)
        {
            foreach ((string k, string label) in PriorityCategories)
                if (k == key) return label;
            return key;
        }

        UIButton IconBtn(string normal, string hover, in Rectangle r, LocalizedText tip, Action onClick)
        {
            var b = new UIButton(new UIButton.StyleTextures(normal, hover, hover), Vector2.Zero, "")
            {
                Tooltip = tip,
                OnClick = _ => onClick(),
                ClickSfx = "sd_ui_accept_alt3",
            };
            b.Rect = r;
            return b;
        }

        void RebuildPriorityRows()
        {
            PriorityHost.RemoveAll();
            var prio = Universe.UState.P.ConstructionPriorities;
            int x = (int)PriorityHost.X, w = (int)PriorityHost.Width;
            int y = (int)PriorityHost.Y;
            const int RowH = 26, Icon = 20;

            void Section(string title, GameText tip = 0)
            {
                // bench 462: the explainer rides the section label, not the tab title
                PriorityHost.Add(tip != 0
                    ? new UILabel(new Vector2(x, y + 3), title, Fonts.Arial12Bold, Colors.Cream, tip)
                    : new UILabel(new Vector2(x, y + 3), title, Fonts.Arial12Bold, Colors.Cream));
                y += RowH - 2;
            }
            UILabel Row(string key)
            {
                var l = new UILabel(new Vector2(x + 8, y + 4), LabelOf(key), Fonts.Arial12Bold, Color.White);
                PriorityHost.Add(l);
                return l;
            }
            Rectangle Slot(int fromRight) => new(x + w - fromRight, y + (RowH - Icon) / 2, Icon, Icon);

            Section("Prioritize", GameText.PrioritizationHeaderTip);
            for (int i = 0; i < prio.Count; i++)
            {
                string key = prio[i];
                Row(key);
                int idx = i;
                if (i > 0)
                    PriorityHost.Add(IconBtn("NewUI/icon_queue_arrow_up", "NewUI/icon_queue_arrow_up_hover1",
                                             Slot(72), "Higher priority", () => MoveCategory(idx, -1)));
                if (i < prio.Count - 1)
                    PriorityHost.Add(IconBtn("NewUI/icon_queue_arrow_down", "NewUI/icon_queue_arrow_down_hover1",
                                             Slot(48), "Lower priority", () => MoveCategory(idx, +1)));
                PriorityHost.Add(IconBtn("NewUI/icon_queue_delete", "NewUI/icon_queue_delete_hover1",
                                         Slot(24), "Stop prioritizing this category", () => DemoteCategory(key)));
                y += RowH;
            }
            if (prio.Count == 0)
            {
                PriorityHost.Add(new UILabel(new Vector2(x + 8, y + 4), "Nothing prioritized", Fonts.Arial12, Color.Gray));
                y += RowH;
            }

            y += 6;
            Section("Do not prioritize");
            foreach ((string key, string _) in PriorityCategories)
            {
                if (prio.Contains(key))
                    continue;
                Row(key);
                PriorityHost.Add(IconBtn("NewUI/icon_build_add", "NewUI/icon_build_add_hover1",
                                         Slot(24), "Prioritize this category", () => PromoteCategory(key)));
                y += RowH;
            }
        }

        void MoveCategory(int index, int delta)
        {
            var prio = Universe.UState.P.ConstructionPriorities;
            string key = prio[index];
            prio.RemoveAt(index);
            prio.Insert(index + delta, key);
            RebuildPriorityRows();
        }

        void DemoteCategory(string key)
        {
            Universe.UState.P.ConstructionPriorities.Remove(key);
            RebuildPriorityRows();
        }

        void PromoteCategory(string key)
        {
            Universe.UState.P.ConstructionPriorities.Add(key);
            RebuildPriorityRows();
        }

        // one category box: a one-tab frame bearing the category's name, with its rows inside
        UIList NewBox(in RectF r, LocalizedText title)
        {
            var box = Add(new Submenu(r, new[] { title }));
            box.PerformLayout();
            UIList list = AddList(new Vector2(box.ClientArea.X + 12, box.ClientArea.Y + 12));
            list.Padding = new Vector2(2f, 10f);
            // NOTE: ReverseZOrder is a one-shot gesture on the rows a list already holds, so a
            // list carrying a dropdown calls it itself once its rows are in - here it is a no-op.
            return list;
        }

        void OnEmpireTabChanged(int index)
            => ScreenGroups.SwitchEmpireTab(index, self: 5, Universe, this);

        void InitDropOptions(DropOptions<int> options, ref string automationShip, string defaultShip, Func<IShipDesign, bool> predicate)
        {
            if (options == null)
                return;
            options.Clear();

            foreach (IShipDesign ship in Universe.Player.ShipsWeCanBuildSnapshot)
            {
                if (predicate(ship))
                    options.AddOption(ship.Name, 0);
            }

            if (!options.SetActiveEntry(automationShip))
            {
                if (!options.SetActiveEntry(defaultShip))
                    options.AddOption(defaultShip, 0);
                automationShip = defaultShip;
            }
        }

        // Checked but nothing buildable yet: give the picker a single "Not available" entry so it
        // never shows as an empty (broken-looking) list. Cleared again once a model is unlocked.
        void ShowNotAvailableIfEmpty(DropOptions<int> options, bool canBuild)
        {
            if (options == null)
                return;
            if (!canBuild || options.Count == 0)
            {
                options.Clear();
                options.AddOption(Localizer.Token(GameText.NotAvailable), 0);
            }
        }

        void UpdateDropDowns()
        {
            Empire player = Universe.Player;
            EmpireData pd = player.data;

            InitDropOptions(ScoutDropDown, ref pd.CurrentAutoScout, pd.StartingScout,
                ship =>
                {
                    if (GlobalStats.Defaults.ReconDropDown)
                        return ship.IsShipGoodToBuild(player) &&
                              (ship.Role == RoleName.scout || ship.ShipCategory == ShipCategory.Recon);
                    return ship.IsShipGoodToBuild(player) &&
                          (ship.Role == RoleName.scout || ship.Role == RoleName.fighter ||
                           ship.ShipCategory == ShipCategory.Recon);
                });
            InitDropOptions(ColonyShipDropDown, ref pd.CurrentAutoColony, pd.DefaultColonyShip,
                ship => ship.IsShipGoodToBuild(player) && ship.IsColonyShip);
            InitDropOptions(ConstructorDropDown, ref pd.CurrentConstructor, pd.DefaultConstructor,
                ship => ship.IsShipGoodToBuild(player) && ship.IsConstructor);
            InitDropOptions(FreighterDropDown, ref pd.CurrentAutoFreighter, pd.DefaultSmallTransport,
                ship => ship.IsShipGoodToBuild(player) && ship.IsFreighter);
            if (player.CanBuildResearchStations)
                InitDropOptions(ResearchStationDropDown, ref pd.CurrentResearchStation, pd.DefaultResearchStation,
                    ship => ship.IsShipGoodToBuild(player) && ship.IsResearchStation);
            if (player.CanBuildMiningStations)
                InitDropOptions(MiningStationDropDown, ref pd.CurrentMiningStation, pd.DefaultMiningStation,
                    ship => ship.IsShipGoodToBuild(player) && ship.IsMiningStation);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();

            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);

            // auto-pick hides the manual picker; the gated ones also need their tech
            Empire player = Universe.Player;
            if (ScoutDropDown != null)     ScoutDropDown.Visible     = !player.AutoPickBestScout;
            if (ColonyShipDropDown != null) ColonyShipDropDown.Visible = !player.AutoPickBestColonizer;
            if (ConstructorDropDown != null) ConstructorDropDown.Visible = !player.AutoPickConstructors;
            if (FreighterDropDown != null) FreighterDropDown.Visible  = !player.AutoPickBestFreighter;
            if (ResearchStationDropDown != null)
            {
                bool canBuild = player.CanBuildResearchStations;
                if (canBuild && ResearchStationDropDown.Count == 0)
                    UpdateDropDowns(); // tech completed while the tab is open - populate late
                // Checked but nothing to pick yet (tech not unlocked): show a "Not available"
                // entry rather than an empty list (an empty list reads as a load bug).
                ShowNotAvailableIfEmpty(ResearchStationDropDown, canBuild);
                ResearchStationDropDown.Visible = !player.AutoPickBestResearchStation;
            }
            if (MiningStationDropDown != null)
            {
                bool canBuild = player.CanBuildMiningStations;
                if (canBuild && MiningStationDropDown.Count == 0)
                    UpdateDropDowns();
                ShowNotAvailableIfEmpty(MiningStationDropDown, canBuild);
                MiningStationDropDown.Visible = !player.AutoPickBestMiningStation;
            }

            base.Draw(batch, elapsed);
            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar - the popup veil must not grey it
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            // H closes what H opened; right-click closes like every table screen of the group
            if ((input.AutomationWindow && !GlobalStats.TakingInput) || input.RightMouseClick)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (base.HandleInput(input))
            {
                // save the active designs back - only the pickers of the visible category exist
                EmpireData pd = Universe.Player.data;
                if (FreighterDropDown != null)  pd.CurrentAutoFreighter = FreighterDropDown.ActiveName;
                if (ColonyShipDropDown != null) pd.CurrentAutoColony    = ColonyShipDropDown.ActiveName;
                if (ConstructorDropDown != null) pd.CurrentConstructor  = ConstructorDropDown.ActiveName;
                if (ScoutDropDown != null)      pd.CurrentAutoScout     = ScoutDropDown.ActiveName;
                if (ResearchStationDropDown != null && Universe.Player.CanBuildResearchStations)
                    pd.CurrentResearchStation = ResearchStationDropDown.ActiveName;
                if (MiningStationDropDown != null && Universe.Player.CanBuildMiningStations)
                    pd.CurrentMiningStation = MiningStationDropDown.ActiveName;
                return true;
            }
            return false;
        }

        bool RushConstruction
        {
            get => Universe.Player.RushAllConstruction;
            set
            {
                Universe.Player.RushAllConstruction = value;
                Universe.RunOnSimThread(() => Universe.Player.SwitchRushAllConstruction(value));
            }
        }
    }
}
