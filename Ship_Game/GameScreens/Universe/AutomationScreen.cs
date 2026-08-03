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
    // CONSTRUCTION, TRADE, and NOTIFICATIONS (what stays quiet).
    public sealed class AutomationScreen : GameScreen
    {
        readonly UniverseScreen Universe;
        Submenu EmpireTabs;
        Submenu CatTabs;
        UIList Controls;

        // the design pickers of the ACTIVE category - null when their category is not showing
        DropOptions<int> FreighterDropDown, ColonyShipDropDown, ScoutDropDown,
                         ConstructorDropDown, ResearchStationDropDown, MiningStationDropDown;
        bool ResearchStationsEnabled, MiningOpsEnabled;

        static readonly LocalizedText[] Categories =
        {
            "Empire", "Colonization", "Construction", "Trade", "Notifications"
        };
        int Category;

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
            EmpireTabs = ReworkScreens.AddGroupTabs(this, ReworkScreens.EmpireTabTitles, 5,
                                                    OnEmpireTabChanged, out Rectangle _);
            ResearchStationsEnabled = !Universe.Player.Universe.P.DisableResearchStations;
            MiningOpsEnabled       = !Universe.Player.Universe.P.DisableMiningOps;

            // the category tabs frame the settings block; the controls list lives inside it
            RectF client = EmpireTabs.ClientArea;
            float top = ReworkScreens.GroupContentTop(client);
            CatTabs = Add(new Submenu(new RectF(client.X + 10, top, 520,
                                                client.Bottom - top - 10), Categories));
            CatTabs.OnTabChange = OnCategoryChanged;
            CatTabs.PerformLayout();
            BuildCategory();
            base.LoadContent();
        }

        void OnCategoryChanged(int index)
        {
            if (index == Category)
                return;
            GameAudio.AcceptClick();
            Category = index;
            BuildCategory();
        }

        void OnEmpireTabChanged(int index)
            => ReworkScreens.SwitchEmpireTab(index, self: 5, Universe, this);

        // rebuilds the controls list for the active category. The pickers of the other
        // categories go null so HandleInput's save-back only touches what is on screen.
        void BuildCategory()
        {
            Controls?.RemoveFromParent();
            FreighterDropDown = ColonyShipDropDown = ScoutDropDown = null;
            ConstructorDropDown = ResearchStationDropDown = MiningStationDropDown = null;

            Empire player = Universe.Player;
            RectF area = CatTabs.ClientArea;
            Controls = AddList(new Vector2(area.X + 12, area.Y + 12));
            Controls.Padding = new Vector2(2f, 10f);

            switch (Category)
            {
                case 0: // EMPIRE - what runs the empire itself
                    Controls.AddCheckbox(() => player.AutoTaxes, title: GameText.AutoTaxes, tooltip: GameText.YourEmpireWillAutomaticallyManage3);
                    Controls.AddCheckbox(() => player.AutoResearch, title: GameText.AutoResearch, tooltip: GameText.YourEmpireWillAutomaticallySelect);
                    Controls.AddCheckbox(() => player.AutoBuildTerraformers, title: GameText.AutoBuildTerraformers, tooltip: GameText.AutoBuildTerraformersTip);
                    Controls.AddCheckbox(() => RushConstruction, title: GameText.RushAllConstruction, tooltip: GameText.RushAllConstructionTip);
                    break;

                case 1: // COLONIZATION
                    ScoutDropDown = Controls.Add(new CheckedDropdown())
                        .Create(() => player.AutoExplore, title: GameText.Autoexplore, tooltip: GameText.YourEmpireWillAutomaticallyManage,
                                autoPick: () => player.AutoPickBestScout);
                    ColonyShipDropDown = Controls.Add(new CheckedDropdown())
                        .Create(() => player.AutoColonize, title: GameText.Autocolonize, tooltip: GameText.YourEmpireWillAutomaticallyCreate,
                                autoPick: () => player.AutoPickBestColonizer);
                    // ⚠ "Auto Governor", no longer "Core": this flag now decides whether a new
                    // colony gets an ASSESSED governor (the behaviour that used to hide inside
                    // Autocolonize) - see Planet_Colonize.SetupColonyType (maintainer design).
                    Controls.AddCheckbox(() => player.AutoCoreGovernor, title: "Auto Governor",
                                         tooltip: "New colonies are assigned a governor suited to the planet. Unchecked, they start unmanaged.");
                    break;

                case 2: // CONSTRUCTION
                    ConstructorDropDown = Controls.Add(new CheckedDropdown())
                        .Create(() => player.AutoBuildSpaceRoads, Localizer.Token(GameText.Autobuild) + " Projectors", GameText.YourEmpireWillAutomaticallyCreate2,
                                autoPick: () => player.AutoPickConstructors);
                    if (ResearchStationsEnabled)
                        ResearchStationDropDown = Controls.Add(new CheckedDropdown())
                            .Create(() => player.AutoBuildResearchStations, title: GameText.AutoBuildResearchStation, tooltip: GameText.AutoBuildResearchStationTip,
                                    autoPick: () => player.AutoPickBestResearchStation);
                    if (MiningOpsEnabled)
                        MiningStationDropDown = Controls.Add(new CheckedDropdown())
                            .Create(() => player.AutoBuildMiningStations, title: GameText.AutoBuildMiningStation, tooltip: GameText.AutoBuildMiningStationTip,
                                    autoPick: () => player.AutoPickBestMiningStation);
                    Controls.AddCheckbox(() => Universe.UState.P.PrioitizeProjectors, title: GameText.PrioritizeProjector, tooltip: GameText.PrioritizeProjectorTip);
                    break;

                case 3: // TRADE
                    FreighterDropDown = Controls.Add(new CheckedDropdown())
                        .Create(() => player.AutoFreighters, title: GameText.AutomaticTrade, tooltip: GameText.YourEmpireWillAutomaticallyManage2,
                                autoPick: () => player.AutoPickBestFreighter);
                    Controls.AddCheckbox(() => Universe.UState.P.AllowPlayerInterTrade, title: GameText.AllowPlayerInterTradeTitle, tooltip: GameText.AllowPlayerInterTradeTip);
                    break;

                default: // NOTIFICATIONS - what stays quiet
                    Controls.AddCheckbox(() => Universe.UState.P.SuppressOnBuildNotifications, title: GameText.DisableBuildingAlerts, tooltip: GameText.NormallyWhenYouManuallyAdd);
                    Controls.AddCheckbox(() => Universe.UState.P.DisableInhibitionWarning, title: GameText.DisableInhibitionAlerts, tooltip: GameText.InhibitionAlertsAreDisplayedWhen);
                    Controls.AddCheckbox(() => Universe.UState.P.DisableVolcanoWarning, title: GameText.DisableVolcanoAlerts, tooltip: GameText.DisableVolcanoActivationOrDeactivation);
                    Controls.AddCheckbox(() => Universe.UState.P.DisableCrashSiteWarning, title: GameText.DisableCrashSiteAlerts, tooltip: GameText.DisableCrashSiteAlertsTip);
                    Controls.AddCheckbox(() => Universe.UState.P.EnableStarvationWarning, title: GameText.EnableStarvationWarning, tooltip: GameText.EnableStarvationWarningTip);
                    // moved here from the Espionage screen's settings band (maintainer)
                    Controls.AddCheckbox(() => player.data.SpyMute, title: "Disable Espionage Messages",
                                         tooltip: "Disable all Espionage notifications.");
                    break;
            }

            Controls.ReverseZOrder(); // dropdowns must draw over the rows below them
            UpdateDropDowns();
        }

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

            batch.FillRectangle(ReworkScreens.GroupFrameFillRect(EmpireTabs), ReworkScreens.GroupFrameFill);

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
            ReworkScreens.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
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
