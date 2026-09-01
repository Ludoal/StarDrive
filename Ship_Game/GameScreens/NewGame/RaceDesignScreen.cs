using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Gameplay;
using Ship_Game.GameScreens.MainMenu;
using Ship_Game.GameScreens.NewGame;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;
using Ship_Game.Data;
using System.Linq;

namespace Ship_Game
{
    public partial class RaceDesignScreen : GameScreen
    {
        readonly MainMenuScreen MainMenu;
        readonly Array<TraitEntry> AllTraits = new();
        RacialTrait RaceSummary = new();
        // Ludoal fork: a fresh setup starts on the player's saved Rule Options, not the stock
        // ruleset. SetCustomSetup below replaces P wholesale when a scenario supplies its own.
        UniverseParams P = NewParamsWithSavedRules();

        static UniverseParams NewParamsWithSavedRules()
        {
            var p = new UniverseParams();
            p.ApplySavedRuleOptions();
            return p;
        }

        Rectangle FlagLeft;
        Rectangle FlagRight;

        // Ludoal fork: one full-screen popup frame carrying two rows of tabs.
        //   row 1, FIXED:   [Empire] [Galaxy] - side by side, half the width each
        //   row 2, DYNAMIC: [Race] | [Physical..Environment] | [Points|Description]
        // Everything stays visible: row 1 sits ABOVE row 2, it never hides it. Row 2 is the one
        // block that absorbs the resolution; the rest are fixed.
        Rectangle ScreenFrame;
        PopupFrame Frame;
        Submenu EnvTab;    // row 1 head: the standing environment preferences, over the race list
        UIButton ClearTraitsBtn;   // lives on the Points page, follows its tab
        Submenu EmpireTab;
        Submenu GalaxyTab;
        Submenu RaceTab;   // row 2 left: the race list
        Submenu InfoTab;   // row 2 right: Points to Spend | Description
        SelectedTraitsSummary PointsSummary;

        // the two tabs of InfoTab share one area - this is which of them is showing
        // the Clear Traits button belongs to the Points page: it hides with it
        void OnInfoTabChanged(int tab)
        {
            PointsSummary.Visible = tab == 0;
            DescriptionTextList.Visible = tab == 1;
            if (ClearTraitsBtn != null)
                ClearTraitsBtn.Visible = tab == 0;
        }

        // the two tabs of the left column share one area - Race (0) or Opponents (1) shows
        void OnLeftTabChanged(int tab)
        {
            if (ChooseRaceList != null)
                ChooseRaceList.Visible = tab == 0;
            if (ChooseOpponentsList != null)
                ChooseOpponentsList.Visible = tab == 1;
            if (OpponentsCountLabel != null)
                OpponentsCountLabel.Visible = tab == 1;
            if (OpponentsCountValue != null)
                OpponentsCountValue.Visible = tab == 1;
        }

        // clicking an opponent toggles it in/out of the chosen set (from the old popup)
        void OnOpponentItemSelected(SelectOpponentListItem item)
        {
            if (P.SelectedOpponents.Remove(item.EmpireData))
                return;
            if (P.SelectedOpponents.Count >= P.NumOpponents)
                GameAudio.NegativeClick();
            else
                P.SelectedOpponents.Add(item.EmpireData);
        }
        EnvPreferencesPanel EnvMenu;
        SubmenuScrollList<TraitsListItem> Traits;
        ScrollList<TraitsListItem> TraitsList;
        UIColorPicker Picker;

        UIButton ModeBtn;
        Rectangle FlagRect;
        ScrollList<RaceArchetypeListItem> ChooseRaceList;
        // the Opponents tab (folded in from the old SelectOpponentsScreen popup)
        ScrollList<SelectOpponentListItem> ChooseOpponentsList;
        UILabel OpponentsCountLabel;   // "Random Opponents:" caption
        UILabel OpponentsCountValue;   // the remaining-random count that follows it
        UITextBox DescriptionTextList;

        UILabel NumSystemsLabel;
        UILabel ExtraPlanetsLabel;
        int FlagIndex;
        public int TotalPointsUsed { get; private set; }

        public IEmpireData SelectedData { get; private set; }

        UITextEntry NameEntry;
        UITextEntry SingEntry;
        UITextEntry PlurEntry;
        UITextEntry SysEntry;
        string RaceName    { get => NameEntry.Text; set => NameEntry.Text = value; }
        string Singular    { get => SingEntry.Text; set => SingEntry.Text = value; }
        string Plural      { get => PlurEntry.Text; set => PlurEntry.Text = value; }
        string HomeSysName { get => SysEntry.Text;  set => SysEntry.Text  = value; }
        string HomeWorldName = "Earth";

        public RaceDesignScreen(MainMenuScreen mainMenu) : base(mainMenu, toPause: null)
        {
            IsPopup = true; // it has to be a popup, otherwise the MainMenuScreen will not be drawn
            MainMenu = mainMenu;
            // no transition: with the panel animations gone, a 0.75s fade only delayed the screen
            // and let whatever sits below show through while it ran
            TransitionOnTime = 0f;
            TransitionOffTime = 0f;
            foreach (RacialTraitOption t in ResourceManager.RaceTraits.TraitList)
                AllTraits.Add(new TraitEntry { Trait = t });
        }

        RacialTrait GetRacialTraits()
        {
            RacialTrait t = RaceSummary.GetClone();
            t.Singular = Singular;
            t.Plural   = Plural;
            t.HomeSystemName = HomeSysName;
            t.HomeworldName  = HomeWorldName;
            t.Color     = Picker.CurrentColor;
            t.FlagIndex = FlagIndex;
            t.Name      = RaceName;
            t.ShipType  = SelectedData.ShipType;
            t.VideoPath = SelectedData.VideoPath;

            Array<string> traitOptions = AllTraits.FilterSelect(trait => trait.Selected, trait => trait.Trait.TraitName).ToArrayList();
            TraitSet traitset = new TraitSet();
            traitset.TraitOptions = traitOptions;
            t.TraitSets.Add(traitset);
            t.TraitSets[0].TraitOptions = AllTraits.FilterSelect(trait => trait.Selected, trait => trait.Trait.TraitName).ToArrayList();
            return t;
        }
        
        public void SetCustomSetup(UniverseParams settings)
        {
            P = settings;
        }
        
        // fixed like the rest of the fixed-window layout
        Graphics.Font DescriptionTextFont => Fonts.Arial14Bold;

        // Ludoal fork: dialogs summoned by this fixed window centre on its frame
        public override Rectangle PageFrame => ScreenFrame.Width > 0 ? ScreenFrame : base.PageFrame;

        public override void LoadContent()
        {
            // ── the frame and the ONE grid every block measures from ─────────────────────────
            const int Pad = 8;
            // Ludoal fork: New Game is a CATEGORY-1 screen - a FIXED 1440x900 window, centred on
            // the display, not a full-screen popup that grows with the resolution. Everything
            // inside derives from ScreenFrame. It sits centred with a black margin on a larger
            // display. (The body-fill inset that keeps the grey off the border shadow is in Draw.)
            const int WinW = 1440, WinH = 900;
            int winX = (ScreenWidth  - WinW) / 2;
            int winY = (ScreenHeight - WinH) / 2;
            ScreenFrame = new Rectangle(winX, winY, WinW, WinH);
            Frame = new PopupFrame(ScreenFrame);

            Rectangle inner = PopupFrame.ContentArea(ScreenFrame);
            int gridLeft   = inner.X + Pad;
            int gridRight  = inner.Right - Pad;
            int gridTop    = inner.Y + Pad;
            // ⚠ Measure from the FRAME's foot: ContentArea already holds back 30px for the bottom
            // band, so measuring from it leaves the content short of the frame.
            // ⚠ The frame's VISIBLE bottom line - the rect runs BottomLine past it, those rows
            // being the band's drop shadow. The grid closes 10px above that line.
            int visibleBottom = ScreenFrame.Bottom - PopupFrame.BottomLine;
            // Ludoal fork (maintainer, 1 Sep): there is no foot ROW any more - every button sits
            // in the tab it serves, and Start Game lives in the notch cut at the foot of the right
            // column. The grid takes back the band the row held, closing 10 above the visible line.
            int gridBottom = visibleBottom - 10;
            int btnH = UITheme.ButtonHeight;          // 25, the theme's universal plate height
            int notchH = btnH + UITheme.TabPadInner;  // the bite taken out of the right column

            // ── ROW 1: Environment | Empire | Galaxy, FIXED height ──────────────────────────
            // The standing Environment tab heads the row, column-aligned with the Race tab below
            // it; Empire takes the middle, Galaxy closes on the right column's width.
            // Ludoal fork (maintainer, 1 Sep): 220 of CONTENT under the tab strip, so the panel
            // carries the strip on top of that. The two side columns are 390 and the middle one
            // takes what is left - one arithmetic, both rows, no column yielding to another.
            const int Row1H = 220 + Submenu.TabHeight;
            const int SideW = 390;   // the fixed side-column width, shared with row 2
            int midLeft = gridLeft + SideW + Pad;
            int midW    = gridRight - SideW - Pad - midLeft;
            EnvTab = Add(new Submenu(new RectF(gridLeft, gridTop, SideW, Row1H), GameText.NgTabEnvironment));
            EmpireTab = Add(new Submenu(new RectF(midLeft, gridTop, midW, Row1H), "Empire"));
            GalaxyTab = Add(new Submenu(new RectF(gridRight - SideW, gridTop, SideW, Row1H), "Galaxy"));

            // row 2 takes what row 1 leaves - the dynamic block, now that the foot row is gone
            int row2Top = gridTop + Row1H + Pad;
            int row2H   = gridBottom - row2Top;

            // the name form lives inside the Empire tab - the tab IS its frame, so there is no
            // separate panel drawn behind it
            RectF nameArea = EmpireTab.ContentArea;

            // ── the EMPIRE tab is three columns, for 900p headroom ────────────────────────────
            // 1: the labels, 2: the value fields, 3: the flag picker on the right. The flag has
            // its own fixed column, so the picker doesn't overlap the value fields at 900p.
            // Constants, not a divide of the leftover, so nothing shifts when the tab resizes.
            // ⚠ the left pull that used to live here is gone: the inset is the theme's TextPad,
            // which the form reads through ContentArea.
            const float SplitPull    = 30f;     // values recede 30 from stock
            const float FlagColW     = 120f;    // the flag picker column, arrows included
            const float FlagNudgeX   = 10f;     // push the whole flag block 10px right
            // the split derives from the longest LOCALIZED label (French runs longer),
            // floored at the historical stock value
            float FormSplit = Math.Max(205f - SplitPull,
                12f + new[] { GameText.EmpireName, GameText.RaceNameSingular, GameText.RaceNamePlural, GameText.HomeSystemName }
                      .Max(t2 => Fonts.Arial14Bold.TextWidth(Localizer.Token(t2) + ": ")));

            SelectedData = GetDefaultRace(); //SelectedData is used to populate the UI

            UIList raceCustomizatioForm = AddList(new Vector2(nameArea.X, nameArea.Y));
            raceCustomizatioForm.Padding = new Vector2(4,4);

            // the text block stops where the flag column starts, so the value underline never
            // runs under the flag - the ONE width all four rows share
            float formWidth = nameArea.W - FlagColW;
            NameEntry = AddSplitter(raceCustomizatioForm, "{EmpireName}: ", SelectedData.Name, formWidth, FormSplit);
            SingEntry = AddSplitter(raceCustomizatioForm, "{RaceNameSingular}: ", SelectedData.Singular, formWidth, FormSplit);
            PlurEntry = AddSplitter(raceCustomizatioForm, "{RaceNamePlural}: ", SelectedData.Plural, formWidth, FormSplit);
            SysEntry = AddSplitter(raceCustomizatioForm,  "{HomeSystemName}: ", SelectedData.HomeSystemName, formWidth, FormSplit);
            HomeWorldName = SelectedData.HomeWorldName;

            // column 3: the flag picker, its "Flag Color" caption aligned on the Empire Name row.
            // +6 on Y for the form list's internal padding, which pushed row 1 below this caption.
            var flagPos = new Vector2(nameArea.Right - FlagColW + 6 + FlagNudgeX, nameArea.Y + 6);
            Add(new UILabel(flagPos, GameText.FlagColor, Fonts.Arial14Bold, Color.BurlyWood));
            FlagRect = new Rectangle((int)flagPos.X, (int)flagPos.Y + 26, 80, 80);

            // ── ROW 2: Race|Opponents | Traits | Points+Description, sharing row2Top and row2H ──
            // Both rows share ONE arithmetic: the two side columns are SideW, the middle block takes
            // what is left. No column yields width to another any more (maintainer, 1 Sep).
            RectF traitsList = new(midLeft, row2Top, midW, row2H);

            LocalizedText[] traitNames = { GameText.Physical, GameText.Sociological, GameText.HistoryAndTradition, GameText.NgTabEnvironmental };
            // ⚠ No Bevel and no Menu1 background - Menu1 paints a second popup frame INSIDE the
            // tab's own, producing a double border; with it goes the SetAbsPos pin it needed.
            Traits = Add(new SubmenuScrollList<TraitsListItem>(traitsList, traitNames));
            Traits.OnTabChange = OnTraitsTabChanged;

            TraitsList = Traits.List;
            TraitsList.EnableItemHighlight = true;
            TraitsList.OnClick = OnTraitsListItemClicked;

            // row 2 LEFT: two tabs sharing one area, Race and Opponents. Ludoal fork: Select
            // Opponents is a second tab here (same pattern as Points|Description on the right),
            // not a separate window. The tab is SideW wide, aligned with Environment above.
            LocalizedText[] leftTabs = { GameText.NgTabRace, GameText.NgTabOpponents };
            RaceTab = Add(new Submenu(new RectF(gridLeft, row2Top, SideW, row2H), leftTabs));
            RaceTab.OnTabChange = OnLeftTabChanged;
            RectF chooseRace = RaceTab.ContentArea;      // the captions: padded like every other text
            // ⚠ a scroll LIST sits on the CLIENT area, not the padded one: it already insets its own
            // items (PaddingLeft, and the theme's ScrollbarLane on the right). Handing it the padded
            // rect stacked two margins and left its bar further in than its neighbours' (bench 566).
            RectF raceClient = RaceTab.ClientArea;
            RectF raceListArea = raceClient;
            ChooseRaceList = Add(new ScrollList<RaceArchetypeListItem>(raceListArea, 135));
            ChooseRaceList.OnClick = OnRaceArchetypeItemClicked;

            foreach (IEmpireData e in ResourceManager.MajorRaces)
                ChooseRaceList.AddItem(new RaceArchetypeListItem(this, e));

            // the Opponents tab: a count caption plus the opponent list, the same content the
            // SelectOpponentsScreen popup carried. Both share chooseRace; OnLeftTabChanged flips
            // which one shows. The count strip sits at the top, the list below it.
            // The caption sits at the head of the padded area, like every other panel's first
            // line in this window - the air above it was a local 14 and is the theme's TextPad now.
            const int OppCountStrip = 42;
            OpponentsCountLabel = Add(new UILabel(
                new Vector2(chooseRace.X, chooseRace.Y),
                "Random Opponents: ", Fonts.Arial14Bold, Colors.Cream));
            OpponentsCountValue = Add(new UILabel(
                new Vector2(chooseRace.X + Fonts.Arial14Bold.TextWidth("Random Opponents: "), chooseRace.Y),
                "", Fonts.Arial14Bold, Color.White));
            RectF oppListArea = new(raceClient.X, raceClient.Y + OppCountStrip,
                                    raceClient.W, raceClient.H - OppCountStrip);
            ChooseOpponentsList = Add(new ScrollList<SelectOpponentListItem>(oppListArea, 135));
            ChooseOpponentsList.OnClick = OnOpponentItemSelected;
            ChooseOpponentsList.OnDoubleClick = OnOpponentItemSelected;
            IEmpireData[] majorRaces = ResourceManager.MajorRaces.Filter(
                data => data.ArchetypeName != SelectedData.ArchetypeName);
            foreach (IEmpireData e in majorRaces)
                ChooseOpponentsList.AddItem(new SelectOpponentListItem(P, e));

            // Race shows first; the opponents controls start hidden.
            OnLeftTabChanged(0);

            Graphics.Font font = Fonts.Arial12Bold;
            // the galaxy readouts live in the Galaxy tab now, not off the right of a name panel
            RectF galaxyArea = GalaxyTab.ContentArea;
            // ⚠ the readouts sit on the FIRST option row (Galaxy Size), to its right. 190 clears
            // the 180-wide split of the option buttons below.
            // ⚠ derived from the option list below, not from the panel: that list starts at X+10
            // and its splitter is 180 wide, so its VALUES sit at X+190 - a label at X+200 landed
            // on top of them. Its first row is one padding down from the list's own Y.
            const int optSplit = 180, optPad = 3;
            float labelX = galaxyArea.X + optSplit + 70;
            float labelY = galaxyArea.Y + optPad;
            NumSystemsLabel = Add(new UILabel(labelX, labelY, $"{Localizer.Token(GameText.SolarSystems)}: {GetSystemsNum()}"));
            NumSystemsLabel.Font  = font;
            NumSystemsLabel.Color = Color.SteelBlue;

            ExtraPlanetsLabel = Add(new UILabel(NumSystemsLabel.X, NumSystemsLabel.Y + font.LineSpacing + 3, ""));
            ExtraPlanetsLabel.Font  = font;
            ExtraPlanetsLabel.Color = Color.Green;

            UIList optionButtons = AddList(galaxyArea.X, galaxyArea.Y);
            optionButtons.CaptureInput = true;
            optionButtons.Padding      = new Vector2(2,3);
            optionButtons.Color        = Color.Black.Alpha(0.5f);

            var customStyle = new UIButton.StyleTextures();
            // [ btn_title : ]  lbl_text
            UIButton AddOption(string title, Action<UIButton> onClick,
                               Func<UILabel, string> getText, LocalizedText tip = default)
            {
                var button = new UIButton(customStyle, new Vector2(160, 18), LocalizedText.Parse(title))
                {
                    Font              = Fonts.Arial11Bold, OnClick = onClick,
                    Tooltip           = tip, TextAlign = ButtonTextAlign.Right,
                    AcceptRightClicks = true, TextShadows = true,
                };
                optionButtons.AddSplit(button, new UILabel(getText, Fonts.Arial11Bold)).Split = 180;
                return button;
            }

            string galaxySizeTip = Localizer.Token(GameText.NgGalaxyScaleTooltip);
            if (GlobalStats.Defaults.ChangeResearchCostBasedOnSize)
                galaxySizeTip += ". Scale other than Medium will increase/decrease research cost of technologies.";
            
            string solarSystemsTip = Localizer.Token(GameText.NgSolarSystemsCountTooltip);
            if (GlobalStats.Defaults.ChangeResearchCostBasedOnSize)
                solarSystemsTip += ". Technology research costs will scale up or down as well";

            string opponentsTip = Localizer.Token(GameText.NgAiOpponentsCountTooltip);
            if (GlobalStats.Defaults.ChangeResearchCostBasedOnSize)
                opponentsTip += ". On a large scale galaxy, this might also affect research cost of technologies.";

            AddOption("{GalaxySize} : ",   OnGalaxySizeClicked,  _ => GalSizeText(P.GalaxySize), tip:galaxySizeTip);
            AddOption("{SolarSystems} : ", OnNumberStarsClicked, _ => P.StarsCount.ToString(), tip:solarSystemsTip);
            AddOption("{Opponents} : ",  OnNumOpponentsClicked,  _ => P.NumOpponents.ToString(), tip:opponentsTip);
            ModeBtn = AddOption("{GameMode} : ",   OnGameModeClicked, _ => GetModeText().Text, tip:GetModeTip());
            AddOption("{Pacing} : ", OnPacingClicked, _ => (P.Pace == 1f) ? "1x" : string.Format(Localizer.Token(GameText.NgPaceSlower), $"{P.Pace:0.##}"), tip:GameText.TheGamesPaceModifiesThe);
            AddOption("{Difficulty} : ", OnDifficultyClicked, _ => DifficultyText(P.Difficulty),
                tip:GameText.NgDifficultyAggressivenessTooltip);
            AddOption("{RemnantPresence} : ", OnExtraRemnantClicked, _ => RemnantText(P.ExtraRemnant),
                tip:GameText.NgRemnantsIntensityTooltip);
            // Ludoal fork (maintainer, 31 Aug '26): the setup offered their NUMBER and nothing
            // else. The complaint from new players is not how many there are - it is how quickly
            // they become dangerous, which is this.
            AddOption(Localizer.Token(GameText.RmPaceLabel) + " : ", OnRemnantPaceClicked,
                _ => RemnantPaceText(P.RemnantPace), tip:GameText.RmPaceTip);

            // row 2 RIGHT: two tabs over one area - the points summary, and the race description.
            // Same rect for both; OnTabChange flips which one is visible.
            // ⚠ the tab list is an IEnumerable - there is no variadic overload
            // "Points", not the full token: the two tabs have to share ONE row
            LocalizedText[] infoTabs = { GameText.NgTabPoints, GameText.NgTabDescription };
            // ⚠ shorter by the notch: Start Game is the one button left outside a tab, and it sits
            // in the bite taken here (maintainer's 35 = the plate's 25 plus one TabPadInner).
            InfoTab = Add(new Submenu(new RectF(gridRight - SideW, row2Top, SideW, row2H - notchH), infoTabs));
            InfoTab.OnTabChange = OnInfoTabChanged;
            RectF description = InfoTab.ClientArea;   // the text box insets its own lines, same rule as a list
            DescriptionTextList = Add(new UITextBox(description, useBorder:false, DescriptionTextFont));
            DescriptionTextList.ItemsList.ItemPadding = new Vector2(10, 0);
            DescriptionTextList.Visible = false; // tab 0 (Points) is the one that opens

            PointsSummary = Add(new SelectedTraitsSummary(this));

            // 600 tall: the colour grid is a fixed 512 plus the title band the frame carries
            Picker = Add(new UIColorPicker(new Rectangle(ScreenWidth / 2 - 310, ScreenHeight / 2 - 300, 620, 600)));
            Picker.Title = "Empire Color";
            Picker.Visible = false;

            // ── the FOOT: each button sits under the sector it affects ─────────────────────
            // ⚠ Medium, not Default: BtnW is 132 and that IS the Medium plate's width. A
            // Default button is 168 wide and a row would overlap itself by 36 per button.
            const int BtnW = 132, BtnGap = 6;
        // enum display names, spoken through the localization system
        static string GalSizeText(GalSize g) => g switch { GalSize.Tiny => Localizer.Token(GameText.GsTiny), GalSize.Small => Localizer.Token(GameText.GsSmall), GalSize.Medium => Localizer.Token(GameText.GsMedium), GalSize.Large => Localizer.Token(GameText.GsLarge), GalSize.Huge => Localizer.Token(GameText.GsHuge), GalSize.Epic => Localizer.Token(GameText.GsEpic), GalSize.TrulyEpic => Localizer.Token(GameText.GsTrulyEpic), _ => g.ToString() };
        static string DifficultyText(GameDifficulty d) => d switch { GameDifficulty.Normal => Localizer.Token(GameText.DfNormal), GameDifficulty.Hard => Localizer.Token(GameText.DfHard), GameDifficulty.Brutal => Localizer.Token(GameText.DfBrutal), GameDifficulty.Insane => Localizer.Token(GameText.DfInsane), _ => d.ToString() };
        static string RemnantText(ExtraRemnantPresence r) => r switch { ExtraRemnantPresence.VeryRare => Localizer.Token(GameText.RmVeryRare), ExtraRemnantPresence.Rare => Localizer.Token(GameText.RmRare), ExtraRemnantPresence.Normal => Localizer.Token(GameText.RmNormal), ExtraRemnantPresence.More => Localizer.Token(GameText.RmMore), ExtraRemnantPresence.MuchMore => Localizer.Token(GameText.RmMuchMore), ExtraRemnantPresence.Everywhere => Localizer.Token(GameText.RmEverywhere), _ => r.ToString() };
            // Ludoal fork: ONE width for every foot button on this screen - what a side column can
            // hold for three of them. Empire's two use it too, so a button reads the same wherever
            // it sits (maintainer, bench 566). The plate is PAINTED, so any width draws.
            int footW = (SideW - 2 * UITheme.TabPadOuter - 2 * BtnGap) / 3;
            // ⚠ a foot row sits on the CLIENT area, not the padded one: the maintainer measures its
            // clearance from the frame's own line, and the inner padding would double that gap.
            int FootRowStart(in RectF client, int count)
            {
                int rowW = count * footW + (count - 1) * BtnGap;
                return (int)client.X + ((int)client.W - rowW) / 2;
            }

            // EMPIRE's foot: the race's own load/save, in the tab that holds the race's name.
            RectF empireClient = EmpireTab.ClientArea;
            int rx = FootRowStart(empireClient, 2);
            int ry = (int)empireClient.Bottom - btnH;
            UIButton loadRace = Button(ButtonStyle.Medium, rx, ry, GameText.NgLoadRace, click: OnLoadRaceClicked);
            loadRace.SetAbsSize(footW, btnH);
            UIButton saveRace = Button(ButtonStyle.Medium, rx + footW + BtnGap, ry, GameText.MmSaveRace, click: OnSaveRaceClicked);
            saveRace.SetAbsSize(footW, btnH);

            // GALAXY's foot: the whole-setup load/save, with Rule Options between them - it
            // configures this tab, so it belongs in this row rather than floating above it.
            RectF galaxyClient = GalaxyTab.ClientArea;
            int gx = FootRowStart(galaxyClient, 3);
            int gy = (int)galaxyClient.Bottom - btnH;
            UIButton loadSetup = Button(ButtonStyle.Medium, gx, gy, GameText.NgLoadSetup, click: OnLoadSetupClicked);
            loadSetup.SetAbsSize(footW, btnH);
            UIButton ruleOptions = Button(ButtonStyle.WideActive, gx + footW + BtnGap, gy,
                   Localizer.Token(GameText.RuleOptions), click: OnRuleOptionsClicked);
            ruleOptions.SetAbsSize(footW, btnH);
            UIButton saveSetup = Button(ButtonStyle.Medium, gx + 2 * (footW + BtnGap), gy, GameText.MmSaveSetup, click: OnSaveSetupClicked);
            saveSetup.SetAbsSize(footW, btnH);

            // Start Game is the ONE button left outside a tab: it sits in the notch cut at the foot
            // of the right column, centred on it, one TabPadInner under the panel. Ludoal fork: the
            // frame's close cross top-right does the cancel, so Exit has no button.
            UIButton engage = Button(ButtonStyle.WideActive, gridRight - SideW + (SideW - BtnW) / 2,
                   row2Top + row2H - notchH + UITheme.TabPadInner, GameText.NgStartGame, click: OnEngageClicked);
            engage.SetAbsSize(BtnW, btnH);

            Vector2 closePos = PopupFrame.ClosePos(ScreenFrame);
            CloseButton(closePos.X, closePos.Y);

            // Clear Traits lives on the Points page and follows its tab; red (hostile) plate.
            // ⚠ WideHostile is painted, so pin its width to the Medium footprint.
            ClearTraitsBtn = Button(ButtonStyle.WideHostile, (int)description.X, (int)description.Bottom - btnH,
                                    GameText.NgClearTraits, click: OnClearClicked);
            ClearTraitsBtn.SetAbsSize(BtnW, btnH);

            DoRaceDescription();
            SetRacialTraits(SelectedData.Traits);

            // the environment preferences hold their standing tab at the head of row 1;
            // the tab frame is the delimiter, the panel draws no background of its own
            RectF envArea = EnvTab.ContentArea;
            var envRect = new Rectangle((int)envArea.X, (int)envArea.Y, (int)envArea.W, (int)envArea.H);
            EnvMenu = Add(new EnvPreferencesPanel(this, RaceSummary, envRect));

            // Ludoal fork: no slide-in/slide-out on this screen - the panels appear where they
            // belong.

            base.LoadContent();
        }
        
        /// <summary>
        /// Extracted from LoadContent() verbatim, defaults to first race if e.Singular == "Human" unavailable
        /// else will throw OutOfBounds
        /// </summary>
        IEmpireData GetDefaultRace()
        {
            var empires = ResourceManager.MajorRaces;
            foreach (IEmpireData e in empires)
                if (e.Singular == "Human")
                    return e;
            return empires[0];
        }

        UITextEntry AddSplitter(UIList list, string title, string inputText, float width, float splitAt = 205f)
        {
            var label = new UILabel(LocalizedText.Parse(title), Fonts.Arial14Bold, Color.BurlyWood);
            var input = new UITextEntry(Vector2.Zero, Fonts.Arial14Bold, inputText)
            {
                // the underline runs 20px shorter at its right end than the field's full width.
                Width = width - splitAt - 20,
                DrawUnderline = true,
                Color = Colors.Cream
            };

            list.AddSplit(label, input).Split = splitAt;
            return input;
        }

        int GetSystemsNum()
        {
            (int numStars, _) = GetNumStars(P.StarsCount, P.GalaxySize, P.NumOpponents);
            return numStars;
        }

        public static (int NumStars, float StarNumModifier)
            GetNumStars(StarsAbundance abundance, GalSize galaxySize, int numOpponents)
        {
            float starNumModifier;
            switch (abundance)
            {
                case StarsAbundance.VeryRare:    starNumModifier = 0.3f;  break;
                case StarsAbundance.Rare:        starNumModifier = 0.5f;  break;
                case StarsAbundance.Uncommon:    starNumModifier = 0.8f;  break;
                default:
                case StarsAbundance.Normal:      starNumModifier = 1f;    break;
                case StarsAbundance.Abundant:    starNumModifier = 1.1f;  break;
                case StarsAbundance.Crowded:     starNumModifier = 1.25f; break;
                case StarsAbundance.Packed:      starNumModifier = 1.5f;  break;
                case StarsAbundance.SuperPacked: starNumModifier = 1.8f;  break;
            }


            int numSystemsFromSize;
            switch (galaxySize)
            {
                default:
                case GalSize.Tiny:      numSystemsFromSize = 16;  break;
                case GalSize.Small:     numSystemsFromSize = 36;  break;
                case GalSize.Medium:    numSystemsFromSize = 60;  break;
                case GalSize.Large:     numSystemsFromSize = 80;  break;
                case GalSize.Huge:      numSystemsFromSize = 100; break;
                case GalSize.Epic:      numSystemsFromSize = 120; break;
                case GalSize.TrulyEpic: numSystemsFromSize = 150; break;
            }

            int numStars = (int)(numSystemsFromSize * starNumModifier)
                         + ((int)galaxySize + 1) * numOpponents;
            return (numStars, starNumModifier);
        }

        public void OnTraitsTabChanged(int tabIndex)
        {
            string category;
            switch (tabIndex)
            {
                default:
                case 0: category = "Physical"; break;
                case 1: category = "Sociological"; break;
                case 2: category = "HistoryAndTradition";  break;
                case 3: category = "Environment"; break;
            }

            TraitsListItem[] traits = AllTraits.FilterSelect(t => t.Trait.Category == category,
                                                             t => new TraitsListItem(this, t));
            TraitsList.SetItems(traits);
        }

        void OnRuleOptionsClicked(UIButton b)
        {
            ScreenManager.AddScreen(new RuleOptionsScreen(this, P));
        }

        void OnClearClicked(UIButton b)
        {
            foreach (TraitEntry trait in AllTraits)
                trait.Selected = false;
            TotalPointsUsed = P.RacialTraitPoints;
        }

        void OnLoadRaceClicked(UIButton b)
        {
            ScreenManager.AddScreen(new LoadRaceScreen(this));
        }

        void OnSaveRaceClicked(UIButton b)
        {
            ScreenManager.AddScreen(new SaveRaceScreen(this, GetRacialTraits()));
        }

        void OnLoadSetupClicked(UIButton b)
        {
            ScreenManager.AddScreen(new LoadNewGameSetupScreen(this));
        }

        void OnSaveSetupClicked(UIButton b)
        {
            ScreenManager.AddScreen(new SaveNewGameSetupScreen(this, P));
        }

        // If we had a left mouse click, increment forward, otherwise decrement
        int OptionIncrement => Input.LeftMouseReleased ? 1 : -1;

        void OnGalaxySizeClicked(UIButton b)
        {
            P.GalaxySize = P.GalaxySize.IncrementWithWrap(OptionIncrement);
        }

        LocalizedText GetModeText()
        {
            switch (P.Mode)
            {
                default:
                case GameMode.Random:        return GameText.RandomGameMode;
                case GameMode.Sandbox:       return GameText.Sandbox;
                case GameMode.Elimination:   return GameText.CapitalElimination;
                case GameMode.Corners:       return GameText.Corners;
                case GameMode.BigClusters:   return GameText.BigClustersGame;
                case GameMode.SmallClusters: return GameText.SmallClustersGame;
                case GameMode.Ring:           return GameText.RingGalaxyGame;
                case GameMode.SpiralTwoArm:     return GameText.SpiralTwoArmGalaxyGame;
                case GameMode.SpiralFourArm:    return GameText.SpiralFourArmGalaxyGame;
                case GameMode.SpiralBarred:     return GameText.SpiralBarredGalaxyGame;
                case GameMode.SpiralMagellanic: return GameText.SpiralMagellanicGalaxyGame;
            }
        }

        LocalizedText GetModeTip()
        {
            switch (P.Mode)
            {
                default:
                case GameMode.Random:         return GameText.InRandomGameMode;
                case GameMode.Sandbox:        return GameText.InTheSandboxGameMode;
                case GameMode.Elimination:    return GameText.InTheCapitalEliminationGame;
                case GameMode.Corners:        return GameText.CornersIsARaceMatch;
                case GameMode.BigClusters:    return GameText.EachEmpireStartsInA;
                case GameMode.SmallClusters:  return GameText.TheGalaxyWillBeConsisted;
                case GameMode.Ring:           return GameText.RingGalaxyGameTip;
                case GameMode.SpiralTwoArm:     return GameText.SpiralTwoArmGalaxyGameTip;
                case GameMode.SpiralFourArm:    return GameText.SpiralFourArmGalaxyGameTip;
                case GameMode.SpiralBarred:     return GameText.SpiralBarredGalaxyGameTip;
                case GameMode.SpiralMagellanic: return GameText.SpiralMagellanicGalaxyGameTip;
            }
        }

        void OnGameModeClicked(UIButton b)
        {
            P.Mode = P.Mode.IncrementWithWrap(OptionIncrement);
            if (P.Mode == GameMode.Corners) P.NumOpponents = 3;
            ModeBtn.Tooltip = GetModeTip();
        }

        void OnNumberStarsClicked(UIButton b)
        {
            P.StarsCount = P.StarsCount.IncrementWithWrap(OptionIncrement);
        }

        void OnNumOpponentsClicked(UIButton b)
        {
            int maxOpponents = P.Mode == GameMode.Corners ? 3 : GlobalStats.Defaults.MaxOpponents;
            P.NumOpponents += OptionIncrement;
            if (P.NumOpponents > maxOpponents) P.NumOpponents = 1;
            else if (P.NumOpponents < 1)       P.NumOpponents = maxOpponents;
            VerifySelectedOpponents();
        }

        void OnPacingClicked(UIButton b)
        {
            P.Pace += OptionIncrement*0.5f;
            if (P.Pace > 10f) P.Pace = 1f;
            if (P.Pace < 1f) P.Pace = 10f;
        }
        
        void OnDifficultyClicked(UIButton b)
        {
            P.Difficulty = P.Difficulty.IncrementWithWrap(OptionIncrement);
        }
        
        void OnExtraRemnantClicked(UIButton b)
        {
            P.ExtraRemnant = P.ExtraRemnant.IncrementWithWrap(OptionIncrement);
        }

        void OnRemnantPaceClicked(UIButton b)
        {
            P.RemnantPace = P.RemnantPace.IncrementWithWrap(OptionIncrement);
        }

        static string RemnantPaceText(RemnantPaceSetting p) => p switch
        {
            RemnantPaceSetting.VerySlow => Localizer.Token(GameText.RmPaceVerySlow),
            RemnantPaceSetting.Slow     => Localizer.Token(GameText.RmPaceSlow),
            _                           => Localizer.Token(GameText.RmPaceNormal),
        };

        public override bool HandleInput(InputState input)
        {
            if (Picker.Visible)
                return Picker.HandleInput(input);

            if (FlagRect.HitTest(input.CursorPosition) && input.LeftMouseClick)
            {
                Picker.Visible = !Picker.Visible;
                return true;
            }

            if (FlagRight.HitTest(input.CursorPosition) && input.LeftMouseClick)
            {
                if (ResourceManager.NumFlags - 1 <= FlagIndex)
                    FlagIndex = 0;
                else
                    FlagIndex += 1;
                GameAudio.BlipClick();
                return true;
            }

            if (FlagLeft.HitTest(input.CursorPosition) && input.LeftMouseClick)
            {
                if (FlagIndex <= 0)
                    FlagIndex = ResourceManager.NumFlags - 1;
                else
                    FlagIndex -= 1;
                GameAudio.BlipClick();
                return true;
            }

            return base.HandleInput(input);
        }

        void OnTraitsListItemClicked(TraitsListItem item)
        {
            TraitEntry t = item.Trait;
            if (t.Selected && TotalPointsUsed + t.Trait.Cost >= 0)
            {
                t.Selected = !t.Selected;
                TotalPointsUsed += t.Trait.Cost;
                GameAudio.BlipClick();
            }
            else if (TotalPointsUsed - t.Trait.Cost < 0 || t.Selected || t.Excluded)
            {
                GameAudio.NegativeClick();
            }
            else
            {
                bool ok = true;
                foreach (TraitEntry ex in AllTraits)
                {
                    if (t.Trait.Excludes.Contains(ex.Trait.TraitName) && ex.Selected)
                        ok = false;
                }
                if (ok)
                {
                    t.Selected = true;
                    TotalPointsUsed -= t.Trait.Cost;
                    GameAudio.BlipClick();
                }
            }

            UpdateTraits();
            DoRaceDescription();
            EnvMenu.UpdatePreferences(RaceSummary);
        }

        void OnRaceArchetypeItemClicked(RaceArchetypeListItem item)
        {
            SelectedData = item.EmpireData;
            SetRacialTraits(SelectedData.Traits);
            UpdateTraits();
            DoRaceDescription();
            VerifySelectedOpponents();
            EnvMenu.UpdateArchetype(SelectedData, RaceSummary);
        }

        void VerifySelectedOpponents()
        {
            P.SelectedOpponents.Remove(SelectedData);
            if (P.SelectedOpponents.Count > P.NumOpponents)
                for (int i = P.SelectedOpponents.Count - 1; i >= P.NumOpponents; --i)
                    P.SelectedOpponents.RemoveAt(i);
        }

        void OnEngageClicked(UIButton b)
        {
            if (P.Mode == GameMode.Elimination)
                P.EliminationMode = true;

            RaceSummary.Color          = Picker.CurrentColor;
            RaceSummary.Singular       = Singular;
            RaceSummary.Plural         = Plural;
            RaceSummary.HomeSystemName = HomeSysName;
            RaceSummary.HomeworldName  = HomeWorldName;
            RaceSummary.Name           = RaceName;
            RaceSummary.FlagIndex      = FlagIndex;
            RaceSummary.ShipType       = SelectedData.ShipType;
            RaceSummary.VideoPath      = SelectedData.VideoPath;
            RaceSummary.Adj1           = SelectedData.Adj1;
            RaceSummary.Adj2           = SelectedData.Adj2;

            RaceSummary.TraitSets.Clear();
            RaceSummary.TraitSets.Add(new TraitSet
            {
                TraitOptions = AllTraits.FilterSelect(t => t.Selected, t => t.Trait.TraitName).ToArrayList()
            });

            P.PlayerData = SelectedData.CreateInstance(copyTraits: false);
            P.PlayerData.SpyModifier = RaceSummary.SpyMultiplier;
            P.PlayerData.Traits      = RaceSummary;
            P.PlayerData.DiplomaticPersonality = new DTrait();

            (P.NumSystems, P.StarsModifier) = GetNumStars(P.StarsCount, P.GalaxySize, P.NumOpponents);
            var ng = new CreatingNewGameScreen(MainMenu, P);

            ScreenManager.GoToScreen(ng, clear3DObjects:true);
        }

        public override void Update(float fixedDeltaTime)
        {
            CreateRaceSummary();
            // the Opponents tab's count: how many of the NumOpponents slots are still random
            OpponentsCountValue.Text = $"{P.NumOpponents - P.SelectedOpponents.Count}";
            OpponentsCountValue.Color = P.SelectedOpponents.Count == P.NumOpponents ? Color.Gray : Color.White;
            base.Update(fixedDeltaTime);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // Ludoal fork: the standard popup dim, not an opaque veil - the menu's animated
            // backdrop stays alive behind New Game. The screen stays IsPopup so the menu
            // underneath keeps being drawn (remove that and this one draws on nothing); the menu
            // hides its own button column while we sit on top.
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            int numSystems = GetSystemsNum();
            NumSystemsLabel.Text = $"{Localizer.Token(GameText.SolarSystems)}: {numSystems}";
            ShowPerformanceWarning(numSystems);
            ShowExtraPlanetsNum(P.ExtraPlanets);

            batch.SafeBegin();
            // ⚠ the frame goes FIRST, before base.Draw: it is painted by hand, not added as a
            // child, so drawing it after would bury every tab and list on the screen.
            // DrawFill insets the body fill off the border rule itself.
            Frame.DrawFill(batch, ScreenFrame);
            Frame.Draw(batch);
            // the window title font, the one Colony and every popup uses - not Laserian
            string screenTitle = Localizer.Token(GameText.NewGame);
            batch.DrawString(UITheme.WindowTitle, screenTitle,
                new Vector2(ScreenFrame.X + ScreenFrame.Width / 2 - UITheme.WindowTitle.TextWidth(screenTitle) / 2f,
                            Frame.TitleRect.CenterY() - UITheme.WindowTitle.LineSpacing / 2f), UITheme.TextPrimary);

            base.Draw(batch, elapsed);
            batch.Draw(ResourceManager.Flag(FlagIndex), FlagRect, Picker.CurrentColor);
            FlagLeft  = new Rectangle(FlagRect.X - 20, FlagRect.Y + 40 - 10, 20, 20);
            FlagRight = new Rectangle(FlagRect.X + FlagRect.Width, FlagRect.Y + 40 - 10, 20, 20);
            batch.Draw(ResourceManager.Texture("UI/leftArrow"), FlagLeft, Color.BurlyWood);
            batch.Draw(ResourceManager.Texture("UI/rightArrow"), FlagRight, Color.BurlyWood);

            batch.SafeEnd();
        }

        void ShowExtraPlanetsNum(int extraPlanets)
        {
            ExtraPlanetsLabel.Visible = extraPlanets > 0;
            ExtraPlanetsLabel.Text = $"Extra Planets: {extraPlanets}";
        }

        // Ludoal fork (maintainer, 1 Sep): the warning was a third line under the two readouts,
        // and the Galaxy column no longer has the room for it. It hangs off the count itself now.
        // ⚠ The COLOUR stays on the number: a warning that only exists under the cursor is a door
        // with no handle - the amber says look, the tooltip says why (Lek's reserve, same day).
        void ShowPerformanceWarning(int numSystems)
        {
            if (numSystems >= 200)
            {
                NumSystemsLabel.Color = Color.Orange;
                NumSystemsLabel.Tooltip = GameText.NgSystemsPerfWarnHeavy;
            }
            else if (numSystems >= 100)
            {
                NumSystemsLabel.Color = Color.Yellow;
                NumSystemsLabel.Tooltip = GameText.NgSystemsPerfWarn;
            }
            else
            {
                NumSystemsLabel.Color = Color.SteelBlue;
                NumSystemsLabel.Tooltip = default;   // Id 0 and no string: IsValid is false, no tooltip
            }
        }

        class SelectedTraitsSummary : UIElementV2
        {
            readonly RaceDesignScreen Screen;
            readonly Graphics.Font Font;
            public SelectedTraitsSummary(RaceDesignScreen screen)
            {
                Screen = screen;
                // fixed like the rest of the fixed-window layout
                Font = Fonts.Arial14Bold;
            }

            public override bool HandleInput(InputState input)
            {
                return false;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                if (!Visible)
                    return;

                // Ludoal fork: the summary and the description are two tabs over one area, so the
                // summary starts at the top of that area.
                RectF area = Screen.InfoTab.ContentArea;
                var r = new Vector2(area.X, area.Y);
                string title = Localizer.Token(GameText.PointsToSpend);
                batch.DrawString(Font, $"{title}: {Screen.TotalPointsUsed}", r, Color.White);
                r.Y += (Font.LineSpacing + 8);
                Vector2 cursor = r;

                // ⚠ ONE column: the tab has a whole column to itself, so the list simply runs
                // down it.
                int line = 0;
                foreach (TraitEntry t in Screen.AllTraits.OrderByDescending(t => t.Trait.Cost))
                {
                    if (t.Selected)
                    {
                        batch.DrawString(Font, $"({t.Trait.Cost}) {t.Trait.LocalizedName.Text}", cursor,
                                               (t.Trait.Cost > 0 ? Color.ForestGreen: Color.Red));
                        cursor.Y += (Font.LineSpacing + 2);
                        line++;
                    }
                }
            }
        }
        
        public enum GameMode
        {
            Sandbox, SpiralTwoArm, SpiralFourArm, SpiralBarred, SpiralMagellanic, Random, Ring, SmallClusters, BigClusters, Elimination, Corners
        }

        public enum StarsAbundance
        {
            VeryRare, Rare, Uncommon, Normal, Abundant, Crowded, Packed, SuperPacked
        }
    }

    public enum GalSize
    {
        Tiny, Small, Medium, Large, Huge, Epic, TrulyEpic
    }

    // Ludoal fork: named in SPEED because that is what it changes - "Very Low" on a rhythm reads
    // as a quantity. Capped at Normal on purpose: this exists to make them gentler, never harder.
    public enum RemnantPaceSetting
    {
        VerySlow,
        Slow,
        Normal,
    }

    public enum ExtraRemnantPresence
    {
        VeryRare,Rare, Normal, More, MuchMore, Everywhere
    }
}
