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

        // Ludoal fork: one full-screen popup frame carrying two rows of tabs (maintainer layout).
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
        EnvPreferencesPanel EnvMenu;
        SubmenuScrollList<TraitsListItem> Traits;
        ScrollList<TraitsListItem> TraitsList;
        UIColorPicker Picker;

        UIButton ModeBtn;
        UIButton SelectOpponentsBtn;
        Rectangle FlagRect;
        ScrollList<RaceArchetypeListItem> ChooseRaceList;
        UITextBox DescriptionTextList;

        UILabel NumSystemsLabel;
        UILabel ExtraPlanetsLabel;
        UILabel PerformanceWarning;
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
        
        // Ludoal fork: first screen converted to Narrow/Tall. Same three sizes as before, but the
        // small one now covers the whole sub-1920 band instead of only sub-1366, and the large
        // one keys off height rather than either dimension.
        // ⚠ a size up (maintainer): the description is a tab with a full column now, not a strip
        // sharing its area with the points summary.
        Graphics.Font DescriptionTextFont => Narrow ? Fonts.Arial12
                                                    : Tall ? Fonts.Arial20Bold : Fonts.Arial14Bold;

        public override void LoadContent()
        {
            // ── the frame and the ONE grid every block measures from ─────────────────────────
            const int Margin = 10, Pad = 8;
            // ⚠ the rect is pushed OUT by what each edge's texture leaves blank, so the visible
            // rule lands on the margin: 7 transparent rows at the top, 2 at the foot, plus the
            // side borders. Measured in the PNGs - guessing these is what cost four passes.
            ScreenFrame = new Rectangle(Margin - PopupFrame.BorderLeft, Margin - PopupFrame.TopInk,
                                        ScreenWidth - 2 * Margin + PopupFrame.BorderLeft + PopupFrame.BorderRight,
                                        ScreenHeight - 2 * Margin + PopupFrame.TopInk + PopupFrame.BottomLine);
            Frame = new PopupFrame(ScreenFrame);

            Rectangle inner = PopupFrame.ContentArea(ScreenFrame);
            int gridLeft   = inner.X + Pad;
            int gridRight  = inner.Right - Pad;
            int gridTop    = inner.Y + Pad;
            // ⚠ from the FRAME's foot: ContentArea already holds back 30px for the bottom band,
            // so measuring from it left the content well short of the frame (maintainer).
            // ⚠ the frame's VISIBLE bottom line - the rect runs BottomLine past it, those rows
            // being the band's drop shadow. The foot buttons close 10px above that line
            // (maintainer), and the grid closes one pad above the buttons.
            int visibleBottom = ScreenFrame.Bottom - PopupFrame.BottomLine;
            int footY = visibleBottom - 10 - 24;   // 24 = the painted buttons' height
            int gridBottom = footY - Pad;

            // ── ROW 1: Environment | Empire | Galaxy, FIXED height ──────────────────────────
            // The standing Environment tab heads the row (maintainer), column-aligned with the
            // Race tab below it; Empire and Galaxy share the remaining width.
            const int Row1H = 192;   // +20 on the bench's word - the fields were tight
            const int SideW = 330;   // the fixed side-column width, shared with row 2
            EnvTab = Add(new Submenu(new RectF(gridLeft, gridTop, SideW, Row1H), "Environment"));
            int halfW = (gridRight - gridLeft - SideW - 2 * Pad) / 2;
            EmpireTab = Add(new Submenu(new RectF(gridLeft + SideW + Pad, gridTop, halfW, Row1H), "Empire"));
            GalaxyTab = Add(new Submenu(new RectF(gridLeft + SideW + 2 * Pad + halfW, gridTop, halfW, Row1H), "Galaxy"));

            // row 2 takes what row 1 and the foot leave - the dynamic block
            int row2Top = gridTop + Row1H + Pad;
            int row2H   = gridBottom - row2Top;

            // the name form lives inside the Empire tab - the tab IS its frame, so there is no
            // separate panel drawn behind it
            RectF nameArea = EmpireTab.ClientArea;

            var flagPos = new Vector2(nameArea.Right - 80 - 100, nameArea.Y + 12);
            FlagRect = new Rectangle((int)flagPos.X, (int)flagPos.Y + 15, 80, 80);
            
            Add(new UILabel(flagPos, GameText.FlagColor, Fonts.Arial14Bold, Color.BurlyWood));
            
            SelectedData = GetDefaultRace(); //SelectedData is used to populate the UI

            UIList raceCustomizatioForm = AddList(new Vector2(nameArea.X + 20, nameArea.Y + 10));
            raceCustomizatioForm.Padding = new Vector2(4,4);

            const float padRight = 200f;
            var splitItemWidth = nameArea.W - FlagRect.Width - padRight;
            NameEntry = AddSplitter(raceCustomizatioForm, "{EmpireName}: ", SelectedData.Name,splitItemWidth);
            SingEntry = AddSplitter(raceCustomizatioForm, "{RaceNameSingular}: ", SelectedData.Singular, splitItemWidth);
            PlurEntry = AddSplitter(raceCustomizatioForm, "{RaceNamePlural}: ", SelectedData.Plural, splitItemWidth);
            SysEntry = AddSplitter(raceCustomizatioForm,  "{HomeSystemName}: ", SelectedData.HomeSystemName, splitItemWidth);
            HomeWorldName = SelectedData.HomeWorldName;

            // ── ROW 2: Race | Traits | Points+Description, all sharing row2Top and row2H ─────
            // The two side columns are FIXED width (SideW, declared with row 1); the traits
            // block in the middle absorbs what is left. One arithmetic for the three.
            RectF traitsList = new(gridLeft + SideW + Pad, row2Top,
                                   gridRight - gridLeft - 2 * (SideW + Pad), row2H);

            LocalizedText[] traitNames = { GameText.Physical, GameText.Sociological, GameText.HistoryAndTradition, "Environment" };
            // ⚠ no Bevel and NO Menu1 background (maintainer: "supprimer le cadre du bloc
            // central"). The Menu1 painted a second popup frame INSIDE the tab's own, which is
            // the double border the bench saw - and with it goes the SetAbsPos pin it needed.
            Traits = Add(new SubmenuScrollList<TraitsListItem>(traitsList, traitNames));
            Traits.OnTabChange = OnTraitsTabChanged;

            TraitsList = Traits.List;
            TraitsList.EnableItemHighlight = true;
            TraitsList.OnClick = OnTraitsListItemClicked;

            // row 2 LEFT: the race list under a tab of its own
            RaceTab = Add(new Submenu(new RectF(gridLeft, row2Top, SideW, row2H), "Race"));
            RectF chooseRace = RaceTab.ClientArea;
            ChooseRaceList = Add(new ScrollList<RaceArchetypeListItem>(chooseRace, 135));
            ChooseRaceList.OnClick = OnRaceArchetypeItemClicked;

            foreach (IEmpireData e in ResourceManager.MajorRaces)
                ChooseRaceList.AddItem(new RaceArchetypeListItem(this, e));

            Graphics.Font font = Fonts.Arial12Bold;
            // the galaxy readouts live in the Galaxy tab now, not off the right of a name panel
            RectF galaxyArea = GalaxyTab.ClientArea;
            // ⚠ the readouts sit on the FIRST option row (Galaxy Size), to its right - they were
            // anchored on the panel's bottom, which floated them away from the row they comment
            // (maintainer). 190 clears the 180-wide split of the option buttons below.
            // ⚠ derived from the option list below, not from the panel: that list starts at X+10
            // and its splitter is 180 wide, so its VALUES sit at X+190 - a label at X+200 landed
            // on top of them. Its first row is one padding down from the list's own Y.
            const int optListX = 10, optSplit = 180, optPad = 3;
            float labelX = galaxyArea.X + optListX + optSplit + 70;
            float labelY = galaxyArea.Y + 6 + optPad;
            NumSystemsLabel = Add(new UILabel(labelX, labelY, $"Solar Systems: {GetSystemsNum()}"));
            NumSystemsLabel.Font  = font;
            NumSystemsLabel.Color = Color.SteelBlue;

            ExtraPlanetsLabel = Add(new UILabel(NumSystemsLabel.X, NumSystemsLabel.Y + font.LineSpacing + 3, ""));
            ExtraPlanetsLabel.Font  = font;
            ExtraPlanetsLabel.Color = Color.Green;

            // under the two readouts it comments, in the same column - it was anchored on the
            // panel's foot, away from the numbers that trigger it (maintainer)
            PerformanceWarning = Add(new UILabel(labelX, labelY + 2 * (font.LineSpacing + 3), ""));
            PerformanceWarning.Font = font;

            UIList optionButtons = AddList(galaxyArea.X + 10, galaxyArea.Y + 6);
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

            string galaxySizeTip = "Sets the scale of the generated galaxy";
            if (GlobalStats.Defaults.ChangeResearchCostBasedOnSize)
                galaxySizeTip += ". Scale other than Medium will increase/decrease research cost of technologies.";
            
            string solarSystemsTip = "Number of Solar Systems packed into the Universe";
            if (GlobalStats.Defaults.ChangeResearchCostBasedOnSize)
                solarSystemsTip += ". Technology research costs will scale up or down as well";

            string opponentsTip = "Sets the number of AI opponents you must face";
            if (GlobalStats.Defaults.ChangeResearchCostBasedOnSize)
                opponentsTip += ". On a large scale galaxy, this might also affect research cost of technologies.";

            AddOption("{GalaxySize} : ",   OnGalaxySizeClicked,  _ => P.GalaxySize.ToString(), tip:galaxySizeTip);
            AddOption("{SolarSystems} : ", OnNumberStarsClicked, _ => P.StarsCount.ToString(), tip:solarSystemsTip);
            AddOption("{Opponents} : ",  OnNumOpponentsClicked,  _ => P.NumOpponents.ToString(), tip:opponentsTip);
            ModeBtn = AddOption("{GameMode} : ",   OnGameModeClicked, _ => GetModeText().Text, tip:GetModeTip());
            AddOption("{Pacing} : ", OnPacingClicked, _ => (P.Pace == 1f) ? "1x" : $"{P.Pace:0.##}x slower", tip:GameText.TheGamesPaceModifiesThe);
            AddOption("{Difficulty} : ", OnDifficultyClicked, _ => P.Difficulty.ToString(),
                tip:"Hard and above increase AI Aggressiveness and gives them extra bonuses");
            AddOption("{RemnantPresence} : ", OnExtraRemnantClicked, _ => P.ExtraRemnant.ToString(),
                tip:"This sets the intensity of Ancient Remnants presence. If you feel overwhelmed by their advanced technology, reduce this to Rare.");

            // row 2 RIGHT: two tabs over one area - the points summary, and the race description.
            // Same rect for both; OnTabChange flips which one is visible.
            // ⚠ the tab list is an IEnumerable - there is no variadic overload
            // "Points", not the full token: the two tabs have to share ONE row (maintainer)
            LocalizedText[] infoTabs = { "Points", "Description" };
            InfoTab = Add(new Submenu(new RectF(gridRight - SideW, row2Top, SideW, row2H), infoTabs));
            InfoTab.OnTabChange = OnInfoTabChanged;
            RectF description = InfoTab.ClientArea;
            DescriptionTextList = Add(new UITextBox(description, useBorder:false, DescriptionTextFont));
            DescriptionTextList.ItemsList.ItemPadding = new Vector2(10, 0);
            DescriptionTextList.Visible = false; // tab 0 (Points) is the one that opens

            PointsSummary = Add(new SelectedTraitsSummary(this));

            // 600 tall: the colour grid is a fixed 512 plus the title band the frame carries
            Picker = Add(new UIColorPicker(new Rectangle(ScreenWidth / 2 - 310, ScreenHeight / 2 - 300, 620, 600)));
            Picker.Title = "Empire Color";
            Picker.Visible = false;

            // ── the FOOT: each button sits under the sector it affects (maintainer) ─────────
            // ⚠ Medium, not Default: BtnW is 132 and that IS the Medium plate's width. A
            // Default button is 168 wide and a row would overlap itself by 36 per button.
            const int BtnW = 132, BtnGap = 6;
            int bx = gridLeft;
            UIButton Foot(string text, Action<UIButton> click, ButtonStyle style = ButtonStyle.Medium)
            {
                UIButton b = Button(style, bx, footY, text, click: click);
                bx += BtnW + BtnGap;
                return b;
            }

            // under RACE: its own load/save
            Foot("Load Race", OnLoadRaceClicked);
            Foot("Save Race", OnSaveRaceClicked);

            // centred under the TRAITS block: the whole-setup actions
            const int MidBtns = 3;
            int midLeft = gridLeft + SideW + Pad;
            int midW    = gridRight - SideW - Pad - midLeft;
            bx = midLeft + (midW - (MidBtns * BtnW + (MidBtns - 1) * BtnGap)) / 2;
            Foot("Load Setup", OnLoadSetupClicked);
            Foot("Save Setup", OnSaveSetupClicked);
            SelectOpponentsBtn = Foot("", OnSelectOpponentsClicked);

            // right end: Exit cancels (hostile), Engage commits (active blue) - maintainer call.
            // ⚠ the Wide styles are PAINTED, so their width is whatever we set.
            UIButton exit = Button(ButtonStyle.WideHostile, gridRight - 2 * BtnW - BtnGap, footY, "Exit", click: OnAbortClicked);
            exit.SetAbsSize(BtnW, 24);
            UIButton engage = Button(ButtonStyle.WideActive, gridRight - BtnW, footY, GameText.Engage, click: OnEngageClicked);
            engage.SetAbsSize(BtnW, 24);

            // Rule Options lives in the Galaxy tab it configures
            Button(ButtonStyle.Medium, (int)(galaxyArea.Right - BtnW - 10), (int)(galaxyArea.Bottom - 28),
                   Localizer.Token(GameText.RuleOptions), click: OnRuleOptionsClicked);

            // Clear Traits lives on the Points page and follows its tab
            ClearTraitsBtn = Button(ButtonStyle.Medium, (int)(description.X + 10), (int)(description.Bottom - 28),
                                    "Clear Traits", click: OnClearClicked);

            DoRaceDescription();
            SetRacialTraits(SelectedData.Traits);

            // the environment preferences hold their standing tab at the head of row 1;
            // the tab frame is the delimiter, the panel draws no background of its own
            RectF envArea = EnvTab.ClientArea;
            var envRect = new Rectangle((int)envArea.X, (int)envArea.Y, (int)envArea.W, (int)envArea.H);
            EnvMenu = Add(new EnvPreferencesPanel(this, RaceSummary, envRect));

            // Ludoal fork: no slide-in/slide-out on this screen (maintainer decision) - the panels
            // appear where they belong.

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

        UITextEntry AddSplitter(UIList list, string title, string inputText, float width)
        {
            const float splitAt = 205f;
            var label = new UILabel(LocalizedText.Parse(title), Fonts.Arial14Bold, Color.BurlyWood);
            var input = new UITextEntry(Vector2.Zero, Fonts.Arial14Bold, inputText)
            {
                Width = width - splitAt,
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

        void OnSelectOpponentsClicked(UIButton b)
        {
            ScreenManager.AddScreen(new SelectOpponentsScreen(this, P, SelectedData));
        }

        void OnAbortClicked(UIButton b)
        {
            ExitScreen();
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
            SelectOpponentsBtn.Visible = P.NumOpponents != maxOpponents;
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
            UpdateSelectedOpponentsButton();
            base.Update(fixedDeltaTime);
        }

        void UpdateSelectedOpponentsButton()
        {
            if (P.SelectedOpponents.Count > 0)
            {
                SelectOpponentsBtn.Style = ButtonStyle.Military;
                SelectOpponentsBtn.Text = $"Select Opponents ({P.SelectedOpponents.Count}/{P.NumOpponents})";
            }
            else
            {
                SelectOpponentsBtn.Style = ButtonStyle.BigDip;
                SelectOpponentsBtn.Text = $"Select Opponents";
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // Ludoal fork: an OPAQUE veil (maintainer decision). This screen stays IsPopup so the
            // main menu underneath keeps being drawn - remove that and this one draws on nothing -
            // but the menu's own buttons and the Jupiter backdrop were reading straight through
            // the panels. At 2/3 the veil never covered them; at full it does, and the screen
            // reads as its own rather than as a layer over the menu.
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha);
            int numSystems = GetSystemsNum();
            NumSystemsLabel.Text = $"Solar Systems: {numSystems}";
            ShowPerformanceWarning(numSystems);
            ShowExtraPlanetsNum(P.ExtraPlanets);

            batch.SafeBegin();
            // ⚠ the frame goes FIRST, before base.Draw: it is painted by hand, not added as a
            // child, so drawing it after would bury every tab and list on the screen.
            Frame.DrawFill(batch, ScreenFrame);
            Frame.Draw(batch);
            // the window title font, the one Colony and every popup uses - not Laserian
            string screenTitle = Localizer.Token(GameText.DesignYourRace);
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

        void ShowPerformanceWarning(int numSystems)
        {
            PerformanceWarning.Visible = numSystems >= 100;
            if (numSystems >= 200)
            {
                PerformanceWarning.Color = NumSystemsLabel.Color = Color.Orange;
                PerformanceWarning.Text = "Warning, performance issues are expected mid to late game.";
            }
            else if (numSystems >= 100)
            {
                PerformanceWarning.Color = NumSystemsLabel.Color = Color.Yellow;
                PerformanceWarning.Text = "Warning, you might experience performance issues late game.";

            }
            else
            {
                NumSystemsLabel.Color = Color.SteelBlue;
            }
        }

        class SelectedTraitsSummary : UIElementV2
        {
            readonly RaceDesignScreen Screen;
            readonly Graphics.Font Font;
            public SelectedTraitsSummary(RaceDesignScreen screen)
            {
                Screen = screen;
                // ⚠ a size up (maintainer): this is a tab of its own now with a full column to
                // itself, so it no longer has to squeeze under the description.
                Font = screen.Narrow ? Fonts.Arial12Bold
                                     : screen.Tall ? Fonts.Arial20Bold : Fonts.Arial14Bold;
            }

            public override bool HandleInput(InputState input)
            {
                return false;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                if (!Visible)
                    return;

                // Ludoal fork: this used to trail BELOW the description, both being visible at
                // once. They are two tabs over one area now, so the summary starts at the top of
                // that area rather than wherever the description happened to end.
                RectF area = Screen.InfoTab.ClientArea;
                var r = new Vector2(area.X + 20, area.Y + 12);
                string title = Localizer.Token(GameText.PointsToSpend);
                batch.DrawString(Font, $"{title}: {Screen.TotalPointsUsed}", r, Color.White);
                r.Y += (Font.LineSpacing + 8);
                Vector2 cursor = r;

                // ⚠ ONE column (maintainer): the negative traits used to start a second column to
                // the right, which is what kept the font small. The tab has a whole column to
                // itself now, so the list simply runs down it.
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

    public enum ExtraRemnantPresence
    {
        VeryRare,Rare, Normal, More, MuchMore, Everywhere
    }
}
