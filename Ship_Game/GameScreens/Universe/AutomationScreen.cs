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

namespace Ship_Game
{
    // Ludoal fork: the Automation tab of the Empire group (maintainer design). These settings
    // lived in a floating overlay window docked to the right screen edge - which had no reason
    // to sit on the map, fought the deep-space build menu for that edge, and lately had its
    // last lines buried under the minimap. The window dies with its minimap icon; the H
    // shortcut opens this tab instead.
    //
    // The categories are the maintainer's: EMPIRE (what runs the empire), COLONIZATION,
    // CONSTRUCTION, TRADE, and NOTIFICATIONS (what stays quiet). Each wears its own one-tab
    // frame and they are ALL visible at once (maintainer: there is room, and a settings page
    // you have to leaf through hides what it is for).
    public sealed class AutomationScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu EmpireTabs;

        DropOptions<int> FreighterDropDown, ColonyShipDropDown, ScoutDropDown,
                         ConstructorDropDown, ResearchStationDropDown, MiningStationDropDown;
        bool ResearchStationsEnabled, MiningOpsEnabled;

        // fixed box geometry - the boxes own their sizes, the columns just stack them.
        // Heights: one-tab strip (~24) + 12 top pad + 26 per row (a checked-dropdown rides
        // its toggle's row now, so it costs the same 26 as a plain checkbox) + 12 bottom pad.
        // BoxW2: the dropdown boxes are WIDER instead of taller - label room + picker.
        const float BoxW = 320f, BoxW2 = 450f, BoxGap = 10f;
        const float EmpireBoxH = 160f, ColonizationBoxH = 130f, ConstructionBoxH = 165f,
                    TradeBoxH = 100f, NotificationsBoxH = 230f;

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
            // the frame hugs its content, anchored on the bar and the left margin
            // (maintainer, 3 Aug) - the first group screen that does not span the display.
            // Two columns: [Empire / Notifications] and [Colonization / Construction / Trade].
            float col1H = EmpireBoxH + BoxGap + NotificationsBoxH;
            float col2H = ColonizationBoxH + BoxGap + ConstructionBoxH + BoxGap + TradeBoxH;
            float contentW = 9 + 10 + BoxW + BoxGap + BoxW2 + 10 + 9;  // ClientArea insets + gutters
            float contentH = 60 + Math.Max(col1H, col2H) + 22;         // tab strip + cross clearance + pads - bench number
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.EmpireTabTitles, 5,
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
            // the Disables together, a breathing line, then the one Enable (maintainer, 3 Aug)
            notifications.AddCheckbox(() => Universe.UState.P.SuppressOnBuildNotifications, title: GameText.DisableBuildingAlerts, tooltip: GameText.NormallyWhenYouManuallyAdd);
            notifications.AddCheckbox(() => Universe.UState.P.DisableInhibitionWarning, title: GameText.DisableInhibitionAlerts, tooltip: GameText.InhibitionAlertsAreDisplayedWhen);
            notifications.AddCheckbox(() => Universe.UState.P.DisableVolcanoWarning, title: GameText.DisableVolcanoAlerts, tooltip: GameText.DisableVolcanoActivationOrDeactivation);
            notifications.AddCheckbox(() => Universe.UState.P.DisableCrashSiteWarning, title: GameText.DisableCrashSiteAlerts, tooltip: GameText.DisableCrashSiteAlertsTip);
            // Disable like its siblings (reviewer doctrine): one verb for the whole box,
            // checked by default - same [StarData] flag, read in the negative
            notifications.AddCheckbox(() => Universe.UState.P.DisableStarvationWarning, title: "Disable Starvation Warnings",
                                      tooltip: GameText.EnableStarvationWarningTip);
            notifications.AddCheckbox(() => player.data.SpyMute, title: "Disable Espionage Messages",
                                      tooltip: "Disable all Espionage notifications.");

            UIList empire = NewBox(new RectF(x0, top, BoxW, EmpireBoxH), "Empire");
            empire.AddCheckbox(() => player.AutoTaxes, title: GameText.AutoTaxes, tooltip: GameText.YourEmpireWillAutomaticallyManage3);
            empire.AddCheckbox(() => player.AutoResearch, title: GameText.AutoResearch, tooltip: GameText.YourEmpireWillAutomaticallySelect);
            empire.AddCheckbox(() => player.AutoBuildTerraformers, title: GameText.AutoBuildTerraformers, tooltip: GameText.AutoBuildTerraformersTip);
            empire.AddCheckbox(() => RushConstruction, title: GameText.RushAllConstruction, tooltip: GameText.RushAllConstructionTip);

            UIList trade = NewBox(new RectF(x1, top + ColonizationBoxH + BoxGap + ConstructionBoxH + BoxGap, BoxW2, TradeBoxH), "Trade");
            FreighterDropDown = trade.Add(new CheckedDropdown())
                .Create(() => player.AutoFreighters, title: GameText.AutomaticTrade, tooltip: GameText.YourEmpireWillAutomaticallyManage2,
                        autoPick: () => player.AutoPickBestFreighter);
            trade.AddCheckbox(() => Universe.UState.P.AllowPlayerInterTrade, title: GameText.AllowPlayerInterTradeTitle, tooltip: GameText.AllowPlayerInterTradeTip);

            UIList construction = NewBox(new RectF(x1, top + ColonizationBoxH + BoxGap, BoxW2, ConstructionBoxH), "Construction");
            ConstructorDropDown = construction.Add(new CheckedDropdown())
                .Create(() => player.AutoBuildSpaceRoads, Localizer.Token(GameText.Autobuild) + " Projectors", GameText.YourEmpireWillAutomaticallyCreate2,
                        autoPick: () => player.AutoPickConstructors);
            // right under Autobuild Projectors, whose behaviour it modifies (maintainer bench)
            construction.AddCheckbox(() => Universe.UState.P.PrioitizeProjectors, title: GameText.PrioritizeProjector, tooltip: GameText.PrioritizeProjectorTip);
            if (ResearchStationsEnabled)
                ResearchStationDropDown = construction.Add(new CheckedDropdown())
                    .Create(() => player.AutoBuildResearchStations, title: GameText.AutoBuildResearchStation, tooltip: GameText.AutoBuildResearchStationTip,
                            autoPick: () => player.AutoPickBestResearchStation);
            if (MiningOpsEnabled)
                MiningStationDropDown = construction.Add(new CheckedDropdown())
                    .Create(() => player.AutoBuildMiningStations, title: GameText.AutoBuildMiningStation, tooltip: GameText.AutoBuildMiningStationTip,
                            autoPick: () => player.AutoPickBestMiningStation);

            UIList colonization = NewBox(new RectF(x1, top, BoxW2, ColonizationBoxH), "Colonization");
            ScoutDropDown = colonization.Add(new CheckedDropdown())
                .Create(() => player.AutoExplore, title: GameText.Autoexplore, tooltip: GameText.YourEmpireWillAutomaticallyManage,
                        autoPick: () => player.AutoPickBestScout);
            ColonyShipDropDown = colonization.Add(new CheckedDropdown())
                .Create(() => player.AutoColonize, title: GameText.Autocolonize, tooltip: GameText.YourEmpireWillAutomaticallyCreate,
                        autoPick: () => player.AutoPickBestColonizer);
            // ⚠ "Auto Governor", no longer "Core": this flag now decides whether a new
            // colony gets an ASSESSED governor (the behaviour that used to hide inside
            // Autocolonize) - see Planet_Colonize.SetupColonyType (maintainer design).
            colonization.AddCheckbox(() => player.AutoCoreGovernor, title: "Auto Governor",
                                     tooltip: "New colonies are assigned a governor suited to the planet. Unchecked, they start unmanaged.");

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
            list.ReverseZOrder(); // dropdowns must draw over the rows below them
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
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
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
                ResearchStationDropDown.Visible = !player.AutoPickBestResearchStation && canBuild;
            }
            if (MiningStationDropDown != null)
            {
                bool canBuild = player.CanBuildMiningStations;
                if (canBuild && MiningStationDropDown.Count == 0)
                    UpdateDropDowns();
                MiningStationDropDown.Visible = !player.AutoPickBestMiningStation && canBuild;
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
