using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.AI.Budget;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Ship_Game.SpriteSystem;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using System.Collections.Generic;

namespace Ship_Game
{
    public class GovernorDetailsComponent : UIElementContainer
    {
        private readonly GameScreen Screen;
        private readonly UniverseScreen Universe;
        private readonly SubTexture PortraitShine = ResourceManager.Texture("Portraits/portrait_shine");
        private Planet Planet;
        Empire Player => Planet.Universe.Player;
        private DrawableSprite PortraitSprite;
        private UIPanel Portrait;
        UIPanel BluePrintsIcon;
        private UILabel WorldType, WorldDescription;
        DropOptions<Planet.ColonyType> ColonyTypeList;
        private UICheckBox GovOrbitals, AutoTroops, GovNoScrap, Quarantine, ManualOrbitals, GovGround, SpecializedTradeHub, Prioritized;
        private UICheckBox AutoBudgetCheck;
        private FloatSlider Garrison;
        private FloatSlider ManualPlatforms;
        private FloatSlider ManualShipyards;
        private FloatSlider ManualStations;
        private Submenu Tabs;

        UIButton BuildCapital; // Visible when the original capital is lost and there is an option to build it on a sustitude planet
        UIButton LaunchAllTroops;
        UIButton LaunchSingleTroop;
        UIButton CallTroops;
        UIButton BuildPlatform;
        UIButton BuildStation;
        UIButton BuildShipyard;
        UIButton EditBlueprints, ClearBlueprints, CreateBlueprints, LoadBlueprints;
        private float ButtonUpdateTimer;   // updates buttons once per second
        UILabel PlatformsText;
        UILabel StationsText;
        UILabel ShipyardsText;
        UILabel NoGovernor;
        UILabel ColonyRank;
        UILabel BudgetSum;
        UILabel BudgetPercent;
        UILabel BudgetLimitReached;


        private readonly Graphics.Font Font14 = Fonts.Arial14Bold;
        private readonly Graphics.Font Font12 = Fonts.Arial12Bold;
        private Graphics.Font Font;
        private Graphics.Font FontBig;
        private bool PlanetAutoBudget;
        private bool LinkingPlanetShares; // guard: renormalizing the others re-fires their OnChange
        private readonly float[] PlanetShares = new float[3];
        private readonly bool[] PlanetShareLocked = new bool[3];
        private bool BudgetLimitWarningVisible;

        Rectangle CivBudgetRect;
        Rectangle GrdBudgetRect;
        Rectangle SpcBudgetRect;
        Rectangle CivBudgetIconRect;
        Rectangle GrdBudgetIconRect;
        Rectangle SpcBudgetIconRect;
        ProgressBar CivBudgetBar;
        ProgressBar GrdBudgetBar;
        ProgressBar SpcBudgetBar;
        // Ludoal fork: one Auto toggle + a monetary Governor Spending
        // slider, and the split as linked % sliders with padlocks beside the bars - the same
        // grammar as the empire Budget screen. Storage is unchanged: the three manual budgets
        // carry total*share; all zero = the governor allocates on its own.
        FloatSlider GovSpending;
        UILabel SpendingLabel;
        UILabel SpendValue;   // current spending, white
        UILabel SpendTarget;  // on Auto: the raw pre-smoothing target, grey, in parentheses
        FloatSlider ShareCiv, ShareGrd, ShareSpc;
        UILabel PctCiv, PctGrd, PctSpc; // own % labels, right-aligned so 100% cannot shove the padlock
        UIButton LockCiv, LockGrd, LockSpc;
        float SpendValueXWithTarget, SpendValueXAtEdge; // the white value's two seats, laid out once

        UILabel ColonyBlueprints, BlueprintsCompletionLbl, BlueprintsAchiveable, BlueprintsName,
            BlueprintsExclusive, BlueprintsLink, BlueprintsGovChange, Blueprintsoverview, BlueprintsEnableGov;
        ProgressBar BlueprintsCompletion;


        bool GovernorOn      => Planet.GovernorOn;
        bool GovernorOff     => Planet.GovernorOff;
        bool GovernorTabView => Tabs.SelectedIndex == 0;
        bool DefenseTabView  => Tabs.SelectedIndex == 1;
        bool BudgetTabView   => Tabs.SelectedIndex == 2;
        bool BlueprintsTabView => Tabs.SelectedIndex == 3;

        public int CurrentTabIndex => Tabs.SelectedIndex;

        public GovernorDetailsComponent(GameScreen screen, UniverseScreen universe,  Planet p, in RectF rect, int selectedIndex = 0) : base(rect)
        {
            Screen = screen;
            Universe = universe;
            SetPlanetDetails(p, rect, selectedIndex);
        }

        public void SetPlanetDetails(Planet p, in RectF rect, int selectedIndex = 0)
        {
            Log.Assert(p != null, "GovernorDetailsComponent Planet cannot be null");
            if (Planet == p || p == null)
                return;

            Planet = p;
            RemoveAll(); // delete all components

            // Full size at every width: the column is FIXED on the tab measure, identical at
            // 900p and 1080p - a width-based font fold piloted nothing.
            Font    = Font12;
            FontBig = Font14;

            // NOTE: Using RootContent here to avoid lag from resource unloading and reloading
            PortraitSprite = DrawableSprite.SubTex(ResourceManager.RootContent, $"Portraits/{Planet.Owner.data.PortraitName}");

            Portrait         = Add(new UIPanel(PortraitSprite));
            BluePrintsIcon   = Add(new UIPanel(ResourceManager.Texture("NewUI/blueprints")));
            WorldType        = Add(new UILabel(Planet.WorldType, Font14)); // full size at every width: the title does not fold
            // Ludoal fork: Font, not Font12 - GetParsedDescription wraps with Font; a wider
            // font here overruns the frame below 1920.
            WorldDescription = Add(new UILabel(Font));
            ColonyBlueprints = Add(new UILabel(GameText.ColonyBlueprintsTitle, FontBig, Color.Wheat));
            BlueprintsName   = Add(new UILabel("", FontBig, Color.Gold));
            BlueprintsCompletionLbl = Add(new UILabel(GameText.Completion, Font, Color.Wheat));
            BlueprintsAchiveable    = Add(new UILabel(GameText.Achievable, Font, Color.Gray));
            // White body text - the semantic colours (green/gold) stay.
            BlueprintsGovChange     = Add(new UILabel(GameText.GovernorChangedTo, Font, Color.White));
            BlueprintsExclusive     = Add(new UILabel("", Font, Color.LightGreen));
            BlueprintsLink          = Add(new UILabel("", Font, Color.White));
            Blueprintsoverview    = Add(new UILabel("", Font, Color.White));
            BlueprintsEnableGov     = Add(new UILabel("", Font, Color.Gold));

            // "Gov.": the full word ran past the Defense column.
            GovOrbitals    = Add(new UICheckBox(() => Planet.GovOrbitals, Font, title:"Gov. Manages Space Defense", tooltip:GameText.TheGovernorWillBuildStations));
            AutoTroops     = Add(new UICheckBox(() => Planet.AutoBuildTroops, Font, title:GameText.GovernorBuildsMilitia, tooltip:GameText.TheGovernorWillCreateA));
            GovNoScrap     = Add(new UICheckBox(() => Planet.DontScrapBuildings, Font, title:GameText.GovernorWillNotScrapBuildings, tooltip:GameText.NormallyGovernorsOperateWithinA));
            Quarantine     = Add(new UICheckBox(() => Planet.Quarantine, Font, title: GameText.QuarantinePlanet, tooltip: GameText.PreventGoodsTransportationInAnd));
            ManualOrbitals = Add(new UICheckBox(() => Planet.ManualOrbitals, Font, title: GameText.ManualOrbitalLimit, tooltip: GameText.OverrideGovernorDecisionsRegardingOrbital));
            GovGround      = Add(new UICheckBox(() => Planet.GovGroundDefense, Font, title: "Gov. Manages Ground Defense", tooltip: GameText.TheGovernorWillManageGround));
            AutoBudgetCheck = Add(new UICheckBox(() => PlanetAutoBudget, Font, title: "Auto",
                tooltip: GameText.GovernorAutoBudgetTooltip));
            Prioritized    = Add(new UICheckBox(() => Planet.PrioritizedPort, Font, title: GameText.PrioritizedPort, tooltip: GameText.PrioritizedPortTip));

            SpecializedTradeHub = Add(new UICheckBox(() => p.SpecializedTradeHub, Font, title: GameText.SpecializedTradeHub, tooltip: GameText.SpecializedTradeHubTip));
            SpecializedTradeHub.OnChange = cb => { Planet.SetSpecializedTradeHub(cb.Checked); };
            SpecializedTradeHub.TextColor = Quarantine.TextColor = Prioritized.TextColor = Color.Gray;
            Quarantine.CheckedTextColor = Color.Red;
            Prioritized.CheckedTextColor = Color.Purple;

            Garrison        = Slider(200, 200, 160, 40, GameText.GarrisonSize, 0, 25,Planet.GarrisonSize);
            ManualPlatforms = Slider(200, 200, 120, 40, GameText.ManualLimit, 0, 15, Planet.WantedPlatforms);
            ManualShipyards = Slider(200, 200, 120, 40, "", 0, 3, Planet.WantedShipyards);
            ManualStations  = Slider(200, 200, 120, 40, "", 0, 10, Planet.WantedStations);

            Garrison.Tip        = GameText.GarrisonSizeEnsuresANumber;
            ManualPlatforms.Tip = GameText.ManuallyAdjustTheNumberOf;
            ManualShipyards.Tip = GameText.ManuallyAdjustTheNumberOf2;
            ManualStations.Tip  = GameText.ManuallyAdjustTheNumberOf3;

            // The budget warning is added BEFORE the colony-type dropdown so the open dropdown
            // list draws OVER it, not the other way round.
            BudgetLimitReached = Add(new UILabel(GameText.BudgetLimitReached, FontBig, Color.Red));

            // Dropdowns will go on top of everything else
            ColonyTypeList = Add(new DropOptions<Planet.ColonyType>(100, 18));
            ColonyTypeList.AddOption(option:"--", Planet.ColonyType.Colony);
            ColonyTypeList.AddOption(option:GameText.Core, Planet.ColonyType.Core);
            ColonyTypeList.AddOption(option:GameText.Industrial, Planet.ColonyType.Industrial);
            ColonyTypeList.AddOption(option:GameText.Agricultural, Planet.ColonyType.Agricultural);
            ColonyTypeList.AddOption(option:GameText.Research, Planet.ColonyType.Research);
            ColonyTypeList.AddOption(option:GameText.Military, Planet.ColonyType.Military);
            // ColonyTypeList.AddOption(option:GameText.TradeHub, Planet.ColonyType.TradeHub); // retired (auto-supplies) - kept in case the role returns with another function
            ColonyTypeList.ActiveValue = Planet.CType;
            ColonyTypeList.OnValueChange = OnColonyTypeChanged;

            CreateBlueprints = Button(ButtonStyle.Medium, GameText.BlueprintsSnapshot, OnCreateBlueprintsClicked);
            EditBlueprints   = Button(ButtonStyle.Small, GameText.Edit, OnEditblueprintsClicked);
            ClearBlueprints  = Button(ButtonStyle.Small, GameText.Clear, OnClearBlueprintsClicked);
            LoadBlueprints   = Button(ButtonStyle.Small, GameText.Load, OnLoadBlueprintsClicked);
            CreateBlueprints.Tooltip = GameText.BlueprintsSnapshotTip;
            EditBlueprints.Tooltip   = GameText.EditBluprintsTip;
            ClearBlueprints.Tooltip  = GameText.ClearBluprintsTip;
            LoadBlueprints.Tooltip   = GameText.UploadBluprintsTip;

            ButtonUpdateTimer    = 1;
            BuildCapital         = Button(ButtonStyle.DefaultActive, GameText.ButtonBuildCapitalName, OnBuildCapitalClicked);
            BuildCapital.Tooltip = GameText.ButtonBuildCapitalTip;

            LaunchAllTroops   = Button(ButtonStyle.Default, GameText.LaunchAllTroops, OnLaunchTroopsClicked);
            LaunchSingleTroop = Button(ButtonStyle.Default, GameText.LaunchOneTroop, OnLaunchSingleTroopClicked);
            CallTroops        = Button(ButtonStyle.Default, GameText.CallTroops, OnSendTroopsClicked);

            LaunchAllTroops.Tooltip   = GameText.LaunchToSpaceAllTroops;
            LaunchSingleTroop.Tooltip = GameText.LaunchASingleRandomTroop;
            CallTroops.Tooltip        = GameText.RebaseASingleTroopFrom;

            BuildShipyard = Button(ButtonStyle.Medium, GameText.BuildShipyard, OnBuildShipyardClick);
            BuildStation  = Button(ButtonStyle.Medium, GameText.BuildStation, OnBuildStationClick);
            BuildPlatform = Button(ButtonStyle.Medium, GameText.BuildPlatform, OnBuildPlatformClick);

            BuildShipyard.Tooltip = GameText.BuildAShipyardOrbitingThis;
            BuildStation.Tooltip  = GameText.BuildAStationTheStrongest;
            BuildPlatform.Tooltip = GameText.BuildAPlatformTheStrongest;

            PlatformsText      = Add(new UILabel(" "));
            ShipyardsText      = Add(new UILabel(" "));
            StationsText       = Add(new UILabel(" "));
            NoGovernor         = Add(new UILabel(GameText.NoGovernor, Font, Color.Gray));
            ColonyRank         = Add(new UILabel(" ", Font, Color.LightGreen));

            CivBudgetRect     = new Rectangle((int)X + 57, (int)Y + 40, (int)(Width*0.33f), 20);
            GrdBudgetRect     = new Rectangle((int)X + 57, (int)Y + 70, (int)(Width*0.33f), 20);
            SpcBudgetRect     = new Rectangle((int)X + 57, (int)Y + 100, (int)(Width*0.33f), 20);
            CivBudgetIconRect = new Rectangle((int)X + 5, (int)Y + 38, 47, 23);
            GrdBudgetIconRect = new Rectangle((int)X + 5, (int)Y + 68, 47, 23);
            SpcBudgetIconRect = new Rectangle((int)X + 5, (int)Y + 96, 47, 23);

            CivBudgetBar = new ProgressBar(CivBudgetRect);
            GrdBudgetBar = new ProgressBar(GrdBudgetRect);
            SpcBudgetBar = new ProgressBar(SpcBudgetRect);

            Rectangle completionRect = new((int)X + 100, (int)Y + 70, (int)(Width * 0.5f), 30);
            BlueprintsCompletion = new ProgressBar(completionRect, 0, 0)
            { DrawPercentage = true, color = "green" };

            CivBudgetBar.Fraction10Values = true;
            GrdBudgetBar.Fraction10Values = true;
            SpcBudgetBar.Fraction10Values = true;
            CivBudgetBar.color = "green";
            SpcBudgetBar.color = "blue";

            // the spending range is seated per planet: twice its current envelope, floor 20
            PlanetShareLocked[0] = PlanetShareLocked[1] = PlanetShareLocked[2] = false;
            float manualTotal = Planet.ManualCivilianBudget + Planet.ManualGrdDefBudget + Planet.ManualSpcDefBudget;
            float seedTotal   = Math.Max(manualTotal, Planet.Budget.TotalAlloc);
            SpendingLabel = Add(new UILabel(GameText.SpendingLabel, Font, Color.White));
            GovSpending   = Add(new FloatSlider(SliderStyle.Decimal1, new Vector2(150, 12), "",
                                                0, (seedTotal * 2f).LowerBound(20f), seedTotal) { DrawValueText = false });
            GovSpending.Tip = GameText.GovernorBudgetTotalTooltip;
            GovSpending.OnChange = s => { if (!LinkingPlanetShares && !PlanetAutoBudget) CommitPlanetBudget(); };
            SpendValue  = Add(new UILabel(l => GovSpending.AbsoluteValue.String(1), Font));
            SpendValue.Color = Color.White;
            SpendTarget = Add(new UILabel(l => $"({Planet.Budget.TargetAlloc.String(1)})", Font));
            SpendTarget.Color = Color.Gray;

            ShareCiv = Add(MakeShareSlider(0));
            ShareGrd = Add(MakeShareSlider(1));
            ShareSpc = Add(MakeShareSlider(2));
            PctCiv = Add(MakeShareLabel(() => ShareCiv));
            PctGrd = Add(MakeShareLabel(() => ShareGrd));
            PctSpc = Add(MakeShareLabel(() => ShareSpc));
            LockCiv  = Add(MakeShareLock(0));
            LockGrd  = Add(MakeShareLock(1));
            LockSpc  = Add(MakeShareLock(2));

            BudgetSum     = Add(new UILabel(" ", FontBig, Color.White));
            BudgetPercent = Add(new UILabel(" ", FontBig, Color.White));


            Tabs = Add(new Submenu(rect, new LocalizedText[]
            {
                // "BP": BLUEPRINT in full cannot fit at the width the 900p centre column
                // allows - the short label carries a hover tooltip instead (see HandleInput).
                GameText.Governor, GameText.Defense2, GameText.Budget, "BP"
            }));

            if (selectedIndex < Tabs.NumTabs)
                Tabs.SelectedIndex = selectedIndex;

            base.PerformLayout();
        }

        Color BlueprintsColor => Planet.HasBlueprints ? BlueprintsScreen.GetBlueprintsIconColor(Planet.Blueprints.ColonyType) : Color.White;

        public override void PerformLayout()
        {
            float aspect  = PortraitSprite.Size.X / PortraitSprite.Size.Y;
            // 0.55: the toggles ride under the portrait.
            float height  = (float)Math.Round(Height * 0.55f);
            Portrait.Size = new Vector2((float)Math.Round(aspect*height), height);
            // Ludoal fork: all four tabs lay their content out from this one value, so they
            // cannot disagree. It follows the tab bar's real bottom, which matters because
            // Submenu wraps its tabs onto a second row once they no longer fit the width - a
            // fixed offset would put every tab's content under the tabs themselves. The 30f is
            // the single-row spacing, used while Tabs is not built yet.
            float contentTop = Tabs != null ? Tabs.ClientArea.Y - Y + 4 : 30f;
            float shift = contentTop - 30f; // what the extra tab rows add
            Portrait.Pos  = new Vector2(X + 10, Y + contentTop);
            BluePrintsIcon.Size = new Vector2(40, 40);
            BluePrintsIcon.Pos  = Portrait.Pos;
            BluePrintsIcon.Color = BlueprintsName.Color = BlueprintsColor;

            // Ludoal fork: the right-hand column follows the portrait, whose width is a fraction
            // of the panel height. It keeps a floor (see ColumnX), and the description wraps on
            // that same value. The type picker rides the TITLE line, to its right - the
            // description gains the row it occupied and no longer runs over the toggles.
            WorldType.Pos           = new Vector2(ColumnX, Portrait.Y);
            // Ludoal fork: the colony-type picker sits 20px further right.
            ColonyTypeList.Pos      = new Vector2(Math.Max(WorldType.Right + 12, ColumnX + 130) + 20, Portrait.Y);
            WorldDescription.Pos    = new Vector2(ColumnX, Portrait.Y + 21);
            WorldDescription.Text   = GetParsedDescription();
            ColonyBlueprints.Pos    = new Vector2(X + 10, Y + 40 + shift);
            ColonyBlueprints.Text   = ColonyBlueprints.Text.Text + ":";
            BlueprintsName.Pos      = new Vector2(X + 15 + FontBig.MeasureString(ColonyBlueprints.Text).X, Y + 40 + shift);
            BlueprintsGovChange.Pos = new Vector2(X + 10, Y + 100 + shift);
            BlueprintsExclusive.Pos = new Vector2(X + 10, Y + 130 + shift);
            BlueprintsLink.Pos      = new Vector2(X + 10, Y + 160 + shift);
            Blueprintsoverview.Pos  = ColonyBlueprints.Pos;
            Blueprintsoverview.Text = GetParsedBlueprintsOverview();
            BlueprintsEnableGov.Pos  = new Vector2(X + 10, Bottom - 60);
            BlueprintsEnableGov.Text = GameText.BluePrintsEnableGovernorToLoad;

            CreateBlueprints.Pos   = new Vector2(X+ 10,  Y+ Height - 38); // 8px up
            EditBlueprints.Pos     = new Vector2(X + Width - 240, CreateBlueprints.Y);
            ClearBlueprints.Pos    = new Vector2(X + Width - 160, CreateBlueprints.Y);
            LoadBlueprints.Pos     = new Vector2(X + Width - 80, CreateBlueprints.Y); 

            BlueprintsAchiveable.Pos        = new Vector2(X+110 + Width*0.5f, Y + 70 + shift);
            BlueprintsAchiveable.Tooltip    = GameText.AchievableTip;
            BlueprintsCompletionLbl.Pos     = new Vector2(X + 10, Y + 70 + shift);
            BlueprintsCompletionLbl.Tooltip = GameText.CompletionTip;

            // The warning's BOTTOM lines up with the portrait's bottom - a fixed anchor,
            // whatever the description length. A label draws from its top, so seat its top one
            // line-height above the portrait foot. X stays in the description column beside it.
            BudgetLimitReached.Pos = new Vector2(WorldDescription.X,
                Portrait.Pos.Y + Portrait.Size.Y - FontBig.LineSpacing);

            // Ludoal fork: seated here rather than in the constructor, which runs before Tabs
            // exists and so cannot know how many rows the tab bar takes.
            CivBudgetRect     = new Rectangle((int)X + 57, (int)(Y + 40 + shift), (int)(Width*0.33f), 20);
            GrdBudgetRect     = new Rectangle((int)X + 57, (int)(Y + 70 + shift), (int)(Width*0.33f), 20);
            SpcBudgetRect     = new Rectangle((int)X + 57, (int)(Y + 100 + shift), (int)(Width*0.33f), 20);
            CivBudgetIconRect = new Rectangle((int)X + 5, (int)(Y + 38 + shift), 47, 23);
            GrdBudgetIconRect = new Rectangle((int)X + 5, (int)(Y + 68 + shift), 47, 23);
            SpcBudgetIconRect = new Rectangle((int)X + 5, (int)(Y + 96 + shift), 47, 23);
            CivBudgetBar.SetRect(CivBudgetRect);
            GrdBudgetBar.SetRect(GrdBudgetRect);
            SpcBudgetBar.SetRect(SpcBudgetRect);
            BlueprintsCompletion.SetRect(new Rectangle((int)X + 100, (int)(Y + 70 + shift), (int)(Width * 0.5f), 30));

            // Ludoal fork: a checkbox is drawn CENTRED on its Y, so the margin comes from the
            // row's own height rather than a guessed constant. The column sits to the RIGHT of
            // the portrait, on the same left edge as the world title above it. Quarantine and
            // Prioritized sit UNDER the portrait; the two contextual toggles share their exact
            // lines at ColumnX.
            Quarantine.Pos          = new Vector2(X + 10, Portrait.Bottom + 14); // a step lower - there is room below
            Prioritized.Pos         = new Vector2(X + 10, Portrait.Bottom + 34);
            SpecializedTradeHub.Pos = new Vector2(ColumnX + 25, Quarantine.Pos.Y); // +25: clear of the left labels at 1080 fonts
            GovNoScrap.Pos          = new Vector2(ColumnX + 25, Prioritized.Pos.Y);
            BuildCapital.Pos        = new Vector2(ColonyTypeList.Right + 50, Quarantine.Pos.Y - 35);

            // Defense tab. These six buttons follow their own column, so the panel's height
            // can be its content's.
            AutoTroops.Pos        = new Vector2(TopLeft.X + 10, Y + 30 + shift);
            Garrison.Pos          = new Vector2(TopLeft.X + 20, Y + 50 + shift);
            float defRow          = Y + 100 + shift;  // a breath under the garrison slider
            // 34 per row, not 26: the buttons breathe vertically
            LaunchAllTroops.Pos   = new Vector2(TopLeft.X + 10, defRow);
            LaunchSingleTroop.Pos = new Vector2(TopLeft.X + 10, defRow + 34);
            CallTroops.Pos        = new Vector2(TopLeft.X + 10, defRow + 68);
            ColonyRank.Pos        = new Vector2(TopLeft.X + 200, Y + 30 + shift);
            NoGovernor.Pos        = ColonyRank.Pos;
            GovGround.Pos         = new Vector2(TopLeft.X + 200, Y + 50 + shift);
            GovOrbitals.Pos       = new Vector2(TopLeft.X + 200, Y + 70 + shift);
            ManualOrbitals.Pos    = new Vector2(TopLeft.X + 200, Y + 90 + shift);
            BuildPlatform.Pos     = new Vector2(TopLeft.X + 200, defRow);
            BuildShipyard.Pos     = new Vector2(TopLeft.X + 200, defRow + 34);
            BuildStation.Pos      = new Vector2(TopLeft.X + 200, defRow + 68);
            Vector2 manualOffset  = new Vector2(125, -15);
            ManualPlatforms.Pos   = BuildPlatform.Pos + manualOffset;
            ManualShipyards.Pos   = BuildShipyard.Pos + manualOffset;
            ManualStations.Pos    = BuildStation.Pos + manualOffset;

            // the share sliders ride their bars: from the bar's right edge to their own %
            // label (right-aligned, so 100% grows leftward), then the padlock at the margin
            // ⚠ lane widths must cover the WIDEST text: UILabel.SetText GROWS Size to fit,
            // and a grown Size shifts the right-align anchor - "100%" in a 34px lane drifted
            const int LockSide = 16, LockGap = 6, RightMargin = 10, PctW = 40, PctGap = 4;
            float lockX  = X + Width - RightMargin - LockSide;
            float pctX   = lockX - LockGap - PctW;
            float shareX = CivBudgetRect.X + CivBudgetRect.Width + 12;
            // +32: the slider track is Width-32; the unused value reserve folds back in
            var shareSize = new Vector2(pctX - PctGap - shareX + 32, 12);
            ShareCiv.Pos = new Vector2(shareX, CivBudgetRect.Y + 1); ShareCiv.Size = shareSize;
            ShareGrd.Pos = new Vector2(shareX, GrdBudgetRect.Y + 1); ShareGrd.Size = shareSize;
            ShareSpc.Pos = new Vector2(shareX, SpcBudgetRect.Y + 1); ShareSpc.Size = shareSize;
            var pctSize = new Vector2(PctW, Font.LineSpacing);
            PctCiv.Pos = new Vector2(pctX, CivBudgetRect.Y + 5); PctCiv.Size = pctSize;
            PctGrd.Pos = new Vector2(pctX, GrdBudgetRect.Y + 5); PctGrd.Size = pctSize;
            PctSpc.Pos = new Vector2(pctX, SpcBudgetRect.Y + 5); PctSpc.Size = pctSize;
            LockCiv.Rect = new Rectangle((int)lockX, CivBudgetRect.Y + 3, LockSide, LockSide);
            LockGrd.Rect = new Rectangle((int)lockX, GrdBudgetRect.Y + 3, LockSide, LockSide);
            LockSpc.Rect = new Rectangle((int)lockX, SpcBudgetRect.Y + 3, LockSide, LockSide);

            // the Auto/Spending row under the bars; the total drops one line below it.
            // Value lanes at the line's end: current spending (white), and on Auto the raw
            // target in parentheses (grey) - UpdateBudgets slides the value to the edge
            // when the target lane is hidden.
            // 20, not RightMargin: the Submenu frame is inset ~9px; RightMargin would let the
            // grey target kiss the painted edge.
            const int TargetW = 50, SpendValueW = 40;
            float spendRow = Y + 130 + shift;
            float rowRight = X + Width - 20;
            AutoBudgetCheck.Pos = new Vector2(TopLeft.X + 10, spendRow + 2);
            SpendingLabel.Pos   = new Vector2(TopLeft.X + 70, spendRow + 3);
            GovSpending.Pos     = new Vector2(TopLeft.X + 70 + Font.TextWidth(SpendingLabel.Text) + 8, spendRow - 6);
            GovSpending.Size    = new Vector2(rowRight - TargetW - SpendValueW - 6 - GovSpending.Pos.X + 32, 12);
            SpendValueXWithTarget = rowRight - TargetW - 4 - SpendValueW;
            SpendValueXAtEdge     = rowRight - SpendValueW;
            SpendValue.TextAlign = TextAlign.Right;
            SpendValue.Size      = new Vector2(SpendValueW, Font.LineSpacing);
            SpendValue.Pos       = new Vector2(SpendValueXWithTarget, spendRow + 3);
            SpendTarget.TextAlign = TextAlign.Right;
            SpendTarget.Size      = new Vector2(TargetW, Font.LineSpacing);
            SpendTarget.Pos       = new Vector2(rowRight - TargetW, spendRow + 3);

            BudgetSum.Pos         = new Vector2(TopLeft.X + 8, Y + 160 + shift);
            BudgetPercent.Pos     = new Vector2(TopLeft.X + CivBudgetRect.Width + 15, Y + 160 + shift);


            PlanetAutoBudget = !(Planet.ManualCivilianBudget.Greater(0) || Planet.ManualGrdDefBudget.Greater(0)
                                 || Planet.ManualSpcDefBudget.Greater(0));
            if (!PlanetAutoBudget)
            {
                // manual planet: the sliders re-read the stored budgets (total * share each)
                float mTotal = Planet.ManualCivilianBudget + Planet.ManualGrdDefBudget + Planet.ManualSpcDefBudget;
                PlanetShares[0] = Planet.ManualCivilianBudget / mTotal;
                PlanetShares[1] = Planet.ManualGrdDefBudget / mTotal;
                PlanetShares[2] = Planet.ManualSpcDefBudget / mTotal;
                LinkingPlanetShares = true;
                GovSpending.AbsoluteValue = mTotal;
                ShareCiv.RelativeValue = PlanetShares[0];
                ShareGrd.RelativeValue = PlanetShares[1];
                ShareSpc.RelativeValue = PlanetShares[2];
                LinkingPlanetShares = false;
            }
            SyncBudgetEnables();

            GovOrbitals.OnChange = cb =>
            {
                if (cb.Checked)
                {
                    UpdateOrbitalTextPos();
                    UpdateGovOrbitalStats();
                }
            };

            AutoBudgetCheck.OnChange = cb =>
            {
                if (cb.Checked)
                {
                    // Back to the governor's own allocation; the padlocks release too -
                    // a pin held across the mode switch would otherwise stick.
                    Planet.SetManualCivBudget(0);
                    Planet.SetManualGroundDefBudget(0);
                    Planet.SetManualSpaceDefBudget(0);
                    PlanetShareLocked[0] = PlanetShareLocked[1] = PlanetShareLocked[2] = false;
                    Planet.Budget.SnapToTarget(); // the EMA would crawl back from the manual values otherwise
                }
                else
                {
                    // take over at the current auto allocation
                    var b = Planet.Budget;
                    float total = b.TotalAlloc.LowerBound(0.1f);
                    PlanetShares[0] = b.CivilianAlloc.LowerBound(0f) / total;
                    PlanetShares[1] = b.GrdDefAlloc.LowerBound(0f) / total;
                    PlanetShares[2] = b.SpcDefAlloc.LowerBound(0f) / total;
                    LinkingPlanetShares = true;
                    GovSpending.AbsoluteValue = total;
                    ShareCiv.RelativeValue = PlanetShares[0];
                    ShareGrd.RelativeValue = PlanetShares[1];
                    ShareSpc.RelativeValue = PlanetShares[2];
                    LinkingPlanetShares = false;
                    CommitPlanetBudget();
                }
                SyncBudgetEnables();
            };

            Prioritized.OnChange = cb =>
            {
                Universe.RunOnSimThread(() =>
                {
                    Planet.SetPrioritizedPort(cb.Checked);
                });
            };

            UpdateButtons();
            UpdateGovOrbitalStats();
            UpdateBudgets();
            UpdateBlueprintsStats();
            UpdateBlueprintsChanged();
            base.PerformLayout(); // update all the sub-elements, like checkbox rects
        }

        // Ludoal fork: the right-hand column's left edge, in one place - both its position and
        // the width the description wraps on come from here, so they cannot disagree. The floor
        // keeps the column off the tab row when the portrait shrinks with the panel height.
        // It must not be read off WorldType.X: OnColonyTypeChanged re-wraps the description
        // outside of a layout pass, when that X is stale.
        float ColumnX => Math.Max(Portrait.Right + 10, X + 130);

        string GetParsedDescription()
        {
            float maxWidth = Right - 10 - ColumnX;
            return Font.ParseText(Planet.ColonyTypeInfoText, maxWidth);
        }

        string GetParsedBlueprintsOverview()
        {
            // Ludoal fork: Right is an absolute coordinate, not a width - wrapping on it gave
            // the text far more room than the frame has, so it ran to the right edge with no
            // margin at all. The text starts at X + 10, so the room it really has is the
            // frame's width less that indent and a matching margin on the right.
            float maxWidth = Width - 40;
            return Font.ParseText(Localizer.Token(GameText.BluePrintsOverView), maxWidth);
        }

        void OnColonyTypeChanged(Planet.ColonyType type)
        {
            Planet.CType = type;
            // auto-supplies: placing or changing a governor hands the three flows back to
            // Auto; the player can still uncheck each toggle after (the governor no longer forces)
            Planet.AutoFood = Planet.AutoProd = Planet.AutoColonists = true;
            WorldType.Text = Planet.WorldType;
            WorldDescription.Text = GetParsedDescription();
            if (type is Planet.ColonyType.Colony or Planet.ColonyType.TradeHub)
            {
                Planet.RemoveBlueprints();
                Planet.SetSpecializedTradeHub(false);
            }
        }

        public void OnBlueprintsChanged(BlueprintsTemplate template)
        {
            Planet.DontScrapBuildings = false;
            Planet.SetSpecializedTradeHub(false);
            Planet.AddBlueprints(template, Player);
            ColonyTypeList.ActiveValue = Planet.CType;
            OnColonyTypeChanged(Planet.CType);
            BlueprintsName.Text = template.Name;
            UpdateBlueprintsChanged();
        }

        void UpdateBlueprintsChanged()
        {
            BlueprintsName.Color = BluePrintsIcon.Color = BlueprintsColor;
            BlueprintsName.Text = Planet.HasBlueprints ? Planet.Blueprints.Name : "";
            BlueprintsGovChange.Text = Planet.HasBlueprints && Planet.Blueprints.ColonyType != Planet.ColonyType.Colony
                ? BlueprintsGovChange.Text = $"{Localizer.Token(GameText.GovernorChangedTo)} {Planet.Blueprints.ColonyType}"
                : BlueprintsGovChange.Text = "";


            BlueprintsExclusive.Text = Planet.HasBlueprints && Planet.Blueprints.Exclusive ? GameText.ExclusiveBlueprints : "";
            BlueprintsLink.Text = Planet.HasBlueprints ? $"{Localizer.Token(GameText.LinkedBlueprints)} {Planet.Blueprints.LinkedBlueprintsName}" : "";
        }

        public override void Update(float fixedDeltaTime)
        {
            if (Planet.Owner != null)
            {
                WorldDescription.Visible   = GovernorTabView && Planet.OwnerIsPlayer;
                ColonyTypeList.Visible     = GovernorTabView && Planet.OwnerIsPlayer;
                Portrait.Visible           = GovernorTabView;
                BluePrintsIcon.Visible     = Portrait.Visible && Planet.HasBlueprints;
                WorldType.Visible          = GovernorTabView;
                Quarantine.Visible         = GovernorTabView && Planet.OwnerIsPlayer;
                Prioritized.Visible        = Quarantine.Visible && Planet.HasSpacePort;
                BudgetLimitReached.Visible = ColonyTypeList.Visible && GovernorOn && Planet.CType != Planet.ColonyType.TradeHub && !Planet.SpecializedTradeHub && BudgetLimitWarningVisible;
                BudgetLimitReached.Color   = Screen.CurrentFlashColorRed;
                BuildCapital.Visible = true;
                BuildCapital.Visible       = GovernorTabView 
                                             && Planet.OwnerIsPlayer 
                                             && !Planet.Owner.GetPlanets().Any(p => p.IsHomeworld);
                SpecializedTradeHub.Visible = Quarantine.Visible && GovernorOn && Planet.CType != Planet.ColonyType.TradeHub && !Planet.HasBlueprints;
                SpecializedTradeHub.CheckedTextColor = Portrait.Border;

                // Not for trade hubs, which do not build structures anyway
                GovNoScrap.Visible = GovernorTabView 
                    && Planet.CType != Planet.ColonyType.TradeHub 
                    && GovernorOn 
                    && Planet.OwnerIsPlayer 
                    && !Planet.SpecializedTradeHub
                    && !Planet.HasBlueprints;

                int numTroopsCanLaunch    = DefenseTabView ? Planet.NumTroopsCanLaunchFor(Planet.Universe.Player) : 0;
                Planet.GarrisonSize       = (int)Math.Round(Garrison.AbsoluteValue);
                CallTroops.Visible        = DefenseTabView && Planet.OwnerIsPlayer;
                LaunchSingleTroop.Visible = CallTroops.Visible && numTroopsCanLaunch > 0;
                LaunchAllTroops.Visible   = CallTroops.Visible && numTroopsCanLaunch > 1;
                Garrison.Visible          = DefenseTabView && Planet.OwnerIsPlayer;
                AutoTroops.Visible        = Garrison.Visible;
                GovOrbitals.Visible       = Garrison.Visible && GovernorOn;
                GovGround.Visible         = GovOrbitals.Visible;
                BuildPlatform.Visible     = DefenseTabView && Planet.OwnerIsPlayer && (!Planet.GovOrbitals || GovernorOff);
                BuildShipyard.Visible     = BuildPlatform.Visible;
                BuildStation.Visible      = BuildPlatform.Visible;
                PlatformsText.Visible     = DefenseTabView;
                ShipyardsText.Visible     = DefenseTabView;
                StationsText.Visible      = DefenseTabView;
                NoGovernor.Visible        = DefenseTabView && GovernorOff;
                ManualOrbitals.Visible    = DefenseTabView && Planet.GovOrbitals && GovernorOn;
                ColonyRank.Visible        = DefenseTabView && GovernorOn;
                ManualPlatforms.Visible   = DefenseTabView && Planet.ManualOrbitals && Planet.GovOrbitals && GovernorOn;
                ManualShipyards.Visible   = ManualPlatforms.Visible;
                ManualStations.Visible    = ManualPlatforms.Visible;
                GovOrbitals.TextColor     = Planet.GovOrbitals        ? Color.White : Color.Gray;
                GovGround.TextColor       = Planet.GovGroundDefense   ? Color.White : Color.Gray;
                ManualOrbitals.TextColor  = Planet.ManualOrbitals     ? Color.White : Color.Gray;
                AutoTroops.TextColor      = Planet.AutoBuildTroops    ? Color.White : Color.Gray;
                GovNoScrap.TextColor      = Planet.DontScrapBuildings ? Color.White : Color.Gray;
                AutoBudgetCheck.TextColor = PlanetAutoBudget ? Color.White : Color.Gray;

                if (ManualOrbitals.Visible && Planet.ManualOrbitals)
                {
                    Planet.SetWantedPlatforms((byte)ManualPlatforms.AbsoluteValue);
                    Planet.SetWantedShipyards((byte)ManualShipyards.AbsoluteValue);
                    Planet.SetWantedStations((byte)ManualStations.AbsoluteValue);
                }
                else
                {
                    ManualPlatforms.AbsoluteValue = Planet.WantedPlatforms;
                    ManualShipyards.AbsoluteValue = Planet.WantedShipyards;
                    ManualStations.AbsoluteValue  = Planet.WantedStations;
                }

                BudgetSum.Visible       = BudgetTabView;
                BudgetPercent.Visible   = BudgetTabView && GovernorOn;
                AutoBudgetCheck.Visible = BudgetTabView && GovernorOn && Planet.OwnerIsPlayer && Planet.CType is not Planet.ColonyType.TradeHub && !Planet.SpecializedTradeHub;
                SpendingLabel.Visible   = AutoBudgetCheck.Visible;
                GovSpending.Visible     = AutoBudgetCheck.Visible;
                SpendValue.Visible      = AutoBudgetCheck.Visible;
                SpendTarget.Visible     = AutoBudgetCheck.Visible && PlanetAutoBudget;
                // the white value slides to the edge when the grey target lane is hidden
                SpendValue.Pos = new Vector2(SpendTarget.Visible ? SpendValueXWithTarget : SpendValueXAtEdge,
                                             SpendValue.Pos.Y);
                ShareCiv.Visible = ShareGrd.Visible = ShareSpc.Visible = AutoBudgetCheck.Visible;
                PctCiv.Visible   = PctGrd.Visible   = PctSpc.Visible   = AutoBudgetCheck.Visible;
                LockCiv.Visible  = LockGrd.Visible  = LockSpc.Visible  = AutoBudgetCheck.Visible;
                // On Auto the whole row is read-only, every padlock reads solid white;
                // manual keeps solid = locked, faint = free.
                LockCiv.IconTint = PlanetAutoBudget || PlanetShareLocked[0] ? Color.White : Color.White.Alpha(0.35f);
                LockGrd.IconTint = PlanetAutoBudget || PlanetShareLocked[1] ? Color.White : Color.White.Alpha(0.35f);
                LockSpc.IconTint = PlanetAutoBudget || PlanetShareLocked[2] ? Color.White : Color.White.Alpha(0.35f);


                CreateBlueprints.Visible = BlueprintsTabView && Planet.OwnerIsPlayer;
                LoadBlueprints.Visible   = CreateBlueprints.Visible && GovernorOn && Planet.CType != Planet.ColonyType.TradeHub;
                EditBlueprints.Visible   = CreateBlueprints.Visible && Planet.HasBlueprints;
                ClearBlueprints.Visible  = EditBlueprints.Visible;
                BlueprintsName.Visible   = EditBlueprints.Visible;
                ColonyBlueprints.Visible = LoadBlueprints.Visible && Planet.HasBlueprints;
                BlueprintsCompletionLbl.Visible = EditBlueprints.Visible;
                BlueprintsAchiveable.Visible    = EditBlueprints.Visible && Planet.Blueprints.PercentAchievable < 100;
                BlueprintsGovChange.Visible = EditBlueprints.Visible && BlueprintsGovChange.Text != "";
                BlueprintsExclusive.Visible = EditBlueprints.Visible && Planet.Blueprints.Exclusive;
                BlueprintsLink.Visible      = EditBlueprints.Visible && Planet.Blueprints.LinkedBlueprintsName != "";
                Blueprintsoverview.Visible  = CreateBlueprints.Visible && !Planet.HasBlueprints;
                BlueprintsEnableGov.Visible = Blueprintsoverview.Visible && GovernorOff;
            }

            UpdateButtonTimer(fixedDeltaTime);
            base.Update(fixedDeltaTime);
        }

        void UpdateButtonTimer(float elapsedTime)
        {
            ButtonUpdateTimer -= elapsedTime;
            if (ButtonUpdateTimer > 0f)
                return;

            ButtonUpdateTimer = 0.5f;
            UpdateButtons();
            UpdateGovOrbitalStats();
            UpdateBudgets();
            UpdateBlueprintsStats();
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            switch (Tabs.SelectedIndex)
            {
                case 0: DrawGovernorTab(batch);   break;
                case 1: DrawTroopsTab(batch);     break;
                case 2: DrawBudgetsTab(batch);    break;
                case 3: DrawBlueprintsTab(batch); break;
            }
        }

        void DrawGovernorTab(SpriteBatch batch)
        {
            // Governor portrait overlay stuff
            Portrait.Color = Planet.CType == Planet.ColonyType.Colony ? new Color(64, 64, 64) : Color.White;
            Color borderColor;
            switch (Planet.CType)
            {
                default:                             borderColor = Color.White; break;
                case Planet.ColonyType.TradeHub:     borderColor = Color.Yellow; break;
                case Planet.ColonyType.Colony:       borderColor = new Color(64, 64, 64); break;
                case Planet.ColonyType.Industrial:   borderColor = Color.Orange; break;
                case Planet.ColonyType.Agricultural: borderColor = Color.Green; break;
                case Planet.ColonyType.Research:     borderColor = Color.CornflowerBlue; break;
                case Planet.ColonyType.Military:     borderColor = Color.Red; break;
            }

            Portrait.Border = borderColor;
            batch.Draw(PortraitShine, Portrait.Rect);
        }

        void UpdateOrbitalTextPos()
        {
            if ((Planet.GovOrbitals || !Planet.OwnerIsPlayer) && GovernorOn)
            {
                PlatformsText.Pos = new Vector2(BuildPlatform.X, BuildPlatform.Y + 3);
                ShipyardsText.Pos = new Vector2(BuildShipyard.X, BuildShipyard.Y + 3);
                StationsText.Pos  = new Vector2(BuildStation.X, BuildStation.Y + 3);
            }
            else
            {
                PlatformsText.Pos = new Vector2(BuildPlatform.X + BuildPlatform.Width + 20, BuildPlatform.Y + 3);
                ShipyardsText.Pos = new Vector2(BuildShipyard.X + BuildShipyard.Width + 20, BuildShipyard.Y + 3);
                StationsText.Pos  = new Vector2(BuildStation.X + BuildStation.Width + 20, BuildStation.Y + 3);
            }
        }

        void DrawTroopsTab(SpriteBatch batch)
        {
            var lineColor = new Color(118, 102, 67, 255);
            Vector2 top   = new Vector2(X + 190, Y + 30);
            Vector2 bot   = new Vector2(X + 190, Bottom - 5);

            UpdateOrbitalTextPos();
            batch.DrawLine(top, bot, lineColor);
        }

        void DrawBudgetsTab(SpriteBatch batch)
        {
            // The bars draw with or without a governor - same presentation, the spent/allocation
            // reading holds either way; only the CONTROLS need a governor.
            if (Planet.CType is not Planet.ColonyType.TradeHub && !Planet.SpecializedTradeHub)
            {
                CivBudgetBar.Draw(batch);
                GrdBudgetBar.Draw(batch);
                SpcBudgetBar.Draw(batch);
            }

            batch.Draw(ResourceManager.Texture("NewUI/BudgetCiv"), CivBudgetIconRect);
            batch.Draw(ResourceManager.Texture("NewUI/BudgetGround"), GrdBudgetIconRect);
            batch.Draw(ResourceManager.Texture("NewUI/BudgetSpace"), SpcBudgetIconRect);
        }

        void DrawBlueprintsTab(SpriteBatch batch)
        {
            if (Planet.HasBlueprints) 
                BlueprintsCompletion.Draw(batch);
        }

        void OnSendTroopsClicked(UIButton b)
        {
            if (Planet.Universe.Player.GetTroopShipForRebase(out Ship troopShip, Planet.Position, Planet.Name))
            {
                GameAudio.EchoAffirmative();
                troopShip.AI.OrderRebase(Planet, true);
                UpdateButtons();
            }
            else
            {
                GameAudio.NegativeClick();
            }
        }

        void OnBuildCapitalClicked(UIButton b)
        {
            Planet.BuildCapitalHere();
        }

        void OnLoadBlueprintsClicked(UIButton b)
        {
            Screen.ScreenManager.AddScreen(new LoadBlueprintsToColonyScreen(Screen, this, Planet.Name));
        }

        void OnClearBlueprintsClicked(UIButton b)
        {
            Planet.RemoveBlueprints();
            BlueprintsName.Text = "";
        }

        void OnCreateBlueprintsClicked(UIButton b)
        {
            HashSet<string> potentialBuildings = Planet.TilesList.FilterSelect(t => t.BuildingOnTile 
                && t.Building.IsSuitableForBlueprints 
                && Player.IsBuildingUnlocked(t.Building.Name), t => t.Building.Name).ToHashSet();

            if (potentialBuildings.Count > 0)
            {
                BlueprintsTemplate template = new BlueprintsTemplate($"Snapshot of {Planet.Name}", false, "", potentialBuildings, Planet.CType);
                // The colony closes first - two groups must not stack; closing Blueprints
                // reopens it (the hosted seat survives the round trip).
                Screen.ExitScreen();
                Screen.ScreenManager.AddScreen(new BlueprintsScreen(Universe, Player, template,
                                                                    returnToColony: Planet));
            }
        }

        void OnEditblueprintsClicked(UIButton b)
        {
            if (Planet.HasBlueprints && ResourceManager.TryGetBlueprints(Planet.Blueprints.Name, out BlueprintsTemplate template))
            {
                // Same round trip as Snapshot.
                Screen.ExitScreen();
                Screen.ScreenManager.AddScreen(new BlueprintsScreen(Universe, Player, template, this,
                                                                    returnToColony: Planet));
            }
        }

        void OnLaunchTroopsClicked(UIButton b)
        {
            bool play = false;
            foreach (PlanetGridSquare pgs in Planet.TilesList)
            {
                if (pgs.TroopsAreOnTile && pgs.LockOnOurTroop(us:Player, out Troop troop) && troop.CanLaunch)
                {
                    play = true;
                    troop.Launch(pgs);
                }
            }

            if (play)
            {
                GameAudio.TroopTakeOff();
                UpdateButtons();
            }
            else
                GameAudio.NegativeClick();
        }

        void OnLaunchSingleTroopClicked(UIButton b)
        {
            foreach (Troop troop in Planet.Troops.GetLaunchableTroops(Planet.Universe.Player))
            {
                if (troop.Launch() != null)
                {
                    GameAudio.TroopTakeOff();
                    UpdateButtons();
                    break;
                }
            }

            GameAudio.NegativeClick();
        }

        void UpdateButtons()
        {
            if (Planet.Owner != Planet.Universe.Player)
                return;
            var ships = Planet.Owner.OwnedShips;

            // todo: double loop count. 
            int troopsLanding = ships
                .Filter(s => s != null && s.TroopCount > 0 && s.AI.State != AIState.Resupply && s.AI.State != AIState.Orbit)
                .Count(troopAI => troopAI.AI.OrderQueue.Any(goal => goal.TargetPlanet != null && goal.TargetPlanet == Planet));

            if (troopsLanding > 0)
            {
                CallTroops.Text = $"{Localizer.Token(GameText.IncomingTroops)} {troopsLanding}"; // "Incoming Troops
                CallTroops.Style = ButtonStyle.DefaultHostile;
            }
            else
            {
                CallTroops.Text = GameText.CallTroops; // "Call Troops"
                CallTroops.Style = ButtonStyle.Default;
            }

            UpdateButtonText(LaunchAllTroops, Planet.Troops.NumTroopsCanMoveFor(Planet.Owner), Localizer.Token(GameText.LaunchAllTroops));
        }

        void UpdateGovOrbitalStats()
        {
            if (Planet.Owner != Planet.Universe.Player
                && !Planet.Universe.Player.data.MoleList.Any(m => m.PlanetId == Planet.Id))
            {
                return;
            }

            int rank             = Planet.GetColonyRank();
            int currentPlatforms = Planet.NumPlatforms + Planet.OrbitalsBeingBuilt(RoleName.platform);
            int currentStations  = Planet.NumStations + Planet.OrbitalsBeingBuilt(RoleName.station);
            int currentShipyards = Planet.NumShipyards + Planet.ShipyardsBeingBuilt();
            ColonyRank.Text      = $"{Localizer.Token(GameText.GovernorColonyRank)} {rank}/15";

            if ((Planet.GovOrbitals || !Planet.OwnerIsPlayer) && GovernorOn)
            {
                PlatformsText.Text  = $"{Localizer.Token(GameText.Platforms)} {currentPlatforms}/{Planet.WantedPlatforms}";
                ShipyardsText.Text  = $"{Localizer.Token(GameText.Shipyards2)} {currentShipyards}/{Planet.WantedShipyards}";
                StationsText.Text   = $"{Localizer.Token(GameText.Stations)} {currentStations}/{Planet.WantedStations}";
                PlatformsText.Color = GetColor(currentPlatforms, Planet.WantedPlatforms);
                ShipyardsText.Color = GetColor(currentShipyards, Planet.WantedShipyards);
                StationsText.Color  = GetColor(currentStations, Planet.WantedStations);
            }
            else
            {
                PlatformsText.Text  = $"{currentPlatforms}";
                ShipyardsText.Text  = $"{currentShipyards}";
                StationsText.Text   = $"{currentStations}";
                PlatformsText.Color = ShipyardsText.Color = StationsText.Color = Color.White;
            }

            // local method
            Color GetColor(int num, int maxNum)
            {
                if (num == 0)      return Color.Gray;
                if (num < maxNum)  return Color.Yellow;
                if (num == maxNum) return Color.Green;

                return Color.OrangeRed;
            }
        }

        void UpdateBlueprintsStats()
        {
            if (!Planet.HasBlueprints || !Planet.OwnerIsPlayer)
                return;

            BlueprintsCompletion.Progress = Planet.Blueprints.PercentCompleted;
            BlueprintsAchiveable.Text = $"({Planet.Blueprints.PercentAchievable.String()}% {Localizer.Token(GameText.Achievable)})";
            if (Planet.HasBlueprints && Planet.Blueprints.Name != BlueprintsName.Text)
                UpdateBlueprintsChanged();
        }

        FloatSlider MakeShareSlider(int which)
        {
            var s = new FloatSlider(SliderStyle.Percent, new Vector2(150, 12), "", 0f, 1f, 0.34f)
            { DrawValueText = false }; // the row draws its own %, right-aligned before the padlock
            s.OnChange = _ => OnPlanetShareChanged(which);
            return s;
        }

        UILabel MakeShareLabel(Func<FloatSlider> slider)
        {
            var l = new UILabel(_ => ((int)Math.Round(slider().RelativeValue * 100f)) + "%", Font);
            l.Color = Color.White;
            l.TextAlign = TextAlign.Right;
            return l;
        }

        UIButton MakeShareLock(int which)
        {
            var b = new UIButton(new UIButton.StyleTextures("NewUI/icon_lock", "NewUI/icon_lock"), Vector2.Zero, "")
            {
                Tooltip = GameText.LockShareTooltip,
            };
            b.OnClick = _ =>
            {
                PlanetShareLocked[which] = !PlanetShareLocked[which];
                SyncBudgetEnables();
            };
            return b;
        }

        void SyncBudgetEnables()
        {
            bool manual = !PlanetAutoBudget;
            GovSpending.Enabled = manual;
            ShareCiv.Enabled = manual && !PlanetShareLocked[0];
            ShareGrd.Enabled = manual && !PlanetShareLocked[1];
            ShareSpc.Enabled = manual && !PlanetShareLocked[2];
            LockCiv.Enabled = LockGrd.Enabled = LockSpc.Enabled = manual;
        }

        void CommitPlanetBudget()
        {
            float total = GovSpending.AbsoluteValue;
            // the floor keeps the planet on manual: a stored zero would read as "auto"
            Planet.SetManualCivBudget((total * PlanetShares[0]).LowerBound(0.01f));
            Planet.SetManualGroundDefBudget((total * PlanetShares[1]).LowerBound(0.01f));
            Planet.SetManualSpaceDefBudget((total * PlanetShares[2]).LowerBound(0.01f));
        }

        void OnPlanetShareChanged(int which)
        {
            if (LinkingPlanetShares || PlanetAutoBudget)
                return;
            LinkingPlanetShares = true;
            float[] v = { ShareCiv.RelativeValue, ShareGrd.RelativeValue, ShareSpc.RelativeValue };
            // locked shares hold their value: the mover is clamped to what the locks leave,
            // and the remainder spreads over the UNLOCKED others by their mutual ratio
            float lockedSum = 0f;
            float unlockedSum = 0f;
            for (int i = 0; i < 3; i++)
            {
                if (i == which) continue;
                if (PlanetShareLocked[i]) lockedSum += v[i];
                else unlockedSum += v[i];
            }
            v[which] = v[which].Clamped(0f, (1f - lockedSum).LowerBound(0f));
            float rest = 1f - lockedSum - v[which];
            for (int i = 0; i < 3; i++)
            {
                if (i == which || PlanetShareLocked[i]) continue;
                v[i] = unlockedSum > 0.0001f ? rest * v[i] / unlockedSum : rest; // one unlocked: it takes it all
            }
            PlanetShares[0] = v[0];
            PlanetShares[1] = v[1];
            PlanetShares[2] = v[2];
            ShareCiv.RelativeValue = v[0];
            ShareGrd.RelativeValue = v[1];
            ShareSpc.RelativeValue = v[2];
            LinkingPlanetShares = false;
            CommitPlanetBudget();
        }

        void UpdateBudgets()
        {
            var budget = Planet.Budget;

            budget.UpdateManualUI();

            CivBudgetBar.Max      = budget.CivilianAlloc;
            CivBudgetBar.Progress = Planet.CivilianBuildingsMaintenance;
            GrdBudgetBar.Max      = budget.GrdDefAlloc;
            GrdBudgetBar.Progress = Planet.GroundDefMaintenance;
            SpcBudgetBar.Max      = budget.SpcDefAlloc;
            SpcBudgetBar.Progress = Planet.SpaceDefMaintenance;

            // on Auto the sliders mirror the governor's own allocation (read-only)
            if (PlanetAutoBudget && GovSpending != null)
            {
                float total = budget.TotalAlloc.LowerBound(0.01f);
                LinkingPlanetShares = true;
                GovSpending.AbsoluteValue = total;
                ShareCiv.RelativeValue = budget.CivilianAlloc.LowerBound(0f) / total;
                ShareGrd.RelativeValue = budget.GrdDefAlloc.LowerBound(0f) / total;
                ShareSpc.RelativeValue = budget.SpcDefAlloc.LowerBound(0f) / total;
                LinkingPlanetShares = false;
            }

            BudgetLimitWarningVisible = CivBudgetBar.Progress >= CivBudgetBar.Max && Planet.GetBuildingsCanBuild().Any(b => !b.IsMilitary);
            float spent = Planet.CivilianBuildingsMaintenance + Planet.GroundDefMaintenance + Planet.SpaceDefMaintenance;
            if (GovernorOn)
            {
                float percentSpent  = spent / budget.TotalAlloc.LowerBound(0.01f) * 100;
                BudgetSum.Text      = $"{Localizer.Token(GameText.Total3)} {spent.String(1)}" +
                                      $" {Localizer.Token(GameText.Of)} {budget.TotalAlloc.String(1)} BC/turn";
                BudgetPercent.Text  = $" ({percentSpent.String(1)}%)";
                BudgetPercent.Pos   = new Vector2(BudgetSum.Pos.X + FontBig.TextWidth(BudgetSum.Text) + 4, BudgetSum.Pos.Y); // follow the total text (BC/turn is wider than the old label)
                BudgetPercent.Color = GetColor();
            }
            else
            {
                BudgetSum.Text            = $"{Localizer.Token(GameText.Total3)} {spent.String(2)} BC/turn";
                BudgetPercent.Text        = "";
            }

            // Local Method
            Color GetColor()
            {
                if (GovernorOff)
                    return Color.White;

                if (spent.AlmostZero()) return Color.Gray;
                if (spent < 25)         return Color.Green;
                if (spent < 50)         return Color.GreenYellow;
                if (spent < 75)         return Color.Yellow;
                if (spent < 100)        return Color.Orange;

                return Color.OrangeRed;
            }
        }

        void UpdateButtonText(UIButton button, int value, string defaultText)
        {
            button.Text = value > 0 ? $"{defaultText} ({value})" : defaultText;
        }

        void OnBuildPlatformClick(UIButton b)
        {
            if (BuildOrbital(Planet.Owner.BestPlatformWeCanBuild))
                GameAudio.AffirmativeClick();
            else
                GameAudio.NegativeClick();
        }

        void OnBuildStationClick(UIButton b)
        {
            if (BuildOrbital(Planet.Owner.BestStationWeCanBuild))
                GameAudio.AffirmativeClick();
            else
                GameAudio.NegativeClick();
        }

        void OnBuildShipyardClick(UIButton b)
        {
            IShipDesign shipyard = ResourceManager.Ships.GetDesign(Planet.Owner.data.DefaultShipyard);

            if (Planet.Owner.CanBuildShipyards && BuildOrbital(shipyard))
                GameAudio.AffirmativeClick();
            else
                GameAudio.NegativeClick();
        }

        bool BuildOrbital(IShipDesign orbital)
        {
            if (orbital == null || Planet.IsOutOfOrbitalsLimit(orbital))
                return false;

            Planet.AddOrbital(orbital);
            return true;
        }

        public override bool HandleInput(InputState input)
        {
            // The folded BP tab says its full name on hover.
            if (Tabs != null && Tabs.Tabs.Count > 3 && Tabs.Tabs[3].Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip("Blueprint");

            if (GovOrbitals.HitTest(input.CursorPosition))
                UpdateGovOrbitalStats();

            if (ColonyRank.Visible && ColonyRank.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.TheRankOfTheColony);

            if (BudgetTabView)
            {
                if      (CivBudgetIconRect.HitTest(input.CursorPosition)) ToolTip.CreateTooltip(GovernorOn ? GameText.CivilianBuildingsExpenditurebudgetInByc : GameText.CivilianBuildingsExpenditureInByc);
                else if (GrdBudgetIconRect.HitTest(input.CursorPosition)) ToolTip.CreateTooltip(GovernorOn ? GameText.GroundDefenseBuildingsExpenditurebudgetIn : GameText.GroundDefenseBuildingsExpenditureIn);
                else if (SpcBudgetIconRect.HitTest(input.CursorPosition)) ToolTip.CreateTooltip(GovernorOn ? GameText.OrbitalsExpenditurebudgetInByc : GameText.OrbitalsExpenditureInByc);
            }

            return base.HandleInput(input);
        }
    }
}
