using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using SDUtils;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.Audio;
using Font = Ship_Game.Graphics.Font;
using RenderStates = Ship_Game.Graphics.RenderStates; // the description pane is scissored
using Ship_Game.Universe.SolarBodies;
using System.Collections.Generic;
using System.Linq;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
using Ship_Game.UI; // UITable.FitText

namespace Ship_Game
{
    public partial class BlueprintsScreen : GameScreen
    {
        public bool PlanAreaHovered { get; private set; }
        readonly Array<BlueprintsTile> TilesList = new(35);
        readonly Submenu SubBlueprintsOptions;
        readonly Submenu PlanStats;
        readonly UILabel BlueprintsName;
        readonly UILabel CannotBuildTroopsWarning, CannotBuildShipsWarning;
        readonly UICheckBox ExclusiveCheckbox;
        public bool Exclusive;
        // Ludoal fork (Blueprints chantier): the catalogue offers the whole game, not only what
        // research has opened. A design-time view, so it is deliberately NOT saved with the plan.
        readonly UICheckBox ShowAllToggle;
        // ⚠ public like Exclusive right above, and for the same reason: the checkbox binds
        // it through an expression tree, which the compiler does not count as an assignment -
        // a private field bound that way trips CS0649, which this build treats as an error.
        public bool ShowAllBuildings;
        readonly Submenu SubPlanArea;
        readonly FloatSlider InitPopulationSlider;
        readonly FloatSlider InitFertilitySlider;
        readonly FloatSlider InitRichnessSlider;
        readonly FloatSlider InitTaxSlider;
        readonly UIButton SaveBlueprints;
        readonly UIButton LoadBlueprints;
        readonly UIButton LinkBlueprints;
        readonly UIButton UnlinkBlueprints;
        float InitPopulationBillion = 5;
        float InitFertility = 1;
        float InitRichness = 1;
        float InitTax = 0.25f;

        readonly ScrollList<BlueprintsBuildableListItem> BuildableList;
        // Ludoal fork (Blueprints chantier): the plan, as an ordered QUEUE. It is a VIEW over
        // the tiles that still hold the buildings - so save, load and the colony simulation
        // never learn that this screen changed.
        readonly ScrollList<BlueprintsPlanListItem> PlanList;

        // ⚠ ONE pin across BOTH lists (the Colony LIST pattern, bench 535): a click holds a
        // building's description in the panel below, a click on the held row releases it. Hover
        // still previews - the pin is what survives the cursor leaving.
        BlueprintsBuildableListItem PinnedBuildable;
        BlueprintsPlanListItem PinnedPlan;
        Building PinnedBuilding => PinnedBuildable?.Building ?? PinnedPlan?.Building;
        // what the Description tab actually shows: the cursor wins while it is on a row, the
        // pin holds the panel the rest of the time. One owner, so the tab and the content
        // cannot disagree.
        Building DescriptionBuilding => HoveredBuilding ?? PinnedBuilding;

        // the description elevator, the Colony pane's own (bench 535): the pane is ALWAYS
        // scissored so long lore cannot bleed past the frame, the wheel offset is clamped to the
        // content measured at the last draw, and the offset resets when the shown building
        // changes identity. It is what lets the Outpost keep its description again.
        float DescriptionScroll;
        float MaxDescriptionScroll;
        Building LastDescribed;
        readonly ScrollList<BlueprintsChainListItem> BlueprintsChainList;
        readonly DropOptions<Planet.ColonyType> SwitchColonyType;
        readonly UILabel LinkBlueprintsName;
        // the STATE, separate from the label (bench 305): the shown text folds to the
        // frame with a tooltip, so it can no longer be read back as the link's name
        string LinkedTo = "";

        void SetLinkedTo(string name)
        {
            LinkedTo = name ?? "";
            string shown = UITable.FitText(Fonts.Arial12Bold, LinkedTo,
                                           (int)(SubBlueprintsOptions.Width - 160 - 12));
            LinkBlueprintsName.Text = shown;
            LinkBlueprintsName.Tooltip = shown != LinkedTo ? LinkedTo : "";
        }

        Building HoveredBuilding;
        int StatsTabPlayerChoice;
        readonly Font Font8 = Fonts.Arial8Bold;
        readonly Font Font12 = Fonts.Arial12Bold;
        readonly Font Font14 = Fonts.Arial14Bold;
        readonly Font Font20 = Fonts.Arial20Bold;
        readonly Font TextFont;
        readonly Font BigFont;
        public readonly Empire Player;
        readonly UniverseScreen Universe; // Ludoal fork: for the live top bar
        Submenu DesignTabs;   // Ludoal fork: the Design group's tab row, this screen being one tab
        // Ludoal fork (bench 387): this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => DesignTabs?.Rect ?? base.PageFrame;

        float PlannedGrossMoney;
        float PlannedColonistIncome; // bench 353: the budget breakdown for Stats+, sums to Gross
        float PlannedBuildingIncome;
        float PlannedFoodEatenPerCol; // bench 354: consumption exposed for the Stats+ yields 'eaten' column
        float PlannedProdEatenPerCol;
        float PlannedMaintenance;
        float PlannedNetIncome;
        float PlannedFertility;
        float PlannedPopulation;
        float PlannedFlatFood;
        float PlannedFoodPerCol;
        float PlannedFlatProd;
        float PlannedProdPerCol;
        float PlannedFlatResearch;
        float PlannedResearchPerCol;
        float PlannnedInfrastructure;
        float PlannedRepairPerTurn;
        float PlannedStorage;
        float PlannedShields;
        int PlanetLevel;

        bool CanBuildTroops;
        bool CanBuildShips;

        UILabel PlannedFertilityLbl, PlannedPopLbl;

        readonly GovernorDetailsComponent GovernorTab;

        // Snapshot/Edit arrive FROM a colony (bench 397): closing this screen goes back to
        // it. Group jumps and Design tab switches call ExitScreen directly and skip this.
        readonly Planet ReturnToColony;

        public BlueprintsScreen(UniverseScreen parent, Empire player, BlueprintsTemplate template = null,
                                GovernorDetailsComponent govTab = null, Planet returnToColony = null)
            : base(parent, toPause: parent)
        {
            Universe = parent; // Ludoal fork: kept for the live top bar
            Player = player;
            IsPopup = true; // Ludoal fork (bench 345): the paused universe shows behind, dimmed - like the table screens
            GovernorTab = govTab;
            ReturnToColony = returnToColony;
            TextFont = Font12;
            BigFont = Font12; // the general stats read smaller (maintainer bench 301)
            // Ludoal fork: the Blueprints tab of the Design group - a FIXED 900p footprint
            // (maintainer bench 301) like Relationships: the blocks are written for it, a
            // bigger screen just leaves space at its right. Inner blocks sit on 10px margins;
            // heights are derived from what each block HOLDS, not cut as fractions - the old
            // fractional cuts clipped the warnings and pushed the Tax slider out of its frame.
            Rectangle frame900 = ScreenGroups.GroupFrame900(ScreenWidth, ScreenHeight);
            DesignTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.DesignTabTitles, 2,
                                                    OnDesignTabChanged, frame900.Width, frame900.Height);
            // the close cross fires ExitScreen itself, then its OnClick - hook the return there
            if (ReturnToColony != null)
                foreach (UIElementV2 e in GetElements())
                    if (e is CloseButton cross)
                        cross.OnClick = b => ReopenParentColony();
            const int Pad = 10;
            RectF client = DesignTabs.ClientArea;
            // ⚠ measured off the FRAME's borders, not the client area: the client is
            // already ~9px inside the nine-slice, so padding from it read as ~19px of
            // margin on the left, bottom and right (maintainer bench 307)
            RectF frameR = DesignTabs.RectF;
            float leftX  = frameR.X + Pad;
            float topY   = client.Y + Pad;
            float botY   = frameR.Bottom - Pad;

            // ⚠ Ludo, Blueprints chantier: the plan and the catalogue are SWAPPED. The plan is
            // what you compose, so it takes the full-height right column; the catalogue is what
            // you pick from, and moves to the centre. The drag that adds a building now runs
            // left to right, the way the gesture reads.
            const float PlanW = 470f; // the Colony screen's own list width at the 900p floor
            RectF planAreaR = new(frameR.Right - Pad - PlanW, topY, PlanW, botY - topY);
            SubPlanArea = base.Add(new Submenu(planAreaR, GameText.CurrentBlueprintsSubMenu));

            // BLUEPRINT OPTIONS: six lines plus the button row - width sized on its widest
            // row (label 150 + dropdown 100 + margins vs four small buttons at 80 pitch)
            const float OptionsW = 360f;
            const float OptionsRow = 20f;
            float optionsH = 28 + 6 * OptionsRow + 8 + 24 + 8;
            RectF blueprintsStatsR = new(leftX, topY, OptionsW, optionsH);
            SubBlueprintsOptions = base.Add(new Submenu(blueprintsStatsR, GameText.BlueprintsOptions));

            // ⚠ the two blocks are SWAPPED against the original order (Ludo, Blueprints
            // chantier): the chain is consulted while designing, the simulation is read once
            // the plan stands - so the chain sits under the options it belongs to, and the
            // sliders take the foot of the column.
            //
            // COLONY SIMULATION: exactly the four sliders (50px pitch each, 35px lead-in),
            // posed at the column's FOOT as a fixed block - the chain then fills what is
            // between, which is the only measurement allowed to be a remainder here.
            float simH = 35 + 4 * 50 + 10;
            RectF experimentalR = new(leftX, botY - simH, OptionsW, simH);

            // FORWARD LINK CHAIN: between the options above and the simulation below
            RectF chainR = new(leftX, blueprintsStatsR.Bottom + Pad, OptionsW,
                               experimentalR.Y - Pad - (blueprintsStatsR.Bottom + Pad));
            base.Add(new Submenu(chainR, GameText.LinksChain));
            base.Add(new Submenu(experimentalR, GameText.BlueprintsSimulation));

            // centre column: the buildings catalogue on top, the tabbed stats block under it
            float centreX = leftX + OptionsW + Pad;
            float centreW = planAreaR.X - Pad - centreX;
            RectF buildableMenuR = new(centreX, topY, centreW, (botY - topY) * 0.5f);
            base.Add(new Submenu(buildableMenuR, GameText.Buildings));

            // ⚠ it rides the CATALOGUE's own title bar, at its right end (maintainer, bench
            // 535): it steers what that list offers and nothing else, so it belongs to that
            // block. Up on the group's tab row it was floating next to nothing. Its left edge
            // is measured from the LOCALISED label, or the French one runs off the frame.
            float toggleW = Font12.TextWidth(Localizer.Token(GameText.BpShowAll)) + 26;
            ShowAllToggle = base.Add(new UICheckBox(buildableMenuR.Right - toggleW - 8, buildableMenuR.Y + 3,
                () => ShowAllBuildings, Font12, GameText.BpShowAll, GameText.BpShowAllTip));
            ShowAllToggle.TextColor = Color.Wheat;
            ShowAllToggle.OnChange = _ => RefreshBuildableList();

            // STATISTICS | DESCRIPTION - hovering a building raises Description on its own
            // (maintainer bench 301, the Colony screen's behavior)
            RectF blueprintsStatsRect = new(centreX, buildableMenuR.Bottom + Pad, centreW,
                                            botY - (buildableMenuR.Bottom + Pad));
            // bench 353: STATS | STATS+ | DESCRIPTION. Stats+ (index 1) is the default view; hovering a
            // building raises Description (2). A literal for Stats+, matching the Colony tab title.
            PlanStats = base.Add(new Submenu(blueprintsStatsRect,
                new LocalizedText[] { GameText.Statistics2, ColonyScreen.StatsPlusTabTitle, GameText.Description }));
            PlanStats.OnTabChange = i => StatsTabPlayerChoice = i; // manual clicks only
            StatsTabPlayerChoice = 1; // default to Stats+


            // one 20px row per line, six lines, then the button row - the block's height
            // was derived from exactly this cascade
            float blueprintsOptionsX = SubBlueprintsOptions.X + 10;
            float optRow0 = SubBlueprintsOptions.Y + 28;
            BlueprintsName = base.Add(new UILabel(new Vector2(blueprintsOptionsX, optRow0),
                GameText.NewBlueprints, Font14, Color.Gold));
            ExclusiveCheckbox = base.Add(new UICheckBox(blueprintsOptionsX, optRow0 + OptionsRow,
                () => Exclusive, TextFont, GameText.ExclusiveBlueprints, GameText.ExclusiveBlueprintsTip));
            ExclusiveCheckbox.TextColor = Color.Wheat;
            CannotBuildShipsWarning = base.Add(new UILabel(new Vector2(blueprintsOptionsX, optRow0 + 4 * OptionsRow),
                GameText.BlueprintsCannotBuildShips, Font12, Color.Pink));
            CannotBuildTroopsWarning = base.Add(new UILabel(new Vector2(blueprintsOptionsX, optRow0 + 5 * OptionsRow),
                GameText.BlueprintsCannotBuildTroops, Font12, Color.Pink));

            float buttonsY = optRow0 + 6 * OptionsRow + 8;
            // Load / Link / Unlink / Save (Ludo, Blueprints chantier): the row now reads in the
            // order the gestures happen - you fetch a plan, chain it, unchain it, and only then
            // commit. Save last, where a commit belongs.
            LoadBlueprints = base.Add(new UIButton(ButtonStyle.Small, new Vector2(blueprintsOptionsX, buttonsY), GameText.Load));
            LoadBlueprints.OnClick = (b) => OnLoadBlueprintsClick();
            LinkBlueprints = base.Add(new UIButton(ButtonStyle.Small, new Vector2(blueprintsOptionsX + 80, buttonsY), GameText.BpLink));
            LinkBlueprints.OnClick = (b) => OnLinkBlueprintsClick();
            LinkBlueprints.Enabled = false;
            UnlinkBlueprints = base.Add(new UIButton(ButtonStyle.Small, new Vector2(blueprintsOptionsX + 160, buttonsY), GameText.BpUnlink));
            UnlinkBlueprints.OnClick = (b) => OnUnlinkBlueprintsClick();
            UnlinkBlueprints.Enabled = false;
            SaveBlueprints = base.Add(new UIButton(ButtonStyle.Small, new Vector2(blueprintsOptionsX + 240, buttonsY), GameText.Save));
            SaveBlueprints.OnClick = (b) => OnSaveBlueprintsClick();

            RectF initPopR = new(blueprintsOptionsX, experimentalR.Y + 40, SubBlueprintsOptions.Width*0.6, 50);
            InitPopulationSlider = SliderDecimal1(initPopR, GameText.Population, 0.1f, 20, InitPopulationBillion);
            InitPopulationSlider.OnChange = (s) => { InitPopulationBillion = s.AbsoluteValue.RoundToFractionOf10(); RecalculateGeneralStats(); };
            PlannedPopLbl = base.Add(new UILabel(Font14));

            RectF initFertR = new(blueprintsOptionsX, experimentalR.Y + 90, SubBlueprintsOptions.Width * 0.6, 50);
            InitFertilitySlider = SliderDecimal1(initFertR, GameText.Fertility, 0, 3, InitFertility);
            InitFertilitySlider.OnChange = (s) => { InitFertility = s.AbsoluteValue.RoundToFractionOf10(); RecalculateGeneralStats(); };
            PlannedFertilityLbl = base.Add(new UILabel(Font14));

            RectF initRichR = new(blueprintsOptionsX, experimentalR.Y + 140, SubBlueprintsOptions.Width * 0.6, 50);
            InitRichnessSlider = SliderDecimal1(initRichR, GameText.MineralRichness, 0, 5, InitRichness);
            InitRichnessSlider.OnChange = (s) => { InitRichness = s.AbsoluteValue.RoundToFractionOf10(); RecalculateGeneralStats(); };

            RectF initTaxR = new(blueprintsOptionsX, experimentalR.Y + 190, SubBlueprintsOptions.Width * 0.6, 50);
            InitTaxSlider = Slider(SliderStyle.Percent, initTaxR, GameText.TaxRate, 0, 1, InitTax);
            InitTaxSlider.OnChange =(s) => { InitTax = s.AbsoluteValue; RecalculateGeneralStats(); };


            RectF buildableR = new(buildableMenuR.X, buildableMenuR.Y+20, buildableMenuR.W, buildableMenuR.H -20);
            BuildableList = base.Add(new ScrollList<BlueprintsBuildableListItem>(buildableR, 40));
            BuildableList.EnableItemHighlight = true;
            BuildableList.OnClick = OnBuildableRowClicked; // single click pins, double click plans it
            BuildableList.OnDoubleClick = OnBuildableItemDoubleClicked;
            BuildableList.EnableDragOutEvents = true;
            BuildableList.OnDragOut = OnBuildableListDrag;
            RectF planListR = new(planAreaR.X, planAreaR.Y + 20, planAreaR.W, planAreaR.H - 20);
            PlanList = base.Add(new ScrollList<BlueprintsPlanListItem>(planListR, 32));
            PlanList.EnableItemHighlight = true;
            PlanList.OnClick = OnPlanRowClicked;
            RectF chainlistR = new(chainR.X, chainR.Y + 20, chainR.W, chainR.H - 20);
            BlueprintsChainList = base.Add(new ScrollList<BlueprintsChainListItem>(chainlistR, 40));
            BlueprintsChainList.EnableItemHighlight = true;


            base.Add(new UILabel(new Vector2(blueprintsOptionsX, optRow0 + 3 * OptionsRow),
                "Linked Blueprints:", TextFont, Color.Wheat, GameText.LinkBlueprintsTip));
            LinkBlueprintsName = base.Add(new UILabel(new Vector2(blueprintsOptionsX + 150, optRow0 + 3 * OptionsRow),
                "", TextFont, Color.White));

            base.Add(new UILabel(new Vector2(blueprintsOptionsX, optRow0 + 2 * OptionsRow),
                "Switch Governor to:", TextFont, Color.Wheat, GameText.ExclusiveBlueprintsTip));
            SwitchColonyType = base.Add(Add(new DropOptions<Planet.ColonyType>(blueprintsOptionsX + 150, optRow0 + 2 * OptionsRow, 100, 18)));
            SwitchColonyType.AddOption(option: "--", Planet.ColonyType.Colony);
            SwitchColonyType.AddOption(option: GameText.Core, Planet.ColonyType.Core);
            SwitchColonyType.AddOption(option: GameText.Industrial, Planet.ColonyType.Industrial);
            SwitchColonyType.AddOption(option: GameText.Agricultural, Planet.ColonyType.Agricultural);
            SwitchColonyType.AddOption(option: GameText.Research, Planet.ColonyType.Research);
            SwitchColonyType.AddOption(option: GameText.Military, Planet.ColonyType.Military);
            SwitchColonyType.ActiveValue = Planet.ColonyType.Colony;

            Rectangle gridPos = new Rectangle(SubPlanArea.Rect.X + 10, SubPlanArea.Rect.Y + 30, 
                                              SubPlanArea.Rect.Width - 20, SubPlanArea.Rect.Height - 35);
            CreateBlueprintsTiles(gridPos);
            RefreshBuildableList();
            if (template != null)
            {
                LoadBlueprintsTemplate(template);
                // ⚠ Load STAYS (maintainer, bench 535). It was hidden when the screen opened on
                // an existing plan, which was invisible while it sat last in the row - the
                // reorder moved it to the front and turned the hiding into a hole. And the
                // button has a real use here: swapping a colony already on Custom to another
                // plan is exactly what the old + did.
                BlueprintsName.Text = template.Name;
            }
        }

        public override void PerformLayout()
        {
            PlannedPopLbl.Pos       = new Vector2(InitPopulationSlider.Right, InitPopulationSlider.Y+23);
            PlannedFertilityLbl.Pos = new Vector2(InitFertilitySlider.Right, InitFertilitySlider.Y+23);
            base.PerformLayout();
        }

        // ⚠ the tiles are the plan's STORAGE and nothing else now: their panels are built but
        // never added to the screen, so no grid is drawn and none of them answers the mouse.
        // Keeping them means the template, the simulation and the save path are untouched by a
        // change that is only about how the plan is SHOWN.
        void CreateBlueprintsTiles(Rectangle gridPos)
        {
            // ⚠ SQUARE cells, centred in whatever box the column gives (Blueprints chantier):
            // dividing each side by its own count stretched the tiles to the shape of the
            // frame, and the frame is now a tall narrow column - the building icons came out
            // twice as high as wide. One cell size, taken from the tighter of the two.
            int cell = System.Math.Min(gridPos.Width / SolarSystemBody.TileMaxX,
                                       gridPos.Height / SolarSystemBody.TileMaxY);
            int originX = gridPos.X + (gridPos.Width - cell * SolarSystemBody.TileMaxX) / 2;

            for (int y = 0; y < SolarSystemBody.TileMaxY; y++)
                for (int x = 0; x < SolarSystemBody.TileMaxX; x++)
                {
                    var panel = new UIPanel(new Rectangle(originX + x * cell, gridPos.Y + y * cell, cell, cell), Color.White);
                    TilesList.Add(new BlueprintsTile(panel));
                }
        }

        void OnBuildableItemDoubleClicked(BlueprintsBuildableListItem item)
        {
            if (!TryAddBuilding(item.Building, item.Unlocked))
                GameAudio.NegativeClick();
        }

        bool TryAddBuilding(Building b, bool unlocked)
        {
            BlueprintsTile tile = TilesList.Find(t => t.IsFree);
            if (tile != null)
            {
                tile.AddBuilding(b, unlocked);
                RefreshBuildableList();
                return true;
            }

            return false;
        }

        void RefreshBuildableList()
        {
            BuildableList.Reset();
            AddOutpost();

            // ⚠ a plan stores NAMES, not unlocked buildings - which is exactly what the
            // achievable percentage measures - so the catalogue can offer the whole game and a
            // plan can be designed ahead of the research that unlocks it. What is not researched
            // yet is listed all the same and drawn as locked, so the offer never lies.
            var unlocked = new HashSet<string>();
            foreach (Building u in Player.GetUnlockedBuildings())
                unlocked.Add(u.Name);

            IEnumerable<Building> catalogue = ShowAllBuildings
                                            ? ResourceManager.BuildingsDict.Values.Sorted(b => b.Name)
                                            : Player.GetUnlockedBuildings().Sorted(b => b.Name);
            foreach (Building b in catalogue)
            {
                if (b.IsSuitableForBlueprints && !TilesList.Any(t => t.BuildingNameHereIS(b.Name)))
                {
                    b.UpdateOffense(PlanetLevel, Player.Universe);
                    BuildableList.AddItem(new BlueprintsBuildableListItem(this, b, unlocked.Contains(b.Name)));
                }
            }

            RefreshPlanList();
            RecalculateGeneralStats();
        }

        // rebuilt whole rather than patched: the plan is short, and a queue that is rebuilt
        // from its storage can never drift from it
        void RefreshPlanList()
        {
            PlanList.Reset();
            foreach (BlueprintsTile tile in TilesList)
                if (tile.HasBuilding)
                    PlanList.AddItem(new BlueprintsPlanListItem(this, tile));
        }

        // ⚠ moving an entry SWAPS what two tiles carry - the tiles stay where they are and keep
        // the plan compact, so the tile order and the queue order remain the same thing.
        public void MovePlanEntry(BlueprintsPlanListItem item, int delta)
        {
            int i = TilesList.IndexOf(item.Tile);
            int j = i + delta;
            if (i < 1 || j < 1 || j >= TilesList.Count || !TilesList[j].HasBuilding)
            {
                GameAudio.NegativeClick(); // tile 0 is the outpost: the plan's foundation never moves
                return;
            }

            BlueprintsTile a = TilesList[i], b = TilesList[j];
            Building ab = a.Building, bb = b.Building;
            bool aUnlocked = a.Unlocked, bUnlocked = b.Unlocked;
            a.RemoveBuilding();
            b.RemoveBuilding();
            a.AddBuilding(bb, bUnlocked);
            b.AddBuilding(ab, aUnlocked);
            RefreshBuildableList();
            GameAudio.AcceptClick();
        }

        public void RemovePlanEntry(BlueprintsPlanListItem item)
        {
            if (item.Building == null || item.Building.IsCapitalOrOutpost)
            {
                GameAudio.NegativeClick();
                return;
            }

            item.Tile.RemoveBuilding();
            LoadBlueprintsTemplate(CreateBlueprintsTemplate()); // re-packs the plan, as the grid did
            RefreshBuildableList();
            GameAudio.AffirmativeClick();
        }

        void OnBuildableRowClicked(BlueprintsBuildableListItem item)
        {
            if (item?.Building == null)
                return;

            bool wasPinned = item == PinnedBuildable;
            ClearListPins();
            PinnedBuildable = wasPinned ? null : item;
            item.DescriptionPinned = !wasPinned;
        }

        void OnPlanRowClicked(BlueprintsPlanListItem item)
        {
            if (item?.Building == null)
                return;

            bool wasPinned = item == PinnedPlan;
            ClearListPins();
            PinnedPlan = wasPinned ? null : item;
            item.DescriptionPinned = !wasPinned;
        }

        // ⚠ both lists are cleared whichever was clicked: two pins would mean two descriptions
        // for one panel, and the panel can only hold one.
        void ClearListPins()
        {
            if (PinnedBuildable != null) PinnedBuildable.DescriptionPinned = false;
            if (PinnedPlan != null) PinnedPlan.DescriptionPinned = false;
            PinnedBuildable = null;
            PinnedPlan = null;
        }

        void RefreshChainList()
        {
            BlueprintsChainList.Reset();
            BuildChain(LinkedTo);

            void BuildChain(string linkTo)
            {
                if (linkTo?.NotEmpty() == true
                    && ResourceManager.TryGetBlueprints(linkTo, out BlueprintsTemplate linked))
                {
                    BlueprintsChainList.AddItem(new BlueprintsChainListItem(linked));
                    BuildChain(linked.LinkTo);
                }
            }
        }

        void AddOutpost()
        {
            Building outpost = ResourceManager.GetBuildingTemplate(Building.OutpostId);
            if (outpost != null)
            {
                TilesList[0].AddBuilding(outpost);
            }
            else
            {
                Log.Error($"Blueprints Screen - Outpost building template not found! " +
                    "Check that the correct building exists in the buildings directory");
            }
        }

        // Ludoal fork: the Design group's tabs.
        void OnDesignTabChanged(int tab)
        {
            if (tab == 2)
                return; // already here

            GameAudio.EchoAffirmative();
            ExitScreen();
            if (tab == 0)
                ScreenManager.AddScreen(new FleetDesignScreen(Universe, Universe.EmpireUI));
            else
                ScreenManager.AddScreen(new ShipDesignScreen(Universe, Universe.EmpireUI));
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // Ludoal fork (bench 345): dim the paused universe drawn behind this popup, the same
            // veil the table screens use, so the map recedes rather than competing with the screen.
            batch.SafeBegin();
            // bench 354 (maintainer): a TRULY opaque frame fill behind the content. GroupFrameFill is
            // only 0.92 alpha, so the dimmed universe still showed through and washed out the text -
            // Stats+' gray labels read terne/illegible where Colony's opaque facilities panel keeps
            // them crisp. Fill with the same colour at full alpha, local to Blueprints (the shared
            // GroupFrameFill stays 0.92 for the table screens that want the veil). All three tabs
            // (Stats / Stats+ / Description) gain the contrast.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(DesignTabs), new Color(14, 12, 9));
            // hovering a building raises the Description tab by itself; the cursor leaving falls
            // back to the player's own choice. bench 351: setting SelectedIndex DOES fire OnTabChange
            // (verified in Submenu.cs) - so the auto-switch was writing StatsTabPlayerChoice=1 and the
            // tab stayed stuck on Description forever. Detach the handler around the automatic write;
            // only a real click on the tab strip (its own HandleInput path) records a player choice.
            // bench 353: three tabs now - 0 STATS, 1 STATS+, 2 DESCRIPTION. Hovering a building raises
            // DESCRIPTION (2) on its own; the cursor leaving falls back to the player's own choice
            // (default STATS+). Detach OnTabChange around the automatic write so the auto-switch never
            // records itself as a player choice (setting SelectedIndex fires OnTabChange, Submenu.cs).
            int wantTab = DescriptionBuilding != null ? 2 : StatsTabPlayerChoice;
            if (PlanStats.SelectedIndex != wantTab)
            {
                var savedHandler = PlanStats.OnTabChange;
                PlanStats.OnTabChange = null;
                PlanStats.SelectedIndex = wantTab;
                PlanStats.OnTabChange = savedHandler;
            }
            base.Draw(batch, elapsed);
            if (PlanStats.SelectedIndex == 2)
                DrawDescriptionPane(batch);
            else if (PlanStats.SelectedIndex == 1)
                DrawStatsPlus(batch);
            else
                DrawPlanStatistics(batch);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            ScreenGroups.DrawDesignTabTip(DesignTabs, Input.CursorPosition);
            batch.SafeEnd();
        }

        void DrawDescriptionPane(SpriteBatch batch)
        {
            Building b = DescriptionBuilding;
            if (b != LastDescribed) // a new subject starts at its first line
            {
                DescriptionScroll = 0f;
                LastDescribed = b;
            }
            if (b == null)
                return;

            var detailPos = new Vector2(PlanStats.X + 15, PlanStats.Y + 35);
            var pane = new Rectangle((int)PlanStats.X, (int)PlanStats.Y + 30,
                                     (int)PlanStats.Width, (int)PlanStats.Height - 35);

            batch.SafeEnd();
            RenderStates.EnableScissorTest(batch.GraphicsDevice, pane);
            batch.SafeBegin(SpriteBlendMode.AlphaBlend, RenderStates.ScissorEnabled);
            float contentBottom = DrawBuildingDescription(batch, b, detailPos - new Vector2(0f, DescriptionScroll));
            batch.SafeEnd();
            RenderStates.DisableScissorTest(batch.GraphicsDevice);
            batch.SafeBegin();

            // a full line of air after the content, so the last line never sits at the razor's
            // edge of the scissor at max scroll
            float contentHeight = contentBottom + DescriptionScroll - detailPos.Y + TextFont.LineSpacing;
            MaxDescriptionScroll = (contentHeight - (pane.Height - 10)).LowerBound(0f);
            if (DescriptionScroll > MaxDescriptionScroll)
                DescriptionScroll = MaxDescriptionScroll;

            if (MaxDescriptionScroll > 0f)
                ColonyScreen.DrawScrollElevator(batch, pane, DescriptionScroll, MaxDescriptionScroll, contentHeight);
        }

        // returns the bottom of what it drew, which is what the elevator measures
        float DrawBuildingDescription(SpriteBatch batch, Building b, Vector2 origin)
        {
            Vector2 bCursor = origin;
            Color color = Color.Wheat;
            batch.DrawString(Font20, b.TranslatedName, bCursor, color);
            bCursor.Y += Font20.LineSpacing + 5;
            // ⚠ the Outpost gets its description BACK (maintainer, bench 535). It was cut at
            // bench 354 because its stat blocks - repair, sensor, storage, defense, infra, all
            // at once - pushed past the bottom of the panel. The panel scrolls now, so nothing
            // is pushed off anything: the reason for the cut is gone.
            string selectionText = TextFont.ParseText(b.DescriptionText.Text, PlanStats.Width - 40);
            batch.DrawString(TextFont, selectionText, bCursor, Color.White);
            bCursor.Y += TextFont.MeasureString(selectionText).Y + Font20.LineSpacing;
            ColonyScreen.DrawBuildingStaticInfo(ref bCursor, batch, TextFont, Player, PlannedFertility,
                InitRichness, Player.data.Traits.PreferredEnv, b);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, TextFont, b.ActualShipRepair(PlanetLevel),
                "NewUI/icon_queue_rushconstruction", GameText.ShipRepair);
            ColonyScreen.DrawBuildingWeaponStats(ref bCursor, batch, TextFont, b, PlanetLevel);
            return bCursor.Y;
        }

        void DrawPlanStatistics(SpriteBatch batch)
        {
            Vector2 bCursor = new Vector2(PlanStats.X + 15, PlanStats.Y + 35);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedGrossMoney,
                "UI/icon_money_22", GameText.GrossIncome);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, -PlannedMaintenance,
                "UI/icon_money_22", GameText.Expenditure2);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedNetIncome,
                "UI/icon_money_22", GameText.NetIncome);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedFoodPerCol,
                "NewUI/icon_food", GameText.NetFoodPerColonistAllocated);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedFlatFood,
                "NewUI/icon_food", GameText.NetFlatFoodGeneratedPer);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedProdPerCol,
                "NewUI/icon_production", GameText.NetProductionPerColonistAllocated);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedFlatProd,
                "NewUI/icon_production", GameText.NetFlatProductionGeneratedPer);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedFlatResearch,
                "NewUI/icon_science", GameText.NetFlatResearchGeneratedPer);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedResearchPerCol,
                "NewUI/icon_science", GameText.NetResearchPerColonistAllocated); 
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannnedInfrastructure,
                "NewUI/icon_queue_rushconstruction", GameText.MaximumProductionToQueuePer);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedStorage,
                "NewUI/icon_storage_production", GameText.Storage);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedShields,
                "NewUI/icon_storage_production", GameText.PlanetaryShieldStrengthAdded);
            ColonyScreen.DrawBuildingInfo(ref bCursor, batch, BigFont, PlannedRepairPerTurn,
                "NewUI/icon_storage_production", GameText.ShipRepair);
        }

        // bench 353 (maintainer): Stats+ on a PLAN. Same layout as the Colony Stats+ tab (shared
        // StatsPlusLayout, tuned to the mm for 1440px), but every figure is a SIMULATED Planned* value,
        // not a live colony's. Its point over the flat STATS tab is the BUDGET BREAKDOWN: where the net
        // comes from. ColonistIncome + BuildingIncome == PlannedGrossMoney by construction (RecalcPlan),
        // so the lines sum to the displayed net exactly. Saturation / starvation / defense are omitted -
        // a plan has no live population to have them.
        void DrawStatsPlus(SpriteBatch batch)
        {
            Vector2 bCursor = new Vector2(PlanStats.X + 15, PlanStats.Y + 35);
            float blockW = (PlanStats.Width - 40) * 0.5f;
            SPCols cols = StatsPlusLayout.SPSetColumns(TextFont, blockW);
            var left = bCursor;

            // ── BUDGET (BC / turn) — the two sources, then upkeep, summing to Net exactly ──
            StatsPlusLayout.SPHeader(ref left, batch, "BUDGET (BC / turn)");
            if (PlannedColonistIncome.NotZero())
                StatsPlusLayout.SPLine(ref left, batch, TextFont, cols, "Colonist income",
                    StatsPlusLayout.SPSigned(PlannedColonistIncome), StatsPlusLayout.SPTone(PlannedColonistIncome));
            if (PlannedBuildingIncome.NotZero())
                StatsPlusLayout.SPLine(ref left, batch, TextFont, cols, "Building income",
                    StatsPlusLayout.SPSigned(PlannedBuildingIncome), StatsPlusLayout.SPTone(PlannedBuildingIncome));
            StatsPlusLayout.SPLine(ref left, batch, TextFont, cols, Localizer.Token(GameText.GrossIncome),
                StatsPlusLayout.SPSigned(PlannedGrossMoney), StatsPlusLayout.SPTone(PlannedGrossMoney));
            if (PlannedMaintenance.NotZero())
                StatsPlusLayout.SPLine(ref left, batch, TextFont, cols, "Building upkeep",
                    StatsPlusLayout.SPSigned(-PlannedMaintenance), StatsPlusLayout.SPTone(-PlannedMaintenance));
            StatsPlusLayout.SPLine(ref left, batch, TextFont, cols,
                Localizer.Token(PlannedNetIncome >= 0 ? GameText.NetIncome : GameText.NetLosses),
                StatsPlusLayout.SPSigned(PlannedNetIncome), PlannedNetIncome >= 0 ? Color.Green : Color.Red);
            StatsPlusLayout.SPGap(ref left, TextFont);

            // ── YIELDS (per turn) — the plan's flat + per-colonist figures, on the right block ──
            float usable = PlanStats.Width - 40;
            var right = new Vector2(bCursor.X + usable + 10 - (cols.YieldColTotal + TextFont.TextWidth(".00") + 2), bCursor.Y);
            // pop = gross per-colonist × pop (net + eaten re-added), flat, eaten = consumption × pop,
            // total = pop + flat - eaten = the net. Sums exactly, like Colony's yields grid (bench 354).
            StatsPlusLayout.SPHeader(ref right, batch, "YIELDS (per turn)");
            StatsPlusLayout.SPYieldHeader(ref right, batch, TextFont, cols);
            float foodEaten = PlannedFoodEatenPerCol * PlannedPopulation;
            float prodEaten = PlannedProdEatenPerCol * PlannedPopulation;
            StatsPlusLayout.SPYield(ref right, batch, TextFont, cols, Localizer.Token(GameText.Food),
                (PlannedFoodPerCol + PlannedFoodEatenPerCol) * PlannedPopulation, PlannedFlatFood,
                PlannedFoodPerCol * PlannedPopulation + PlannedFlatFood, foodEaten);
            StatsPlusLayout.SPYield(ref right, batch, TextFont, cols, Localizer.Token(GameText.Production),
                (PlannedProdPerCol + PlannedProdEatenPerCol) * PlannedPopulation, PlannedFlatProd,
                PlannedProdPerCol * PlannedPopulation + PlannedFlatProd, prodEaten);
            StatsPlusLayout.SPYield(ref right, batch, TextFont, cols, Localizer.Token(GameText.Research),
                PlannedResearchPerCol * PlannedPopulation, PlannedFlatResearch,
                PlannedResearchPerCol * PlannedPopulation + PlannedFlatResearch, 0f);
        }


        void RecalculateGeneralStats()
        {
            float tax = InitTax;
            float taxInverted = 1 - tax;

            float taxRateMultiplier = 1f + Player.data.Traits.TaxMod;
            Building[] plannedBuildings = TilesList.FilterSelect(t => t.HasBuilding, t => t.Building);
            PlannedMaintenance = 0;
            PlannedNetIncome   = 0;
            PlannedFertility   = InitFertility;
            PlannedPopulation  = InitPopulationBillion + plannedBuildings.Sum(b => b.MaxPopIncrease)*0.001f;
            PlannedGrossMoney  = PlannedPopulation;
            // bench 353: split the two income sources so Stats+ can show the budget breakdown. The
            // colonist base is PlannedPopulation; buildings add Income + CreditsPerColonist*Pop below.
            // Both get the same *tax*taxRateMultiplier at the end, so their sum stays exactly Gross.
            PlannedColonistIncome = PlannedPopulation;
            PlannedBuildingIncome = 0;
            PlannedFlatFood        = 0;
            PlannedFoodPerCol      = 0;
            PlannedFlatProd        = 0;
            PlannedProdPerCol      = 0;
            PlannedFlatResearch    = 0;
            PlannedResearchPerCol  = 0;
            PlannnedInfrastructure = 1;
            PlannedRepairPerTurn   = 0;
            PlannedStorage         = 0;
            PlannedShields         = 0;
            CanBuildShips = false;
            CanBuildTroops = false;


            foreach (Building b in plannedBuildings)
            {
                PlannedGrossMoney += b.Income + b.CreditsPerColonist*PlannedPopulation;
                PlannedBuildingIncome += b.Income + b.CreditsPerColonist*PlannedPopulation;
                taxRateMultiplier += b.PlusTaxPercentage;
                PlannedMaintenance += b.Maintenance;
                PlannedFertility += b.MaxFertilityOnBuildFor(Player, Player.data.PreferredEnvPlanet);
                PlannedFlatFood += b.PlusFlatFoodAmount;
                PlannedFoodPerCol += b.PlusFoodPerColonist;
                PlannedFlatProd += b.PlusFlatProductionAmount + b.PlusProdPerRichness*InitRichness;
                PlannedProdPerCol += b.PlusProdPerColonist;
                PlannedFlatResearch += b.PlusFlatResearchAmount;
                PlannedResearchPerCol += b.PlusResearchPerColonist;
                PlannnedInfrastructure += b.Infrastructure;
                PlannedStorage += b.StorageAdded;
                PlannedShields += b.PlanetaryShieldStrengthAdded;
                PlannedRepairPerTurn += b.ActualShipRepair(PlanetLevel);
                CanBuildTroops |= b.AllowInfantry;
                CanBuildShips |= b.AllowShipBuilding || b.IsSpacePort;
                b.UpdateOffense(PlanetLevel, Player.Universe);
            }

            PlannedGrossMoney  = PlannedGrossMoney * tax * taxRateMultiplier;
            // same factor on each source: ColonistIncome + BuildingIncome == PlannedGrossMoney (bench 353)
            PlannedColonistIncome = PlannedColonistIncome * tax * taxRateMultiplier;
            PlannedBuildingIncome = PlannedBuildingIncome * tax * taxRateMultiplier;
            PlannedMaintenance *= Player.data.Traits.MaintMultiplier;
            PlannedNetIncome = PlannedGrossMoney - PlannedMaintenance;
            PlannedShields *= 1 + Player.data.ShieldPowerMod;
            PlannedFertility = PlannedFertility.LowerBound(0);
            PlanetLevel = Planet.GetLevel(PlannedPopulation);

            float foodConsumptionPerColonist = Player.NonCybernetic ? 1 + Player.data.Traits.ConsumptionModifier : 0;
            PlannedFoodEatenPerCol = foodConsumptionPerColonist; // bench 354: kept before it folds into the net
            PlannedFoodPerCol = ColonyResource.FoodYieldFormula(PlannedFertility, PlannedFoodPerCol) - foodConsumptionPerColonist;
            float productionTax = Player.IsCybernetic ? tax * 0.5f : tax;

            float ProdConsumptionPerColonist = Player.IsCybernetic ? 1 + Player.data.Traits.ConsumptionModifier : 0;
            PlannedProdEatenPerCol = ProdConsumptionPerColonist; // bench 354
            PlannedFlatProd *= (1 - productionTax);
            PlannedProdPerCol = ColonyResource.ProdYieldFormula(InitRichness, PlannedProdPerCol, Player)
                * (1 - productionTax) - ProdConsumptionPerColonist;

            float researchMultiplier = 1 + Player.data.Traits.ResearchMod;
            PlannedFlatResearch = PlannedFlatResearch.LowerBound(0) * researchMultiplier * taxInverted * Player.data.Traits.ResearchTaxMultiplier;
            PlannedResearchPerCol *= researchMultiplier * taxInverted * Player.data.Traits.ResearchTaxMultiplier;

            CannotBuildShipsWarning.Visible = !CanBuildShips && plannedBuildings.Length > 1;
            CannotBuildTroopsWarning.Visible = !CanBuildTroops && plannedBuildings.Length > 1;

            PlannedPopLbl.Text = $"({PlannedPopulation})";
            PlannedFertilityLbl.Text = $"({PlannedFertility})";
        }

        public override void Update(float elapsedTime)
        {
            SaveBlueprints.Enabled = TilesList.Count(t => t.HasBuilding) > 1;
            UpdateShipAndTroopBuioildWarnings();
            PlannedPopLbl.Visible = PlannedPopulation.NotEqual(InitPopulationBillion);
            PlannedFertilityLbl.Visible = PlannedFertility.NotEqual(InitFertility);
            PlannedPopLbl.Color = PlannedPopulation >= InitPopulationBillion ? Color.LightGreen : Color.Pink;
            PlannedFertilityLbl.Color = PlannedFertility >= InitFertility ? Color.LightGreen : Color.Pink;
            UnlinkBlueprints.Enabled = LinkedTo.NotEmpty();
            base.Update(elapsedTime);
        }

        void UpdateShipAndTroopBuioildWarnings()
        {
            if (CannotBuildShipsWarning.Visible)
                CannotBuildShipsWarning.Color = Exclusive ? Color.Red : Color.Yellow;

            if (CannotBuildTroopsWarning.Visible)
                CannotBuildTroopsWarning.Color = Exclusive ? Color.Red : Color.Yellow;

        }

        public override bool HandleInput(InputState input)
        {
            // bench 347 (Lek): clear the hover FIRST, so any early-return below (the live top bar
            // especially) can't leave HoveredBuilding stuck on its last value - which kept the
            // Description tab raised and frozen. Recomputed further down when the cursor is over a row.
            HoveredBuilding = null;

            if (input.OpenScreenSaveMenu && SaveBlueprints.Enabled)
            {
                OnSaveBlueprintsClick();
                return true;
            }

            // Ludoal fork: live top bar
            if (!BuildableList.IsDragging && Universe.EmpireUI.HandleInput(input, caller: this))
                return true;

            // Ludoal fork: close with the key that opens this screen (F) — it previously
            // only closed via ESC. Guarded against text entry (blueprint naming).
            if (input.BlueprintsSceen && !GlobalStats.TakingInput && !BuildableList.IsDragging)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                ReopenParentColony();
                return true;
            }

            PlanAreaHovered = BuildableList.IsDragging && SubPlanArea.HitTest(Input.CursorPosition);
            HoveredBuilding = GetHoveredBuildingFromBuildableList(input);
            // ⚠ the plan is a queue now, so what the pointer is over comes from its rows. The
            // ROW does the removing and consumes its own right-click (bench 347: an unconsumed
            // one falls through to the popup's generic close and shuts the screen) - all this
            // has to do is report the hover, because the close test below reads it as "the
            // pointer is busy".
            if (!BuildableList.IsDragging && PlanList.HitTest(input.CursorPosition))
            {
                foreach (BlueprintsPlanListItem e in PlanList.AllEntries)
                {
                    if (e.Building != null && e.HitTest(input.CursorPosition))
                    {
                        HoveredBuilding = e.Building;
                        break;
                    }
                }
            }

            // Ludoal fork: right-click and Esc close the screen like other full-screen panels —
            // only when not aimed at a building (tile removal keeps priority) and nothing is
            // dragged. Esc is intercepted BEFORE the base popup dismiss so the return fires.
            if ((input.RightMouseClick && HoveredBuilding == null || input.Escaped)
                && !BuildableList.IsDragging)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                ReopenParentColony();
                return true;
            }

            // the description pane keeps its own wheel; the two lists keep theirs, and the
            // pane only answers when it is the tab on show and it holds something (bench 535)
            if ((input.ScrollIn || input.ScrollOut) && PlanStats.SelectedIndex == 2
                && DescriptionBuilding != null
                && !BuildableList.HitTest(input.CursorPosition)
                && !PlanList.HitTest(input.CursorPosition))
            {
                if (input.ScrollIn) DescriptionScroll = (DescriptionScroll - 48f).LowerBound(0f);
                else                DescriptionScroll = (DescriptionScroll + 48f).UpperBound(MaxDescriptionScroll);
                return true;
            }

            return base.HandleInput(input);

        }

        // the user-close routes land here; the hosted seat survived the trip, so the
        // reopened colony wears its row again
        void ReopenParentColony()
        {
            if (ReturnToColony != null)
                Universe.ScreenManager.AddScreen(new ColonyScreen(Universe, ReturnToColony, Universe.EmpireUI));
        }

        BlueprintsTemplate CreateBlueprintsTemplate()
        {
            // ⚠ an Array, not a set: the plan carries an ORDER now, and this is where it is
            // written down. The tiles' own sequence is that order until the design screen lets
            // the player rearrange it (bench 531).
            var plannedBuildings = new Array<string>(
                TilesList.FilterSelect(t => t.HasBuilding && !t.Building.IsOutpost, t => t.Building.Name));
            return new BlueprintsTemplate(BlueprintsName.Text.Text, Exclusive, LinkedTo, plannedBuildings, SwitchColonyType.ActiveValue);
        }

        void OnSaveBlueprintsClick()
        {
            ScreenManager.AddScreen(new SaveLoadBlueprintsScreen(this, CreateBlueprintsTemplate()));
        }

        void OnLoadBlueprintsClick()
        {
            ScreenManager.AddScreen(new SaveLoadBlueprintsScreen(this));
        }

        void OnLinkBlueprintsClick()
        {
            ScreenManager.AddScreen(new LinkBlueprintsScreen(this, BlueprintsName.Text.Text));
        }

        void OnUnlinkBlueprintsClick()
        {
            SetLinkedTo("");
            RefreshChainList();
        }

        public void AfterBluprintsSave(BlueprintsTemplate template)
        {
            BlueprintsName.Text = template.Name;
            Player.Universe.RefreshEmpiresPlanetsBlueprints(template, delete: false);
            GovernorTab?.OnBlueprintsChanged(template);
            LinkBlueprints.Enabled = true;
            RefreshChainList();
        }

        public void AfterBluprintsDelete(BlueprintsTemplate template)
        {
            Player.Universe.RefreshEmpiresPlanetsBlueprints(template, delete: true);
            LinkBlueprints.Enabled = BlueprintsName.Text != template.Name;
            ResourceManager.BlueprintsTemplatesDict.Remove(template.Name);
        } 

        public void RemoveAllBlueprintsLinkTo(BlueprintsTemplate template)
        {
            Player.Universe.RefreshEmpiresPlanetsBlueprints(template, delete: false);
            if (LinkedTo == template.Name)
                    SetLinkedTo("");
        }

        public void OnBlueprintsLinked(BlueprintsTemplate linkedBlueprints)
        {
            SetLinkedTo(linkedBlueprints.Name);
            RefreshChainList();
        }

        public void LoadBlueprintsTemplate(BlueprintsTemplate template)
        {
            ClearPlannedBuildings();
            SetLinkedTo("");
            LinkBlueprints.Enabled = true;
            BlueprintsName.Text = template.Name;
            Exclusive = template.Exclusive;
            SwitchColonyType.ActiveValue = template.ColonyType;
            if (template.LinkTo!= null && ResourceManager.TryGetBlueprints(template.LinkTo, out _)) 
                SetLinkedTo(template.LinkTo);

            AddOutpost();
            foreach (string name in template.PlannedBuildings) 
            {
                var b = ResourceManager.GetBuildingTemplate(name);
                if (b != null) 
                    TryAddBuilding(b, Player.IsBuildingUnlocked(name));
            }

            RefreshChainList();
        }

        void ClearPlannedBuildings()
        {
            foreach (BlueprintsTile tile in TilesList)
                tile.RemoveBuilding();
        }

        void OnBuildableListDrag(BlueprintsBuildableListItem item, DragEvent evt, bool outside)
        {
            if (evt != DragEvent.End)
                return;

            if (outside && item != null) // TODO: somehow `item` can be null, not sure how it happens
            {
                if (PlanAreaHovered)
                {
                    OnBuildableItemDoubleClicked(item);
                    return;
                }
            }

            GameAudio.NegativeClick();
        }

        Building GetHoveredBuildingFromBuildableList(InputState input)
        {
            if (BuildableList.HitTest(input.CursorPosition))
            {
                foreach (BlueprintsBuildableListItem e in BuildableList.AllEntries)
                {
                    if (e.Hovered && e.Building != null)
                        return e.Building;
                }
            }

            return null; // default: use Plan Statistics
        }

        public static Color GetBlueprintsIconColor(Planet.ColonyType colonyType)
        {
            switch (colonyType)
            {
                case Planet.ColonyType.Research:     return Color.CornflowerBlue;
                case Planet.ColonyType.Industrial:   return Color.Orange;
                case Planet.ColonyType.Agricultural: return Color.Green;
                case Planet.ColonyType.Military:     return Color.Red;
                case Planet.ColonyType.Core:         return Color.White;
                default:                             return Color.Yellow;
            }
        }

        public override void ExitScreen()
        {
            TilesList.Clear();
            base.ExitScreen();
        }
    }
}
