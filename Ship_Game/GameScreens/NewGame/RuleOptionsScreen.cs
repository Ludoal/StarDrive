using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using Ship_Game.Universe;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game;

// Ludoal fork: a PopupWindow rather than a GameScreen holding a Menu2 - it is modal, it has a
// title and a close cross, so it wears the same frame as Options and the Codex now.
public sealed class RuleOptionsScreen : PopupWindow
{
    readonly UniverseParams P;

    FloatSlider FTLPenaltySlider;
    FloatSlider EnemyFTLPenaltySlider;
    FloatSlider GravityWellSize;
    FloatSlider ExtraPlanets;
    FloatSlider IncreaseMaintenance;
    FloatSlider StartingRichness;
    FloatSlider TurnTimer;
    FloatSlider CustomMineralDecay;
    FloatSlider VolcanicActivity;

    // 720x580: the width Options uses, and the height this screen's slider column always had.
    public RuleOptionsScreen(GameScreen parent, UniverseParams settings) : base(parent, 720, 580)
    {
        P = settings;
        TitleText = Localizer.Token(GameText.AdvancedRuleOptions);
        TransitionOnTime  = 0.25f;
        TransitionOffTime = 0.25f;
    }

    // Ludoal fork: whatever the player leaves this panel on becomes the default ruleset of the
    // next new game, so a house ruleset is entered once instead of at every start. Captured on
    // the way out, which covers the close button and Escape alike - the sliders write straight
    // into P as they move, so P is already the final answer by the time we get here.
    public override void ExitScreen()
    {
        // Leaving the panel on the stock ruleset means "no house rules" - saving it would make
        // the Reset button undo itself the moment the panel closes.
        var stock = new UniverseParams();
        if (P.FTLModifier == stock.FTLModifier
            && P.EnemyFTLModifier == stock.EnemyFTLModifier
            && P.GravityWellRange == stock.GravityWellRange
            && P.ExtraPlanets == stock.ExtraPlanets
            && P.ShipMaintenanceMultiplier == stock.ShipMaintenanceMultiplier
            && P.StartingPlanetRichnessBonus == stock.StartingPlanetRichnessBonus
            && P.TurnTimer == stock.TurnTimer
            && P.CustomMineralDecay == stock.CustomMineralDecay
            && P.VolcanicActivity == stock.VolcanicActivity
            && P.PreventFederations == stock.PreventFederations
            && P.FixedPlayerCreditCharge == stock.FixedPlayerCreditCharge
            && P.AIUsesPlayerDesigns == stock.AIUsesPlayerDesigns
            && P.DisablePirates == stock.DisablePirates
            && P.DisableRemnantStory == stock.DisableRemnantStory
            && P.DisableAlternateAITraits == stock.DisableAlternateAITraits
            && P.DisableResearchStations == stock.DisableResearchStations
            && P.DisableMiningOps == stock.DisableMiningOps
            && P.UseUpkeepByHullSize == stock.UseUpkeepByHullSize
            && P.UseLegacyEspionage == stock.UseLegacyEspionage)
        {
            GlobalStats.ClearSavedRuleOptions();
            base.ExitScreen();
            return;
        }

        GlobalStats.RuleFTLModifier = P.FTLModifier;
        GlobalStats.RuleEnemyFTLModifier = P.EnemyFTLModifier;
        GlobalStats.RuleGravityWellRange = P.GravityWellRange;
        GlobalStats.RuleExtraPlanets = P.ExtraPlanets;
        GlobalStats.RuleShipMaintenanceMultiplier = P.ShipMaintenanceMultiplier;
        GlobalStats.RuleStartingPlanetRichnessBonus = P.StartingPlanetRichnessBonus;
        GlobalStats.RuleTurnTimer = P.TurnTimer;
        GlobalStats.RuleCustomMineralDecay = P.CustomMineralDecay;
        GlobalStats.RuleVolcanicActivity = P.VolcanicActivity;

        GlobalStats.RulePreventFederations = P.PreventFederations;
        GlobalStats.RuleFixedPlayerCreditCharge = P.FixedPlayerCreditCharge;
        GlobalStats.RuleAIUsesPlayerDesigns = P.AIUsesPlayerDesigns;
        GlobalStats.RuleDisablePirates = P.DisablePirates;
        GlobalStats.RuleDisableRemnantStory = P.DisableRemnantStory;
        GlobalStats.RuleDisableAlternateAITraits = P.DisableAlternateAITraits;
        GlobalStats.RuleDisableResearchStations = P.DisableResearchStations;
        GlobalStats.RuleDisableMiningOps = P.DisableMiningOps;
        GlobalStats.RuleUseUpkeepByHullSize = P.UseUpkeepByHullSize;
        GlobalStats.RuleUseLegacyEspionage = P.UseLegacyEspionage;
        GlobalStats.RulesCustomised = true;
        GlobalStats.SaveSettings();
        base.ExitScreen();
    }

    public override void Draw(SpriteBatch batch, DrawTimes elapsed)
    {
        // base.Draw opens and closes its own batch (PopupWindow draws the frame there), and this
        // screen adds nothing of its own on top - its sliders and boxes are all children.
        ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
        base.Draw(batch, elapsed);
    }

    public override void LoadContent()
    {
        // ⚠ base.LoadContent() lays the frame out and calls RemoveAll(): everything below is
        // added AFTER it, or it would be discarded. The frame supplies the title and the cross.
        base.LoadContent();

        // the window's own rect is the anchor now, centred by PopupWindow rather than computed
        // from a chain of screen fractions that shifted with every resolution
        Rectangle leftRect = Rect;
        int x = leftRect.X + 60;

        var ftlRect = new Rectangle(x, leftRect.Y + 100, 270, 50);
        FTLPenaltySlider = Add(new FloatSlider(SliderStyle.Percent, ftlRect,
                                               GameText.InsystemFtlSpeedModifier, 0.1f, 1f, P.FTLModifier));
        FTLPenaltySlider.OnChange = (s) => P.FTLModifier = s.AbsoluteValue;

        var eftlRect = new Rectangle(x, leftRect.Y + 150, 270, 50);
        EnemyFTLPenaltySlider = Add(new FloatSlider(SliderStyle.Percent, eftlRect, 
                                                    GameText.InsystemEnemyFtlSpeedModifier, 0.1f, 1f, P.EnemyFTLModifier));
        EnemyFTLPenaltySlider.OnChange = (s) => P.EnemyFTLModifier = s.AbsoluteValue;
            
        // the second column, measured from the FRAME rather than from the screen: at 1440 the old
        // ScreenWidth/4.5 gave 320, which started before the 270-wide sliders at X+60 had ended,
        // and it moved with the display while the panel no longer does. 280 clears them.
        const int indent = 280;
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*0, () => P.PreventFederations, title: GameText.PreventAiFederations, tooltip: GameText.PreventsAiEmpiresFromMerging);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*1, () => P.FixedPlayerCreditCharge, title: GameText.FixedShipAndBuildingsCost, tooltip: GameText.KeepFixedCreditCostOf);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*2, () => P.AIUsesPlayerDesigns, title: GameText.UsePlayerDesignsTitle, tooltip: GameText.UsePlayerDesignsTip);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*3, () => P.DisablePirates, title: GameText.DisablePirates, tooltip: GameText.DisablesAllPirateFactionsFor);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*4, () => P.DisableRemnantStory, title: GameText.DisableRemnantStory, tooltip: GameText.IfCheckedRemnantForcesIn);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*5, () => P.DisableAlternateAITraits, title: GameText.DisableAlternateTraits, tooltip: GameText.DisableAlternateTraitsTip);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*6, () => P.DisableResearchStations, title: GameText.DisableResearchStationsName, tooltip: GameText.DisableResearchStationsTip);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*7, () => P.DisableMiningOps, title: GameText.DisableMiningOpsName, tooltip: GameText.DisableMiningOpsTip);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*8, () => P.UseUpkeepByHullSize, title: GameText.RuleOptionsUseHullUpkeepName, tooltip: GameText.RuleOptionsUseHullUpkeepTip);
        Checkbox(ftlRect.X + indent, ftlRect.Y + 25*9, () => P.UseLegacyEspionage, title: GameText.UseLegacyEspionage, tooltip: GameText.UseLegacyEspionageTip);

        var mdRect = new Rectangle(ftlRect.X + indent+2, ftlRect.Y + 250, 270, 50);
        CustomMineralDecay = SliderDecimal1(mdRect, GameText.MineralDecayRate, 0.2f, 3, P.CustomMineralDecay);
        CustomMineralDecay.OnChange = (s) => P.CustomMineralDecay = (s.AbsoluteValue).RoundToFractionOf10();

        var vaRect = new Rectangle(ftlRect.X + indent + 2, ftlRect.Y + 310, 270, 50);
        VolcanicActivity = SliderDecimal1(vaRect, GameText.VolcanicActivity, 0.5f, 3, P.VolcanicActivity);
        VolcanicActivity.OnChange = (s) => P.VolcanicActivity = (s.AbsoluteValue).RoundToFractionOf10();

        var gwRect = new Rectangle(x, leftRect.Y + 210, 270, 50);
        var epRect = new Rectangle(x, leftRect.Y + 270, 270, 50);
        var richnessRect = new Rectangle(x, leftRect.Y + 330, 270, 50);

        GravityWellSize = Slider(gwRect, GameText.GravityWellRadius, 4000, 16000, P.GravityWellRange);
        GravityWellSize.OnChange = (s) => P.GravityWellRange = s.AbsoluteValue;

        ExtraPlanets = Slider(epRect, GameText.ExtraPlanets, 0, 3f, P.ExtraPlanets);
        ExtraPlanets.OnChange = (s) => P.ExtraPlanets = (int)s.AbsoluteValue;

        StartingRichness = Slider(richnessRect, GameText.StartingPlanetRichnessBonus, 0, 5f, P.StartingPlanetRichnessBonus);
        StartingRichness.OnChange = (s) => P.StartingPlanetRichnessBonus = s.AbsoluteValue;


        var optionTurnTimer  = new Rectangle(x, leftRect.Y + 390, 270, 50);
        var maintenanceRect  = new Rectangle(x, leftRect.Y + 450, 270, 50);

        TurnTimer = Slider(optionTurnTimer,  GameText.SecondsPerTurn, 4, 10f, P.TurnTimer);
        TurnTimer.OnChange = (s) => P.TurnTimer = (int)s.AbsoluteValue;

        IncreaseMaintenance = SliderDecimal1(maintenanceRect,  GameText.MaintenanceMultiplier, 1, 2, P.ShipMaintenanceMultiplier);
        IncreaseMaintenance.OnChange = (s) => P.ShipMaintenanceMultiplier = s.AbsoluteValue.RoundToFractionOf10();

        EnemyFTLPenaltySlider.Tip = GameText.UsingThisSliderYouCan2;
        CustomMineralDecay.Tip = GameText.HigherMineralDecayIncreasesThe;
        VolcanicActivity.Tip = GameText.ThisWillControlTheChances;
        FTLPenaltySlider.Tip = GameText.UsingThisSliderYouCan;
        GravityWellSize.Tip = GameText.DefinesTheRadiusOfPlanetary;

        string extraPlanetsTip = Localizer.Token(GameText.AddExtraPlanetsToEach);
        if (GlobalStats.Defaults.ChangeResearchCostBasedOnSize)
            extraPlanetsTip = $"{extraPlanetsTip} {Localizer.Token(GameText.ThisWillSlightlyIncreaseResearch)}";

        ExtraPlanets.Tip = extraPlanetsTip;
        IncreaseMaintenance.Tip = GameText.MultiplyGlobalMaintenanceCostBy;
        StartingRichness.Tip = GameText.AddToAllStartingEmpire;
        TurnTimer.Tip = GameText.TimeInSecondsPerTurn;


        // Ludoal fork: back to the game's own defaults, and clear the saved ruleset with them -
        // otherwise the next new game would restore what the player just reset. Rebuilding the
        // screen is what moves the sliders: they read their value at construction.
        Button(ButtonStyle.Default, leftRect.X + 40, leftRect.Y + leftRect.Height - 60,
               "Reset", b =>
        {
            var stock = new UniverseParams();
            P.FTLModifier = stock.FTLModifier;
            P.EnemyFTLModifier = stock.EnemyFTLModifier;
            P.GravityWellRange = stock.GravityWellRange;
            P.ExtraPlanets = stock.ExtraPlanets;
            P.ShipMaintenanceMultiplier = stock.ShipMaintenanceMultiplier;
            P.StartingPlanetRichnessBonus = stock.StartingPlanetRichnessBonus;
            P.TurnTimer = stock.TurnTimer;
            P.CustomMineralDecay = stock.CustomMineralDecay;
            P.VolcanicActivity = stock.VolcanicActivity;
            P.PreventFederations = stock.PreventFederations;
            P.FixedPlayerCreditCharge = stock.FixedPlayerCreditCharge;
            P.AIUsesPlayerDesigns = stock.AIUsesPlayerDesigns;
            P.DisablePirates = stock.DisablePirates;
            P.DisableRemnantStory = stock.DisableRemnantStory;
            P.DisableAlternateAITraits = stock.DisableAlternateAITraits;
            P.DisableResearchStations = stock.DisableResearchStations;
            P.DisableMiningOps = stock.DisableMiningOps;
            P.UseUpkeepByHullSize = stock.UseUpkeepByHullSize;
            P.UseLegacyEspionage = stock.UseLegacyEspionage;
            GlobalStats.ClearSavedRuleOptions();
            LoadContent();
        });

        // the heading is the frame's title now - only the explanatory line stays, tucked under
        // the title bar rather than under a heading this screen drew for itself
        string text = Fonts.Arial12.ParseText(GameText.InThisPanelYouMay, leftRect.Width - 80);
        Label(leftRect.X + 40, PopupFrame.ContentTop(leftRect) + 4, text, Fonts.Arial12);
    }
}