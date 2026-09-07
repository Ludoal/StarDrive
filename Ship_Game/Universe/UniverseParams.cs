using SDGraphics;
using SDUtils;
using Ship_Game.Data.Serialization;
using static Ship_Game.RaceDesignScreen;

namespace Ship_Game.Universe;

[StarDataType]
public class UniverseParams
{
    // this is only used during first time universe generation and shouldn't be serialized
    public EmpireData PlayerData;

    // Universe Generator parameters:
    [StarData(DefaultValue=GameDifficulty.Normal)]
    public GameDifficulty Difficulty = GameDifficulty.Normal;
    
    [StarData(DefaultValue=StarsAbundance.Normal)]
    public StarsAbundance StarsCount = StarsAbundance.Normal;
    
    [StarData(DefaultValue=GalSize.Medium)]
    public GalSize GalaxySize = GalSize.Medium;
    
    [StarData(DefaultValue=ExtraRemnantPresence.Normal)]
    public ExtraRemnantPresence ExtraRemnant = ExtraRemnantPresence.Normal;

    // Ludoal fork (maintainer feedback): how fast the Remnants gain power, beside the setting for
    // their NUMBER - the complaint from new players is not how many there are, it is that they
    // grow dangerous too quickly.
    [StarData(DefaultValue=RemnantPaceSetting.Normal)]
    public RemnantPaceSetting RemnantPace = RemnantPaceSetting.Normal;

    // ...and a way back to the base game's remnant strength when a mod has raised it. Shown only
    // when the loaded mod actually changed the value, so it never appears in vanilla.
    // ⚠ OBSOLETE, kept READABLE for older saves - the setting is now the three positions below.
    // Never written; OnDeserialized translates a true into the matching notch.
    [StarData] public bool VanillaRemnantStrength;

    [StarData(DefaultValue=RemnantStrengthSetting.Default)]
    public RemnantStrengthSetting RemnantStrength = RemnantStrengthSetting.Default;

    public Array<IEmpireData> SelectedOpponents = new();

    [StarData] public int NumSystems;
    [StarData] public int NumOpponents;
    [StarData] public int RacialTraitPoints;
    [StarData] public GameMode Mode = GameMode.Sandbox;
    [StarData(DefaultValue=1f)] public float Pace = 1f;
    [StarData(DefaultValue=1f)] public float StarsModifier = 1f;

    // Universe customization parameters:
    [StarData] public int TurnTimer; // seconds between Empire turns, every turn advances stardate by 0.1
    [StarData] public bool PreventFederations;
    [StarData] public bool EliminationMode;
    [StarData] public float CustomMineralDecay;
    [StarData] public float VolcanicActivity;
    [StarData] public float ShipMaintenanceMultiplier;
    [StarData] public bool AIUsesPlayerDesigns;
    [StarData] public bool UseUpkeepByHullSize;
    // Ludoal fork: SAVE BALLAST - the legacy espionage system is gone; the field remains so
    // saves written under that rule still deserialize. Never set, never read.
    [StarData] public bool UseLegacyEspionage;
    [StarData] public float StartingPlanetRichnessBonus;

    // in-system FTL modifier is the BASE FTL modifier when ships are inside solar systems
    const float DefaultInSystemFTLModifier = 1f;

    [StarData(DefaultValue=DefaultInSystemFTLModifier)]
    public float FTLModifier = DefaultInSystemFTLModifier;
    
    // if within enemy projector range, then this BASE FTL modifier is used
    const float DefaultEnemyFTLModifier = 0.5f;

    [StarData(DefaultValue=DefaultEnemyFTLModifier)]
    public float EnemyFTLModifier = DefaultEnemyFTLModifier;


    // configured gravity wells for this game, if 0, then gravity wells are disabled
    [StarData] public float GravityWellRange;
    [StarData] public int ExtraPlanets;

    // persistent toggle flags for different checkboxes
    [StarData(DefaultValue=true)] public bool PlanetsScreenHideInhospitable = true;
    [StarData(DefaultValue=true)] public bool DisableInhibitionWarning = true;
    [StarData(DefaultValue=false)] public bool EnableStarvationWarning = false;
    // Ludoal fork (maintainer feedback): the Automation tab lists DISABLES -
    // same [StarData] flag read in the negative, not serialized itself. Default: disabled.
    public bool DisableStarvationWarning { get => !EnableStarvationWarning; set => EnableStarvationWarning = !value; }
    [StarData(DefaultValue=true)] public bool AllowPlayerInterTrade  = true;
    [StarData] public bool SuppressOnBuildNotifications;
    [StarData] public bool PlanetScreenHideOwned;
    [StarData] public bool ShipListFilterPlayerShipsOnly;
    [StarData] public bool ShipListFilterInFleetsOnly;
    [StarData] public bool ShipListFilterNotInFleets;
    [StarData] public bool CordrazinePlanetCaptured;
    [StarData] public bool DisableVolcanoWarning;
    [StarData] public bool DisableCrashSiteWarning;
    [StarData] public bool PrioitizeProjectors;  // superseded by ConstructionPriorities - kept so old saves load, nothing reads it
    [StarData] public bool PrioritizeFreighters; // superseded by ConstructionPriorities - kept so old saves load, nothing reads it
    // Ludoal fork (maintainer feedback): the ORDERED construction priority list. Categories in
    // it jump the colony build queues at insertion, best rank first, FIFO within a category.
    [StarData] public Array<string> ConstructionPriorities = new();

    // Ludoal fork (maintainer feedback): the player's taps on the economy.
    // GovernorSpendingRatio throttles what the governors may spend of their AUTO allocations
    // (manual overrides bypass it - an explicit order is not throttled). The three SHARES
    // are a linked split summing to 1: how the pooled governor budget divides between the
    // areas (the Budget screen keeps them linked).
    [StarData] public float GovernorSpendingRatio = 1f;
    [StarData(DefaultValue = true)] public bool AutoBudgetShares = true;   // locked on the default split below
    [StarData] public float ColonyBudgetShare = 0.55f;
    [StarData] public float DefenseBudgetShare = 0.25f;
    [StarData] public float SSPBudgetShare = 0.20f;
    [StarData(DefaultValue=true)] public bool ShowAllDesigns = true;
    [StarData] public bool FilterOldModules;

    // (Rework screen toggles live in GlobalStats, not here: which interface a player prefers
    // is a player preference, not a property of one game.)

    // ⚠ OBSOLETE, kept READABLE for older saves - the checkbox is now the Off notch of
    // RemnantPace. Never written; OnDeserialized translates a true into Off.
    [StarData] public bool DisableRemnantStory;

    // the one place that knows Off means "no story", so the three readers do not each carry it
    public bool NoRemnantStory => RemnantPace == RemnantPaceSetting.Off;
    [StarData] public bool EnableRandomizedAIFleetSizes;
    [StarData] public bool DisableAlternateAITraits;
    // ⚠ OBSOLETE, kept READABLE for older saves - the checkbox is now the notches below.
    // Never written; OnDeserialized translates a true into None.
    [StarData] public bool DisablePirates;

    // ⚠ OBSOLETE, kept READABLE for older saves - the rank is now a NAME below.
    // Never written; OnDeserialized folds it into the new choice.
    [StarData(DefaultValue=PirateFactionsSetting.All)]
    public PirateFactionsSetting PirateFactions = PirateFactionsSetting.All;

    // Ludoal fork (maintainer feedback): WHICH pirate factions start alive, by name. The three
    // reserved words are the only values that are not a faction name; anything else is one,
    // matched against EmpireData.Name. A name that does not resolve - a mod removed, a save
    // carried to another install - falls back to All rather than silently emptying the galaxy.
    // ⚠ a STRING and a new field, never the old enum retyped: changing the type of a [StarData]
    // field does not throw on load, it reads as null.
    public const string PirateChoiceNone   = "None";
    public const string PirateChoiceAll    = "All";
    public const string PirateChoiceRandom = "Random";

    [StarData(DefaultValue=PirateChoiceAll)]
    public string PirateFactionChoice = PirateChoiceAll;

    [StarData(DefaultValue=PiratePaceSetting.Normal)]
    public PiratePaceSetting PiratePace = PiratePaceSetting.Normal;

    // ⚠ OBSOLETE, kept READABLE for older saves - the notch is now PirateStrength below.
    // Never written; OnDeserialized folds it into the new scale.
    [StarData(DefaultValue=PirateTributeSetting.Normal)]
    public PirateTributeSetting PirateTribute = PirateTributeSetting.Normal;

    // Ludoal fork (maintainer feedback): one notch for what the pirates ARE. It carries the
    // tribute plus the two things the base game never let a player touch - the level they start
    // at, and the fraction of the local defence a raid aims for.
    [StarData(DefaultValue=PirateStrengthSetting.Default)]
    public PirateStrengthSetting PirateStrength = PirateStrengthSetting.Default;
    [StarData] public bool FixedPlayerCreditCharge;
    [StarData] public bool DisableResearchStations;
    [StarData] public bool DisableMiningOps;

    public bool DebugDisableShipLaunch; // Only for testing

    public UniverseParams()
    {
        // initialize defaults from Settings
        var s = GlobalStats.Defaults;

        NumOpponents = s.DefaultNumOpponents.UpperBound(ResourceManager.MajorRaces.Count - 1);
        RacialTraitPoints = s.TraitPoints;
        TurnTimer = s.TurnTimer;
        CustomMineralDecay = s.CustomMineralDecay;
        VolcanicActivity = s.VolcanicActivity;
        ShipMaintenanceMultiplier = s.ShipMaintenanceMultiplier;
        AIUsesPlayerDesigns = s.AIUsesPlayerDesigns;
        UseUpkeepByHullSize = s.UseUpkeepByHullSize;
        StartingPlanetRichnessBonus = s.StartingPlanetRichnessBonus;
        GravityWellRange = s.GravityWellRange;
        // a mod may declare the story off by default - that is the Off notch now
        if (s.DisableRemnantStory)
            RemnantPace = RemnantPaceSetting.Off;
        // a mod may declare piracy off by default - that is the None notch now
        if (s.DisablePirates)
            PirateFactionChoice = PirateChoiceNone;
        EnableRandomizedAIFleetSizes = s.EnableRandomizedAIFleetSizes;
    }

    // Ludoal fork: overlay the Rule Options the player last set up, so a new game starts on
    // their house ruleset instead of the stock one. Called only when the setup screen builds a
    // fresh params object - a save carries its own rules and must never be touched by this.
    // Each value is only applied if it was actually customised, so an untouched install keeps
    // every stock default.
    public void ApplySavedRuleOptions()
    {
        if (GlobalStats.RuleFTLModifier >= 0f) FTLModifier = GlobalStats.RuleFTLModifier;
        if (GlobalStats.RuleEnemyFTLModifier >= 0f) EnemyFTLModifier = GlobalStats.RuleEnemyFTLModifier;
        if (GlobalStats.RuleGravityWellRange >= 0f) GravityWellRange = GlobalStats.RuleGravityWellRange;
        if (GlobalStats.RuleExtraPlanets >= 0) ExtraPlanets = GlobalStats.RuleExtraPlanets;
        if (GlobalStats.RuleShipMaintenanceMultiplier >= 0f) ShipMaintenanceMultiplier = GlobalStats.RuleShipMaintenanceMultiplier;
        if (GlobalStats.RuleStartingPlanetRichnessBonus >= 0f) StartingPlanetRichnessBonus = GlobalStats.RuleStartingPlanetRichnessBonus;
        if (GlobalStats.RuleTurnTimer >= 0) TurnTimer = GlobalStats.RuleTurnTimer;
        if (GlobalStats.RuleCustomMineralDecay >= 0f) CustomMineralDecay = GlobalStats.RuleCustomMineralDecay;
        if (GlobalStats.RuleVolcanicActivity >= 0f) VolcanicActivity = GlobalStats.RuleVolcanicActivity;

        if (GlobalStats.RulesCustomised)
        {
            PreventFederations = GlobalStats.RulePreventFederations;
            FixedPlayerCreditCharge = GlobalStats.RuleFixedPlayerCreditCharge;
            AIUsesPlayerDesigns = GlobalStats.RuleAIUsesPlayerDesigns;
            DisableAlternateAITraits = GlobalStats.RuleDisableAlternateAITraits;
            DisableResearchStations = GlobalStats.RuleDisableResearchStations;
            DisableMiningOps = GlobalStats.RuleDisableMiningOps;
            UseUpkeepByHullSize = GlobalStats.RuleUseUpkeepByHullSize;
        }
    }

    [StarDataDeserialized]
    public void OnDeserialized()
    {
        // BUGFIX: if FTL modifiers become 0, then reset the defaults,
        //         because if they are 0, the game would break
        if (FTLModifier == 0f) FTLModifier = DefaultInSystemFTLModifier;
        if (EnemyFTLModifier == 0f) EnemyFTLModifier = DefaultEnemyFTLModifier;

        // Only TRUE says anything: the retired checkbox was the one way to ask for the base game
        // value and nothing writes it, so a true can only come from an older save. It folds onto
        // the closest notch of the scale that replaced it - under a mod that doubles the value it
        // lands on Half; under one that raises nothing, on Default.
        if (VanillaRemnantStrength)
        {
            float mod = GlobalStats.Defaults.RemnantDesignStrMultiplier;
            float ratio = mod > 0 ? UniverseState.VanillaRemnantDesignStr / mod : 1f;
            RemnantStrength = ratio <= 0.375f ? RemnantStrengthSetting.Quarter
                            : ratio <= 0.625f ? RemnantStrengthSetting.Half
                            : ratio <= 0.875f ? RemnantStrengthSetting.ThreeQuarters
                            : RemnantStrengthSetting.Default;
        }

        if (DisableRemnantStory)
            RemnantPace = RemnantPaceSetting.Off;

        if (DisablePirates)
            PirateFactions = PirateFactionsSetting.None;

        // The old rank notch folds into the name scale. Only a value OTHER than All says
        // anything, for the same reason as the tribute below: nothing writes the enum any more,
        // so on a new save it sits at its default and must not overwrite the chosen faction.
        if (PirateFactions != PirateFactionsSetting.All)
        {
            PirateFactionChoice = PirateFactions == PirateFactionsSetting.None
                                ? PirateChoiceNone
                                : PirateChoiceRandom;
        }

        // The old tribute notch folds into the strength scale that replaced it, onto the notch
        // asking for the same money. Only a value OTHER than Normal says anything: nothing
        // writes this field any more, so on a new save it sits at its default and must not
        // speak - an unconditional fold would overwrite the notch the player just chose.
        // ⚠ a loaded game gains the new bite along with the tribute it asked for; the starting
        // level does not apply, it is spent at galaxy creation and never re-runs.
        if (PirateTribute != PirateTributeSetting.Normal)
        {
            PirateStrength = PirateTribute switch
            {
                PirateTributeSetting.Low      => PirateStrengthSetting.Weak,
                PirateTributeSetting.High     => PirateStrengthSetting.Strong,
                PirateTributeSetting.VeryHigh => PirateStrengthSetting.Brutal,
                _                             => PirateStrengthSetting.Default,
            };
        }

        // ★ folded once, the retired fields go back to their defaults: the next save does not
        // carry them and the folds above do not run again. None has a reader outside this file.
        VanillaRemnantStrength = false;
        DisableRemnantStory    = false;
        DisablePirates         = false;
        PirateFactions         = PirateFactionsSetting.All;
        PirateTribute          = PirateTributeSetting.Normal;
    }

    // Ludoal fork (maintainer feedback): the setup as one block of text, written into the save
    // HEADER so the load list can answer "what game is this?" without opening megabytes of
    // universe - the load screen reads headers only, by design.
    // The lines follow the creation screen's own order, one per group. The rules line lists only
    // what departs from the defaults and is dropped when nothing does, so a standard game reads
    // as four short lines rather than a wall of "Normal".
    public string SetupSummary()
    {
        string s = $"Galaxy: {GalaxySize}, {NumSystems} systems, {NumOpponents} opponents, {Mode}, {Pace:0.##}x, {Difficulty}";
        s += $"\nRemnant: Presence {ExtraRemnant}, Pace {RemnantPace}, Strength {RemnantStrength}";
        s += $"\nPirates: {PirateFactionChoice}, Pace {PiratePace}, Strength {PirateStrength}";

        string rules = "";
        void Rule(string r) { rules += rules.Length > 0 ? ", " + r : r; }
        if (PreventFederations)           Rule("no federations");
        if (EliminationMode)              Rule("elimination");
        if (AIUsesPlayerDesigns)          Rule("AI uses player designs");
        if (UseUpkeepByHullSize)          Rule("upkeep by hull size");
        if (EnableRandomizedAIFleetSizes) Rule("randomized AI fleets");
        if (DisableAlternateAITraits)     Rule("no alternate AI traits");
        if (FixedPlayerCreditCharge)      Rule("fixed credit charge");
        if (DisableResearchStations)      Rule("no research stations");
        if (DisableMiningOps)             Rule("no mining ops");
        if (!AllowPlayerInterTrade)       Rule("no player inter-empire trade");
        if (ExtraPlanets > 0)             Rule($"{ExtraPlanets} extra planets");
        if (rules.Length > 0)
            s += "\nRules: " + rules;

        string mod = GlobalStats.HasMod ? (GlobalStats.ModName + " " + GlobalStats.ModVersion).Trim() : "none";
        s += $"\nMod: {mod}, build {GlobalStats.Version.Split(' ')[0]}";
        return s;
    }
}
