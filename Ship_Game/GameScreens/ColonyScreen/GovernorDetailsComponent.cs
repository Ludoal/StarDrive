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
        // Ludoal fork: what the governor may build, and what it may demolish - two
        // mirrored lists so a family it cannot build is not one it can tear down.
        DropOptions<Planet.BuildMandate> BuildMandateList, ScrapMandateList;
        UILabel BuildMandateLabel, ScrapMandateLabel, BlueprintModeLabel;
        UILabel GroundTroopsHeader, SpaceDefenseHeader; // Defense tab column headers
        bool BuildListWasOpen, ScrapListWasOpen, TypeListWasOpen; // raise a list once, on its opening
        float BudgetCommaX; // the decimal column the three budget figures align on
        private UICheckBox GovOrbitals, AutoTroops, Quarantine, ManualOrbitals, SpecializedTradeHub, Prioritized;
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
        UIButton EditBlueprints, LoadBlueprints;
        // Ludoal fork (maintainer feedback): the plan's mode. DERIVED, never stored - a colony
        // either has a plan of its own or it does not. Auto, which will defer to the empire's
        // plan for this governor type, waits for that table: an option that points at nothing
        // is an option that lies.
        public enum BlueprintMode { None, Custom }
        DropOptions<BlueprintMode> BlueprintModeList;
        bool ModeListWasOpen;
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
        // Ludoal fork (maintainer feedback): one line per area - an Auto toggle and a monetary
        // slider each. A single pot split by linked shares meant setting one area MOVED the
        // other two, which the padlocks then tried to compensate; three independent amounts
        // remove the coupling instead of managing it. Each slider runs 0 to max(2x its own
        // auto target, 20) - manual is mostly used to boost, so the room above target is the
        // point of it.
        FloatSlider CivBudgetSlider, GrdBudgetSlider, SpcBudgetSlider;
        UICheckBox AutoCiv, AutoGrd, AutoSpc;

        UILabel ColonyBlueprints, BlueprintsCompletionLbl, BlueprintsAchiveable, BlueprintsName,
            BlueprintsLink, BlueprintsGovChange, BlueprintsEnableGov;
        // Ludoal fork (maintainer feedback): the exclusive flag as a padlock rather than a
        // line of text - it arms demolitions, so it stays in the façade, but it costs a row
        // in a column that no longer has rows to spare.
        UIPanel BlueprintsExclusiveIcon;
        ProgressBar BlueprintsCompletion;


        bool GovernorOn      => Planet.GovernorOn;
        bool GovernorOff     => Planet.GovernorOff;
        bool GovernorTabView => Tabs.SelectedIndex == 0;
        bool BudgetTabView   => Tabs.SelectedIndex == 1;
        bool DefenseTabView  => Tabs.SelectedIndex == 2;

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
            // Ludoal fork (maintainer feedback): the row names its control instead of repeating
            // the governor's own name - the picker beside it already says which type it is.
            // A static label also settles the picker's X: a UILabel only ever grows, so a title
            // that changed with the type pushed the picker right and never brought it back.
            WorldType        = Add(new UILabel(GameText.GovernorTypeLabel, Font, Color.Wheat));
            // Ludoal fork: Font, not Font12 - GetParsedDescription wraps with Font; a wider
            // font here overruns the frame below 1920.
            WorldDescription = Add(new UILabel(Font));
            // bench 457 (maintainer): the inline description leaves the GOVERNOR tab
            // entirely - the Description sub-tab owns the portraits now, and the freed
            // space is reserved for future policy levers
            WorldDescription.Visible = false;
            // Ludoal fork (maintainer feedback): every row of this block names a control, so it
            // wears the standard font and the same label colour. No colon before a list or a bar -
            // the control that follows already says what the label is for.
            ColonyBlueprints = Add(new UILabel(GameText.BlueprintNameLabel, Font, Color.Wheat));
            BlueprintsName   = Add(new UILabel("", Font, Color.Gold));
            BlueprintsCompletionLbl = Add(new UILabel(GameText.CompletionNoColon, Font, Color.Wheat));
            BlueprintsAchiveable    = Add(new UILabel(GameText.Achievable, Font, Color.Gray));
            // White body text - the semantic colours (green/gold) stay.
            BlueprintsGovChange     = Add(new UILabel(GameText.GovernorChangedTo, Font, Color.White));
            BlueprintsExclusiveIcon = Add(new UIPanel(ResourceManager.Texture("NewUI/icon_lock")));
            BlueprintsExclusiveIcon.Tooltip = GameText.ExclusiveBlueprints;
            BlueprintsLink          = Add(new UILabel("", Font, Color.White));
            BlueprintsEnableGov     = Add(new UILabel("", Font, Color.Gold));

            // "Gov.": the full word ran past the Defense column.
            GovOrbitals    = Add(new UICheckBox(() => Planet.GovOrbitals, Font, title:"Gov. Manages Space Defense", tooltip:GameText.TheGovernorWillBuildStations));
            AutoTroops     = Add(new UICheckBox(() => Planet.AutoBuildTroops, Font, title:GameText.GovernorBuildsMilitia, tooltip:GameText.TheGovernorWillCreateA));
            Quarantine     = Add(new UICheckBox(() => Planet.Quarantine, Font, title: GameText.QuarantinePlanet, tooltip: GameText.PreventGoodsTransportationInAnd));
            ManualOrbitals = Add(new UICheckBox(() => Planet.ManualOrbitals, Font, title: GameText.ManualOrbitalLimit, tooltip: GameText.OverrideGovernorDecisionsRegardingOrbital));
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

            GroundTroopsHeader = Add(new UILabel(GameText.GroundTroopsHeader, Font, Color.Wheat));
            SpaceDefenseHeader = Add(new UILabel(GameText.SpaceDefenseHeader, Font, Color.Wheat));
            BuildMandateLabel = Add(new UILabel(GameText.BuildMandate, Font, Color.White) { Tooltip = GameText.BuildMandateTip });
            ScrapMandateLabel = Add(new UILabel(GameText.ScrapMandate, Font, Color.White) { Tooltip = GameText.ScrapMandateTip });
            // added AFTER their labels: children draw in the order they were added, and an
            // open list has to cover what sits below it
            // one factory for both ends: the colony's picker carries Auto, the empire's - on
            // Policies > Colony - does not. Two copies of a list of options is how the two ends
            // come to disagree about what a mandate means.
            BuildMandateList = Add(MandateDropdown.Make(Planet.GovBuildMandate,
                m => Universe.RunOnSimThread(() => Planet.SetBuildMandate(m)), withAuto: true));
            ScrapMandateList = Add(MandateDropdown.Make(Planet.GovScrapMandate,
                m => Universe.RunOnSimThread(() => Planet.SetScrapMandate(m)), withAuto: true));

            // Ludoal fork (maintainer feedback): the three blueprint gestures wear the icons the
            // construction list already uses for the same verbs - pencil to edit, plus to bring one
            // in, cross to drop it. Three labelled buttons no longer fit beside the portrait, and a
            // vocabulary the player has already learned costs nothing to read.
            EditBlueprints   = BpIconButton("NewUI/icon_build_edit", GameText.EditBluprintsTip, OnEditblueprintsClicked);
            LoadBlueprints   = BpIconButton("NewUI/icon_build_add", GameText.UploadBluprintsTip, OnLoadBlueprintsClicked);
            // None replaces the cross: clearing a plan is one of the two states of this list,
            // not a gesture of its own.
            BlueprintModeList = Add(new DropOptions<BlueprintMode>(100, 18));
            BlueprintModeList.AddOption(option: GameText.MandateNone, BlueprintMode.None);
            BlueprintModeList.AddOption(option: GameText.BlueprintModeCustom, BlueprintMode.Custom);
            BlueprintModeList.ActiveValue = Planet.HasBlueprints ? BlueprintMode.Custom : BlueprintMode.None;
            BlueprintModeList.OnValueChange = OnBlueprintModeChanged;
            BlueprintModeLabel = Add(new UILabel(GameText.BlueprintModeLabel, Font, Color.Wheat)
                { Tooltip = GameText.BlueprintModeTip });

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

            CivBudgetBar.Fraction10Values = CivBudgetBar.FixedDecimal = true;
            GrdBudgetBar.Fraction10Values = GrdBudgetBar.FixedDecimal = true;
            SpcBudgetBar.Fraction10Values = SpcBudgetBar.FixedDecimal = true;
            CivBudgetBar.color = "green";
            SpcBudgetBar.color = "blue";

            // One line per area: Auto toggle, then a monetary slider for the manual amount.
            CivBudgetSlider = Add(MakeBudgetSlider(BudgetArea.Civilian));
            GrdBudgetSlider = Add(MakeBudgetSlider(BudgetArea.GroundDef));
            SpcBudgetSlider = Add(MakeBudgetSlider(BudgetArea.SpaceDef));
            AutoCiv = Add(MakeAutoToggle(BudgetArea.Civilian));
            AutoGrd = Add(MakeAutoToggle(BudgetArea.GroundDef));
            AutoSpc = Add(MakeAutoToggle(BudgetArea.SpaceDef));

            BudgetSum     = Add(new UILabel(" ", FontBig, Color.White));
            BudgetPercent = Add(new UILabel(" ", FontBig, Color.White));


            Tabs = Add(new Submenu(rect, new LocalizedText[]
            {
                // Ludoal fork (maintainer feedback): the BP tab folded into GOVERNOR. A colony's
                // plan is part of how it is governed, and the right-hand column had the room -
                // the description label that used to sit there is hidden (bench 458).
                GameText.Governor, GameText.Budget, GameText.Defense2
            }));

            if (selectedIndex < Tabs.NumTabs)
                Tabs.SelectedIndex = selectedIndex;

            base.PerformLayout();
        }

        Color BlueprintsColor => Planet.HasBlueprints ? BlueprintsScreen.GetBlueprintsIconColor(Planet.Blueprints.ColonyType) : Color.White;

        public override void PerformLayout()
        {
            const int GovRowPitch = 20; // the Governor tab row step, written once
            // Defense tab button step, written once so the troop column and the orbital
            // column cannot drift apart (bench 506: the block sat 5px low and 34 was airy).
            const int DefButtonPitch = 29;
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
            // ── Blueprints, folded into this tab (maintainer feedback) ─────────────────────
            // They ride the right-hand column, under the world title row. Fixed steps, and every
            // element is placed FROM them - never from a share of the space that happens to be
            // left, which moves the whole block the day a row is added above it.
            const int BpLabelW = 92, BpBarW = 150, BpIconSize = 20, BpGestureStep = 26;
            float bpX    = ColumnX;
            const int BpRowStep = 22;
            float bpRow0 = Portrait.Y + 24;          // the plan's mode
            float bpRow1 = bpRow0 + BpRowStep;       // the name row: label, icon, name, padlock, gestures
            float bpRow2 = bpRow1 + BpRowStep;       // completion, before the link (maintainer's order)
            float bpRow3 = bpRow2 + BpRowStep;       // the blueprint this one links to
            float bpRow4 = bpRow3 + BpRowStep;       // the governor-change notice
            // one value column for the whole block: labels at bpX, what they name at bpValueX.
            // The category icon heads the NAME, since it describes the plan and not the row.
            float bpValueX = bpX + BpLabelW;
            BlueprintModeLabel.Pos  = new Vector2(bpX, bpRow0 + 2);
            BlueprintModeList.Pos   = new Vector2(bpValueX, bpRow0);
            ColonyBlueprints.Pos    = new Vector2(bpX, bpRow1);
            BluePrintsIcon.Size     = new Vector2(BpIconSize, BpIconSize);
            BluePrintsIcon.Pos      = new Vector2(bpValueX, bpRow1);
            BlueprintsName.Pos      = new Vector2(bpValueX + BpIconSize + 5, bpRow1);
            // ⚠ the padlock and the three gestures take FIXED columns off the right edge, never the
            // name's own edge: a UILabel measures its first text and only ever grows, so anchoring
            // to it drifts the moment a longer name is loaded.
            BlueprintsExclusiveIcon.Size = new Vector2(16, 16);
            BlueprintsExclusiveIcon.Pos  = new Vector2(X + Width - 34 - 2*BpGestureStep - 22, bpRow1 + 2);
            BlueprintsGovChange.Pos = new Vector2(bpX, bpRow4);
            BlueprintsLink.Pos      = new Vector2(bpX, bpRow3);
            BlueprintsEnableGov.Pos  = new Vector2(X + 10, Bottom - 60);
            BlueprintsEnableGov.Text = GameText.BluePrintsEnableGovernorToLoad;

            // the three gestures ride the name's row, right to left in fixed columns, vertically
            // centred on it - and none of them is anchored to the name's own width.
            EditBlueprints.Size  = LoadBlueprints.Size = new Vector2(20, 20);
            EditBlueprints.Pos   = new Vector2(X + Width - 34 - 2*BpGestureStep, bpRow1 + 1);
            LoadBlueprints.Pos   = new Vector2(X + Width - 34 - BpGestureStep,   bpRow1 + 1);

            BlueprintsCompletionLbl.Pos     = new Vector2(bpX, bpRow2 + 3);
            BlueprintsCompletionLbl.Tooltip = GameText.CompletionTip;
            BlueprintsAchiveable.Pos        = new Vector2(bpX + BpLabelW + BpBarW + 8, bpRow2 + 3);
            BlueprintsAchiveable.Tooltip    = GameText.AchievableTip;

            // The warning's BOTTOM lines up with the portrait's bottom - a fixed anchor,
            // whatever the description length. A label draws from its top, so seat its top one
            // line-height above the portrait foot. X stays in the description column beside it.
            // Ludoal fork (maintainer feedback): one row higher than the portrait foot - the
            // freed line below carries the Build Mandate dropdown.
            BudgetLimitReached.Pos = new Vector2(WorldDescription.X,
                Portrait.Pos.Y + Portrait.Size.Y - FontBig.LineSpacing - GovRowPitch);

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
            BlueprintsCompletion.SetRect(new Rectangle((int)(bpX + BpLabelW), (int)bpRow2, BpBarW, 18));

            // Ludoal fork: a checkbox is drawn CENTRED on its Y, so the margin comes from the
            // row's own height rather than a guessed constant. The column sits to the RIGHT of
            // the portrait, on the same left edge as the world title above it. Quarantine and
            // Prioritized sit UNDER the portrait; the two contextual toggles share their exact
            // lines at ColumnX.
            Quarantine.Pos          = new Vector2(X + 10, Portrait.Bottom + 14); // a step lower - there is room below
            Prioritized.Pos         = new Vector2(X + 10, Portrait.Bottom + 34);
            // one row higher: the Build Mandate dropdown takes the line it leaves behind
            SpecializedTradeHub.Pos = new Vector2(ColumnX + 25, Quarantine.Pos.Y - GovRowPitch); // +25: clear of the left labels at 1080 fonts

            // the two mandates take the line the trade hub left, label then list, one per row
            const int MandateLabelW = 118;
            float mandateX = ColumnX + 25;
            float mandateRow = Quarantine.Pos.Y;
            BuildMandateLabel.Pos = new Vector2(mandateX, mandateRow + 2);
            BuildMandateList.Pos  = new Vector2(mandateX + MandateLabelW, mandateRow);
            ScrapMandateLabel.Pos = new Vector2(mandateX, mandateRow + GovRowPitch + 2);
            ScrapMandateList.Pos  = new Vector2(mandateX + MandateLabelW, mandateRow + GovRowPitch);
            BuildCapital.Pos        = new Vector2(ColonyTypeList.Right + 50, Quarantine.Pos.Y - 35);

            // Defense tab. These six buttons follow their own column, so the panel's height
            // can be its content's.
            // Ludoal fork (maintainer feedback): each column says what it owns - troops on the
            // ground at left, everything in orbit at right. The headers take the first row and
            // the content steps down one.
            const int DefColLeft = 10, DefColRight = 200, DefRowPitch = 20;
            float defHeaderRow    = Y + 30 + shift;
            float defFirstRow     = defHeaderRow + DefRowPitch;
            // each header is centred on the column it names; the right edge is written once
            float defRight   = X + Width - 20;
            float leftMid    = TopLeft.X + DefColLeft + (DefColRight - DefColLeft) * 0.5f;
            float rightMid   = (TopLeft.X + DefColRight + defRight) * 0.5f;
            GroundTroopsHeader.Pos = new Vector2(leftMid - Font.TextWidth(GroundTroopsHeader.Text) * 0.5f, defHeaderRow);
            SpaceDefenseHeader.Pos = new Vector2(rightMid - Font.TextWidth(SpaceDefenseHeader.Text) * 0.5f, defHeaderRow);

            AutoTroops.Pos        = new Vector2(TopLeft.X + DefColLeft, defFirstRow);
            Garrison.Pos          = new Vector2(TopLeft.X + DefColLeft + 10, defFirstRow + DefRowPitch);
            float defRow          = Y + 115 + shift;  // a breath under the garrison slider
            LaunchAllTroops.Pos   = new Vector2(TopLeft.X + DefColLeft, defRow);
            LaunchSingleTroop.Pos = new Vector2(TopLeft.X + DefColLeft, defRow + DefButtonPitch);
            CallTroops.Pos        = new Vector2(TopLeft.X + DefColLeft, defRow + 2*DefButtonPitch);
            ColonyRank.Pos        = new Vector2(TopLeft.X + DefColRight, defFirstRow);
            NoGovernor.Pos        = ColonyRank.Pos;
            // the ground-defense checkbox left this column: the orbital pair closes the gap
            GovOrbitals.Pos       = new Vector2(TopLeft.X + DefColRight, defFirstRow + DefRowPitch);
            ManualOrbitals.Pos    = new Vector2(TopLeft.X + DefColRight, defFirstRow + 2*DefRowPitch);
            // the pair is button + its value 125px further: centre the pair, not the button
            Vector2 manualOffset  = new Vector2(125, -15);
            float pairWidth       = manualOffset.X + 24; // the value lane past the button
            float buildX          = rightMid - pairWidth * 0.5f;
            BuildPlatform.Pos     = new Vector2(buildX, defRow);
            BuildShipyard.Pos     = new Vector2(buildX, defRow + DefButtonPitch);
            BuildStation.Pos      = new Vector2(buildX, defRow + 2*DefButtonPitch);
            ManualPlatforms.Pos   = BuildPlatform.Pos + manualOffset;
            ManualShipyards.Pos   = BuildShipyard.Pos + manualOffset;
            ManualStations.Pos    = BuildStation.Pos + manualOffset;

            // One row per area, all three identical: bar | slider | amount | Auto.
            // The right edge is written ONCE and the lanes cascade back from it - the fixed
            // ones first, the slider absorbing what is left.
            // 4: the Submenu frame is inset ~9px; the maintainer bench took the margin down
            // to what the paint really needs, and the slider absorbs what the lanes give up.
            // The figures are drawn by hand, aligned on their decimal point like the Labor
            // sliders: a proportional font makes right-alignment land the point in a
            // different place for "8.4" and "14.8". The lane is the integer room left of
            // the comma plus the fraction right of it.
            const int AutoW = 52, LaneGap = 6;
            float unitsW  = Font.TextWidth("100");
            float fracW   = Font.TextWidth(".0");
            float budgetRight = X + Width - 4;
            float autoX       = budgetRight - AutoW;
            BudgetCommaX      = autoX - LaneGap - fracW;
            float amountX     = BudgetCommaX - unitsW;
            // back to +12: pulling the slider left made it overlap the bar it belongs to.
            // Its extra room comes from the value lane instead, which is measured now
            // rather than reserved at a guessed width.
            float sliderX     = CivBudgetRect.X + CivBudgetRect.Width + 12;
            // +32: the slider track is Width-32; the unused value reserve folds back in
            var sliderSize = new Vector2(amountX - 4 - sliderX + 32, 12);

            void SeatBudgetRow(FloatSlider slider, UICheckBox auto, in Rectangle barRect)
            {
                slider.Pos = new Vector2(sliderX, barRect.Y - 3); slider.Size = sliderSize;
                auto.Pos   = new Vector2(autoX,   barRect.Y + 4);
            }

            SeatBudgetRow(CivBudgetSlider, AutoCiv, CivBudgetRect);
            SeatBudgetRow(GrdBudgetSlider, AutoGrd, GrdBudgetRect);
            SeatBudgetRow(SpcBudgetSlider, AutoSpc, SpcBudgetRect);

            // the row under the bars: the all-areas shortcut on the left, the total at the end
            float spendRow = Y + 130 + shift;

            BudgetSum.Pos         = new Vector2(TopLeft.X + 8, Y + 160 + shift);
            BudgetPercent.Pos     = new Vector2(TopLeft.X + CivBudgetRect.Width + 15, Y + 160 + shift);


            // each row re-reads its own amount: a manual area shows what is stored, an
            // automatic one shows what the governor currently allocates it
            SeatBudgetSlider(CivBudgetSlider, BudgetArea.Civilian, Planet.ManualCivilianBudget);
            SeatBudgetSlider(GrdBudgetSlider, BudgetArea.GroundDef, Planet.ManualGrdDefBudget);
            SeatBudgetSlider(SpcBudgetSlider, BudgetArea.SpaceDef, Planet.ManualSpcDefBudget);
            SyncBudgetEnables();

            GovOrbitals.OnChange = cb =>
            {
                if (cb.Checked)
                {
                    UpdateOrbitalTextPos();
                    UpdateGovOrbitalStats();
                }
            };

            // Kept as a shortcut over the three per-area toggles: it flips all of them at once
            // rather than carrying a state of its own - a second source of truth beside the
            // rows would drift from them.
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
            // Policies phase 0 (Lek's portraits): the shared note rides once at the head -
            // budget-bound governance, manual Supply settings always win. Manual colonies
            // keep their own line without the note.
            if (Planet.CType == Planet.ColonyType.Colony)
                return Font.ParseText(Planet.ColonyTypeInfoText, maxWidth);
            return Font.ParseText(Planet.ColonyTypeInfoText.Text + "\n\n" + Localizer.Token(GameText.GovCommonNote), maxWidth);
        }

        // Policies phase 0: the hovered type of the governor dropdown, for the host's
        // Description tab
        public bool TryGetHoveredColonyType(out Planet.ColonyType type)
            => ColonyTypeList.TryGetHoveredEntry(out type);

        void OnColonyTypeChanged(Planet.ColonyType type)
        {
            Planet.CType = type;
            // auto-supplies: placing or changing a governor hands the three flows back to
            // Auto; the player can still uncheck each toggle after (the governor no longer forces)
            Planet.AutoFood = Planet.AutoProd = Planet.AutoColonists = true;
            WorldDescription.Text = GetParsedDescription();
            if (type is Planet.ColonyType.Colony or Planet.ColonyType.TradeHub)
            {
                Planet.RemoveBlueprints();
                Planet.SetSpecializedTradeHub(false);
            }
        }

        public void OnBlueprintsChanged(BlueprintsTemplate template)
        {
            // a blueprint is an explicit plan: it takes the mandates back to full rights
            Planet.SetBuildMandate(Planet.BuildMandate.All);
            Planet.SetScrapMandate(Planet.BuildMandate.All);
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


            BlueprintsLink.Text = Planet.HasBlueprints ? $"{Localizer.Token(GameText.LinkedBlueprints)} {Planet.Blueprints.LinkedBlueprintsName}" : "";
        }

        public override void Update(float fixedDeltaTime)
        {
            if (Planet.Owner != null)
            {
                // bench 458: the DESCRIPTION tab is the only home for governor prose -
                // this per-frame refresh was resurrecting the label the ctor hides
                WorldDescription.Visible   = false;
                ColonyTypeList.Visible     = GovernorTabView && Planet.OwnerIsPlayer;
                Portrait.Visible           = GovernorTabView;
                BluePrintsIcon.Visible     = Portrait.Visible && Planet.HasBlueprints;
                WorldType.Visible          = GovernorTabView;
                Quarantine.Visible         = GovernorTabView && Planet.OwnerIsPlayer;
                Prioritized.Visible        = Quarantine.Visible && Planet.HasSpacePort;
                BudgetLimitReached.Visible = ColonyTypeList.Visible && GovernorOn && Planet.CType != Planet.ColonyType.TradeHub && !Planet.SpecializedTradeHub && BudgetLimitWarningVisible;
                // Manual budget: overspending is the player's own choice, so caution it in yellow
                // rather than the red "limit reached" alarm the auto governor raises.
                bool manualBudget          = AnyAreaManual;
                BudgetLimitReached.Text    = manualBudget ? GameText.SpendingOverManualBudget : GameText.BudgetLimitReached;
                BudgetLimitReached.Color   = manualBudget ? Screen.ApplyCurrentAlphaToColor(Color.Yellow) : Screen.CurrentFlashColorRed;
                BuildCapital.Visible = true;
                BuildCapital.Visible       = GovernorTabView 
                                             && Planet.OwnerIsPlayer 
                                             && !Planet.Owner.GetPlanets().Any(p => p.IsHomeworld);
                // Ludoal fork (maintainer feedback): SUSPENDED, not erased. A blueprint is an
                // explicit plan that overrides both mandates and the hub, so neither may lie about
                // what the governor will do - but hiding them made them look lost, now that the
                // plan sits on this same tab. Same shape as the colony's Rush toggle under an
                // empire policy: greyed, still there, and saying why on hover.
                bool planRules             = Planet.HasBlueprints;
                SpecializedTradeHub.Visible = Quarantine.Visible && GovernorOn && Planet.CType != Planet.ColonyType.TradeHub;
                SpecializedTradeHub.Greyed  = planRules;
                // ⚠ UICheckBox.Tooltip is readonly - the box can only grey. The two mandate labels
                // beside it carry the reason on hover, which is where a player will look anyway.
                // blueprints are an explicit plan and override both mandates, so the lists
                // would lie about what the governor is going to do
                bool mandates = GovernorTabView && GovernorOn && Planet.OwnerIsPlayer;
                // an open list has to cover everything, and add-order only settles the
                // siblings added before it. Raise it ON THE OPENING, not every frame:
                // BringToFrontZOrder removes and re-inserts the child.
                if (ColonyTypeList.Open != TypeListWasOpen)
                {
                    TypeListWasOpen = ColonyTypeList.Open;
                    if (TypeListWasOpen) BringToFrontZOrder(ColonyTypeList);
                }
                if (BuildMandateList.Open != BuildListWasOpen)
                {
                    BuildListWasOpen = BuildMandateList.Open;
                    if (BuildListWasOpen) BringToFrontZOrder(BuildMandateList);
                }
                if (ScrapMandateList.Open != ScrapListWasOpen)
                {
                    ScrapListWasOpen = ScrapMandateList.Open;
                    if (ScrapListWasOpen) BringToFrontZOrder(ScrapMandateList);
                }
                BuildMandateList.Visible = ScrapMandateList.Visible = mandates;
                BuildMandateLabel.Visible = ScrapMandateLabel.Visible = mandates;
                // ⚠ a DropOptions has no greyed state of its own - Enabled stops the input and the
                // label carries the message, which is the whole reason the tooltip is on the label.
                BuildMandateList.Enabled = ScrapMandateList.Enabled = !planRules;
                BuildMandateLabel.Color  = ScrapMandateLabel.Color  = planRules ? Color.Gray : Color.White;
                BuildMandateLabel.Tooltip = planRules ? GameText.SuspendedByBlueprintsTip : GameText.BuildMandateTip;
                ScrapMandateLabel.Tooltip = planRules ? GameText.SuspendedByBlueprintsTip : GameText.ScrapMandateTip;
                SpecializedTradeHub.CheckedTextColor = Portrait.Border;

                int numTroopsCanLaunch    = DefenseTabView ? Planet.NumTroopsCanLaunchFor(Planet.Universe.Player) : 0;
                Planet.GarrisonSize       = (int)Math.Round(Garrison.AbsoluteValue);
                CallTroops.Visible        = DefenseTabView && Planet.OwnerIsPlayer;
                LaunchSingleTroop.Visible = CallTroops.Visible && numTroopsCanLaunch > 0;
                LaunchAllTroops.Visible   = CallTroops.Visible && numTroopsCanLaunch > 1;
                Garrison.Visible          = DefenseTabView && Planet.OwnerIsPlayer;
                AutoTroops.Visible        = Garrison.Visible;
                GovOrbitals.Visible       = Garrison.Visible && GovernorOn;
                BuildPlatform.Visible     = DefenseTabView && Planet.OwnerIsPlayer && (!Planet.GovOrbitals || GovernorOff);
                BuildShipyard.Visible     = BuildPlatform.Visible;
                BuildStation.Visible      = BuildPlatform.Visible;
                PlatformsText.Visible     = DefenseTabView;
                ShipyardsText.Visible     = DefenseTabView;
                StationsText.Visible      = DefenseTabView;
                NoGovernor.Visible        = DefenseTabView && GovernorOff;
                ManualOrbitals.Visible    = DefenseTabView && Planet.GovOrbitals && GovernorOn;
                ColonyRank.Visible        = DefenseTabView && GovernorOn;
                GroundTroopsHeader.Visible = SpaceDefenseHeader.Visible = DefenseTabView;
                ManualPlatforms.Visible   = DefenseTabView && Planet.ManualOrbitals && Planet.GovOrbitals && GovernorOn;
                ManualShipyards.Visible   = ManualPlatforms.Visible;
                ManualStations.Visible    = ManualPlatforms.Visible;
                GovOrbitals.TextColor     = Planet.GovOrbitals        ? Color.White : Color.Gray;
                ManualOrbitals.TextColor  = Planet.ManualOrbitals     ? Color.White : Color.Gray;
                AutoTroops.TextColor      = Planet.AutoBuildTroops    ? Color.White : Color.Gray;

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
                bool budgetRows = BudgetTabView && GovernorOn && Planet.OwnerIsPlayer && Planet.CType is not Planet.ColonyType.TradeHub && !Planet.SpecializedTradeHub;
                CivBudgetSlider.Visible = GrdBudgetSlider.Visible = SpcBudgetSlider.Visible = budgetRows;
                AutoCiv.Visible = AutoGrd.Visible = AutoSpc.Visible = budgetRows;
                // an automatic row reads grey: the number is the governor's, not the player's


                // folded into the Governor tab: the blueprint block shows with the portrait
                bool bpBlock             = GovernorTabView && Planet.OwnerIsPlayer;
                LoadBlueprints.Visible   = bpBlock && GovernorOn && Planet.CType != Planet.ColonyType.TradeHub;
                EditBlueprints.Visible   = bpBlock && Planet.HasBlueprints;
                BlueprintModeLabel.Visible = BlueprintModeList.Visible = LoadBlueprints.Visible;
                // the list mirrors the colony's real state; setting it only when it differs keeps
                // a per-frame write from fighting the player's own click.
                BlueprintMode mode = Planet.HasBlueprints ? BlueprintMode.Custom : BlueprintMode.None;
                if (BlueprintModeList.ActiveValue != mode)
                    BlueprintModeList.ActiveValue = mode;
                if (BlueprintModeList.Open != ModeListWasOpen)
                {
                    ModeListWasOpen = BlueprintModeList.Open;
                    if (ModeListWasOpen) BringToFrontZOrder(BlueprintModeList);
                }
                BlueprintsName.Visible   = EditBlueprints.Visible;
                ColonyBlueprints.Visible = LoadBlueprints.Visible && Planet.HasBlueprints;
                BlueprintsCompletionLbl.Visible = EditBlueprints.Visible;
                BlueprintsAchiveable.Visible    = EditBlueprints.Visible && Planet.Blueprints.PercentAchievable < 100;
                BlueprintsGovChange.Visible = EditBlueprints.Visible && BlueprintsGovChange.Text != "";
                BlueprintsExclusiveIcon.Visible = EditBlueprints.Visible && Planet.Blueprints.Exclusive;
                BlueprintsLink.Visible      = EditBlueprints.Visible && Planet.Blueprints.LinkedBlueprintsName != "";
                BlueprintsEnableGov.Visible = bpBlock && !Planet.HasBlueprints && GovernorOff;
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
                case 0: DrawGovernorTab(batch); DrawBlueprintsTab(batch); break;
                case 1: DrawBudgetsTab(batch);    break;
                case 2: DrawTroopsTab(batch);     break;
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

            // The allocations, aligned on their decimal point - the same treatment the Labor
            // sliders give their figures, and for the same reason: the font is proportional,
            // so right-aligning "8.4" and "14.8" puts their points in different places.
            // An automatic row reads grey: that number is the governor's, not the player's.
            if (CivBudgetSlider?.Visible == true)
            {
                PlanetBudget b = Planet.Budget;
                DrawBudgetValue(batch, b.CivilianAlloc, CivBudgetRect, Planet.ManualCivBudgetOn);
                DrawBudgetValue(batch, b.GrdDefAlloc,   GrdBudgetRect, Planet.ManualGrdBudgetOn);
                DrawBudgetValue(batch, b.SpcDefAlloc,   SpcBudgetRect, Planet.ManualSpcBudgetOn);
            }
        }

        void DrawBudgetValue(SpriteBatch batch, float value, in Rectangle barRect, bool manual)
        {
            ColonySlider.DrawAlignedNumber(batch, Font, value.StringFixed1(), BudgetCommaX,
                                           barRect.Y + 5, manual ? Color.White : Color.Gray);
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

        void OnBlueprintModeChanged(BlueprintMode mode)
        {
            // Custom is not a thing you pick - it is what loading or editing a plan makes true.
            // Only None acts, and only when there is something to clear.
            if (mode == BlueprintMode.None && Planet.HasBlueprints)
            {
                Planet.RemoveBlueprints();
                BlueprintsName.Text = "";
            }
        }

        // one blueprint gesture as an icon: normal, and the same texture's hover for both
        // hovered and pressed - the shape the repo uses everywhere else for these three.
        UIButton BpIconButton(string tex, LocalizedText tip, Action<UIButton> onClick)
        {
            var b = new UIButton(new UIButton.StyleTextures(tex, tex + "_hover1", tex + "_hover1"), Vector2.Zero, "")
            {
                Tooltip = tip,
                ClickSfx = "sd_ui_accept_alt3",
            };
            b.OnClick += onClick;
            return Add(b);
        }

        // Ludoal fork (maintainer feedback): Snapshot photographs what is BUILT, so it belongs
        // at the head of the frame that lists the built buildings rather than beside the portrait.
        // The colony screen owns the button; the gesture stays here, with the plan it makes.
        public void TakeBlueprintsSnapshot() => OnCreateBlueprintsClicked(null);

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

        enum BudgetArea { Civilian, GroundDef, SpaceDef }

        bool AnyAreaManual => Planet.ManualCivBudgetOn || Planet.ManualGrdBudgetOn || Planet.ManualSpcBudgetOn;

        float AutoTargetFor(BudgetArea area)
        {
            PlanetBudget b = Planet.Budget;
            if (b == null)
                return 0f;

            switch (area)
            {
                default:
                case BudgetArea.Civilian:  return b.CivilianAlloc;
                case BudgetArea.GroundDef: return b.GrdDefAlloc;
                case BudgetArea.SpaceDef:  return b.SpcDefAlloc;
            }
        }

        bool IsAreaManual(BudgetArea area)
        {
            switch (area)
            {
                default:
                case BudgetArea.Civilian:  return Planet.ManualCivBudgetOn;
                case BudgetArea.GroundDef: return Planet.ManualGrdBudgetOn;
                case BudgetArea.SpaceDef:  return Planet.ManualSpcBudgetOn;
            }
        }

        void SetAreaAmount(BudgetArea area, float amount)
        {
            switch (area)
            {
                case BudgetArea.Civilian:  Planet.SetManualCivBudget(amount);      break;
                case BudgetArea.GroundDef: Planet.SetManualGroundDefBudget(amount); break;
                case BudgetArea.SpaceDef:  Planet.SetManualSpaceDefBudget(amount);  break;
            }
        }

        void SetAreaManual(BudgetArea area, bool manual)
        {
            switch (area)
            {
                case BudgetArea.Civilian:  Planet.SetManualCivBudgetOn(manual);  break;
                case BudgetArea.GroundDef: Planet.SetManualGrdBudgetOn(manual);  break;
                case BudgetArea.SpaceDef:  Planet.SetManualSpcBudgetOn(manual);  break;
            }
        }

        // Manual is mostly used to BOOST an area, so the room above the auto target is the
        // point of the slider - not headroom to waste. Floor 20 so a small colony still has
        // usable travel (maintainer decision).
        FloatSlider MakeBudgetSlider(BudgetArea area)
        {
            float seed = AutoTargetFor(area);
            var s = new FloatSlider(SliderStyle.Decimal1, new Vector2(150, 12), "",
                                    0f, (seed * 2f).LowerBound(20f), seed) { DrawValueText = false };
            s.Tip = GameText.GovernorBudgetTotalTooltip;
            s.OnChange = sl => { if (IsAreaManual(area)) SetAreaAmount(area, sl.AbsoluteValue); };
            return s;
        }

        // Ticking Auto hands the area back to the governor; unticking takes it over at the
        // allocation it has right now, so the amount never jumps when the player takes control.
        UICheckBox MakeAutoToggle(BudgetArea area)
        {
            // A getter/setter Ref, not an expression binding: Ref picks a field or property
            // apart from the expression tree, and this state is computed - a negated method
            // call is a UnaryExpression and throws when the box is built.
            var binding = new Ref<bool>(() => !IsAreaManual(area), auto =>
            {
                bool manual = !auto;
                // taking over: start from what the governor allocates right now, so the amount
                // never jumps at the moment the player takes control
                SetAreaAmount(area, manual ? AutoTargetFor(area) : 0f);
                SetAreaManual(area, manual);
                if (!manual)
                    Planet.Budget?.SnapToTarget(); // the EMA would crawl back from the manual value

                SyncBudgetEnables();
            });

            return new UICheckBox(0, 0, binding, Font,
                                  title: "Auto", tooltip: GameText.OverrideThisBudgetAndSet);
        }

        // The range is seated at construction from the area's own auto target (FloatSlider owns
        // its bounds privately), so it follows the colony between two openings of the panel.
        void SeatBudgetSlider(FloatSlider slider, BudgetArea area, float storedAmount)
        {
            slider.AbsoluteValue = IsAreaManual(area) ? storedAmount : AutoTargetFor(area);
        }

        void SyncBudgetEnables()
        {
            CivBudgetSlider.Enabled = IsAreaManual(BudgetArea.Civilian);
            GrdBudgetSlider.Enabled = IsAreaManual(BudgetArea.GroundDef);
            SpcBudgetSlider.Enabled = IsAreaManual(BudgetArea.SpaceDef);
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

            // an automatic row mirrors the governor's own allocation, read-only; a manual one
            // holds the player's number and is left alone
            if (CivBudgetSlider != null)
            {
                if (!Planet.ManualCivBudgetOn) CivBudgetSlider.AbsoluteValue = budget.CivilianAlloc;
                if (!Planet.ManualGrdBudgetOn) GrdBudgetSlider.AbsoluteValue = budget.GrdDefAlloc;
                if (!Planet.ManualSpcBudgetOn) SpcBudgetSlider.AbsoluteValue = budget.SpcDefAlloc;
            }

            BudgetLimitWarningVisible = CivBudgetBar.Progress >= CivBudgetBar.Max && Planet.GetBuildingsCanBuild().Any(b => !b.IsMilitary);
            float spent = Planet.CivilianBuildingsMaintenance + Planet.GroundDefMaintenance + Planet.SpaceDefMaintenance;
            if (GovernorOn)
            {
                BudgetSum.Text      = $"{Localizer.Token(GameText.Total3)} {spent.String(1)}" +
                                      $" {Localizer.Token(GameText.Of)} {budget.TotalAlloc.String(1)} BC/turn";
                // A budget below 0.5 has no meaningful denominator - the old 0.01 floor turned a
                // near-zero alloc into absurd percentages (3.5 / 0.01 = 11666.7%). Below the floor
                // we draw no ratio at all: the total reads plainly, no parenthesis.
                if (budget.TotalAlloc >= 0.5f)
                {
                    float percentSpent  = spent / budget.TotalAlloc * 100;
                    BudgetPercent.Text  = $" ({percentSpent.String(1)}%)";
                    BudgetPercent.Pos   = new Vector2(BudgetSum.Pos.X + FontBig.TextWidth(BudgetSum.Text) + 4, BudgetSum.Pos.Y); // follow the total text (BC/turn is wider than the old label)
                    BudgetPercent.Color = GetColor();
                }
                else
                {
                    BudgetPercent.Text = "";
                }
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
