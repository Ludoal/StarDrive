using SDGraphics;
using SDUtils;
using Ship_Game.Data.Serialization;
using System;

namespace Ship_Game
{
    // This is a new Espionage System that uses Infiltration Level instead of Agents
    [StarDataType]
    public class Espionage
    {
        public const byte MaxLevel = 5;
        public const float PercentMoneyLeech = 0.02f;
        public const float SlowResearchBy = 0.1f;
        [StarData] public byte Level;
        [StarData] readonly Empire Owner;
        [StarData] public readonly Empire Them;
        [StarData] public float LevelProgress { get; private set; }
        [StarData] public byte LimitLevel { get; private set; } = MaxLevel;
        [StarData] int Weight = 1; // maintainer: a fresh relation starts weighted, not mute (saves keep their value)
        [StarData] Array<InfiltrationOperation> Operations = new();
        [StarData] Mole StickyMole;
        [StarData] public int SlowResearchChance { get; private set; }
        [StarData] public float TotalMoneyLeeched { get; private set; }
        [StarData] float MoneyLeechedThisTurn;
        [StarData] public int NumPlantedMoles { get; private set; }
        // Ludoal fork (Trends, Wishlist): the StarDate each intel domain was FIRST unlocked
        // at - the curves clip there (espionage does not invent the past). Never cleared: a
        // level that falls back does not close history already taken. 0 = not yet stamped
        // (legacy save or never reached); the Trends screen stamps on open as a fallback.
        [StarData] float[] DomainUnlockDates = new float[4];
        public enum IntelDomain { Population = 0, Military = 1, Economy = 2, Science = 3 }
        public float DomainUnlockDate(IntelDomain d)
            => DomainUnlockDates == null ? 0f : DomainUnlockDates[(int)d];
        // bench 454: stamp ONLY the domains THIS level change just opened. A date of 0 on
        // an already-open domain means "unlocked before the feature existed" and keeps its
        // full history (the maintainer's ruling) - blanket-stamping them dated every open
        // curve "today" at each level change and made them all restart.
        public void StampDomainUnlocks(byte previousLevel)
        {
            DomainUnlockDates ??= new float[4]; // a legacy save deserializes the field null
            float now = Owner?.Universe?.StarDate ?? 0f;
            if (now <= 0f) return;
            void Stamp(int domain, byte requiredLevel)
            {
                if (Level >= requiredLevel && previousLevel < requiredLevel && DomainUnlockDates[domain] == 0f)
                    DomainUnlockDates[domain] = now;
            }
            Stamp(0, 1); // Population
            Stamp(1, 2); // Military
            Stamp(2, 3); // Economy
            Stamp(3, 3); // Science
        }

        [StarDataConstructor]
        public Espionage() { }

        public Espionage(Empire us, Empire them) 
        {
            Owner = us;
            Them  = them;
        }


        // Used for Remnants and Pirates
        public void IncreaseInfiltrationLevelTo(byte value)
        {
            for (byte i = 1; i <= value; i++)
                IncreaseInfiltrationLevel();
        }

        public void IncreaseInfiltrationLevel(bool withMessage = true)
        {
            if (Level == MaxLevel) 
                return;

            byte prevLevel = Level; // captured before the raise
            Level++;
            LevelProgress = 0;
            StampDomainUnlocks(prevLevel); // Trends: a fresh unlock opens its curves from today

            if (!Them.IsFaction && withMessage)
            {
                string message = $"{Them.data.Name}: {Localizer.Token(GameText.MessageInfiltrationLevelIncrease)} {Level}.";
                Owner.Universe.Notifications.AddAgentResult(true, message, Owner);
            }

            EnablePassiveEffects();
            if (!Owner.isPlayer)
                Owner.AI.EspionageManager.Update(forceRun: true);
        }

        public void IncreasePlantedMoleCount()
        {
            NumPlantedMoles++;
        }

        public void DecreasePlantedMoleCount()
        {
            NumPlantedMoles--;
        }

        public void WipeoutInfiltration() => SetInfiltrationLevelTo(0);

        public void ReduceInfiltrationLevel() => SetInfiltrationLevelTo((byte)(Level.LowerBound(1) - 1));

        public void SetInfiltrationLevelTo(byte value)
        {
            byte prevLevel = Level;
            Level = value.LowerBound(0);
            LevelProgress = 0;
            StampDomainUnlocks(prevLevel); // stamps only NEW unlocks; a drop never closes history
            RemoveOperations();
            EnablePassiveEffects();
            if (!Owner.isPlayer)
                Owner.AI.EspionageManager.Update(forceRun: true);
        }

        public void DecreaseProgress(float value)
        {
            if (Level == 0)
                return;

            LevelProgress -= value;
            if (LevelProgress < 0)
                SetInfiltrationLevelTo((byte)(Level-1));
        }

        public void Update(int totalWeight)
        {
            if (GlobalStats.RestrictAIPlayerInteraction && Them.isPlayer)
                return;

            EnablePassiveEffects();
            RemoveOperations();
            float progressToIncrease = GetProgressToIncrease(Owner.EspionagePointsPerTurn, totalWeight);
            int validOps = ValidOps;
            UpdateOperations(validOps > 0 ? progressToIncrease / validOps : 0);

            if (AtLimitLevel)
                return;

            LevelProgress = (LevelProgress + progressToIncrease).UpperBound(LevelCost(MaxLevel));
            if (LevelProgress >= NextLevelCost)
                IncreaseInfiltrationLevel();
        }

        public string RemainingTurnsForOps(InfiltrationOpsType type)
        {
            int remainingTurns =  CalcRemainingTurnsForOps(type);
            string turns = remainingTurns < 10_000 ? "turns" : "ts";
            return remainingTurns > -1 ? $"({HelperFunctions.GetNumberString(remainingTurns)} {turns})" : "(INF)";
        }

        int CalcRemainingTurnsForOps(InfiltrationOpsType type)
        {
            if (Weight == 0 || GetOpsLevel(type) > LimitLevel)
                return -1;

            float progressPerTurn = 0;
            int validOps = ValidOps;
            InfiltrationOperation ops = Operations.Find(o => o.Type == type);
            if (ops != null)
            {
                if (validOps == 0)
                    return -1;

                int totalWeight = Owner.CalcTotalEspionageWeight();
                progressPerTurn = GetProgressToIncrease(Owner.EspionagePointsPerTurn, totalWeight) / validOps;
                return ops.TurnsToComplete(progressPerTurn);
            }
            else if (GetOpsLevel(type) <= LimitLevel) // Checking the first Operation
            {
                int totalWeight = Owner.CalcTotalEspionageWeight(grossWeight: true);
                progressPerTurn = GetProgressToIncrease(Owner.EspionagePointsPerTurn, totalWeight, true) / (validOps + 1);
                if (ActualWeight > 0 && validOps == 0)
                    progressPerTurn *= 0.5f;
                return InfiltrationOperation.BaseRemainingTurns(type, LevelCost(GetOpsLevel(type)), progressPerTurn, Owner.Universe);
            }

            return -1; // ops level over LimitLevel
        }

        int ValidOps => Operations.Count(o => o.Level <= LimitLevel);

        public byte EffectiveLevel => Level.UpperBound(LimitLevel);

        public bool IsCounterEspionageActive => IsOperationActive(InfiltrationOpsType.CounterEspionage);

        float MoleCoverage => (float)(NumPlantedMoles) / Them.GetPlanets().Count.LowerBound(1);
        public bool MoleCoverageReached => MoleCoverage >= Owner.PersonalityModifiers.WantedMoleCovreage;

        void RemoveOperations()
        {
            for (int i = Operations.Count - 1; i >= 0; i--)
            {
                InfiltrationOperation mission = Operations[i];
                if (mission.Level > Level) // not checking actual level since ops above limit level are paused and not removed.
                    Operations.Remove(mission);
            }

            if (!CanPlantStickyMole && StickyMole != null)
            {
                Owner.RemoveMole(StickyMole, Them);
                StickyMole = null;
            }

            if (!CanSlowResearch && SlowResearchChance > 0) 
                SlowResearchChance = 0;
        }

        void UpdateOperations(float progress)
        {
            for (int i = 0; i < Operations.Count; i++)
            {
                InfiltrationOperation operation = Operations[i];
                if (operation.Level <= LimitLevel)
                    operation.Update(progress);
            }
        }

        public void DecreaseSlowResearchChance()
        {
            SlowResearchChance -= 1;
        }

        public void SetSlowResearchChance(int value)
        {
            SlowResearchChance = value;
        }

        public void SetDisruptProjectionChance(int value)
        {
            Them.SetInfluenceDisableChance(value);
        }

        void EnablePassiveEffects()
        {
            if (Level >= 1)
                Them.SetCanBeScannedByPlayer(true); // This ability cannot be lost after it was achieved.

            if (CanPlantStickyMole && StickyMole == null)
            {
                StickyMole = Mole.PlantStickyMoleAtHomeworld(Owner, Them, out Planet targetPlanet);
                if (StickyMole != null)
                {
                    string message = $"{Localizer.Token(GameText.NewSuccessfullyInfiltratedAColony)} {targetPlanet.Name}";
                    Owner.Universe.Notifications.AddAgentResult(true, message, Owner, targetPlanet);
                }
            }
        }

        public float GetProgressToIncrease(float espionagePoints, float totalWeight, bool forFirstOperation = false)
        {
            float activeMissionRatio = !HasOperations
                                       ? 1
                                       : AtLimitLevel ? 1 : 0.5f;
                                       

            if (AtLimitLevel && !HasOperations && !forFirstOperation)
                return 0;

            return espionagePoints
                   * (Weight / totalWeight.LowerBound(1))
                   * (Them.TotalPopBillion / Owner.TotalPopBillion.LowerBound(0.1f))
                   * (1 - Them.EspionageDefenseRatio*0.75f)
                   * activeMissionRatio;
        }

        public void SetWeight(int value)
        {
            Weight = value;
        }

        public void SetLimitLevel(byte value) 
        {
            LimitLevel = value;
        }

        public int ActualWeight => AtLimitLevel && !HasOperations ? 0 : Weight;

        public int LevelCost(byte level)
        {
            // default costs
            // 1 - 75
            // 2 - 150
            // 3 - 300
            // 4 - 600
            // 5 - 1200
            return level == 0 ? 0 : (int)(75 * Math.Pow(2, level-1) * Owner.Universe.SettingsResearchModifier.LowerBound(0.25f) * Owner.Universe.P.Pace);
        }

        public void AddLeechedMoney(float money)
        {
            Owner.AddMoney(money);
            TotalMoneyLeeched += money;
            MoneyLeechedThisTurn += money;
        }

        public float ExtractMoneyLeechedThisTurn()
        {
            float monetLeeched = MoneyLeechedThisTurn;
            MoneyLeechedThisTurn = 0;
            return monetLeeched;
        }

        public int GrossWeight => Weight;
        public int NextLevelCost => LevelCost((byte)(Level+1));

        public bool CanViewPersonality   => Level >= 1;
        public bool CanViewNumPlanets    => Level >= 1;
        public bool CanViewPop           => Level >= 1;
        public bool CanViewTheirTreaties => Level >= 1;

        public bool CanViewNumShips     => Level >= 2;
        public bool CanViewTechType     => Level >= 2;
        public bool CanViewArtifacts    => Level >= 2;
        public bool CanViewRanks        => Level >= 2;
        // a rank unlocks with the DATUM that founds it (maintainer design, Wishlist):
        // the flat CanViewRanks gate leaked one floor up (an economy rank at level 2
        // derives from a treasury that is a level-3 secret) and one floor down (the
        // population rank hid at 2 while the raw count shows at 1).
        public bool CanViewPopRank      => CanViewPop;
        public bool CanViewMilitaryRank => CanViewNumShips;
        public bool CanViewScienceRank  => CanViewResearchTopic; // spec: science strength is a research secret, level 3
        public bool CanViewEconomyRank  => CanViewMoneyAndMaint;
        public bool ProjectorsCanAlert  => EffectiveLevel >= 2;

        public bool CanViewDefenseRatio   => Level >= 3;
        public bool CanViewMoneyAndMaint  => Level >= 3;
        public bool CanViewResearchTopic  => Level >= 3;
        public bool CanViewBonuses        => Level >= 3;
        public bool CanDetectRemnantGifts => Level >= 3;
        bool CanPlantStickyMole           => EffectiveLevel>= 3;

        public bool CanLeechTech    => EffectiveLevel >= 4;
        public bool CanSlowResearch => EffectiveLevel >= 4 && SlowResearchChance > 0;
        public bool CanViewTraitSet => Level >= 4;

        public bool CanLeechMoney     => EffectiveLevel >= 5;
        public bool CanViewTheirMoles => Level >= 5;


        bool AtLimitLevel => Level >= LimitLevel;
        public float ProgressPercent => LevelProgress/NextLevelCost * 100;
        bool HasOperations => Operations.Count > 0;
        public bool WeHaveInfoOnTheirInfiltration => Level >= 2;
        public string InfiltrationLevelSummary()
        {
            if (Level <= 1)
                return Localizer.Token(GameText.EspInfilUnknown);

            int theirInfiltrationLevel = Them.GetRelations(Owner).Espionage.EffectiveLevel;
            if (Level <= 2)
                // "Exist" not "Exists": the label reads "Spies" (plural) now (maintainer bench 336)
                return theirInfiltrationLevel > 0 ? Localizer.Token(GameText.EspInfilExist) : Localizer.Token(GameText.EspInfilProbablyNone);

            if (Level <= 4)
                return theirInfiltrationLevel == 0 ? Localizer.Token(GameText.EspInfilNone)
                                                   : theirInfiltrationLevel > 3 ? Localizer.Token(GameText.EspInfilDeep) : Localizer.Token(GameText.EspInfilShallow);

            return $"{theirInfiltrationLevel}";
        }

        // Raw add: callers must guarantee the op isn't already active. UI checkboxes can't
        // (sim turns change Operations under the open screen), so they go through
        // ActivateOpsIfAble instead; reaching the duplicate here means a real logic error.
        public void AddOperation(InfiltrationOpsType type)
        {
            if (Operations.Any(m => m.Type == type))
            {
                Log.Error($"Mission type {type} already exists for {Owner}");
                return;
            }

            int levelCost = LevelCost(GetOpsLevel(type));
            switch (type) 
            {
                case InfiltrationOpsType.PlantMole:         Operations.Add(new InfiltrationOpsPlantMole(Owner, Them, levelCost));         break;
                case InfiltrationOpsType.Uprise:            Operations.Add(new InfiltrationOpsUprise(Owner, Them, levelCost));            break;
                case InfiltrationOpsType.CounterEspionage:  Operations.Add(new InfiltrationOpsCounterEspionage(Owner, Them, levelCost));  break;
                case InfiltrationOpsType.Sabotage:          Operations.Add(new InfiltrationOpsSabotage(Owner, Them, levelCost));          break;
                case InfiltrationOpsType.SlowResearch:      Operations.Add(new InfiltrationOpsDisruptResearch(Owner, Them, levelCost));   break;
                case InfiltrationOpsType.Rebellion:         Operations.Add(new InfiltrationOpsRebellion(Owner, Them, levelCost));         break;
                case InfiltrationOpsType.DisruptProjection: Operations.Add(new InfiltrationOpsDisruptProjection(Owner, Them, levelCost)); break;
                default: throw new ArgumentOutOfRangeException("InfiltrationOpsType", $"InfiltrationOpsType {type} case not defined");
            }
        }

        public void RemoveOperation(InfiltrationOpsType type) 
        {
            for (int i = Operations.Count - 1; i >= 0; i--)
            {
                InfiltrationOperation mission = Operations[i];
                if (mission.Type == type)
                    Operations.Remove(mission);
            }
        }

        public bool IsOperationActive(InfiltrationOpsType type) => Operations.Any(m => m.Type == type);

        public bool CanActivateOperation(InfiltrationOpsType type) => Level >= GetOpsLevel(type);

        public void ActivateOpsIfAble(InfiltrationOpsType type)
        {
            if (!IsOperationActive(type) && CanActivateOperation(type))
                AddOperation(type);
        }

        static public byte GetOpsLevel(InfiltrationOpsType type)
        {
            switch (type)
            {
                case InfiltrationOpsType.PlantMole:         return 2;
                case InfiltrationOpsType.Uprise:            return 3;
                case InfiltrationOpsType.CounterEspionage:  return 3;
                case InfiltrationOpsType.Sabotage:          return 4;
                case InfiltrationOpsType.SlowResearch:      return 4;
                case InfiltrationOpsType.Rebellion:         return 5;
                case InfiltrationOpsType.DisruptProjection: return 5;
                default: throw new ArgumentOutOfRangeException("InfiltrationOpsType", $"InfiltrationOpsType {type} case not defined");
            }
        }
    }
}
