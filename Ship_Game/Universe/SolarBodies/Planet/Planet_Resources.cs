using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDGraphics;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Universe.SolarBodies;

namespace Ship_Game
{
    public partial class Planet
    {
        public enum GoodState
        {
            STORE,
            IMPORT,
            EXPORT
        }

        [StarData] public ColonyStorage Storage;
        [StarData] public ColonyResource Food;
        [StarData] public ColonyResource Prod;
        [StarData] public ColonyResource Res;
        public ColonyMoney Money;

        public float FoodHere
        {
            get => Storage.Food;
            set => Storage.Food = value;
        }

        public float ProdHere
        {
            get => Storage.Prod;
            set => Storage.Prod = value;
        }

        public float Population
        {
            get => Storage.Population;
            set
            {
                Storage.Population = value;
                PopulationBillion = value / 1000f;
            }
        }

        [StarData] public float PopulationBillion { get; private set; }
        public float PlusFlatPopulationPerTurn { get; private set; }

        public bool HasProduction    => Prod.GrossIncome > 1.0f;
        public float PopulationRatio => MaxPopulation.AlmostZero() ? 0 : Storage.Population / MaxPopulation;

        public string PopulationStringForPlayer
        {
            get
            {
                float maxPopForPlayer = MaxPopulationBillionFor(Universe.Player);
                int numDecimalsPop    = PopulationBillion < 2 ? 2 : 1;
                int numDecimalsPopMax = maxPopForPlayer < 2 ? 2 : 1;
                string popString      = $"{PopulationBillion.String(numDecimalsPop)} / {maxPopForPlayer.String(numDecimalsPopMax.UpperBound(numDecimalsPop))}";

                if (PopulationRatio.NotZero())
                    popString += $" ({(PopulationRatio * 100).String()}%)";

                return popString;
            }
        }

        [StarData] public GoodState FS = GoodState.STORE;      // I dont like these names, but changing them will affect a lot of files
        [StarData] public GoodState PS = GoodState.STORE;
        // Ludoal fork (wishlist): the colonist flow gets a player override. With the flag
        // off the migration formula below stays in charge (the AI default every colony
        // has always run on); on, CS pins the direction like FS and PS pin theirs.
        [StarData] public bool ColonistsManual;
        [StarData] public GoodState CS = GoodState.STORE;
        // Ludoal fork (wishlist, auto-supplies): the same override for the two cargo flows.
        // The MANUAL flag is what serializes, so an old save's default (false) reads as
        // Auto ON - the design's default. Auto: the governing tick keeps writing FS/PS
        // (governor thresholds, or the neutral pair without one); Manual: the player's
        // choice HOLDS and is never overwritten again.
        [StarData] public bool FoodManual;
        [StarData] public bool ProdManual;
        // Ludoal fork (wishlist, 20 Aug): per-planet Continuous Rush - same behaviour as
        // the Automation global, scoped to this colony. The global stays the master: while
        // it is on, the local toggle shows checked read-only and this flag is untouched, so
        // a global round-trip never lies about the local choice (spec's trap, avoided by
        // reading the two flags live instead of sweeping item states).
        [StarData] public bool RushConstruction;

        // Ludoal fork: automatic RUSHES left to a new colony. It is its own countdown and not a
        // use of RushConstruction above: that toggle is the player's gesture on this colony, and
        // an automatic behaviour that switched it off when it expired would quietly erase a choice
        // he made by hand.
        //
        // ⚠ it counts rushes that HAPPEN, not turns that pass (bench 530): a young colony with an
        // empty queue, or an empire too poor to pay, would otherwise spend its allowance on turns
        // where nothing was rushed at all.
        [StarData] public int RushTurnsLeft;
        public bool AutoFood      { get => !FoodManual;      set => FoodManual      = !value; }
        public bool AutoProd      { get => !ProdManual;      set => ProdManual      = !value; }
        public bool AutoColonists { get => !ColonistsManual; set => ColonistsManual = !value; }
        public bool ImportFood => FS == GoodState.IMPORT;
        public bool ImportProd => PS == GoodState.IMPORT;
        public bool ExportFood => FS == GoodState.EXPORT;
        public bool ExportProd => PS == GoodState.EXPORT;

        GoodState ColonistsTradeState()
        {
            bool needFood = ShortOnFood();
            if (needFood && Population > 500f)                       return GoodState.EXPORT;
            if (!needFood && ShouldImportColonists(PopulationRatio)) return GoodState.IMPORT;
            if (MaxPopulation > 2000 && PopulationRatio > 0.9f)      return GoodState.EXPORT;
            return GoodState.STORE;
        }

        // bounded at the export ratio: any higher and a queued biosphere alone would
        // decide the traffic direction, flipping it as the build queue changes
        bool ShouldImportColonists(float popRatio) => popRatio < 0.8f || BiosphereInTheWorks && PopPerBiosphere(Owner) > 100 && popRatio < 0.9f;

        public bool ShortOnFood()
        {
            if (Owner == null || Owner.IsFaction)
                return false;

            if (Owner is { NonCybernetic: true })
            {
                if (TurnsToEmptyStorage(Food.NetIncome, FoodHere + IncomingFood) < AverageFoodImportTurns)
                    return true;
            }
            else if (TurnsToEmptyStorage(Prod.NetIncome, ProdHere + IncomingProd) < AverageProdImportTurns)
                return true;

            return false;
        }

        float TurnsToEmptyStorage(float output, float storage)
        {
            if (output.GreaterOrEqual(0))
                return 1000;

            return storage / -output;
        }

        public GoodState GetGoodState(Goods good)
        {
            switch (good)
            {
                case Goods.Food:       return FS;
                case Goods.Production: return PS;
                case Goods.Colonists:  return ColonistsManual ? CS : ColonistsTradeState();
                default:               return 0;
            }
        }

        public bool IsExporting()
        {
            foreach (Goods good in Enum.GetValues(typeof(Goods)))
                if (GetGoodState(good) == GoodState.EXPORT)
                    return true;
            return false;
        }

        private string ImportsDescr()
        {
            if (!ImportFood && !ImportProd) return "";
            if (ImportFood && !ImportProd) return "(IMPORT FOOD)";
            if (ImportProd && !ImportFood) return "(IMPORT PROD)";
            return "(IMPORT ALL)";
        }

        public float StorageRatio(Goods goods)
        {
            switch (goods)
            {
                case Goods.Food:       return Storage.FoodRatio;
                case Goods.Production: return Storage.ProdRatio;
                case Goods.Colonists:  return PopulationRatio;
                default:               return 1;
            }
        }

        void DetermineFoodState(float importThreshold, float exportThreshold)
        {
            if (IsCybernetic) return;

            if (Owner.NumPlanets == 1 || TradeBlocked)
            {
                FS = GoodState.STORE; // Easy out for solo planets or blockades
                return;
            }

            float ratio               = Storage.FoodRatio;
            bool belowImportThreshold = ratio < importThreshold 
                                        && (CType != ColonyType.Agricultural || Food.NetMaxPotential < 0)
                                        && Food.NetFlatBonus < Consumption;

            // This will allow a buffer for import / export, so they dont constantly switch between them
            if      (ShortOnFood() || belowImportThreshold)   FS = GoodState.IMPORT; 
            else if (Food.NetMaxPotential < 0)                FS = GoodState.STORE;  // Negative food production: keep the buffer, never export it away
            else if (ratio > exportThreshold)                 FS = GoodState.EXPORT; // Until we get back to the Threshold, then export
            else                                              FS = GoodState.STORE;  // We are between our thresholds
        }

        void DetermineProdState(float importThreshold, float exportThreshold)
        {
            if (Owner.NumPlanets == 1 || TradeBlocked)
            {
                PS = GoodState.STORE; // Easy out for solo planets or blockades
                return;
            }

            if (IsCybernetic)  //Account for excess food for the filthy Opteris
            {
                if (Prod.FlatBonus > PopulationBillion)
                {
                    float offsetAmount = (Prod.FlatBonus - PopulationBillion) * 0.05f;
                    offsetAmount = offsetAmount.Clamped(0.00f, 0.15f);
                    importThreshold = (importThreshold - offsetAmount).Clamped(0.10f, 1.00f);
                    exportThreshold = (exportThreshold - offsetAmount).Clamped(0.10f, 1.00f);
                }
            }
            else if (importThreshold > 0 || Construction.Count > 0)
            {
                float offsetAmount = Prod.FlatBonus * 0.05f;
                offsetAmount = offsetAmount.Clamped(0.00f, 0.15f);
                importThreshold = (importThreshold - offsetAmount).Clamped(0.10f, 1.00f);
                exportThreshold = (exportThreshold - offsetAmount).Clamped(0.10f, 1.00f);
            }

            float ratio = Storage.ProdRatio;
            if (ratio < importThreshold)      PS = GoodState.IMPORT;
            else if (ratio > exportThreshold) PS = GoodState.EXPORT;
            else                              PS = GoodState.STORE;
        }
    }
}
