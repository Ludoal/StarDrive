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
    // Ludoal fork: the Automation tab of the Empire group. It ships UNBOUND (H went to
    // Policies, which is opened far more often) and is reached from the tab row; a player
    // who binds it a key gets the close-on-key behaviour below back.
    //
    // Categories: COLONIZATION, CONSTRUCTION, TRADE, NOTIFICATIONS. Each wears its own one-tab
    // frame and they are ALL visible at once.
    //
    // The EMPIRE box and the Prioritization column left for the Policies tab: what belongs
    // here answers "do it for me", what moved answers "do it THIS way".
    public sealed class AutomationScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu EmpireTabs;
        // Ludoal fork: this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;

        DropOptions<int> FreighterDropDown, ColonyShipDropDown, ScoutDropDown,
                         ConstructorDropDown, ResearchStationDropDown, MiningStationDropDown;
        // (player feedback) the garrison's troop: a mode or a template name, see GarrisonTroopValue
        DropOptions<string> GarrisonTroopDropDown;
        bool ResearchStationsEnabled, MiningOpsEnabled;

        // fixed box geometry - the boxes own their sizes, the columns just stack them.
        // Heights: one-tab strip (~24) + 12 top pad + 26 per row (a checked-dropdown rides
        // its toggle's row now, so it costs the same 26 as a plain checkbox) + 12 bottom pad.
        // BoxW2: the dropdown boxes are WIDER instead of taller - label room + picker.
        const float BoxW = 320f, BoxW2 = 450f, BoxGap = 10f;
        // Expansion loses Auto Governor (-26); Deep Space Building gave Auto-terraform back to
        // Policies>Colony (-26), which is where a command that acts on a colony we already hold
        // belongs; Freighters lost the priority row and Inter-Empire Trade (-52), also to Policies.
        const float ColonizationBoxH = 130f, ConstructionBoxH = 139f,
                    TradeBoxH = 152f,
                    TroopsBoxH = 74f, // one row: the garrison troop picker
                    // two switches + slider label + slider + the column title + seven paired
                    // category rows + the Miscellaneous heading + Inhibition, at 26 per row.
                    NotificationsBoxH = 446f; // +26: the Remove-on-left-click switch
        // the second column's X inside the Notifications frame, a constant the rows are placed
        // FROM - never a share of the width left over (bench 523)
        const float CatColumnSplit = 148f;

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
            // Two columns: [Notifications] and [Colonization / Construction / Trade].
            float col1H = NotificationsBoxH;
            float col2H = ColonizationBoxH + BoxGap + ConstructionBoxH + BoxGap + TradeBoxH + BoxGap + TroopsBoxH;
            float contentW = 9 + 10 + BoxW + BoxGap + BoxW2 + 10 + 9;  // ClientArea insets + gutters
            float contentH = 60 + Math.Max(col1H, col2H) + 22;  // tab strip + cross clearance + pads
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), ScreenGroups.TabIndexOf(this),
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

            UIList notifications = NewBox(new RectF(x0, top, BoxW, NotificationsBoxH), "Notifications");
            var P = Universe.UState.P;

            // Ludoal fork (wishlist): two switches above the families, both about the OLDEST
            // notification - the head of the queue. Whether you want a family on screen at all is
            // the per-family box below; that is the only question those rows answer now.
            // Both off by default: the stock conduct, nothing shown without a hover, nothing
            // clearing by itself.
            notifications.AddCheckbox(() => GlobalStats.ShowOldestNotificationText,
                                      title: "Show oldest Notification text",
                                      tooltip: "The oldest notification keeps its text on screen instead of waiting for a hover");
            notifications.AddCheckbox(() => GlobalStats.AutoClearOldest,
                                      title: "Auto-clear oldest",
                                      tooltip: "Only the oldest ages out, after the delay below, so the pile empties in the order it filled");
            // (maintainer feedback) on by default - the stock conduct; off, a left click opens the
            // page and keeps the notification, a right click still drops it without opening anything
            notifications.AddCheckbox(() => GlobalStats.RemoveNotificationOnLeftClick,
                                      title: "Remove on left click",
                                      tooltip: "A left click opens the notification's page and removes it. Unchecked, the page opens and the notification stays; a right click always removes it without opening the page");

            // How long the head of the queue stands before it ages out. 0 = off, nothing clears.
            notifications.Add(new UILabel(GameText.NotificationAutoClear, Fonts.Arial12Bold, Colors.Cream)).Tooltip = GameText.NotificationAutoClearTip;
            // Height must contain the 26px crosshair knob, or it overflows below its declared box
            // and the next row overlaps the handle (bench 485).
            var autoClear = notifications.Add(new FloatSlider(SliderStyle.Decimal, new Vector2(BoxW - 40, 28),
                                                              "", 0, 60, GlobalStats.NotificationAutoClearSeconds)
            {
                Step = 1,
                Tip = GameText.NotificationAutoClearTip,
                TrackYOffset = -5, // tuck the rail up close under its title; the box still holds the knob (bench 486)
            });
            autoClear.OnChange = s => GlobalStats.NotificationAutoClearSeconds = s.AbsoluteValue;

            // One row per notification category. POSITIVE voice: checked = you SEE this category
            // (bitmask NotificationHiddenCategories, all shown by default). Whether you want a
            // family at all is the only question here; how it leaves the screen is the one switch
            // above. A few categories carry indented SHOW sub-options - fine filters that grey
            // with their parent category.
            //
            // (bench 523) The families sit in TWO columns, titled, so the ladder stops growing
            // downward as sub-options are added. The split is by WHOLE family: a category and its
            // indented sub-options always stay in the same column, never astride the two.
            notifications.Add(new UILabel("Categories Shown", Fonts.Arial12Bold, Colors.Cream));

            (NotificationCategory cat, string title, LocalizedText tip)[] leftCats =
            {
                (NotificationCategory.Exploration,  "Exploration",  GameText.NotifCatExplorationTip),
                (NotificationCategory.Colony,       "Colony",       GameText.NotifCatColonyTip),
                (NotificationCategory.Construction, "Construction", GameText.NotifCatConstructionTip),
                (NotificationCategory.Combat,       "Combat",       GameText.NotifCatCombatTip),
            };
            (NotificationCategory cat, string title, LocalizedText tip)[] rightCats =
            {
                (NotificationCategory.Diplomacy,    "Diplomacy",    GameText.NotifCatDiplomacyTip),
                (NotificationCategory.Espionage,    "Espionage",    GameText.NotifCatEspionageTip),
                (NotificationCategory.Economy,      "Economy",      GameText.NotifCatEconomyTip),
                (NotificationCategory.Events,       "Events",       GameText.NotifCatEventsTip),
                (NotificationCategory.Threats,      "Threats",      GameText.NotifCatThreatsTip),
            };

            var left = new Array<UIElementV2>();
            var right = new Array<UIElementV2>();
            BuildColumn(leftCats, left);
            BuildColumn(rightCats, right);

            for (int i = 0; i < Math.Max(left.Count, right.Count); ++i)
            {
                if (i < left.Count && i < right.Count)
                    notifications.Add(new SplitElement(left[i], right[i]) { Split = CatColumnSplit });
                else
                    notifications.Add(i < left.Count ? left[i] : right[i]);
            }

            void BuildColumn((NotificationCategory cat, string title, LocalizedText tip)[] cats,
                             Array<UIElementV2> column)
            {
                foreach ((NotificationCategory cat, string title, LocalizedText tip) in cats)
                {
                    NotificationCategory c = cat; // capture per iteration
                    var subBoxes = new Array<UICheckBox>(); // indented Show sub-options that grey with the parent
                    var showBox = new UICheckBox(0f, 0f, () => !GlobalStats.IsHiddenCategory(c),
                                                 show =>
                                                 {
                                                     GlobalStats.SetHiddenCategory(c, !show);
                                                     foreach (UICheckBox sub in subBoxes) sub.Greyed = !show;
                                                 },
                                                 Fonts.Arial12Bold, title, tip);
                    column.Add(showBox);

                    // indented Show sub-options, checked by default (positive voice), greyed when
                    // the parent category is hidden - they filter WITHIN the category.
                    void AddSub(Func<bool> get, Action<bool> set, string subTitle, LocalizedText subTip)
                    {
                        var sub = new UICheckBox(0f, 0f, get, set, Fonts.Arial12Bold, subTitle, subTip)
                            { Indent = 18, Greyed = GlobalStats.IsHiddenCategory(c) };
                        subBoxes.Add(sub);
                        column.Add(sub);
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
            }
            // Inhibition Alerts stays here (maintainer feedback) but out of the auto-clear group:
            // it is a map OVERLAY toggle, not a notification, so it has nothing to auto-clear. Its
            // own heading says as much, so it does not read as a tenth category without a column.
            notifications.Add(new UILabel("Miscellaneous", Fonts.Arial12Bold, Colors.Cream));
            notifications.AddCheckbox(() => !P.DisableInhibitionWarning, v => P.DisableInhibitionWarning = !v,
                                      title: "Inhibition Alerts (map overlay)", tooltip: GameText.InhibitionAlertsAreDisplayedWhen);

            // (player feedback) the troop the governor rebuilds a garrison with - the same family
            // as the model pickers above: what the machine takes when it builds for you.
            UIList troops = NewBox(new RectF(x1, top + ColonizationBoxH + BoxGap + ConstructionBoxH + BoxGap + TradeBoxH + BoxGap, BoxW2, TroopsBoxH), "Troops");
            GarrisonTroopDropDown = troops.Add(new LabeledDropdown<string>())
                .Create(GameText.GarrisonTroop, GameText.GarrisonTroopTip);
            GarrisonTroopDropDown.OnValueChange = v => SetGarrisonTroop(player, v);
            troops.ReverseZOrder();

            UIList trade = NewBox(new RectF(x1, top + ColonizationBoxH + BoxGap + ConstructionBoxH + BoxGap, BoxW2, TradeBoxH), "Freighters");
            // The picker names the shared Freighter Model that Auto-build and Auto-upgrade both
            // use; its Auto Pick box picks the best model when checked, or reveals the manual
            // list when unchecked.
            FreighterDropDown = trade.Add(new CheckedDropdown())
                .CreateTitled(GameText.FreighterModel, GameText.FreighterModelTip, autoPick: () => player.AutoPickBestFreighter);
            trade.AddCheckbox(() => player.AutoBuildFreighters, title: GameText.AutoBuildFreighters, tooltip: GameText.AutoBuildFreightersTip);
            trade.AddCheckbox(() => player.AutoUpgradeFreighters, title: GameText.AutoUpgradeFreighters, tooltip: GameText.AutoUpgradeFreightersTip);
            trade.AddCheckbox(() => player.AutoScrapIdleFreighters, title: GameText.AutoScrapIdleFreighters, tooltip: GameText.AutoScrapIdleFreightersTip);

            trade.ReverseZOrder(); // an open list draws over the rows beneath it

            UIList construction = NewBox(new RectF(x1, top + ColonizationBoxH + BoxGap, BoxW2, ConstructionBoxH), "Deep Space Building");
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

            UIList colonization = NewBox(new RectF(x1, top, BoxW2, ColonizationBoxH), "Expansion");
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
            colonization.ReverseZOrder(); // an open list draws over the rows beneath it

            UpdateDropDowns();
            base.LoadContent();
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
            => ScreenGroups.SwitchEmpireTab(index, self: ScreenGroups.TabIndexOf(this), Universe, this);

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

        // The picker's value: "" = Cheapest, "*" = Best, otherwise a template name. The two modes
        // are not names, so they cannot collide with a mod's troop.
        const string GarrisonCheapestValue = "", GarrisonBestValue = "*";

        static string GarrisonTroopValue(Empire player)
            => player.GarrisonTroopMode == Empire.GarrisonBest  ? GarrisonBestValue
             : player.GarrisonTroopMode == Empire.GarrisonNamed ? player.GarrisonTroop
             : GarrisonCheapestValue;

        static void SetGarrisonTroop(Empire player, string v)
        {
            player.GarrisonTroopMode = v == GarrisonCheapestValue ? Empire.GarrisonCheapest
                                     : v == GarrisonBestValue     ? Empire.GarrisonBest
                                     : Empire.GarrisonNamed;
            player.GarrisonTroop = player.GarrisonTroopMode == Empire.GarrisonNamed ? v : null;
        }

        // the two modes first, then every template the empire can build, cheapest first - the
        // list fills itself as techs unlock troops, since the screen is rebuilt on each opening
        void RebuildGarrisonTroopOptions(Empire player)
        {
            if (GarrisonTroopDropDown == null)
                return;
            GarrisonTroopDropDown.Clear();
            GarrisonTroopDropDown.AddOption(GameText.GarrisonTroopCheapest, GarrisonCheapestValue);
            GarrisonTroopDropDown.AddOption(GameText.GarrisonTroopBest, GarrisonBestValue);
            foreach (Troop t in ResourceManager.GetTroopTemplatesFor(player))
                GarrisonTroopDropDown.AddOption(t.Name, t.Name);
            // a named troop no longer buildable shows as Cheapest, which is what the refill does
            if (!GarrisonTroopDropDown.SetActiveValue(GarrisonTroopValue(player)))
                GarrisonTroopDropDown.ActiveIndex = 0;
        }

        void UpdateDropDowns()
        {
            Empire player = Universe.Player;
            EmpireData pd = player.data;

            RebuildGarrisonTroopOptions(player);
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

        // the Notifications block edits GlobalStats - the settings file, not the save - and
        // every way out of the screen passes here (bench 603)
        public override void ExitScreen()
        {
            GlobalStats.SaveSettings();
            base.ExitScreen();
        }

        public override bool HandleInput(InputState input)
        {
            // its own key closes what its own key opened - unbound by default, so in practice
            // right-click, like every table screen of the group
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

    }
}
