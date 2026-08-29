using System;
using Ship_Game.AI;
using Ship_Game.Commands.Goals;
using Ship_Game.Gameplay;
using Ship_Game.Ships;
using System.Collections.Generic;
using System.Linq;
using SDGraphics;
using SDUtils;
using Ship_Game.Data.Serialization;

namespace Ship_Game
{
    using static HelperFunctions;

    // When freighters run short, the dispatch order decides who is served first.
    // Auto keeps the vanilla population-weighted dice; the other two let the player
    // pin the order. Auto MUST stay value 0 so existing saves load as Auto.
    // (Named CargoPriority to avoid Ship_Game.AI.FreighterPriority, an unrelated
    // freighter-sizing status enum.)
    public enum CargoPriority { Auto, ProductionFirst, ColonistsFirst, TradeFirst }

    public partial class Empire
    {
        // LEGACY: the old single "Automatic Trade" toggle. Kept for save compatibility - a save
        // written before the dissection deserializes this, and OnDeserialized seeds the three
        // split toggles below from it (see MigrateFreighterAutomation). Not shown in the UI anymore.
        [StarData] public bool AutoFreighters;
        [StarData] public bool AutoPickBestFreighter;
        // The three empire-level freighter automations, split out of the old AutoFreighters.
        // New games start decoupled (all false); migrated saves inherit AutoFreighters (see below).
        [StarData] public bool AutoBuildFreighters;    // build new freighters (model = the pick)
        [StarData] public bool AutoUpgradeFreighters;  // modernise existing freighters to the pick
        [StarData] public bool AutoScrapIdleFreighters;// scrap freighters left idle too long
        // Set once the split toggles have been seeded; distinguishes a pre-dissection save (seed
        // from AutoFreighters) from a fresh/new-format one (leave the toggles as the player set them).
        [StarData] public bool FreighterAutomationSplit;
        // Ludoal fork (maintainer feedback): the three quantity levers. All are ints so an
        // old save that lacks them reads 0, which every reader below treats as vanilla conduct.
        // The first two are SHARES of the freighter fleet, so they hold as the empire grows
        // instead of needing a new number after every expansion.
        [StarData] public int FreighterReservePct;   // 0 = build only when the pool runs dry
        [StarData] public int MaxFreighterRefitsPct; // 0 = refits run on the game's own formula
        [StarData] public int FreighterIdleTurns;    // 0 = the vanilla 20 idle turns before scrapping
        // Stored as an int, not as the enum: CargoPriority does not exist in vanilla, and a
        // build without the deleted-enum skip cannot read past a type it has never heard of.
        // An int is a fundamental type every build reads, so the save stays loadable downstream.
        [StarData] int CargoPriorityValue;
        public CargoPriority CargoPriority
        {
            get => (CargoPriority)CargoPriorityValue;
            set => CargoPriorityValue = (int)value;
        }
        [StarData] public float FastVsBigFreighterRatio { get; private set; } = 0.5f;
        public float TradeMoneyAddedThisTurn { get; private set; }
        public float TotalTradeMoneyAddedThisTurn { get; private set; }
        [StarData] public float AverageFreighterCargoCap { get; private set; } = 20;
        [StarData] public int AverageFreighterFTLSpeed { get; private set; } = 20000;
        [StarData] public float TotalPlanetStorage { get; private set; }
        [StarData] public float AveragePlanetStorage { get; private set; } = 100;

        public int  TotalProdExportSlots { get; private set; }

        // Save migration: a game saved before "Automatic Trade" was split into three toggles
        // inherits its old on/off state into all three, so an ongoing game keeps its conduct.
        // A fresh game (or an already-split save) leaves the toggles as they are.
        public void MigrateFreighterAutomation()
        {
            if (FreighterAutomationSplit)
                return;

            AutoBuildFreighters    = AutoFreighters;
            AutoUpgradeFreighters  = AutoFreighters;
            AutoScrapIdleFreighters= AutoFreighters;
            FreighterAutomationSplit = true;
        }

        public int FreighterCap => (int)(AveragePlanetStorage / AverageFreighterCargoCap * OwnedPlanets.Count).Clamped(1, OwnedPlanets.Count*10);
        public int FreightersBeingBuilt  => AI.CountGoals(goal => goal is IncreaseFreighters);
        public int MaxFreightersInQueue  => (int)Math.Ceiling((OwnedPlanets.Count / 5f)).Clamped(2, 5);
        public int TotalFreighters       => OwnedShips.Count(s => s?.IsFreighter == true);
        // A share of the fleet, rounded UP: a tenth of five freighters would otherwise round to
        // nothing exactly where the reserve matters most. Zero stays zero.
        static int ShareOfFleet(int fleet, int percent) => percent <= 0 ? 0 : (fleet * percent + 99) / 100;
        // Counted in FREE freighters - the same unit the build gate and the scrap floor work in,
        // so the two cannot pull against each other. The share is of the WHOLE fleet, runs
        // included: taking it off the free ones only would chase its own tail.
        public int FreighterReserve => ShareOfFleet(TotalFreighters, FreighterReservePct);
        public int MaxFreighterRefits => ShareOfFleet(TotalFreighters, MaxFreighterRefitsPct);
        // A freighter under refit is neither lost nor free: IsIdleFreighter excludes AIState.Refit,
        // so without this term a refit would empty the reserve and the build gate would replace it.
        // RefitShip is also the warship refit goal and the player's manual refit - only ours counts.
        public int FreightersInRefit => AI.CountGoals(g => g is RefitShip r && r.OldShip?.IsFreighter == true);
        public int IdleTurnsBeforeScrap => FreighterIdleTurns > 0 ? FreighterIdleTurns : 20;
        // No ceiling set: freighter refits run on the game's own formula - the fleet-fill
        // thresholds and the original dice. That position is the comparison baseline, which
        // is why it reads "Auto" rather than zero.
        public bool RefitAuto => MaxFreighterRefitsPct <= 0;
        public int FreighterRefitDice => RefitAuto ? 10 : 20;
        public int AverageTradeIncome    => AllTimeTradeIncome / TurnCount;
        // ManualTrade now governs only the ROUTING side (per-cargo restrictions / trade routes):
        // the AI always auto-routes, the player auto-routes unless it drives its freighters by hand.
        public bool ManualTrade          => isPlayer && !AutoFreighters;
        // The three split automations. The AI always runs them; the player runs each only when its
        // own toggle is on. Freighter model pick (CurrentAutoFreighter) is shared by build & upgrade.
        bool BuildFreightersActive  => !isPlayer || AutoBuildFreighters;
        bool UpgradeFreightersActive=> !isPlayer || AutoUpgradeFreighters;
        bool ScrapIdleFreightersActive => !isPlayer || AutoScrapIdleFreighters;
        public float TotalAvgTradeIncome => TotalTradeTreatiesIncome() + AverageTradeIncome;
        public bool EconomicSafeToBuildFreighter => AI.CreditRating >= 0.4;
        public int TotalLevelsOfPirateFactionsAtWar => Universe.PirateFactions.Sum(e => IsAtWarWith(e) ? e.Pirates.Level : 0);

        Array<Relationship> TradeTreaties = new();
        public IReadOnlyList<Relationship> TradeRelations => TradeTreaties;

        void UpdateTradeTreaties()
        {
            var tradeTreaties = new Array<Relationship>();
            foreach (Relationship r in ActiveRelations)
                if (r.Treaty_Trade) tradeTreaties.Add(r);

            TradeTreaties = tradeTreaties;
        }

        public Array<Planet> TradingEmpiresPlanetList()
        {
            var list = new Array<Planet>();
            foreach (Relationship rel in TradeTreaties)
            {
                list.AddRange(rel.Them.OwnedPlanets);
            }
            return list;
        }

        public void TaxGoods(float goods, Planet planet)
        {
            float taxedGoods = 0;
            float taxRate    = data.TaxRate;
            // First - tax the goods if the Mercantilism was unlocked
            if (data.Traits.TaxGoods)
                taxedGoods = goods * taxRate;

            // Then, add credits per goods if the race has the Mercantile trait
            taxedGoods += goods * data.Traits.Mercantile;

            // Finally, add Inter Empire Trade Tariff
            if (this != planet.Owner) 
                taxedGoods += goods * 2f;

            TradeMoneyAddedThisTurn += taxedGoods;
            AllTimeTradeIncome      += (int)taxedGoods;
        }

        // once per turn with 3 passes if possible
        void DispatchBuildAndScrapFreighters()
        {
            UpdateTradeTreaties();
            // a colony that stops being ours leaves the zones that named it, and a zone left
            // without one is dissolved - the housekeeping has to RUN, not merely exist
            RefreshTradeZones();
            TradeState tradeState = new(this, false);
            // Trade First lifts the foreign runs above production and colonists - once in the
            // turn, and never above food, which stays the first call below.
            bool tradeFirst = isPlayer && CargoPriority == CargoPriority.TradeFirst;
            bool servedAbroad = false;
            bool servedZones = false;
            for (int i = 1; i <= 3; i++)
            {
                if (tradeState.NoFreeFreighters)
                    break;

                if (NonCybernetic)
                    DispatchOrBuildFreighters(Goods.Food, OwnedPlanets, false, ref tradeState);

                if (!servedZones)
                {
                    DispatchTradeZones(ref tradeState);
                    servedZones = true;
                    if (tradeState.NoFreeFreighters)
                        break;
                }

                if (tradeFirst && !servedAbroad)
                {
                    // the domestic state must have fetched before the foreign runs take their
                    // pick: it is the only one allowed to BUILD, and a cybernetic empire runs
                    // no food pass to trigger that fetch. Under this priority a run abroad is
                    // therefore reason enough to lay down a freighter, which is its point.
                    tradeState.FetchIdleFreightersOrBuild();
                    DispatchInterEmpireTrade(ref tradeState);
                    servedAbroad = true;
                    if (tradeState.NoFreeFreighters)
                        break;
                }

                // Under a freighter shortage the dispatch order is the priority. The player
                // can pin it (Policies > Trade); Auto — and every AI empire — keeps the
                // vanilla population-weighted dice (colonists win more often early game).
                bool productionFirst;
                // only the two pinned orders answer here; Trade First reorders the PASSES and
                // leaves production against colonists to the dice, exactly as Auto does.
                if (isPlayer && (CargoPriority == CargoPriority.ProductionFirst
                                 || CargoPriority == CargoPriority.ColonistsFirst))
                    productionFirst = CargoPriority == CargoPriority.ProductionFirst;
                else
                {
                    float popRatio = TotalPopBillion / MaxPopBillion;
                    float productionFirstChance = popRatio * (NonCybernetic ? 200 : 300);
                    productionFirst = Random.RollDice(productionFirstChance);
                }

                if (productionFirst)
                {
                    DispatchOrBuildFreighters(Goods.Production, OwnedPlanets, false, ref tradeState);
                    DispatchOrBuildFreighters(Goods.Colonists, OwnedPlanets, false, ref tradeState);
                }
                else
                {
                    DispatchOrBuildFreighters(Goods.Colonists, OwnedPlanets, false, ref tradeState);
                    DispatchOrBuildFreighters(Goods.Production, OwnedPlanets, false, ref tradeState);
                }

                tradeState.UpdatePlanetsTradeGoods();
            }

            if (!servedAbroad)
                DispatchInterEmpireTrade(ref tradeState);

            UpdateFreighterTimersAndScrap();
        }

        // Ludoal fork (maintainer feedback): the trade zones take their share before the general
        // dispatch. A zone is a named list of colonies with a quota of freighters; it OWNS no
        // ship, it borrows from the common pool and hands back what it did not send - so nothing
        // here needs a member, a mask or a migration.
        //
        // The zones are served in LIST order, and that is the whole point: when two zones want
        // more than the pool holds, the one the player put first is served first. An order taken
        // from the array's own arrangement would be no order at all.
        //
        // Called once a turn, after food and before the domestic passes. Food is never displaced.
        void DispatchTradeZones(ref TradeState domestic)
        {
            if (!isPlayer || TradeZones.IsEmpty)
                return;

            // the domestic state is the only one allowed to BUILD, so it fetches first - a
            // cybernetic empire runs no food pass to have triggered that fetch.
            domestic.FetchIdleFreightersOrBuild();
            if (domestic.NoFreeFreighters)
                return;

            foreach (TradeZone zone in TradeZones)
            {
                // a quota of nought means the number is measured rather than ordered
                int quota = zone.Quota > 0 ? zone.Quota : zone.RequiredFreighters(this);
                if (quota <= 0)
                    continue;

                Array<Planet> colonies = zone.ColonyPlanets(this);
                if (colonies.Count == 0)
                    continue;

                var lent = new Array<Ship>();
                var kept = new Array<Ship>();
                Ship[] pool = domestic.IdleFreighters;
                for (int i = 0; i < pool.Length; ++i)
                {
                    if (i < quota) lent.Add(pool[i]);
                    else           kept.Add(pool[i]);
                }

                if (lent.Count == 0)
                    return; // nothing left to share out; the zones below get nothing either

                TradeState zoneState = new(this, false);
                zoneState.SetIdleFreighters(lent.ToArray());
                if (NonCybernetic)
                    DispatchOrBuildFreighters(Goods.Food, colonies, false, ref zoneState);

                DispatchOrBuildFreighters(Goods.Production, colonies, false, ref zoneState);
                DispatchOrBuildFreighters(Goods.Colonists, colonies, false, ref zoneState);

                // what the zone did not send goes back to the common pool, with the ships it was
                // never lent: a quota is a share of a turn, not a possession.
                foreach (Ship unsent in zoneState.IdleFreighters)
                    kept.Add(unsent);

                domestic.SetIdleFreighters(kept.ToArray());
                if (domestic.NoFreeFreighters)
                    return;
            }
        }

        // Export to empires we hold trade treaties with. These runs get their own state: a
        // different planet list, a different ship filter, and no construction at all - an
        // interTrade state never builds. It therefore borrows the domestic state's free
        // freighters and hands back what it did not send, rather than replacing it.
        // Cybernetic factions never touch Food trade. Filthy Opteris are disgusted by protein-bugs. Ironic.
        void DispatchInterEmpireTrade(ref TradeState domestic)
        {
            if (!domestic.HasFreeFreighters || isPlayer && !Universe.P.AllowPlayerInterTrade)
                return;

            var interTradePlanets = TradingEmpiresPlanetList();
            if (interTradePlanets.Count == 0)
                return;

            TradeState abroad = new(this, true);
            abroad.SetIdleFreighters(domestic.IdleFreighters.ToArr());
            if (NonCybernetic)
                DispatchOrBuildFreighters(Goods.Food, interTradePlanets, true, ref abroad);

            DispatchOrBuildFreighters(Goods.Production, interTradePlanets, true, ref abroad);
            domestic.SetIdleFreighters(abroad.IdleFreighters);
        }

        struct TradeState
        {
            readonly bool InterTrade;
            public Ship[] IdleFreighters {get; private set; }
            public EmpireIdleFreighters State { get; private set; }
            bool BuildFreighterRequested;
            readonly Empire Owner;
            HashSet<Planet> PlanetsNeedUpdate = new();
            public bool HasImportingFoodPlanets { get; private set; } = true;
            public bool HasImportingProductionPlanets { get; private set; } = true;
            public bool HasImportingColonistsPlanets { get; private set; } = true;
            public bool HasExportingFoodPlanets { get; private set; } = true;
            public bool HasExportingProductionPlanets { get; private set; } = true;
            public bool HasExportingColonistsPlanets { get; private set; } = true;

            public TradeState(Empire owner,  bool interTrade)
            {
                IdleFreighters = Array.Empty<Ship>();
                State = EmpireIdleFreighters.Fetch;
                InterTrade = interTrade;
                Owner = owner;
            }

            public Ship[] FetchIdleFreightersOrBuild()
            {
                if (State == EmpireIdleFreighters.Fetch)
                {
                    IdleFreighters = Owner.GetIdleFreighters(InterTrade);
                    SetIdleFreightesState();
                    // the vanilla socle stays in the condition: at reserve 0 this is the old test.
                    // A reserve only adds a second reason to build, and counts the freighters on
                    // their way back from a refit as present.
                    if (IdleFreighters.Length == 0
                        || IdleFreighters.Length + Owner.FreightersInRefit < Owner.FreighterReserve)
                        BuildFreighter();
                }

                return IdleFreighters;
            }

            public void SetIdleFreighters(Ship[] freighters)
            {
                IdleFreighters = freighters;
                SetIdleFreightesState();
            }

            public void SetIdleFreightesState()
            {
                if (IdleFreighters.Length > 0)
                    State = EmpireIdleFreighters.SomeIdle;
                else
                    State = EmpireIdleFreighters.None;
            }

            public void BuildFreighter()
            {
                if (!BuildFreighterRequested && !InterTrade)
                {
                    BuildFreighterRequested = true;
                    Owner.BuildFreighter();
                }
            }

            public void AddToPlanetsNeedUpdate(Planet import, Planet export)
            {
                PlanetsNeedUpdate.Add(import);
                PlanetsNeedUpdate.Add(export);
            }

            public void UpdatePlanetsTradeGoods()
            {
                foreach (Planet p in PlanetsNeedUpdate)
                    p.UpdateIncomingTradeGoods();

                PlanetsNeedUpdate = new();
            }

            public void SetNoImportPlanetOf(Goods goods)
            {
                switch (goods)
                {
                    case Goods.Food:       HasImportingFoodPlanets       = false; break;
                    case Goods.Production: HasImportingProductionPlanets = false; break;
                    case Goods.Colonists:  HasImportingColonistsPlanets  = false; break;
                }
            }

            public void SetNoExportPlanetOf(Goods goods)
            {
                switch (goods)
                {
                    case Goods.Food:       HasExportingFoodPlanets       = false; break;
                    case Goods.Production: HasExportingProductionPlanets = false; break;
                    case Goods.Colonists:  HasExportingColonistsPlanets  = false; break;
                }
            }

            public bool HasImportPlanetOf(Goods goods)
            {
                return goods switch
                {
                    Goods.Food       => HasImportingFoodPlanets,
                    Goods.Production => HasImportingProductionPlanets,
                    Goods.Colonists  => HasImportingColonistsPlanets,
                    _                => false,
                };
            }

            public bool HasExportPlanetOf(Goods goods)
            {
                return goods switch
                {
                    Goods.Food       => HasExportingFoodPlanets,
                    Goods.Production => HasExportingProductionPlanets,
                    Goods.Colonists  => HasExportingColonistsPlanets,
                    _                => false,
                };
            }

            public bool NoFreeFreighters => State == EmpireIdleFreighters.None;
            public bool HasFreeFreighters => State == EmpireIdleFreighters.SomeIdle;
            public bool ShouldFetchIdleFreighters => State == EmpireIdleFreighters.Fetch;
        }

        enum EmpireIdleFreighters
        {
            Fetch,
            SomeIdle,
            None,
        }


        void UpdateFreighterTimersAndScrap()
        {
            if (!ScrapIdleFreightersActive)
                return;

            Ship[] ownedFreighters = OwnedShips.Filter(s => s.IsFreighter);
            // The floor is a POPULATION test, never a pick of individuals: the order of this
            // array is an artefact of the filter, so sparing "the first N" would spare a
            // different set every turn and reset their timers at random. Counted once.
            int reserve = FreighterReserve;
            int freeOrReturning = reserve > 0 ? GetIdleFreighters(false).Length + FreightersInRefit : 0;
            for (int i = 0; i < ownedFreighters.Length; ++i)
            {
                Ship freighter = ownedFreighters[i];
                if (freighter.IsIdleFreighter)
                {
                    freighter.TradeTimer -= Universe.P.TurnTimer;
                    if (freighter.TradeTimer < 0)
                    {
                        if (reserve > 0 && freeOrReturning <= reserve)
                        {
                            ResetTradeTimer(freighter); // kept: the empire is down to its reserve
                            continue;
                        }

                        freighter.AI.OrderScrapShip();
                        --freeOrReturning; // AIState.Scrap drops it out of the free pool at once
                        ResetTradeTimer(freighter);
                    }
                }
                else
                {
                    ResetTradeTimer(freighter);
                }
            }

            void ResetTradeTimer(Ship freighter)
            {
                freighter.TradeTimer = Universe.P.TurnTimer * IdleTurnsBeforeScrap;
            }
        }

        public bool TryDispatchGoodsSupplyToStation(Goods goods, Ship targetStation, out ExportPlanetAndFreighter exportAndFreighter)
        {
            exportAndFreighter = default;
            // TODO: maybe use IEnumerable generators for these?
            Planet[] exportingPlanets = OwnedPlanets.Filter(p => p.FreeGoodsExportSlots(goods) > 0);
            if (exportingPlanets.Length == 0)
                return false;

            Ship[] idleFreighters = GetIdleFreighters(interTrade: false);
            if (idleFreighters.Length == 0) // Need trade for auto trade but no freighters found
                return false;

            return GetTradeParameters(goods, idleFreighters, targetStation, exportingPlanets, out exportAndFreighter);
        }

        void DispatchOrBuildFreighters(Goods goods, Array<Planet> importPlanetList, bool interTrade, ref TradeState state)
        {
            if (state.NoFreeFreighters)
                return;

            // Order importing planets to balance freighters distribution
            Planet[] importingPlanets = Array.Empty<Planet>();
            if (state.HasImportPlanetOf(goods))
            {
                importingPlanets = importPlanetList.Filter(p => p.FreeGoodsImportSlots(goods) > 0);
                if (importingPlanets.Length == 0)
                {
                    state.SetNoImportPlanetOf(goods);
                    return;
                }
            }
            else
            {
                return;
            }

            Planet[] exportingPlanets = Array.Empty<Planet>();
            if (state.HasExportPlanetOf(goods))
            {
                // TODO: maybe use IEnumerable generators for these?
                exportingPlanets = OwnedPlanets.Filter(p => p.FreeGoodsExportSlots(goods) > 0);
                if (exportingPlanets.Length == 0)
                {
                    state.SetNoExportPlanetOf(goods);
                    return;
                }
            }
            else
            {
                return;
            }

            Ship[] idleFreighters = state.FetchIdleFreightersOrBuild();
            if (state.NoFreeFreighters) // Need trade for auto trade but no freighters found
                return;

            importingPlanets.Sort(p => p.GetCachedIncomingCargoPriority(goods));

            for (int i = 0; i < importingPlanets.Length; i++)
            {
                Planet importPlanet = importingPlanets[i];
                // Check export planets
                if (GetTradeParameters(goods, idleFreighters, importPlanet, exportingPlanets, out ExportPlanetAndFreighter exportAndFreighter))
                {
                    Planet exportPlanet = exportAndFreighter.Planet;
                    Ship freighter      = exportAndFreighter.Freighter;
                    freighter.RefreshTradeRoutes();
                    freighter.AI.SetupFreighterPlan(exportPlanet, importPlanet, goods);
                    idleFreighters.Remove(freighter, out idleFreighters);

                    // Remove the export planet from the exporting list if no more export slots left
                    if (exportPlanet.FreeGoodsExportSlots(goods) == 0)
                        exportingPlanets.Remove(exportPlanet, out exportingPlanets);

                    state.AddToPlanetsNeedUpdate(importPlanet, exportPlanet);
                }
            }

            state.SetIdleFreighters(idleFreighters);
        }

        Ship[] GetIdleFreighters(bool interTrade)
        {
            return interTrade ? OwnedShips.Filter(s => s.IsIdleFreighter && s.AllowInterEmpireTrade)
                              : OwnedShips.Filter(s => s.IsIdleFreighter); 
        }

        bool GetTradeParameters(Goods goods, Ship[] freighterList, GameObject target, 
            Planet[] exportPlanets,  out ExportPlanetAndFreighter exportAndFreighter)
        {
            var potentialRoutes = new Map<int, ExportPlanetAndFreighter>();
            for (int i = 0; i < freighterList.Length; i++)
            {
                Ship freighter = freighterList[i];
                if ((target is Planet importPlanet && freighter.TryGetBestTradeRoute(goods, exportPlanets, importPlanet, out Ship.ExportPlanetAndEta exportAndEta)
                    || target is Ship targetShip && freighter.TryGetBestTradeRoute(goods, exportPlanets, targetShip, out exportAndEta))
                    && !potentialRoutes.ContainsKey(exportAndEta.Eta))
                {
                    potentialRoutes.Add(exportAndEta.Eta, new ExportPlanetAndFreighter(exportAndEta.Planet, freighter));
                }
            }

            exportAndFreighter = default;
            if (potentialRoutes.Count == 0)
                return false;

            int shortest       = potentialRoutes.FindMinKey(e => e);
            exportAndFreighter = potentialRoutes[shortest];
            return true;
        }

        public struct ExportPlanetAndFreighter
        {
            public readonly Planet Planet;
            public readonly Ship Freighter;

            public ExportPlanetAndFreighter(Planet exportPlanet, Ship freighter)
            {
                Planet    = exportPlanet;
                Freighter = freighter;
            }
        }

        void BuildFreighter()
        {
            if (!BuildFreightersActive || !EconomicSafeToBuildFreighter)
                return;

            int beingBuilt = FreightersBeingBuilt;
            // the reserve widens THIS gate only. FreighterCap has five readers - two AI refit
            // gates and the colony queue priority - and inflating the property would move them too.
            if (beingBuilt < MaxFreightersInQueue && (TotalFreighters + beingBuilt) < FreighterCap + FreighterReserve)
                AI.AddGoalAndEvaluate(new IncreaseFreighters(this));
        }

        int NumFreightersTrading(Goods goods)
        {
            return OwnedShips.Count(s => s?.IsFreighter == true && !s.IsIdleFreighter && s.AI.HasTradeGoal(goods));
        }

        // centralized method to deal with freighter priority ratio (fast or big)
        public void IncreaseFastVsBigFreighterRatio(FreighterPriority reason)
        {
            float ratioDiff = 0;
            switch (reason)
            {
                case FreighterPriority.TooSmall:         ratioDiff = -0.01f;  break;
                case FreighterPriority.TooBig:           ratioDiff = +0.01f;  break;
                case FreighterPriority.TooSlow:          ratioDiff = +0.01f;  break;
                case FreighterPriority.ExcessCargoLeft:  ratioDiff = +0.005f; break;
                case FreighterPriority.UnloadedAllCargo: ratioDiff = -0.02f;  break;
            }

            IncreaseFastVsBigFreighterRatio(ratioDiff);
        }

        public void AffectFastVsBigFreighterByEta(Planet importPlanet, Goods goods, float eta)
        {
            bool freighterTooSlow;
            switch (goods)
            {
                case Goods.Food: freighterTooSlow = (importPlanet.FoodHere - importPlanet.Food.NetIncome * eta) < 0; break;
                default: freighterTooSlow         = eta > 50;                                                        break;
            }

            if (freighterTooSlow)
                IncreaseFastVsBigFreighterRatio(FreighterPriority.TooSlow);
        }

        public void IncreaseFastVsBigFreighterRatio(float amount)
        {
            // 1.0f = all fast, 0.1f = all big
            FastVsBigFreighterRatio = (FastVsBigFreighterRatio + amount).Clamped(0.1f, 1);
        }

        public float TotalTradeTreatiesIncome()
        {
            float total = 0f;
            foreach (Relationship rel in ActiveRelations)
                if (rel.Treaty_Trade) total += rel.TradeIncome(this);
            return total;
        }

        void UpdateTradeIncome()
        {
            TotalTradeMoneyAddedThisTurn = TotalTradeTreatiesIncome() + TradeMoneyAddedThisTurn;
            TradeMoneyAddedThisTurn = 0; // Reset Trade Money for the next turn.
        }

        // FB - Refit some idle freighters to better ones, if unlocked
        public void TriggerFreightersRefit()
        {
            // Auto-upgrade modernises idle freighters. The fleet-fill threshold is the game's own:
            // it holds for the AI always, and for a player who has not set a refit ceiling. Setting
            // one is what lifts it - the ceiling then bounds the wave instead of the fleet's fill.
            if (!UpgradeFreightersActive || (!isPlayer || RefitAuto) && TotalFreighters / (float)FreighterCap <= 0.75f)
                return;

            IShipDesign betterFreighter = ShipBuilder.PickFreighter(this);
            if (betterFreighter == null)
                return;

            var ships = GetIdleFreighters(false);
            for (int i = 0; i < ships.Length; i++)
            {
                Ship idleFreighter = ships[i];
                CheckForRefitFreighter(idleFreighter, RefitAuto ? 25 : 20, betterFreighter);
            }
        }

        // Percentage to check if there is better suited freighter model available
        public void CheckForRefitFreighter(Ship freighter, int percentage, IShipDesign betterFreighter = null)
        {
            // ceiling on simultaneous freighter refits, so a modernisation wave never parks
            // the whole trade fleet at a shipyard. Only counted once a ceiling is set.
            if (!RefitAuto && FreightersInRefit >= MaxFreighterRefits)
                return;

            if (UpgradeFreightersActive && Random.RollDice(percentage)
                // the per-ship fleet-fill threshold, lifted only for a player who set a ceiling
                && ((isPlayer && !RefitAuto) || TotalFreighters / (float)FreighterCap > 0.5f))
            {
                if (betterFreighter == null)
                    betterFreighter = ShipBuilder.PickFreighter(this);

                if (betterFreighter != null && betterFreighter.Name != freighter.Name)
                    AI.AddGoalAndEvaluate(new RefitShip(freighter, betterFreighter, this));
            }
        }

        public void UpdateAverageFreightFTL(float value)
        {
            AverageFreighterFTLSpeed = (int)ExponentialMovingAverage(AverageFreighterFTLSpeed, value);
        }

        public void UpdateAverageFreightCargoCap(float value)
        {
            AverageFreighterCargoCap = ExponentialMovingAverage(AverageFreighterCargoCap, value).RoundToFractionOf10();
        }

        void UpdatePlanetStorageStats()
        {
            UpdateTotalPlanetStorage();
            UpdateAveragePlanetStorage(TotalPlanetStorage);

        }

        void UpdateTotalPlanetStorage()
        {
            TotalPlanetStorage = OwnedPlanets.Sum(p => p.Storage.Max);
        }

        void UpdateAveragePlanetStorage(float totalStorage)
        {
            AveragePlanetStorage = OwnedPlanets.Count > 0 
                ? ExponentialMovingAverage(AveragePlanetStorage, totalStorage / OwnedPlanets.Count) 
                : 0;
        }

        public float TotalPlanetsTradeValue => OwnedPlanets.Sum(p => p.Level).LowerBound(1);
    }
}
