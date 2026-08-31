using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.GameScreens.ShipDesign;
using Ship_Game.Graphics;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI;
using Ship_Game.Audio;
using System;
using SDUtils;
using Ship_Game.Universe.SolarBodies;

namespace Ship_Game
{
    public partial class ColonyScreen : PlanetScreen
    {
        readonly ToggleButton PlayerDesignsToggle;
        // Ludoal fork: the screen's frame. The title bar the planet name sits in is the one every
        // window uses, declared with the colours it goes with.
        CloseButton CloseBtn; // served explicitly on read-only (infiltrated) colonies
        Submenu GroupRow;     // the hosting group's live tab row, when opened on a hosted seat
        // Ludoal fork: this page's real frame is its tab row's rect - the band excludes
        // exactly what the page occupies, dynamic size included.
        public override Rectangle PageFrame => GroupRow?.Rect ?? base.PageFrame;

        // Ludoal fork: the Colony panel runs live by default - it opts out of
        // auto-pause unless the player ticks "Auto-pause Colony panel" in Options.
        protected override bool PageOptsOutOfAutoPause => !GlobalStats.AutoPauseColonyPanel;

        void OnGroupRowTabChanged(int index)
        {
            if (GroupRow == null || index == GroupRow.NumTabs - 1)
                return; // the colony's own tab - already here
            GameAudio.AcceptClick();
            // a stacked page swaps like every tab; the seat stays armed - the hosted tab
            // survives visits to its neighbors (spec), it only dies on Esc or with the group
            UniverseScreen u = P.Universe.Screen;
            ExitScreen();
            ScreenManager.AddScreen(GameScreens.ScreenGroups.TabOf(u.HostedTabGroup, index, u));
        }

        // Ludoal fork: Esc and right-click close the colony's TAB - seat cleared, back to the
        // origin panel, or nothing more when it came from the map. Handled BEFORE the base
        // popup dismiss, which exits without the routing.
        void CloseColonyPage()
        {
            UniverseScreen u = P.Universe.Screen;
            var group = u.HostedTabGroup;
            int origin = u.HostedTabOrigin;
            u.ClearHostedTab();
            ExitScreen();
            u.SetSelectedPlanet(P); // land on the planet, selected - as closing always did
            // Ludoal fork (maintainer, bench 548): closing a colony returns to the table it was
            // opened from, in EVERY group. The Empire group used to be excepted - its colony tab
            // is permanent, so closing the colony closed the whole group instead. The exception
            // cost more than it bought: one gesture behaved two ways depending on where you
            // stood. The permanence is untouched by this - the Empire row keeps showing the last
            // colony viewed, which is where that tab comes from, not from this routing.
            if (origin >= 0)
                ScreenManager.AddScreen(GameScreens.ScreenGroups.TabOf(group, origin, u));
        }
        readonly Submenu PlanetInfo;
        readonly Submenu PStorage;
        readonly Submenu PFacilities;
        RectF LaborRect; // the Assign Labor block - the terraform details anchor on it now
        // Sticky across colonies: a session inspecting terraform keeps the tab up from one
        // colony screen to the next.
        static int LaborTabSticky;
        readonly UITextEntry PlanetName;
        readonly Rectangle PlanetIcon;
        public EmpireUIOverlay Eui;
        readonly UIButton LeftColony;
        readonly UIButton RightColony;
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
        readonly ScrollList<BuiltBuildingListItem> BuiltList; // COLONY tab, LIST view: built instances
        // Policies phase 0 (maintainer): REAL dropdowns instead of the click-rotation
        // relics - open a list, hover an entry, pick. One grammar for every list.
        readonly DropOptions<Planet.GoodState> FoodDropDown;
        readonly DropOptions<Planet.GoodState> ProdDropDown;
        readonly DropOptions<Planet.GoodState> ColonistsDropDown; // Ludoal fork (wishlist): migration control
        Rectangle ColonistsIcon;
        readonly ProgressBar FoodStorage;
        readonly ProgressBar ProdStorage;
        readonly ProgressBar PopStorage; // bench 426: the population bar joins the Supply panel

        // Ludoal fork (maintainer feedback): the colony's trade zones, a fourth row of the Supply
        // panel. Membership is an EMPIRE fact read from the colony's end, so the line is rebuilt
        // every update rather than cached - the Trade page can change it while this screen is up.
        UILabel TradeZonesLabel;
        float TradeZonesWidth; // what the VALUE may occupy between the caption and the pencil

        // Ludoal fork (maintainer, bench 524): population wears the same blue as research, and
        // the two bars sat close enough to be read as one another. A GREY rather than a paler
        // blue, and a dark one: the value is printed in white over the fill, so a light tint
        // would cost the reading it was meant to help. Kept here rather than at each site so
        // both screens desaturate by exactly the same amount.
        public static readonly Color PopBarTint = new(110, 115, 122);
        readonly Rectangle FoodStorageIcon;
        readonly Rectangle ProfStorageIcon;

        AssignLaborComponent AssignLabor;
        readonly ShipInfoOverlayComponent ShipInfoOverlay;
        readonly GovernorDetailsComponent GovernorDetails;
        UIButton SnapshotBlueprints; // wishlist: the LIST frame's own gesture, see PerformLayout

        object DetailInfo;
        object LastBuiltHover; // LIST view: the live hovered row's tile (cleared on leave)
        object LastDetailDrawn; // the elevator resets when the panel's content changes
        PlanetGridSquare PinnedBuilt; // LIST view: click-pinned building (bench 426, Lek's design)
        public float DescriptionScroll; // wheel offset while a pinned lore scrolls
        float MaxDescriptionScroll; // measured each frame from the drawn content (bench 429)
        // the description pane is up when none of the bar/stat tabs owns the panel -
        // only then may the wheel offset apply (bars must never ride the elevator)
        bool DescriptionPaneUp => !IsDysonSwarmTabSelected && !IsStatTabSelected
                                  && !IsStatsPlusTabSelected && !IsTradeTabSelected;
        Building ToScrap;
        PlanetGridSquare BioToScrap;

        public bool ClickedTroop;

        Rectangle EditNameButton;
        readonly Font Font8  = Fonts.Arial8Bold;
        readonly Font Font12 = Fonts.Arial12Bold;
        readonly Font Font14 = Fonts.Arial14Bold;
        readonly Font Font20 = Fonts.Arial20Bold;
        readonly Font TextFont;

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
        UICheckBox RushToggle; // wishlist 20 Aug: per-planet Continuous Rush, global-mastered
        UIPanel DysonSwarmControllerPanel;
        UIPanel DysonSwarmPanel;
        UIPanel DysonSwarmProdBoost;
        ProgressBar DysonSwarmControllerProgress;
        ProgressBar DysonSwarmProgress;
        ProgressBar DysonSwarmProductionBoost;

        public ColonyScreen(GameScreen parent, Planet p, EmpireUIOverlay empUI,
            int governorTabSelected = 0, int facilitiesTabSelected = -1, // Ludoal fork: -1 = fresh open, defaults to Stats+
            int colonyViewTabSelected = 0) // Ludoal fork: COLONY sub-tab (0=Map, 1=List), carried across the colony walk
            : base(parent, p, p.Universe.Screen) // auto-pause gated by its own opt-in sub-option
        {
            Eui = empUI;
            IsPopup = true; // the full live universe map (and its cartouches) shows behind Colony
            Player.UpdateShipsWeCanBuild();
            TextFont = Font12;

            // Ludoal fork: the colony lives INSIDE the tab frame. The group row's Submenu IS
            // the frame - its ClientArea comes from the owner formula, so the content rect is
            // pixel-identical to a real tab's, by construction. The planet's name rides the
            // tab. An unhosted colony (the infiltration mole) wears a one-tab row of its own
            // name - same furniture, no neighbors.
            UniverseScreen u = p.Universe.Screen;
            var titles = u.HostedTabTitle != null
                ? GameScreens.ScreenGroups.LiveTitles(u.HostedTabGroup, u)
                : new[] { new LocalizedText(p.Name, LocalizationMethod.RawText) };
            GroupRow = GameScreens.ScreenGroups.AddGroupTabs(this, titles, titles.Length - 1,
                                                             OnGroupRowTabChanged, out _, withClose: false);
            // The close cross at the group's standard seat; ref kept - the read-only
            // early-out must still serve it.
            Vector2 closePos = GameScreens.ScreenGroups.GroupClosePos(GroupRow.ClientArea);
            CloseBtn = Add(new CloseButton(closePos.X, closePos.Y));

            RectF client = GroupRow.ClientArea;
            Rectangle inner = new((int)client.X, (int)client.Y, (int)client.W, (int)client.H);
            // ⚠ At 900 high the LEFT COLUMN does not fit and that is not the frame's doing: its
            // three fixed panels are 250 + 300 + 220 = 770, plus gaps, against 749px of usable
            // height. It overflows before STORAGE gets a single pixel. The column needs real
            // rework at that height - shrinking the frame would only hide it.

            // ── the screen's one grid ────────────────────────────────────────────────────────
            // Ludoal fork: every panel is placed from THESE, and nothing re-derives a margin of
            // its own. Pad is the gap to the frame AND between panels - one number, so a change
            // moves the whole layout together rather than half of it.
            const float Pad = 10;
            float gridLeft   = inner.X + Pad;
            float gridRight  = inner.Right - Pad;
            float gridTop    = inner.Y + Pad;
            float gridBottom = inner.Bottom - Pad; // the Submenu client already stops above the chrome

            // ── what is FIXED and what STRETCHES (Ludoal fork) ────────────────────
            // Left column: FIXED width. Planet Info, Governor and Assign Labor keep fixed
            // heights; STORAGE is the one that stretches, taking what is left to the foot.
            // The left column's width comes from the Governor tab row. Submenu's REAL per-tab
            // arithmetic, read in UpdateTabRect: TextWidth + 2 + the header_right texture
            // (33px), +8 wrap slack. Three tabs since BP folded into GOVERNOR - the sum sits
            // under the 380 floor either way, so the fold does not narrow the column.
            float govTabsW = Fonts.Arial12Bold.TextWidth("GOVERNOR") + Fonts.Arial12Bold.TextWidth("DEFENSE")
                           + Fonts.Arial12Bold.TextWidth("BUDGET")
                           + 3 * (2 + 33) + 8;
            float colLeftW = Math.Max(govTabsW, 380) + 40;

            // ── the three fixed heights, each derived from what it HOLDS ─────────────────────
            // Each height is the content's own size: PLANET INFO is the portrait plus its
            // lines; GOVERNOR is measured on DEFENSE, the tallest of its four tabs, with its
            // buttons riding under the slider; ASSIGN LABOR is three sliders and nothing more.
            // ⚠ the PORTRAIT sets this height, not the other way round. It is 128 square - the
            // same number the icon itself uses below, so the two cannot drift.
            // Title bar + the portrait + a margin under it.
            float portraitH   = 128;
            // ⚠ the panel holds TWO things side by side: the portrait on the right, and the name
            // plus four lines on the left. Sizing on the portrait alone left 3px of slack, which
            // one font change would eat - so it takes the TALLER of the two, measured in the
            // fonts that draw them.
            // ⚠ FIVE lines, not four: Incoming/Outgoing Pop is conditional but the room is the
            // OBJECT's, not the moment's - reserved on everything the panel can declare
            float infoLinesH  = 45 + Fonts.Arial20Bold.LineSpacing * 2
                              + 5 * (TextFont.LineSpacing + 2);
            float planetInfoH = Math.Max(26 + portraitH + 14, infoLinesH + 10);
            // 208: the same height the Colonies band gives the very same component, so the
            // governor block reads identically on both screens (maintainer feedback).
            const float governorH   = 208;
            const float laborH      = 150;   // three sliders, their locks and the title bar

            RectF planetInfoR = new(gridLeft, gridTop, colLeftW, planetInfoH);
            PlanetInfo = new(planetInfoR, GameText.PlanetInfo);

            // The left column stacks four panels with ONE gap between each. The first three carry
            // fixed content and keep their height; STORAGE is the variable block - it takes what
            // is left down to the foot, so the column always closes on the grid rather than
            // wherever four quarters happen to land.
            Submenu pDescription = new(gridLeft, PlanetInfo.Bottom + Pad, colLeftW, governorH);

            var labor = new RectF(gridLeft, pDescription.Bottom + Pad, colLeftW, laborH);
            LaborRect = labor;
            // Terraforming rides as a second tab of this block: the terraform panel is
            // light enough to live here.
            bool terraTab = Player.data.Traits.TerraformingLevel > 0 || P.Terraformable;
            AssignLabor = Add(new AssignLaborComponent(P, labor, useTitleFrame: true,
                terraTab ? new LocalizedText[] { GameText.AssignLabor, GameText.BB_Tech_Terraforming_Name } : null,
                showMaxValue: true));
            if (terraTab)
            {
                AssignLabor.TitleMenu.SelectedIndex = LaborTabSticky.Clamped(0, 1);
                AssignLabor.TitleMenu.OnTabChange = i => LaborTabSticky = i;
            }

            RectF pStorageR = new(gridLeft, labor.Bottom + Pad, colLeftW,
                                  gridBottom - (labor.Bottom + Pad));
            PStorage = new(pStorageR, GameText.Supply); // bench 426: the panel carries stock AND flow - Supply says both

            // Ludoal fork: STARVATION! rides the title bar's empty right end instead of
            // overlapping the food bar and Import button below it.
            string starvTxt = Localizer.Token(GameText.Starvation);
            Vector2 starvationPos = new Vector2(PStorage.Right - Fonts.Pirulen16.TextWidth(starvTxt) - 15, PStorage.Y + 4);
            StarvationLabel = Add(new UILabel(starvationPos, starvTxt, Fonts.Pirulen16, Color.Red));

            // bench 432: BLOCKADE! joins it on the title bar, seated to its LEFT - its
            // historical spot overlapped the food row and the Store button
            string blockTxt = Localizer.Token(GameText.Blockade2);
            Vector2 blockadePos = new Vector2(starvationPos.X - Fonts.Pirulen16.TextWidth(blockTxt) - 12, PStorage.Y + 4);
            BlockadeLabel = Add(new UILabel(blockadePos, blockTxt, Fonts.Pirulen16, Color.Red));
            BlockadeLabel.Tooltip = GameText.IndicatesThatThisPlanetIs;
            // ⚠ the two bars sit a FIXED distance below the title bar - content aligned to the
            // TOP, not centred, so they stay put as STORAGE (the column's variable block) grows.
            // three rows: the elders keep their historical spacing (bench 427 - the
            // squeeze belonged to the Colonies table, not here), population seats third
            const float storeRow1 = 46, storeRow2 = 92;
            // auto-supplies (maintainer spec): the food/prod icons shrink to the pop icon's
            // 22px gabarit - the width bought pays for the Auto column on the right edge
            const int SupplyIconSize = 22, SupplyBarX = 52;
            float supplyAutoX = PStorage.Right - 62; // the Auto column, far right, one X for the three rows
            FoodStorage = new ProgressBar(PStorage.X + SupplyBarX, PStorage.Y + storeRow1, 0.4f*PStorage.Width + 20, 18);
            FoodStorage.Max = p.Storage.Max;
            FoodStorage.Progress = p.FoodHere;
            FoodStorage.color = "green";
            FoodDropDown = new DropOptions<Planet.GoodState>(new Vector2(PStorage.X + SupplyBarX + 0.4f * PStorage.Width + 36, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9), (int)(0.2f*PStorage.Width), 18);
            FoodDropDown.AddOption(GameText.Store, Planet.GoodState.STORE);
            FoodDropDown.AddOption(GameText.Import, Planet.GoodState.IMPORT);
            FoodDropDown.AddOption(GameText.Export, Planet.GoodState.EXPORT);
            FoodDropDown.ActiveIndex = (int)p.FS;
            FoodDropDown.OnValueChange = v => P.FS = v;
            FoodStorageIcon = new Rectangle((int)PStorage.X + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - SupplyIconSize / 2, SupplyIconSize, SupplyIconSize);
            ProdStorage = new ProgressBar(PStorage.X + SupplyBarX, PStorage.Y + storeRow2, 0.4f*PStorage.Width + 20, 18);
            ProdStorage.Max = p.Storage.Max;
            ProdStorage.Progress = p.ProdHere;
            ProfStorageIcon = new Rectangle((int)PStorage.X + 20, ProdStorage.pBar.Y + ProdStorage.pBar.Height / 2 - SupplyIconSize / 2, SupplyIconSize, SupplyIconSize);
            ProdDropDown = new DropOptions<Planet.GoodState>(new Vector2(PStorage.X + SupplyBarX + 0.4f*PStorage.Width + 36, ProdStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9), (int)(0.2f*PStorage.Width), 18);
            ProdDropDown.AddOption(GameText.Store, Planet.GoodState.STORE);
            ProdDropDown.AddOption(GameText.Import, Planet.GoodState.IMPORT);
            ProdDropDown.AddOption(GameText.Export, Planet.GoodState.EXPORT);
            ProdDropDown.ActiveIndex = (int)p.PS;
            ProdDropDown.OnValueChange = v => P.PS = v;
            // the Auto toggles: QUI decide - the dropdowns keep the vocabulary, greyed
            // read-only while their automatics hold the pen (maintainer design, Wishlist)
            if (p.NonCybernetic)
                Add(new UICheckBox(supplyAutoX, FoodStorage.pBar.Y + 1, () => P.AutoFood, v => P.AutoFood = v,
                                   Fonts.Arial12Bold, "Auto", GameText.AutoSupplyTip));
            Add(new UICheckBox(supplyAutoX, ProdStorage.pBar.Y + 1, () => P.AutoProd, v => P.AutoProd = v,
                               Fonts.Arial12Bold, "Auto", GameText.AutoSupplyTip));

            // Ludoal fork (wishlist + bench 426): the population row, full seat grammar -
            // a storage bar (population against its cap) like Food and Production, the
            // migration dropdown beside it. Auto = the formula keeps deciding; the manual
            // states pin the direction. The colonist freighter line seats under the bar.
            const float storeRow3 = 138;
            PopStorage = new ProgressBar(PStorage.X + SupplyBarX, PStorage.Y + storeRow3, 0.4f * PStorage.Width + 20, 18);
            PopStorage.Max = p.MaxPopulationBillionFor(p.Owner);
            PopStorage.Progress = p.PopulationBillion;
            PopStorage.color = "blue";
            // muted: at full blue it read as another research bar (maintainer, bench 524)
            PopStorage.FillTint = ColonyScreen.PopBarTint;
            var iconPop = ResourceManager.Texture("UI/icon_pop_22");
            ColonistsIcon = new Rectangle((int)PStorage.X + 20, (int)(PStorage.Y + storeRow3 + 9 - iconPop.Height / 2f), iconPop.Width, iconPop.Height);
            ColonistsDropDown = new DropOptions<Planet.GoodState>(new Vector2(PStorage.X + SupplyBarX + 0.4f * PStorage.Width + 36, PStorage.Y + storeRow3), (int)(0.2f * PStorage.Width), 18);
            // people words, not cargo words (bench 425): Stay / Bring in / Resettle map
            // onto STORE / IMPORT / EXPORT in the same order. QUI decides moved to the
            // Auto checkbox (auto-supplies) - in Auto the list shows the formula's live pick
            ColonistsDropDown.AddOption(GameText.Stay, Planet.GoodState.STORE);
            ColonistsDropDown.AddOption(GameText.BringIn, Planet.GoodState.IMPORT);
            ColonistsDropDown.AddOption(GameText.Resettle, Planet.GoodState.EXPORT);
            ColonistsDropDown.ActiveIndex = (int)(p.ColonistsManual ? p.CS : p.GetGoodState(Goods.Colonists));
            ColonistsDropDown.OnValueChange = v => P.CS = v;
            Add(new UICheckBox(supplyAutoX, PopStorage.pBar.Y + 1, () => P.AutoColonists, v => P.AutoColonists = v,
                               Fonts.Arial12Bold, "Auto", GameText.AutoSupplyTip));

            // Ludoal fork (maintainer feedback): TRADE ZONES, the fourth row. Its Y follows the
            // elders' own pitch (46 apart, like 46/92/138) so it lands under population rather
            // than at a fraction of whatever the stretching panel happens to have left. Only the
            // owner edits them, and only zones of THIS empire are ever named here.
            const float storeRow4 = 184;
            // the PENCIL, not a worded button (maintainer bench 554): the same icon and the same
            // verb the governor tab already uses two panels up, at its own 20px gabarit - a word
            // took a column the row would rather give to the zone names.
            const float SupplyEditW = 20;
            if (P.Owner == Player)
            {
                float editX = PStorage.Right - 14 - SupplyEditW;
                // ⚠ caption and VALUE are two labels, not one string (maintainer bench 554): the
                // screen's convention is a cream caption and a white value, and one label cannot
                // wear two colours. The value's X is measured off the caption it follows.
                string zonesCap = Localizer.Token(GameText.TzColonyZones) + ":";
                float capX = PStorage.X + 20;
                Add(new UILabel(new Vector2(capX, PStorage.Y + storeRow4), zonesCap,
                                Fonts.Arial12Bold, Colors.Cream, GameText.TzColonyZonesTip));
                float valueX = capX + Fonts.Arial12Bold.TextWidth(zonesCap) + 6;
                TradeZonesLabel = Add(new UILabel(new Vector2(valueX, PStorage.Y + storeRow4),
                                                  "", Fonts.Arial12Bold, Color.White));
                TradeZonesLabel.Tooltip = GameText.TzColonyZonesTip;
                TradeZonesWidth = editX - 12 - valueX;
                const string editTex = "NewUI/icon_build_edit";
                Add(new UIButton(new UIButton.StyleTextures(editTex, editTex + "_hover1"),
                                 new Vector2(SupplyEditW, SupplyEditW), "")
                {
                    Pos = new Vector2(editX, PStorage.Y + storeRow4 - 1),
                    Tooltip = GameText.TzColonyZonesEditTip,
                    OnClick = OnEditTradeZonesClicked,
                    ClickSfx = "sd_ui_accept_alt3",
                });
            }

            // Centre column: the colony grid keeps its height, STATISTICS below takes the rest -
            // it is the variable block of this column, and it closes on the grid's foot.
            // Right column: FIXED width - the buildable rows and the queue rows are written for
            // Ludoal fork: col 2 (COLONY + STATS) is the BOUNDED one, capped at 672; col 3
            // (BUILDINGS + QUEUE) absorbs the surplus. From 1440 to the point col 2 hits 672 the
            // two grow together off the leftover; past that, everything extra goes to col 3.
            const float ColCentreMax = 672f;
            float colCentreX = gridLeft + colLeftW + Pad;
            float available  = gridRight - colCentreX - Pad;     // what col 2 + col 3 share
            float colCentreW = Math.Min(available * 0.5f, ColCentreMax); // 50/50 until the 672 cap
            float colRightW  = available - colCentreW;           // col 3 takes the rest

            // COLONY holds a 7x5 tile grid, so its height FOLLOWS its width - square tiles are the
            // point of it. The panel's chrome (10 each side, 30 above, 5 below) is taken off
            // before the ratio and added back, so it is the GRID that keeps 7:5, not the frame.
            // COLONY keeps its 7:5 from the WIDTH again, the width itself bounded so the grid
            // cannot go giant at high resolutions - the stats block below takes the rest.
            float gridInnerW = Math.Min(colCentreW - 20, 620f); // width cap
            float subColonyH = gridInnerW * (5f / 7f) + 35;
            subColonyH = Math.Min(subColonyH, gridBottom - gridTop - Pad - 260); // stats floor, safety

            RectF subColonyR = new(colCentreX, gridTop, colCentreW, subColonyH);
            // Ludoal fork: the COLONY frame carries two views of the same content - MAP (the
            // 7x5 grid) and LIST (built instances). Not Add()ed: drawn by hand like before,
            // its tab row served explicitly in HandleInput.
            SubColonyGrid = new(subColonyR, GameText.Map);
            SubColonyGrid.AddTab(GameText.List);

            // the LIST view: same client area the frame gives its content (chrome: 10 each
            // side, 30 above, 5 below - the constants documented on the frame math above)
            RectF builtR = new(subColonyR.X + 10, subColonyR.Y + 30, subColonyR.W - 20, subColonyR.H - 35);
            // Ludoal fork (maintainer feedback): Snapshot at the HEAD of this frame - it
            // photographs what is built, and this is where what is built is listed. Pinned above
            // the rows rather than seated in them: a gesture that scrolls away is a gesture lost.
            SnapshotBlueprints = base.Add(new UIButton(ButtonStyle.Small, GameText.BlueprintsSnapshot)
            {
                Tooltip = GameText.BlueprintsSnapshotTip,
                ClickSfx = "sd_ui_accept_alt3",
            });
            SnapshotBlueprints.Rect = new Rectangle((int)(subColonyR.X + subColonyR.W - 150),
                                                    (int)(subColonyR.Y + 1), 140, 22); // bench 526: 3px up
            SnapshotBlueprints.OnClick += _ => GovernorDetails?.TakeBlueprintsSnapshot();
            SnapshotBlueprints.Visible = colonyViewTabSelected == 1 && p.OwnerIsPlayer;

            BuiltList = base.Add(new ScrollList<BuiltBuildingListItem>(builtR));
            BuiltList.EnableItemHighlight = true;
            BuiltList.OnHovered = OnBuiltHoverChange;
            BuiltList.OnClick = OnBuiltRowClicked;
            BuiltList.Visible = colonyViewTabSelected == 1;

            // ALWAYS resolve the initial tab - a fresh Submenu sits at -1 (nothing selected)
            // and the draw gate would show neither view until the first click (bench 420).
            // Set before OnTabChange is wired: a tab callback firing during setup must
            // never touch a not-yet-created child.
            SubColonyGrid.SelectedIndex = colonyViewTabSelected;
            if (colonyViewTabSelected == 1)
                ResetBuiltList();
            SubColonyGrid.OnTabChange = OnColonyViewTabChanged;

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
            // Column 3 steps down one tab strip - the filter's top on the frames' rule line of
            // columns 1 and 2, clear of the close cross at the seat.
            float col3Top = gridTop + Submenu.TabHeight - 2;
            float clearW = 17;
            float buildingsTop = col3Top + filterH + Pad;

            var filterBgRect = new RectF(colRightX + 60, col3Top,
                                         colRightW - 60 - clearW - 10, filterH);
            var filterRect = new RectF(filterBgRect.X + 5, filterBgRect.Y, filterBgRect.W, filterBgRect.H);
            FilterBuildableItems = Add(new UITextEntry(filterRect, Font12, ""));
            FilterBuildableItems.AutoCaptureOnHover = true;
            FilterBuildableItems.Background = new Submenu(filterBgRect);
            Vector2 filterLabelPos = new Vector2(colRightX, filterRect.Y + 2);
            FilterBuildableItemsLabel = Add(new UILabel(filterLabelPos, GameText.FilterLabel, Font12, Color.Gray));
            
            var customStyle = new UIButton.StyleTextures("NewUI/icon_clear_filter", "NewUI/icon_clear_filter_hover2");
            Add(new UIButton(customStyle, new Vector2(17, 17), "")
            {
                Tooltip = GameText.ClearBuildableItemsFilter,
                OnClick = OnClearFilterClick,
                Pos     = new Vector2(filterRect.Right + 10, filterRect.Y + 3)
            });

            // BUILDINGS and the queue split the column 50/50 - the column does not chase
            // COLONY's foot, which floats with the stats block.
            RectF buildableR = new(colRightX, buildingsTop, colRightW,
                                   (gridBottom - buildingsTop - Pad) / 2);
            BuildableTabs = base.Add(new SubmenuScrollList<BuildableListItem>(buildableR, BuildingsTabText));
            BuildableTabs.OnTabChange = OnBuildableTabChanged;

            BuildableList = BuildableTabs.List;
            BuildableList.EnableItemHighlight = true;
            BuildableList.OnClick = OnBuildableRowClicked; // bench 444: click pins the description (gold liseré, LIST pattern)
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

            // wishlist 20 Aug: the per-planet Continuous Rush toggle, seated beside the
            // CONSTRUCTION QUEUE tab OUTSIDE the frame. While the Automation global holds
            // the pen it shows checked read-only (Update grays it); unchecked global hands
            // the colony back its own flag, off by default.
            if (p.OwnerIsPlayer)
            {
                RushToggle = base.Add(new UICheckBox(queueR.Right - 150, queueTop + 4,
                    () => P.Owner.RushAllConstruction || P.RushConstruction,
                    v => { if (!P.Owner.RushAllConstruction) P.RushConstruction = v; },
                    // bench 459: its OWN tooltip - the empire-wide tip was speaking here
                    Fonts.Arial12Bold, GameText.RushAllConstruction, GameText.ContinuousRushColonyTip));
            }

            ConstructionQueue = queue.List;
            ConstructionQueue.EnableItemHighlight = true;
            ConstructionQueue.OnClick = OnQueueRowClicked; // bench 444: same pin as the buildable list
            ConstructionQueue.OnHovered = OnConstructionItemHovered;
            if (p.OwnerIsPlayer || p.Universe.Debug)
                ConstructionQueue.OnDragReorder = OnConstructionItemReorder;

            // ⚠ ONE source for the portrait's size: portraitH decided this panel's height above,
            // so the icon reads it back rather than repeating the number. It sits UNDER the title
            // bar instead of centring itself in the panel - a portrait that centres in a panel
            // sized for it just floats.
            int iconSize = (int)portraitH;
            int iconOffsetX = 148;

            // ⚠ Centred BELOW the title bar, not in the whole rect - or it rides up into it.
            float iconBandTop = PlanetInfo.Y + 26;
            float iconBandH   = PlanetInfo.Bottom - iconBandTop;
            PlanetIcon = new Rectangle((int)PlanetInfo.Right - iconOffsetX,
                                       (int)(iconBandTop + (iconBandH - iconSize) / 2),
                                       iconSize, iconSize);

            // The arrows sit on the panel's own header band, a tight pair centred over the planet image.
            const int arrowW = 14, arrowH = 20, NavGap = 40; // between the two arrows
            int arrowY = (int)PlanetInfo.Y + (Submenu.TabHeight - 2 - arrowH) / 2;
            int navCentre = PlanetIcon.CenterX();

            // Plain buttons, not toggles - the arrows only ever navigate.
            LeftColony = Add(new UIButton(new UIButton.StyleTextures("SelectionBox/button_arrow_left", "SelectionBox/button_arrow_left_hover"),
                                          new Vector2(arrowW, arrowH), "")
            {
                Pos = new Vector2(navCentre - NavGap / 2 - arrowW, arrowY),
                // bench 460: on a mole-host page the arrows walk the mole network
                Tooltip = (p.OwnerIsPlayer ? Localizer.Token(GameText.ViewPreviousColony)
                                           : Localizer.Token(GameText.ViewPrevInfiltrated))
                          + " (← / " + KeyBindings.Name(KeyBindings.PrevColony) + ")", // bench 428: the keys ride the tooltip
                OnClick = b => OnChangeColony(-1),
                ClickSfx = "sd_ui_accept_alt3", // the click every toggle played
            });

            RightColony = Add(new UIButton(new UIButton.StyleTextures("SelectionBox/button_arrow_right", "SelectionBox/button_arrow_right_hover"),
                                           new Vector2(arrowW, arrowH), "")
            {
                Pos = new Vector2(navCentre + NavGap / 2, arrowY),
                Tooltip = (p.OwnerIsPlayer ? Localizer.Token(GameText.ViewNextColony)
                                           : Localizer.Token(GameText.ViewNextInfiltrated))
                          + " (→ / " + KeyBindings.Name(KeyBindings.NextColony) + ")",
                OnClick = b => OnChangeColony(+1),
                ClickSfx = "sd_ui_accept_alt3",
            });

            // the HOME button between the arrows - straight back to the capital. Panel
            // navigation pans without zooming; the zoomed route is the cartouche's.
            const int homeSize = 16; // a notch under the arrows
            if (p.OwnerIsPlayer) // bench 460: YOUR capital has no seat on a mole-host page
            {
                Add(new UIButton(new UIButton.StyleTextures("UI/icon_home", "UI/icon_home"),
                                 new Vector2(homeSize, homeSize), "")
                {
                    Pos = new Vector2(navCentre - homeSize / 2, arrowY + (arrowH - homeSize) / 2),
                    Tooltip = GameText.ViewYourHomeworld,
                    OnClick = b => GoToHomeworld(),
                    ClickSfx = "sd_ui_accept_alt3",
                });
            }

            // ⚠ it sat ON the colony arrows, and not by accident: both were built from
            // PlanetIcon - the bar spanning its width, the arrows centred on its middle - so
            // they overlapped by construction, on the same title bar (maintainer, bench 535).
            const int ShieldBarClearance = 100; // moved clear of the arrows (maintainer's call)
            Rectangle planetShieldBarRect = new Rectangle(PlanetIcon.X - ShieldBarClearance,
                                                          PlanetInfo.Rect.Y + 2, PlanetIcon.Width, 20);
            PlanetShieldBar = new ProgressBar(planetShieldBarRect)
            {
                color = "blue"
            };

            PlanetShieldIconRect = new Rectangle(planetShieldBarRect.X - 30, planetShieldBarRect.Y-2, 20, 20);

            // square tiles whatever shaped the panel: the limiting dimension sets the tile,
            // and the grid centres in the other one
            int innerW = SubColonyGrid.Rect.Width - 20;
            int innerH = SubColonyGrid.Rect.Height - 35;
            int tileSize = Math.Min(innerW / 7, innerH / 5);
            GridPos = new Rectangle(SubColonyGrid.Rect.X + 10 + (innerW - tileSize * 7) / 2,
                                    SubColonyGrid.Rect.Y + 30 + (innerH - tileSize * 5) / 2,
                                    tileSize * 7, tileSize * 5);
            int width = tileSize;
            int height = tileSize;
            foreach (PlanetGridSquare planetGridSquare in p.TilesList)
                planetGridSquare.ClickRect = new Rectangle(GridPos.X + planetGridSquare.X * width, GridPos.Y + planetGridSquare.Y * height, width, height);
            
            PlanetName = Add(new UITextEntry(p.Name));
            // bench 460 (maintainer): a mole-host page wears the target's colors - the
            // name in the faction's color, the crest where the rename pencil sits
            PlanetName.Color = p.OwnerIsPlayer || p.Owner == null ? Colors.Cream : p.Owner.EmpireColor;
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
            // Terraform details live on the ASSIGN LABOR block.
            CreateTerraformingDetails(new Vector2(LaborRect.X + 15, LaborRect.Y + 38)); // air under the tab strip
            CreateDysonSwarmDetails(detailsVector);
        }

        void PopulatePfacilitieTabs()
        {
            PFacilities.ClearTabs();
            // ⚠ a literal, not the Statistics2 token: shortening the token would rename it
            // everywhere in the game. Only this row needs to fit on one line.
            PFacilities.AddTab("Stats");
            PFacilities.AddTab(StatsPlusTabTitle); // Ludoal fork: Stats+ add-on tab, next to its witness
            PFacilities.AddTab(GameText.Description);
            PFacilities.AddTab(GameText.Trade2);
            // Terraforming is a tab of the ASSIGN LABOR block.

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

            Font font = Font14;
            int spacing = font.LineSpacing + 10;
            int barWidth = (int)(PFacilities.Width * 0.5f);
            float indent = 30;

            AddLabel(ref DysonSwarmTypeTitle, pos, DysonSwarm.DysonSwarmTypeTitle(P.System.DysonSwarmType), font, Color.White);

            Vector2 buttonsPos = new Vector2(pos.X, pos.Y + spacing);
            AddButton(ref DysonSwarmStartButton, buttonsPos, GameText.BuildDysonSwarm, ButtonStyle.Default, GameText.BuildDysonSwarmTip);
            AddButton(ref DysonSwarmKillButton, new Vector2(buttonsPos.X + barWidth- spacing-110, buttonsPos.Y), GameText.KillDysonSwarm, ButtonStyle.DefaultHostile, GameText.KillDysonSwarmTip);
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
            Font font       = Font14;
            int spacing     = font.LineSpacing + 10;
            int barWidth    = (int)(PFacilities.Width * 0.33f);
            int sliderWidth = (int)(PFacilities.Width * 0.33f);
            int sliderSize  = 30;
            float indent    = 30;
            float indentTradeAmount = indent + barWidth + 5;
            float indentSlider      = indentTradeAmount + 35; // slid left for 900p

            // No "Colony Trade" title: the tab already names the page, and this is the
            // tallest tab - the row it frees is what makes it fit.
            Vector2 incomingTitlePos = new Vector2(pos.X, pos.Y);
            AddLabel(ref IncomingTradeTitle, incomingTitlePos, GameText.IncomingFreighters, font, Color.Gray);

            Vector2 manualImportTitlePos = new Vector2(pos.X + indentSlider - 10, incomingTitlePos.Y);
            AddLabel(ref ManualImportTitle, manualImportTitlePos, Localizer.Token(GameText.ManualImport), font, Color.Gray);

            // Incoming food
            Vector2 incomingFoodPos = new Vector2(pos.X, incomingTitlePos.Y + spacing + 3);
            AddPanel(ref IncomingFoodPanel, incomingFoodPos, "NewUI/icon_food", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle incomingFoodRect = new Rectangle((int)(incomingFoodPos.X + indent), (int)incomingFoodPos.Y, barWidth, 20);
            AddProgressBar(ref IncomingFoodBar, incomingFoodRect, P.FoodImportSlots, "green");
            Vector2 incomingFoodAmountPos = new Vector2(pos.X + indentTradeAmount, incomingFoodPos.Y + 2);
            AddLabel(ref IncomingFoodAmount, incomingFoodAmountPos, "", Font8, Color.White);
            Rectangle importFoodSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(incomingFoodPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ImportFoodSlotSlider, importFoodSlotsRect, "", 0, 20, P.ManualFoodImportSlots, GameText.ManualTradeSlotTip);

            // Incoming Prod
            Vector2 incomingProdPos = new Vector2(pos.X, incomingFoodPos.Y + spacing);
            AddPanel(ref IncomingProdPanel, incomingProdPos, "NewUI/icon_production", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle incomingProdRect = new Rectangle((int)(incomingProdPos.X + indent), (int)incomingProdPos.Y, barWidth, 20);
            AddProgressBar(ref IncomingProdBar, incomingProdRect, P.ProdImportSlots, "brown");
            Vector2 incomingProdAmountPos = new Vector2(pos.X + indentTradeAmount, incomingProdPos.Y + 2);
            AddLabel(ref IncomingProdAmount, incomingProdAmountPos, "", Font8, Color.White);
            Rectangle importProdSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(incomingProdPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ImportProdSlotSlider, importProdSlotsRect, "", 0, 20, P.ManualProdImportSlots, GameText.ManualTradeSlotTip);

            // Incoming Colonists
            Vector2 incomingColoPos = new Vector2(pos.X, incomingProdPos.Y + spacing);
            AddPanel(ref IncomingColoPanel, incomingColoPos, "UI/icon_pop", font.LineSpacing, GameText.IncomingOutGoingTip);
            Rectangle incomingColoRect = new Rectangle((int)(incomingColoPos.X + indent), (int)incomingColoPos.Y, barWidth, 20);
            AddProgressBar(ref IncomingColoBar, incomingColoRect, P.ColonistsImportSlots, "blue");
            Vector2 incomingColoAmountPos = new Vector2(pos.X + indentTradeAmount, incomingColoPos.Y + 2);
            AddLabel(ref IncomingColoAmount, incomingColoAmountPos, "", Font8, Color.White);
            Rectangle importColoSlotsRect = new Rectangle((int)(pos.X + indentSlider), (int)(incomingColoPos.Y-12), sliderWidth, sliderSize);
            AddUiSlider(ref ImportColoSlotSlider, importColoSlotsRect, "", 0, 20, P.ManualColoImportSlots, GameText.ManualTradeSlotTip);

            Vector2 outgoingTitlePos = new Vector2(pos.X, incomingColoAmountPos.Y + spacing * 1.5f);
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
            // Compact cascade: the block lives on the Assign Labor frame, 150px tall - Font14
            // rows on a tight pitch, no Font20 title (the tab names it), one line per datum.
            // The title label stays allocated but never shows.
            Font font    = Font14;
            // The rows SPREAD over the block's full height. The row count follows the owner's
            // terraforming level, the same gates the update applies.
            int lvlRows = Player.data.Traits.TerraformingLevel >= 3 ? 7
                        : Player.data.Traits.TerraformingLevel == 2 ? 4 : 3;
            int spacing = (int)((LaborRect.H - 55) / lvlRows).Clamped(font.LineSpacing + 1, 34);
            int barWidth = (int)(LaborRect.W * 0.33f);

            AddLabel(ref TerraformTitle, pos, "", Font20, Color.White);

            Vector2 statusTitlePos = new Vector2(pos.X, pos.Y);
            AddLabel(ref TerraformStatusTitle, statusTitlePos, GameText.TerraformingStatus, font, Color.White);

            float indent = font.MeasureString(TerraformStatusTitle.Text).X + 100;

            Vector2 statusPos = new Vector2(pos.X + indent, pos.Y);
            AddLabel(ref TerraformStatus, statusPos, " ", font, Color.Gray);

            Vector2 numTerraformersTitlePos = new Vector2(pos.X, TerraformStatusTitle.Y + spacing);
            AddLabel(ref TerraformersHereTitle, numTerraformersTitlePos, GameText.TerraformersHere, font, Color.Gray);

            Vector2 numTerraformersPos = new Vector2(pos.X + indent, numTerraformersTitlePos.Y);
            AddLabel(ref TerraformersHere, numTerraformersPos, " ", font, Color.White);

            Vector2 terraVolcanoTitlePos = new Vector2(pos.X, numTerraformersTitlePos.Y + spacing);
            AddLabel(ref TerrainTerraformTitle, terraVolcanoTitlePos, " ", font, Color.Gray);

            Vector2 terraVolcanoPos = new Vector2(pos.X + indent, terraVolcanoTitlePos.Y);
            AddLabel(ref VolcanoTerraformDone, terraVolcanoPos, GameText.TerraformersDone, font, Color.Green);

            Rectangle terraVolcanoRect = new Rectangle((int)terraVolcanoPos.X, (int)terraVolcanoPos.Y, barWidth, 14);
            AddProgressBar(ref TerrainTerraformBar, terraVolcanoRect, 100, "brown", percentage: true);

            Vector2 terraTileTitlePos = new Vector2(pos.X, terraVolcanoTitlePos.Y + spacing);
            AddLabel(ref TileTerraformTitle, terraTileTitlePos, " ", font, Color.Gray);

            Vector2 terraTilePos = new Vector2(pos.X + indent, terraTileTitlePos.Y);
            AddLabel(ref TileTerraformDone, terraTilePos, GameText.TerraformersDone, font, Color.Green);

            Rectangle terraTileRect = new Rectangle((int)terraTilePos.X, (int)terraTilePos.Y, barWidth, 14);
            AddProgressBar(ref TileTerraformBar, terraTileRect, 100, "green", percentage: true);

            Vector2 terraPlanetTitlePos = new Vector2(pos.X, terraTileTitlePos.Y + spacing);
            AddLabel(ref PlanetTerraformTitle, terraPlanetTitlePos, GameText.TerraformPlanet, font, Color.Gray);

            Vector2 terraPlanetPos = new Vector2(pos.X + indent, terraPlanetTitlePos.Y);
            AddLabel(ref PlanetTerraformDone, terraPlanetPos, GameText.TerraformersDone, font, Color.Green);

            Rectangle terraPlanetRect = new Rectangle((int)terraPlanetPos.X, (int)terraPlanetPos.Y, barWidth, 14);
            AddProgressBar(ref PlanetTerraformBar, terraPlanetRect, 100, "blue", percentage: true);

            Vector2 targetFertilityTitlePos = new Vector2(pos.X, terraPlanetTitlePos.Y + spacing);
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
                PinnedBuilt = null; // a scrapped pin must not haunt the panel
                if (BuiltList.Visible)
                    ResetBuiltList(); // the LIST view reflects the scrap immediately
            }
        }

        // ONE scrap path for both gestures: MAP's right-click on a tile and LIST's row delete
        // button. The guard is the building's own Scrappable flag - the list and the grid can
        // never disagree on what is removable.
        public void PromptScrapBuilding(PlanetGridSquare pgs)
        {
            if (pgs?.Building is not { Scrappable: true })
            {
                GameAudio.NegativeClick();
                return;
            }
            ToScrap = pgs.Building;
            string message = Localizer.Token(GameText.DoYouWishToScrapBuilding) + pgs.Building.TranslatedName.Text
                           + Localizer.Token(GameText.ScrapBuildingRecovery);
            var messageBox = new MessageBoxScreen(P.Universe.Screen, message);
            // centre the confirm on the COLONY frame, not the display (bench 420)
            messageBox.CenterOn = new Vector2(SubColonyGrid.Rect.X + SubColonyGrid.Rect.Width / 2f,
                                              SubColonyGrid.Rect.Y + SubColonyGrid.Rect.Height / 2f);
            messageBox.Accepted = ScrapAccepted;
            ScreenManager.AddScreen(messageBox);
        }

        void OnColonyViewTabChanged(int tabIndex)
        {
            BuiltList.Visible = tabIndex == 1;
            // the LIST frame's own gesture follows its list - wired HERE as well as in the
            // layout, or it would stay on screen over the MAP view
            if (SnapshotBlueprints != null)
                SnapshotBlueprints.Visible = BuiltList.Visible && P.OwnerIsPlayer;
            LastBuiltHover = null;
            PinnedBuilt = null;
            DescriptionScroll = 0f;
            if (tabIndex == 1)
                ResetBuiltList();
        }

        void OnBuiltHoverChange(BuiltBuildingListItem item)
        {
            // live hover only (bench 426): leaving a row - even into the list's own empty
            // space - returns the bottom panel to the pinned row, or the default tab
            LastBuiltHover = item?.Tile;
        }

        // hover previews, CLICK pins (bench 426, Lek's design): the bottom panel holds on
        // the pinned building, the wheel scrolls its long lore, re-click unpins
        // bench 444: the LIST tab's pin pattern, extended to the two right-column lists.
        // One pin at a time across both; a click on the pinned row releases it.
        void OnBuildableRowClicked(BuildableListItem item)
        {
            if (item == null || (item.Building == null && item.Troop == null && item.Ship == null))
                return; // headers have nothing to hold
            bool wasPinned = item == PinnedBuildable;
            ClearListPins();
            PinnedBuildable = wasPinned ? null : item;
            item.DescriptionPinned = !wasPinned;
            DescriptionScroll = 0f;
            // no click sound here: the list item base already played it (bench 447 double-buzz)
            if (item.Ship != null)
            {
                if (item.DescriptionPinned) ShipInfoOverlay.ShowInRect(DescriptionPane, item.Ship);
                else                        ShipInfoOverlay.Hide();
            }
        }

        void OnQueueRowClicked(ConstructionQueueScrollListItem item)
        {
            if (item == null || (item.Item.Building == null && item.Item.TroopType == null && !item.Item.isShip))
                return;
            bool wasPinned = item == PinnedQueue;
            ClearListPins();
            PinnedQueue = wasPinned ? null : item;
            item.DescriptionPinned = !wasPinned;
            DescriptionScroll = 0f;
            if (item.Item.isShip)
            {
                if (item.DescriptionPinned) ShipInfoOverlay.ShowInRect(DescriptionPane, item.Item.ShipData);
                else                        ShipInfoOverlay.Hide();
            }
        }

        void ClearListPins()
        {
            if (PinnedBuildable != null) PinnedBuildable.DescriptionPinned = false;
            if (PinnedQueue != null) PinnedQueue.DescriptionPinned = false;
            PinnedBuildable = null;
            PinnedQueue = null;
        }

        void OnBuiltRowClicked(BuiltBuildingListItem item)
        {
            if (item?.Tile == null)
                return;
            PinnedBuilt = PinnedBuilt == item.Tile ? null : item.Tile;
            DescriptionScroll = 0f;
            GameAudio.AcceptClick();
        }

        public bool IsPinnedBuilt(PlanetGridSquare t) => PinnedBuilt != null && PinnedBuilt == t;

        // The LIST rows' reading (bench 424 arbitration: NET, as the colony runs): the
        // building's MARGINAL contribution through the sim's own pipeline - labor share,
        // fertility/richness, racial modifiers, then the resource tax via the sim's own
        // AfterTax. The yield formulas are linear in each building's share, so these
        // marginals sum exactly to the colony totals STATS+ shows (consumption excluded -
        // it belongs to the colony, not to a building).
        public void BuildingNetYields(Building b, out float food, out float prod, out float res)
        {
            float workers = P.PopulationBillion;
            var traits = P.Owner.data.Traits;

            float grossFood = b.PlusFlatFoodAmount
                            + P.Food.Percent * workers * P.Fertility * b.PlusFoodPerColonist;
            food = P.Food.AfterTax(grossFood);

            float grossProd = b.PlusFlatProductionAmount
                            + b.PlusProdPerRichness * P.MineralRichness
                            + P.Prod.Percent * workers * P.MineralRichness * (1f + traits.ProductionMod) * b.PlusProdPerColonist;
            grossProd *= P.Owner.GetStaticExoticBonusMuliplier(ExoticBonusType.Production); // the sim applies it to flat AND yield
            prod = P.Prod.AfterTax(grossProd);

            float grossRes = (b.PlusFlatResearchAmount
                            + P.Res.Percent * workers * b.PlusResearchPerColonist) * (1f + traits.ResearchMod);
            res = P.Res.AfterTax(grossRes);
        }

        // The building's NET money as the treasury books it (bench 425): its taxed direct
        // income (credits per colonist and flat income only yield their taxed share) plus
        // its tax-boost share (a PlusTaxPercentage building like the Capital earns its cut
        // of taxing the base workforce), minus its real maintenance. The boost share is
        // attributed on the workforce base so it does not double-count building credits.
        public float BuildingNetMoney(Building b)
        {
            float pop = P.PopulationBillion;
            float exotic = P.Owner.ExoticCreditsBonus;
            float empireRate = P.Owner.data.TaxRate;

            float taxBoostSum = 0f;
            foreach (Building o in P.Buildings)
                taxBoostSum += o.PlusTaxPercentage;
            float rateEff = empireRate * (1f + P.Owner.data.Traits.TaxMod + taxBoostSum);

            float direct = (b.CreditsPerColonist * pop + b.Income) * rateEff * exotic;
            float boost  = pop * empireRate * b.PlusTaxPercentage * exotic;
            return direct + boost - b.ActualMaintenance(P);
        }

        // ONE arithmetic for a building's yields on this colony - the MAP tiles' icon rows
        // and the LIST view's value columns both read it. laborShare=true follows what the
        // colony collects THIS instant (the sliders weigh the per-colonist parts - the
        // tiles' historical behaviour); false shows the building's contribution at current
        // population regardless of today's labor allocation - the LIST's reading, where
        // Capital City's research must scale with its colonists (bench 424). Fertility and
        // richness weigh the per-colonist parts either way; flat amounts land whole.
        public void BuildingActualYields(Building b, out float food, out float prod, out float res, bool laborShare = true)
        {
            food = 0f; prod = 0f; res = 0f;
            float pop = P.PopulationBillion;
            if (b.PlusFlatFoodAmount > 0f || b.PlusFoodPerColonist > 0f)
            {
                food += b.PlusFoodPerColonist * pop * (laborShare ? P.Food.Percent : 1f) * P.Fertility;
                food += b.PlusFlatFoodAmount;
            }

            if (b.PlusFlatProductionAmount > 0f || b.PlusProdPerColonist > 0f)
            {
                prod += b.PlusFlatProductionAmount;
                prod += b.PlusProdPerColonist * pop * (laborShare ? P.Prod.Percent : 1f) * P.MineralRichness;
            }

            if (b.PlusProdPerRichness > 0f)
                prod += b.PlusProdPerRichness * P.MineralRichness;

            if (b.PlusResearchPerColonist > 0f || b.PlusFlatResearchAmount > 0f)
            {
                res += b.PlusResearchPerColonist * pop * (laborShare ? P.Res.Percent : 1f);
                res += b.PlusFlatResearchAmount;
            }
        }

        // LIST view content (bench 424 - nothing hides, everything classifies): the
        // capital first and out of any group, then BUILDINGS (the player's works - they
        // all carry a build cost), RESOURCES (commodity deposits like exotic minerals),
        // FEATURES (terrain and event tiles - Mountain, anomalies). One row per INSTANCE.
        // Headers always show, even for a lone family (bench 425) - the label informs.
        void ResetBuiltList()
        {
            BuiltList.Reset();
            var occupied = P.TilesList.Filter(t => t.Building != null);

            // the pinned Colonists line (bench 425): the workforce's own share, so the
            // columns sum to the STATS+ totals for every resource
            BuiltList.AddItem(new BuiltBuildingListItem(this));

            foreach (PlanetGridSquare t in occupied)
                if (t.Building.IsCapitalOrOutpost)
                    BuiltList.AddItem(new BuiltBuildingListItem(this, t));

            var rest      = occupied.Filter(t => !t.Building.IsCapitalOrOutpost);
            var buildings = rest.Filter(t => t.Building.Cost > 0 && !t.Building.IsCommodity);
            var resources = rest.Filter(t => t.Building.IsCommodity);
            var features  = rest.Filter(t => t.Building.Cost <= 0 && !t.Building.IsCommodity);

            var groups = new Array<(LocalizedText Title, PlanetGridSquare[] Tiles)>();
            if (buildings.Length > 0) groups.Add((GameText.Buildings, buildings));
            if (resources.Length > 0) groups.Add((GameText.Resources, resources));
            if (features.Length > 0)  groups.Add((GameText.Features, features));

            // bench 425: headers stay even for a lone family - the label IS the information
            foreach ((LocalizedText title, PlanetGridSquare[] tiles) in groups)
            {
                tiles.Sort(t => t.Building.TranslatedName.Text);
                BuiltBuildingListItem header = BuiltList.AddItem(new BuiltBuildingListItem(this, title.Text));
                // the category bar runs the full row, stopping short of the delete lane (bench 426)
                header.HeaderMaxWidth = (int)(BuiltList.Width - 50);
                foreach (PlanetGridSquare t in tiles)
                    header.AddSubItem(new BuiltBuildingListItem(this, t));
                header.Expand(true); // an inventory opens legible, not folded
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
