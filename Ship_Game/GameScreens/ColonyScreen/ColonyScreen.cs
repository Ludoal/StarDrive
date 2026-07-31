using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.GameScreens.ShipDesign;
using Ship_Game.Graphics;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI;
using System;
using SDUtils;
using Ship_Game.Universe.SolarBodies;

namespace Ship_Game
{
    public partial class ColonyScreen : PlanetScreen
    {
        readonly ToggleButton PlayerDesignsToggle;
        Submenu ColonyTabs;   // Ludoal fork: the frame's tab row - a group of one
        readonly Submenu PlanetInfo;
        readonly Submenu PStorage;
        readonly Submenu PFacilities;
        readonly UITextEntry PlanetName;
        readonly Rectangle PlanetIcon;
        public EmpireUIOverlay Eui;
        readonly ToggleButton LeftColony;
        readonly ToggleButton RightColony;
        readonly UITextEntry FilterBuildableItems;
        readonly Rectangle GridPos;
        readonly Submenu SubColonyGrid;
        readonly UILabel BlockadeLabel;
        readonly UILabel StarvationLabel;
        readonly Rectangle PlanetShieldIconRect;
        readonly ProgressBar PlanetShieldBar;
        readonly UILabel FilterBuildableItemsLabel;
        
        readonly SubmenuScrollList<BuildableListItem> BuildableTabs;
        readonly ScrollList<BuildableListItem> BuildableList;
        readonly ScrollList<ConstructionQueueScrollListItem> ConstructionQueue;
        readonly DropDownMenu FoodDropDown;
        readonly DropDownMenu ProdDropDown;
        readonly ProgressBar FoodStorage;
        readonly ProgressBar ProdStorage;
        readonly Rectangle FoodStorageIcon;
        readonly Rectangle ProfStorageIcon;

        AssignLaborComponent AssignLabor;
        readonly ShipInfoOverlayComponent ShipInfoOverlay;
        readonly GovernorDetailsComponent GovernorDetails;

        object DetailInfo;
        Building ToScrap;
        PlanetGridSquare BioToScrap;

        public bool ClickedTroop;

        Rectangle EditNameButton;
        readonly Font Font8  = Fonts.Arial8Bold;
        readonly Font Font12 = Fonts.Arial12Bold;
        readonly Font Font14 = Fonts.Arial14Bold;
        readonly Font Font20 = Fonts.Arial20Bold;
        readonly Font TextFont;

        UILabel TradeTitle;
        UILabel IncomingTradeTitle;
        UILabel OutgoingTradeTitle;
        UILabel ManualImportTitle;
        UILabel ManualExportTitle;
        UIPanel IncomingFoodPanel;
        UIPanel IncomingProdPanel;
        UIPanel IncomingColoPanel;
        UIPanel OutgoingFoodPanel;
        UIPanel OutgoingProdPanel;
        UIPanel OutgoingColoPanel;
        ProgressBar IncomingFoodBar;
        ProgressBar IncomingProdBar;
        ProgressBar IncomingColoBar;
        ProgressBar OutgoingFoodBar;
        ProgressBar OutgoingProdBar;
        ProgressBar OutgoingColoBar;
        UILabel IncomingFoodAmount;
        UILabel IncomingProdAmount;
        UILabel IncomingColoAmount;
        FloatSlider ImportFoodSlotSlider;
        FloatSlider ImportProdSlotSlider;
        FloatSlider ImportColoSlotSlider;
        FloatSlider ExportFoodSlotSlider;
        FloatSlider ExportProdSlotSlider;
        FloatSlider ExportColoSlotSlider;

        UILabel TerraformTitle;
        UILabel TerraformStatusTitle;
        UILabel TerraformStatus;
        UILabel TerraformersHereTitle;
        UILabel TerraformersHere;
        UILabel TerrainTerraformTitle;
        UILabel TileTerraformTitle;
        UILabel PlanetTerraformTitle;
        UILabel VolcanoTerraformDone;
        UILabel TileTerraformDone;
        UILabel PlanetTerraformDone;
        ProgressBar TerrainTerraformBar;
        ProgressBar TileTerraformBar;
        ProgressBar PlanetTerraformBar;

        UILabel TargetFertilityTitle;
        UILabel TargetFertility;
        UILabel EstimatedMaxPopTitle;
        UILabel EstimatedMaxPop;

        UILabel DysonSwarmTypeTitle;
        UILabel DysonSwarmStatus;
        UIButton DysonSwarmStartButton;
        UIButton DysonSwarmKillButton;
        UICheckBox DysonSwarmOverclock;
        UIPanel DysonSwarmControllerPanel;
        UIPanel DysonSwarmPanel;
        UIPanel DysonSwarmProdBoost;
        ProgressBar DysonSwarmControllerProgress;
        ProgressBar DysonSwarmProgress;
        ProgressBar DysonSwarmProductionBoost;

        public ColonyScreen(GameScreen parent, Planet p, EmpireUIOverlay empUI, 
            int governorTabSelected = 0, int facilitiesTabSelected = -1) // Ludoal fork: -1 = fresh open, defaults to Stats+
            : base(parent, p)
        {
            Eui = empUI;
            Player.UpdateShipsWeCanBuild();
            TextFont = LowRes ? Font8 : Font12;

            // Ludoal fork: the 80px title cartouche gives way to the group's tab row - a group
            // of ONE, since Colony opens from the map and has no siblings, but a full-frame
            // screen that looked different for that would read as an oversight. The two Menu1
            // panels went with it: every rect below is placed from the grid instead.
            ColonyTabs = GameScreens.ReworkScreens.AddGroupTabs(
                this, GameScreens.ReworkScreens.ColonyTabTitles(p.Name), 0, _ => { }, out Rectangle _);
            RectF client = ColonyTabs.ClientArea;

            // ── the screen's one grid ────────────────────────────────────────────────────────
            // Ludoal fork: every panel is placed from THESE, and nothing re-derives a margin of
            // its own. Pad is the gap to the frame AND between panels - one number, so a change
            // moves the whole layout together rather than half of it.
            const float Pad = 10;
            float gridLeft   = client.X + Pad;
            float gridRight  = client.Right - Pad;
            float gridTop    = client.Y + Pad;
            float gridBottom = client.Bottom - Pad;

            // Left column: wide enough that the Governor's third tab (BLUEPRINTS) fits on the
            // first row - measured in the font that draws the tabs rather than guessed.
            float govTabsW = Fonts.Arial12Bold.TextWidth("GOVERNOR") + Fonts.Arial12Bold.TextWidth("DEFENSE")
                           + Fonts.Arial12Bold.TextWidth("BUDGET") + Fonts.Arial12Bold.TextWidth("BLUEPRINTS")
                           + 4 * 30;   // Submenu pads each tab
            float colLeftW = Math.Max(govTabsW, (gridRight - gridLeft) * 0.26f);

            RectF planetInfoR = new(gridLeft, gridTop, colLeftW,
                                    (gridBottom - gridTop) * 0.26f);
            PlanetInfo = new(planetInfoR, GameText.PlanetInfo);

            // Ludoal fork: the colony arrows sit side by side on Planet Info's title line, flush
            // with the panel's RIGHT edge - beside the panel whose contents they step through.
            // Shortened from the style's 35: that height belongs to the selection box they were
            // drawn for, and it overruns a title row.
            const int arrowW = 14, arrowH = 20, arrowGap = 2;
            int arrowY = (int)PlanetInfo.Y + 4;
            int arrowRight = (int)PlanetInfo.Right - 8;

            LeftColony = Add(new ToggleButton(arrowRight - 2 * arrowW - arrowGap, arrowY,
                                              ToggleButtonStyle.ArrowLeft));
            LeftColony.SetAbsSize(arrowW, arrowH);
            LeftColony.Tooltip = GameText.ViewPreviousColony;
            LeftColony.OnClick = b => OnChangeColony(-1);

            RightColony = Add(new ToggleButton(arrowRight - arrowW, arrowY,
                                               ToggleButtonStyle.ArrowRight));
            RightColony.SetAbsSize(arrowW, arrowH);
            RightColony.Tooltip = GameText.ViewNextColony;
            RightColony.OnClick = b => OnChangeColony(+1);

            // The left column stacks four panels with ONE gap between each. The first three carry
            // fixed content and keep their height; STORAGE is the variable block - it takes what
            // is left down to the foot, so the column always closes on the grid rather than
            // wherever four quarters happen to land.
            float govH   = 0.30f * (gridBottom - gridTop);
            float laborH = 0.22f * (gridBottom - gridTop);

            Submenu pDescription = new(gridLeft, PlanetInfo.Bottom + Pad, colLeftW, govH);

            var labor = new RectF(gridLeft, pDescription.Bottom + Pad, colLeftW, laborH);
            AssignLabor = Add(new AssignLaborComponent(P, labor, useTitleFrame: true));

            RectF pStorageR = new(gridLeft, labor.Bottom + Pad, colLeftW,
                                  gridBottom - (labor.Bottom + Pad));
            PStorage = new(pStorageR, GameText.Storage);

            Vector2 blockadePos = new Vector2(PStorage.X + 20, PStorage.Y + 35);
            BlockadeLabel = Add(new UILabel(blockadePos, Localizer.Token(GameText.Blockade2), Fonts.Pirulen16, Color.Red));
            BlockadeLabel.Tooltip = GameText.IndicatesThatThisPlanetIs;
            
            Vector2 starvationPos = new Vector2(PStorage.X + 200, PStorage.Y + 35);
            StarvationLabel = Add(new UILabel(starvationPos, Localizer.Token(GameText.Starvation), Fonts.Pirulen16, Color.Red));
            FoodStorage = new ProgressBar(PStorage.X + 100, PStorage.Y + 25 + 0.33f*(PStorage.Height - 25), 0.4f*PStorage.Width, 18);
            FoodStorage.Max = p.Storage.Max;
            FoodStorage.Progress = p.FoodHere;
            FoodStorage.color = "green";
            FoodDropDown = new DropDownMenu(PStorage.X + 100 + 0.4f * PStorage.Width + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, 0.2f*PStorage.Width, 18);
            FoodDropDown.AddOption(Localizer.Token(GameText.Store));
            FoodDropDown.AddOption(Localizer.Token(GameText.Import));
            FoodDropDown.AddOption(Localizer.Token(GameText.Export));
            FoodDropDown.ActiveIndex = (int)p.FS;
            var iconStorageFood = ResourceManager.Texture("NewUI/icon_storage_food");
            FoodStorageIcon = new Rectangle((int)PStorage.X + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - iconStorageFood.Height / 2, iconStorageFood.Width, iconStorageFood.Height);
            ProdStorage = new ProgressBar(PStorage.X + 100, PStorage.Y + 25 + 0.66f*(PStorage.Height - 25), 0.4f*PStorage.Width, 18);
            ProdStorage.Max = p.Storage.Max;
            ProdStorage.Progress = p.ProdHere;
            var iconStorageProd = ResourceManager.Texture("NewUI/icon_storage_production");
            ProfStorageIcon = new Rectangle((int)PStorage.X + 20, ProdStorage.pBar.Y + ProdStorage.pBar.Height / 2 - iconStorageFood.Height / 2, iconStorageProd.Width, iconStorageFood.Height);
            ProdDropDown = new DropDownMenu(PStorage.X + 100 + 0.4f*PStorage.Width + 20, ProdStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, 0.2f*PStorage.Width, 18);
            ProdDropDown.AddOption(Localizer.Token(GameText.Store));
            ProdDropDown.AddOption(Localizer.Token(GameText.Import));
            ProdDropDown.AddOption(Localizer.Token(GameText.Export));
            ProdDropDown.ActiveIndex = (int)p.PS;

            // Centre column: the colony grid keeps its height, STATISTICS below takes the rest -
            // it is the variable block of this column, and it closes on the grid's foot.
            float colCentreX = gridLeft + colLeftW + Pad;
            float colRightW  = (gridRight - gridLeft) * 0.30f;   // right column, dynamic
            float colCentreW = gridRight - colRightW - Pad - colCentreX;

            RectF subColonyR = new(colCentreX, gridTop, colCentreW, (gridBottom - gridTop) * 0.5f);
            SubColonyGrid = new(subColonyR, GameText.Colony);

            RectF pFacilitiesR = new(colCentreX, SubColonyGrid.Bottom + Pad, colCentreW,
                                     gridBottom - (SubColonyGrid.Bottom + Pad));

            PFacilities = base.Add(new Submenu(pFacilitiesR));
            PopulatePfacilitieTabs();
            PFacilities.OnTabChange = OnPFacilitiesTabChange;
            // FB - sticky tab selection on colony change via arrows
            // Ludoal fork: on a fresh open (no sticky selection carried), default to Stats+
            if (facilitiesTabSelected < 0)
            {
                facilitiesTabSelected = 0;
                for (int i = 0; i < PFacilities.Tabs.Count; ++i)
                    if (PFacilities.Tabs[i].Title == StatsPlusTabTitle)
                    {
                        facilitiesTabSelected = i;
                        break;
                    }
            }
            if (facilitiesTabSelected < PFacilities.Tabs.Count)
                PFacilities.SelectedIndex = facilitiesTabSelected;

            // Right column. BUILDINGS starts on COLONY's top line; the filter row sits ABOVE it,
            // outside the frame, right-aligned on it - the clear button ends where the panel ends.
            float colRightX = gridRight - colRightW;
            float filterH = 20;
            float clearW = 17;
            float buildingsTop = gridTop + filterH + Pad;

            var filterBgRect = new RectF(colRightX + 60, gridTop,
                                         colRightW - 60 - clearW - 10, filterH);
            var filterRect = new RectF(filterBgRect.X + 5, filterBgRect.Y, filterBgRect.W, filterBgRect.H);
            FilterBuildableItems = Add(new UITextEntry(filterRect, Font12, ""));
            FilterBuildableItems.AutoCaptureOnHover = true;
            FilterBuildableItems.Background = new Submenu(filterBgRect);
            Vector2 filterLabelPos = new Vector2(colRightX, filterRect.Y + 2);
            FilterBuildableItemsLabel = Add(new UILabel(filterLabelPos, "Filter:", Font12, Color.Gray));
            
            var customStyle = new UIButton.StyleTextures("NewUI/icon_clear_filter", "NewUI/icon_clear_filter_hover2");
            Add(new UIButton(customStyle, new Vector2(17, 17), "")
            {
                Tooltip = GameText.ClearBuildableItemsFilter,
                OnClick = OnClearFilterClick,
                Pos     = new Vector2(filterRect.Right + 10, filterRect.Y + 3)
            });

            // BUILDINGS' top lines up with COLONY's; the queue below takes what is left, so the
            // column closes on the grid's foot exactly as the other two do.
            RectF buildableR = new(colRightX, buildingsTop, colRightW,
                                   (gridBottom - buildingsTop) * 0.5f);
            BuildableTabs = base.Add(new SubmenuScrollList<BuildableListItem>(buildableR, BuildingsTabText));
            BuildableTabs.OnTabChange = OnBuildableTabChanged;

            BuildableList = BuildableTabs.List;
            BuildableList.EnableItemHighlight = true;
            BuildableList.OnDoubleClick = OnBuildableItemDoubleClicked;
            BuildableList.OnHovered = OnBuildableHoverChange;

            if (p.OwnerIsPlayer || p.Universe.Debug)
                BuildableList.OnDragOut = OnBuildableListDrag;

            PlayerDesignsToggle = Add(new ToggleButton(new Vector2(BuildableTabs.Right - 270, BuildableTabs.Y+1),
                                                       ToggleButtonStyle.PlayerDesigns, "SelectionBox/icon_PlayerDesigns"));
            PlayerDesignsToggle.IsToggled = !Universe.P.ShowAllDesigns;
            PlayerDesignsToggle.Tooltip = GameText.ToggleToDisplayOnlyPlayerdesigned;
            PlayerDesignsToggle.OnClick = OnPlayerDesignsToggleClicked;
            ResetBuildableTabs();

            float queueTop = BuildableTabs.Bottom + Pad;
            RectF queueR = new(colRightX, queueTop, colRightW, gridBottom - queueTop);
            var queue = base.Add(new SubmenuScrollList<ConstructionQueueScrollListItem>(queueR, GameText.ConstructionQueue));

            ConstructionQueue = queue.List;
            ConstructionQueue.EnableItemHighlight = true;
            ConstructionQueue.OnHovered = OnConstructionItemHovered;
            if (p.OwnerIsPlayer || p.Universe.Debug)
                ConstructionQueue.OnDragReorder = OnConstructionItemReorder;

            int iconSize = LowRes ? 80 : 128;
            int iconOffsetX = LowRes ? 100 : 148;
            int iconOffsetY = LowRes ? 0 : 25;

            PlanetIcon = new Rectangle((int)PlanetInfo.Right - iconOffsetX, 
                (int)PlanetInfo.Y + ((int)PlanetInfo.Height - iconOffsetY) / 2 - iconSize/2 + (LowRes ? 0 : 25), iconSize, iconSize);

            Rectangle planetShieldBarRect = new Rectangle(PlanetIcon.X, PlanetInfo.Rect.Y + 4, PlanetIcon.Width, 20);
            PlanetShieldBar = new ProgressBar(planetShieldBarRect)
            {
                color = "blue"
            };

            PlanetShieldIconRect = new Rectangle(planetShieldBarRect.X - 30, planetShieldBarRect.Y-2, 20, 20);

            GridPos = new Rectangle(SubColonyGrid.Rect.X + 10, SubColonyGrid.Rect.Y + 30, SubColonyGrid.Rect.Width - 20, SubColonyGrid.Rect.Height - 35);
            int width = GridPos.Width / 7;
            int height = GridPos.Height / 5;
            foreach (PlanetGridSquare planetGridSquare in p.TilesList)
                planetGridSquare.ClickRect = new Rectangle(GridPos.X + planetGridSquare.X * width, GridPos.Y + planetGridSquare.Y * height, width, height);
            
            PlanetName = Add(new UITextEntry(p.Name));
            PlanetName.Color = Colors.Cream;
            PlanetName.MaxCharacters = 20;
            PlanetName.OnTextChanged = OnPlanetNameChanged;
            PlanetName.OnTextSubmit = OnPlanetNameSubmit;

            if (p.Owner != null)
            {
                GovernorDetails = Add(new GovernorDetailsComponent(this, (UniverseScreen)parent, p, pDescription.RectF, governorTabSelected));
            }
            else
            {
                p.Universe.Screen.LookingAtPlanet = false;
            }

            ShipInfoOverlay = Add(new ShipInfoOverlayComponent(this, Universe));
            P.RefreshBuildingsWeCanBuildHere();
            Vector2 detailsVector = new Vector2(PFacilities.Rect.X + 15, PFacilities.Rect.Y + 35);
            CreateTradeDetails(detailsVector);
            CreateTerraformingDetails(detailsVector);
            CreateDysonSwarmDetails(detailsVector);
        }

        void PopulatePfacilitieTabs()
        {
            PFacilities.ClearTabs();
            PFacilities.AddTab(GameText.Statistics2);
            PFacilities.AddTab(StatsPlusTabTitle); // Ludoal fork: Stats+ add-on tab, next to its witness
            PFacilities.AddTab(GameText.Description);
            PFacilities.AddTab(GameText.Trade2);

            if (Player.data.Traits.TerraformingLevel > 0 || P.Terraformable)
                PFacilities.AddTab(GameText.BB_Tech_Terraforming_Name);

            if (DysonSwarmTabAllowed)
            {
                PFacilities.AddTab(GameText.DysonSwarm);
                Vector2 detailsVector = new Vector2(PFacilities.Rect.X + 15, PFacilities.Rect.Y + 35);
                CreateDysonSwarmDetails(detailsVector);
            }
        }

        void AddLabel(ref UILabel uiLabel, Vector2 pos, LocalizedText text, Font font, Color color)
        {
            if (uiLabel == null)
                uiLabel = Add(new UILabel(pos, text, font, color));

            uiLabel.Visible = false;
        }

        void AddButton(ref UIButton button, Vector2 pos, LocalizedText text, ButtonStyle buttonStyle, LocalizedText tip) 
        {
            if (button == null)
                button = Add(new UIButton(buttonStyle, pos, text));

            button.Visible = false;
            button.Tooltip = tip;
        }

        void AddPanel(ref UIPanel panel, Vector2 pos, string texPath, int size, LocalizedText tip)
        {
            if (panel == null)
                panel = Add(new UIPanel(pos, ResourceManager.Texture(texPath)));

            panel.Size    = new Vector2(size, size);
            panel.Visible = false;
            panel.Tooltip = tip;
        }

        void AddProgressBar(ref ProgressBar bar, Rectangle rect, float max, string colorStr, bool percentage = false)
        {
            if (bar == null)
            {
                bar = new ProgressBar(rect)
                {
                    Max            = max,
                    color          = colorStr,
                    DrawPercentage = percentage
                };
            }
        }

        void AddUiSlider(ref FloatSlider slider, Rectangle rect, LocalizedText text, float min, float max, float value, LocalizedText tip)
        {
            if (slider == null)
            {
                slider            = Slider(rect, text, min, max, value);
                slider.Visible    = false;
                slider.ZeroString = Localizer.Token(GameText.Automatic);
                slider.Tip        = tip;
            }
        }

        void CreateDysonSwarmDetails(Vector2 pos)
        {
            DysonSwarmTabAllowed = P.Owner.CanBuildDysonSwarmIn(P.System);
            if (P.Owner == null || !DysonSwarmTabAllowed)
                return;

            Font font = LowRes ? Font8 : Font14;
            int spacing = font.LineSpacing + 10;
            int barWidth = (int)(PFacilities.Width * 0.5f);
            float indent = 30;

            AddLabel(ref DysonSwarmTypeTitle, pos, DysonSwarm.DysonSwarmTypeTitle(P.System.DysonSwarmType), font, Color.White);

            Vector2 buttonsPos = new Vector2(pos.X, pos.Y + spacing);
            AddButton(ref DysonSwarmStartButton, buttonsPos, GameText.BuildDysonSwarm, ButtonStyle.Default, GameText.BuildDysonSwarmTip);
            AddButton(ref DysonSwarmKillButton, new Vector2(buttonsPos.X + barWidth- spacing-110, buttonsPos.Y), GameText.KillDysonSwarm, ButtonStyle.Military, GameText.KillDysonSwarmTip);
            DysonSwarmOverclock = Add(new UICheckBox(buttonsPos.X, buttonsPos.Y + 130,
                () => DysonSwarmEnabled,
                (x) => { if (P.System.HasDysonSwarm)
                            P.System.DysonSwarm.SetOverclock(x);
                },
                font, GameText.DysonSwarmOverClockSwarm, GameText.DysonSwarmOverClockSwarmTip));
            DysonSwarmOverclock.CheckedTextColor = Color.Red;
            DysonSwarmOverclock.Visible = false;
            AddLabel(ref DysonSwarmStatus, new Vector2(buttonsPos.X+5, buttonsPos.Y+3), GameText.Completion, font, Color.Green);
            DysonSwarmStartButton.OnClick = (b) => OnStartDysonSwarmClick();
            DysonSwarmKillButton.OnClick = (b) => OnStartDysonSwarmKill();
            // Controller Progress
            Vector2 controllerProgressPos = new Vector2(pos.X, buttonsPos.Y + spacing + 3);
            AddPanel(ref DysonSwarmControllerPanel, controllerProgressPos, "Suns/red_giant_icon", font.LineSpacing, GameText.DysonSwarmControllerProgressTip);
            Rectangle dysonSwarmControllerProgressRect = new Rectangle((int)(controllerProgressPos.X + indent), 
                                                                       (int)controllerProgressPos.Y, 
                                                                       barWidth, 20);
            AddProgressBar(ref DysonSwarmControllerProgress, dysonSwarmControllerProgressRect, 100, "red", percentage: true);
            
            // Swarm Progress
            Vector2 swarmProgressPos = new Vector2(controllerProgressPos.X, controllerProgressPos.Y + spacing + 3);
            AddPanel(ref DysonSwarmPanel, swarmProgressPos, "NewUI/icon_projection", font.LineSpacing, GameText.DysonSwarmProgressTip);
            DysonSwarmPanel.Color = Color.Yellow;
            Rectangle dysonSwarmProgressRect = new Rectangle((int)(swarmProgressPos.X + indent),
                                                             (int)swarmProgressPos.Y,
                                                             barWidth, 20);
            AddProgressBar(ref DysonSwarmProgress, dysonSwarmProgressRect, DysonSwarm.GetRequiredSwarmSats(P.System.DysonSwarmType), "yellow");

            // Swarm production boost
            Vector2 swarmProdBoostPos = new Vector2(swarmProgressPos.X, swarmProgressPos.Y + spacing + 3);
            AddPanel(ref DysonSwarmProdBoost, swarmProdBoostPos, "NewUI/icon_production", font.LineSpacing, GameText.DysonSwarmProductionBoostTip);
            Rectangle dysonSwarmProdBoostRect = new Rectangle((int)(swarmProdBoostPos.X + indent),
                                                              (int)swarmProdBoostPos.Y,
                                                               barWidth, 20);
            AddProgressBar(ref DysonSwarmProductionBoost, dysonSwarmProdBoostRect,
                P.System.HasDysonSwarm ? P.System.DysonSwarm.MaxProductionBoost : DysonSwarm.BaseSwarmProductionBoost, "brown");
        }

        bool DysonSwarmEnabled => P.System.EmpireOwnsDysonSwarm(Player) ? P.System.DysonSwarm.OverclockEnabled : false;

        void OnStartDysonSwarmClick()
        {
            if (P.OwnerIsPlayer)
                P.System.ActivateDysonSwarm(P.Owner);
        }

        void OnStartDysonSwarmKill()
        {
            if (!P.OwnerIsPlayer)
                return;

            HideDysonSwarmUI();
            P.System.KillDysonSwarm();
        }

        void CreateTradeDetails(Vector2 pos)
        {
            Font font       = LowRes ? Font8 : Font14;
            int spacing     = font.LineSpacing + 10;
            int barWidth    = (int)(PFacilities.Width * 0.33f);
            int sliderWidth = (int)(PFacilities.Width * 0.33f);
            int sliderSize  = 30;
            float indent    = 30;
            float indentTradeAmount = indent + barWidth + 5;
            float indentSlider      = indentTradeAmount + 60;

            AddLabel(ref TradeTitle, pos, GameText.ColonyTrade, LowRes ? Font14 : Font20, Color.White);

            Vector2 incomingTitlePos = new Vector2(pos.X, pos.Y + spacing * (LowRes ? 1 : 1.5f));
            AddLabel(ref IncomingTradeTitle, incomingTitlePos, GameText.IncomingFreighters, font, Color.Gray);

            Vector2 manualImportTitlePos = new Vector2(pos.X + indentSlider - 10, incomingTitlePos.Y);
            AddLabel(ref ManualImportTitle, manualImportTitlePos, Localizer.Token(GameText.ManualImport), font, Color.Gray);

            // Incoming food
            Vector2 incomingFoodPos = new Vector2(pos.X, incomingTitlePos.Y + spacing + 3);
            AddPanel(ref IncomingFoodPanel, incomingFoodPos, "NewUI/icon_food", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle incomingFoodRect = new Rectangle((int)(incomingFoodPos.X + indent), (int)incomingFoodPos.Y, barWidth, 20);
            AddProgressBar(ref IncomingFoodBar, incomingFoodRect, P.FoodImportSlots, "green");
            Vector2 incomingFoodAmountPos = new Vector2(pos.X + indentTradeAmount, incomingFoodPos.Y + (LowRes ? 0 : 2));
            AddLabel(ref IncomingFoodAmount, incomingFoodAmountPos, "", Font8, Color.White);
            Rectangle importFoodSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(incomingFoodPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ImportFoodSlotSlider, importFoodSlotsRect, "", 0, 20, P.ManualFoodImportSlots, GameText.ManualTradeSlotTip);

            // Incoming Prod
            Vector2 incomingProdPos = new Vector2(pos.X, incomingFoodPos.Y + spacing);
            AddPanel(ref IncomingProdPanel, incomingProdPos, "NewUI/icon_production", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle incomingProdRect = new Rectangle((int)(incomingProdPos.X + indent), (int)incomingProdPos.Y, barWidth, 20);
            AddProgressBar(ref IncomingProdBar, incomingProdRect, P.ProdImportSlots, "brown");
            Vector2 incomingProdAmountPos = new Vector2(pos.X + indentTradeAmount, incomingProdPos.Y + (LowRes ? 0 : 2));
            AddLabel(ref IncomingProdAmount, incomingProdAmountPos, "", Font8, Color.White);
            Rectangle importProdSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(incomingProdPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ImportProdSlotSlider, importProdSlotsRect, "", 0, 20, P.ManualProdImportSlots, GameText.ManualTradeSlotTip);

            // Incoming Colonists
            Vector2 incomingColoPos = new Vector2(pos.X, incomingProdPos.Y + spacing);
            AddPanel(ref IncomingColoPanel, incomingColoPos, "UI/icon_pop", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle incomingColoRect = new Rectangle((int)(incomingColoPos.X + indent), (int)incomingColoPos.Y, barWidth, 20);
            AddProgressBar(ref IncomingColoBar, incomingColoRect, P.ColonistsImportSlots, "blue");
            Vector2 incomingColoAmountPos = new Vector2(pos.X + indentTradeAmount, incomingColoPos.Y + (LowRes ? 0 : 2));
            AddLabel(ref IncomingColoAmount, incomingColoAmountPos, "", Font8, Color.White);
            Rectangle importColoSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(incomingColoPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ImportColoSlotSlider, importColoSlotsRect, "", 0, 20, P.ManualColoImportSlots, GameText.ManualTradeSlotTip);

            Vector2 outgoingTitlePos = new Vector2(pos.X, incomingColoAmountPos.Y + spacing * (LowRes ? 1 : 1.5f));
            AddLabel(ref OutgoingTradeTitle, outgoingTitlePos, GameText.OutgoingFreighters, font, Color.Gray);

            Vector2 manualExportTitlePos = new Vector2(pos.X + indentSlider - 10, outgoingTitlePos.Y);
            AddLabel(ref ManualExportTitle, manualExportTitlePos, GameText.ManualExport, font, Color.Gray);

            // Outgoing food
            Vector2 outgoingFoodPos = new Vector2(pos.X, outgoingTitlePos.Y + spacing + 3);
            AddPanel(ref OutgoingFoodPanel, outgoingFoodPos, "NewUI/icon_food", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle outgoingFoodRect = new Rectangle((int)(outgoingFoodPos.X + indent), (int)outgoingFoodPos.Y, barWidth, 20);
            AddProgressBar(ref OutgoingFoodBar, outgoingFoodRect, P.FoodExportSlots, "green");
            Rectangle exportFoodSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(outgoingFoodPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ExportFoodSlotSlider, exportFoodSlotsRect, "", 0, 25, P.ManualFoodExportSlots, GameText.ManualTradeSlotTip);

            // Outgoing Prod
            Vector2 outgoingProdPos = new Vector2(pos.X, outgoingFoodPos.Y + spacing);
            AddPanel(ref OutgoingProdPanel, outgoingProdPos, "NewUI/icon_production", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle outgoingProdRect = new Rectangle((int)(outgoingProdPos.X + indent), (int)outgoingProdPos.Y, barWidth, 20);
            AddProgressBar(ref OutgoingProdBar, outgoingProdRect, P.ProdExportSlots, "brown");
            Rectangle exportProdSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(outgoingProdPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ExportProdSlotSlider, exportProdSlotsRect, "", 0, 25, P.ManualProdExportSlots, GameText.ManualTradeSlotTip);

            // Outgoing Colonists
            Vector2 outgoingColoPos = new Vector2(pos.X, outgoingProdPos.Y + spacing);
            AddPanel(ref OutgoingColoPanel, outgoingColoPos, "UI/icon_pop", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle outgoingColoRect = new Rectangle((int)(outgoingColoPos.X + indent), (int)outgoingColoPos.Y, barWidth, 20);
            AddProgressBar(ref OutgoingColoBar, outgoingColoRect, P.ColonistsExportSlots, "blue");
            Rectangle exportColoSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(outgoingColoPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ExportColoSlotSlider, exportColoSlotsRect, "", 0, 25, P.ManualColoExportSlots, GameText.ManualTradeSlotTip);
        }

        void CreateTerraformingDetails(Vector2 pos)
        {
            Font font    = LowRes ? Font8 : Font14;
            int spacing  = font.LineSpacing + 2;
            int barWidth = (int)(PFacilities.Width * 0.33f);

            AddLabel(ref TerraformTitle, pos, "", LowRes ? Font14 : Font20, Color.White);

            Vector2 statusTitlePos = new Vector2(pos.X, pos.Y + spacing*2);
            AddLabel(ref TerraformStatusTitle, statusTitlePos, GameText.TerraformingStatus, font, Color.White);

            float indent = font.MeasureString(TerraformStatusTitle.Text).X + 125;

            Vector2 statusPos = new Vector2(pos.X + indent, pos.Y + spacing*2);
            AddLabel(ref TerraformStatus, statusPos, " ", font, Color.Gray);

            Vector2 numTerraformersTitlePos = new Vector2(pos.X, TerraformStatusTitle.Y + spacing);
            AddLabel(ref TerraformersHereTitle, numTerraformersTitlePos, GameText.TerraformersHere, font, Color.Gray);

            Vector2 numTerraformersPos = new Vector2(pos.X + indent, numTerraformersTitlePos.Y);
            AddLabel(ref TerraformersHere, numTerraformersPos, " ", font, Color.White);

            Vector2 terraVolcanoTitlePos = new Vector2(pos.X, numTerraformersTitlePos.Y + spacing*2);
            AddLabel(ref TerrainTerraformTitle, terraVolcanoTitlePos, " ", font, Color.Gray);

            Vector2 terraVolcanoPos = new Vector2(pos.X + indent, terraVolcanoTitlePos.Y);
            AddLabel(ref VolcanoTerraformDone, terraVolcanoPos, GameText.TerraformersDone, font, Color.Green);

            Rectangle terraVolcanoRect = new Rectangle((int)terraVolcanoPos.X, (int)terraVolcanoPos.Y, barWidth, 20);
            AddProgressBar(ref TerrainTerraformBar, terraVolcanoRect, 100, "brown", percentage: true);

            Vector2 terraTileTitlePos = new Vector2(pos.X, terraVolcanoTitlePos.Y + spacing);
            AddLabel(ref TileTerraformTitle, terraTileTitlePos, " ", font, Color.Gray);

            Vector2 terraTilePos = new Vector2(pos.X + indent, terraTileTitlePos.Y);
            AddLabel(ref TileTerraformDone, terraTilePos, GameText.TerraformersDone, font, Color.Green);

            Rectangle terraTileRect = new Rectangle((int)terraTilePos.X, (int)terraTilePos.Y, barWidth, 20);
            AddProgressBar(ref TileTerraformBar, terraTileRect, 100, "green", percentage: true);

            Vector2 terraPlanetTitlePos = new Vector2(pos.X, terraTileTitlePos.Y + spacing);
            AddLabel(ref PlanetTerraformTitle, terraPlanetTitlePos, GameText.TerraformPlanet, font, Color.Gray);

            Vector2 terraPlanetPos = new Vector2(pos.X + indent, terraPlanetTitlePos.Y);
            AddLabel(ref PlanetTerraformDone, terraPlanetPos, GameText.TerraformersDone, font, Color.Green);

            Rectangle terraPlanetRect = new Rectangle((int)terraPlanetPos.X, (int)terraPlanetPos.Y, barWidth, 20);
            AddProgressBar(ref PlanetTerraformBar, terraPlanetRect, 100, "blue", percentage: true);

            Vector2 targetFertilityTitlePos = new Vector2(pos.X, terraPlanetTitlePos.Y + spacing * 2);
            AddLabel(ref TargetFertilityTitle, targetFertilityTitlePos, GameText.TerraformTargetFert, font, Color.Gray);

            Vector2 targetFertilityPos = new Vector2(pos.X + indent, targetFertilityTitlePos.Y);
            AddLabel(ref TargetFertility, targetFertilityPos, "", font, Color.Green);

            Vector2 estimatedMaxPopTitlePos = new Vector2(pos.X, targetFertilityTitlePos.Y + spacing);
            AddLabel(ref EstimatedMaxPopTitle, estimatedMaxPopTitlePos, GameText.TerraformEsPop, font, Color.Gray);

            Vector2 estimatedMaxPopPos = new Vector2(pos.X + indent, estimatedMaxPopTitlePos.Y);
            AddLabel(ref EstimatedMaxPop, estimatedMaxPopPos, "", font, Color.Green);
        }

        void OnPlanetNameSubmit(string name)
        {
            P.Name = name;
            if (string.IsNullOrWhiteSpace(P.Name))
            {
                P.Name = P.GetDefaultPlanetName();
                PlanetName.Reset(P.Name);
            }
        }

        void OnPlanetNameChanged(string name)
        {
            P.Name = name;
        }

        public float TerraformTargetFertility()
        {
            float fertilityOnBuild = P.SumBuildings(b => b.MaxFertilityOnBuild);
            return (1 + fertilityOnBuild*Player.PlayerPreferredEnvModifier).LowerBound(0);
        }

        void ScrapAccepted()
        {
            if (ToScrap != null)
            {
                P.ScrapBuilding(ToScrap);
                P.RefreshBuildingsWeCanBuildHere();
                ToScrap = null;
            }
        }

        void ScrapBioAccepted()
        {
            if (BioToScrap != null)
            {
                P.DestroyBioSpheres(BioToScrap, !BioToScrap.Building?.CanBuildAnywhere == true);
                P.RefreshBuildingsWeCanBuildHere();
                BioToScrap = null;
            }
        }
    }
}
