using Ship_Game.AI;
using Ship_Game.Commands.Goals;
using Ship_Game.Ships;
using System;
using System.Collections.Generic;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;
using SDUtils;
using System.Linq;
using Ship_Game.Data.Serialization;

namespace Ship_Game.Universe.SolarBodies
{
    // Production facilities
    [StarDataType]
    public class SBProduction
    {
        [StarData] readonly Planet P;
        Empire Owner => P.Owner;

        public bool NotEmpty => ConstructionQueue.NotEmpty;
        public bool Empty => ConstructionQueue.IsEmpty;
        public int Count => ConstructionQueue.Count;

        /// <summary>
        /// The Construction queue should be protected
        /// If you want to remove items from this array, use the Cancel method!
        /// </summary>
        [StarData] readonly Array<QueueItem> ConstructionQueue = new();

        // Cached cross-thread snapshot (see GetConstructionQueueSnapshot). Rebuilt lazily only
        // when the queue's membership/order changes, so per-frame UI readers don't allocate.
        QueueItem[] CachedQueueSnapshot;
        bool QueueSnapshotDirty = true;

        float ProductionHere
        {
            get => P.ProdHere;
            set => P.ProdHere = value;
        }

        [StarData] float SurplusThisTurn;
        
        [StarDataConstructor]
        public SBProduction(Planet planet)
        {
            P = planet;
        }

        public IReadOnlyList<QueueItem> GetConstructionQueue()
        {
            return ConstructionQueue;
        }

        // Thread-safe snapshot for cross-thread (UI) readers. The live queue is
        // mutated on the sim thread under lock(ConstructionQueue), so UI code must
        // not iterate GetConstructionQueue() directly or it can index a shrinking list.
        // The snapshot array is cached and only rebuilt when the queue's membership or
        // order changed (QueueSnapshotDirty), so per-frame UI Draw doesn't re-allocate.
        public QueueItem[] GetConstructionQueueSnapshot()
        {
            lock (ConstructionQueue)
            {
                if (QueueSnapshotDirty || CachedQueueSnapshot == null)
                {
                    CachedQueueSnapshot = ConstructionQueue.IsEmpty ? Empty<QueueItem>.Array : ConstructionQueue.ToArray();
                    QueueSnapshotDirty = false;
                }
                return CachedQueueSnapshot;
            }
        }

        // Rush button is used only in debug mode for fast debug rush
        public bool RushProduction(int itemIndex, float maxAmount, bool rushButton = false)
        {
            // don't allow rush if we're crippled
            if (P.IsCrippled || ConstructionQueue.IsEmpty || Owner == null)
                return false;

            // dont charge rush fees if in debug and the rush button was clicked
            bool rushFees = !P.Universe.Debug || !rushButton;

            float amount = maxAmount.UpperBound(ProductionHere);
            if (rushFees && amount > Owner.Money)
                return false; // Not enough credits to rush

            // inject artificial surplus to instantly rush & finish production
            if (P.Universe.Debug && rushButton)
                amount = SurplusThisTurn = 1000;

            return ApplyProductionToQueue(maxAmount: amount, itemIndex, rushFees, immediate: rushButton);
        }

        // Spend up to `max` production for QueueItem
        // @return TRUE if QueueItem is complete
        bool SpendProduction(QueueItem q, float max, bool chargeFees)
        {
            float needed = q.ProductionNeeded;
            if (needed <= 0f) return true; // complete!

            float spendMax = Math.Min(needed, max); // how much can we spend?
            float spend = spendMax;

            MathExt.Consume(ref SurplusThisTurn, ref spend);
            P.Storage.ConsumeProduction(ref spend);

            float netSpend     = spendMax - spend;
            q.ProductionSpent += netSpend; // apply it
            if (chargeFees && (!P.Owner.isPlayer || !P.Universe.Debug))
                Owner.ChargeCreditsOnProduction(q, netSpend);

            // if we spent everything, this QueueItem is complete
            return spend <= 0f;
        }

        // @note `maxProduction` is a max limit, this method will attempt
        //       to consume no more than `maxAmount` from local production
        // @return true if at least some production was applied
        bool ApplyProductionToQueue(float maxAmount, int itemIndex, bool rushFees, bool immediate)
        {
            if (maxAmount <= 0.0f || ConstructionQueue.IsEmpty)
                return false;

            // apply production to specified item
            if (ConstructionQueue.Count > itemIndex)
            {
                QueueItem item = ConstructionQueue[itemIndex];
                if (rushFees && (!P.OwnerIsPlayer || !P.Universe.Debug))
                {
                    SpendProduction(item, maxAmount, chargeFees: false);
                    Owner.ChargeRushFees(maxAmount, immediate);
                }
                else
                {
                    SpendProduction(item, maxAmount, chargeFees: true);
                }
            }

            for (int i = 0; i < ConstructionQueue.Count;)
            {
                QueueItem q = ConstructionQueue[i];
                if (q.isTroop && !HasRoomForTroops()) // remove excess troops from queue
                {
                    Cancel(q);
                    continue; // this item was removed, so skip ++i
                }
                if (q.IsCancelled)
                {
                    Cancel(q);
                    continue; // this item was removed, so skip ++i
                }
                if (q.IsComplete)
                {
                    ProcessCompleteQueueItem(q);
                    continue; // this item was removed, so skip ++i
                }
                ++i;
            }

            return true;
        }

        void ProcessCompleteQueueItem(QueueItem q)
        {
            bool ok = false;

            if (q.isBuilding)   ok = OnBuildingComplete(q);
            else if (q.isShip)  ok = OnShipComplete(q);
            else if (q.isTroop) ok = TrySpawnTroop(q);

            Finish(q, success: ok);
        }

        bool HasRoomForTroops()
        {
            foreach (PlanetGridSquare tile in P.TilesList)
            {
                if (tile.TroopsHere.Count < tile.MaxAllowedTroops &&
                    (tile.Building == null || (tile.Building != null && tile.Building.CombatStrength == 0)))
                    return true;
            }
            return false;
        }

        bool OnBuildingComplete(QueueItem q)
        {
            // we can't place it...
            if (q.Building.Unique && P.BuildingBuilt(q.Building.BID))
            {
                Log.Warning($"Unique building {q.Building} already exists on planet {P}");
                return false;
            }
            // ⚠ an entry may now reach here with no tile if anything ever spends on it out of
            // turn - the buildable pass is what normally prevents it, and this is the belt
            if (q.pgs == null || !q.pgs.CanPlaceBuildingHere(q.Building))
            {
                Log.Warning($"We can no longer build {q.Building} at tile {q.pgs}");
                return false;
            }

            Building b = ResourceManager.CreateBuilding(P, q.Building.Name);
            b.IsPlayerAdded = q.IsPlayerAdded;
            q.pgs.PlaceBuilding(b, P);
            if (!P.Universe.P.SuppressOnBuildNotifications
                && !P.Universe.Screen.IsViewingColonyScreen(P)
                && P.OwnerIsPlayer
                && (q.IsPlayerAdded || q.Building.IsCapital))
            {
                P.Universe.Notifications.AddBuildingConstructed(P, b);
            }

            return true;
        }

        bool TrySpawnTroop(QueueItem q)
        {
            if (!ResourceManager.TryCreateTroop(q.TroopType, Owner, out Troop troop))
                return false;
            if (!troop.PlaceNewTroop(P) && troop.Launch(P) == null)
                return false; // Could not find a place to the troop or launch it to space
            q.Goal?.NotifyMainGoalCompleted();
            return true;
        }

        bool OnShipComplete(QueueItem q)
        {
            if (!ResourceManager.ShipTemplateExists(q.ShipData.Name))
                return false;

            Vector2 launchPos = P.GetBuilderShipTargetVector(launch: true, out bool fromShipyard);

            Ship shipAt = fromShipyard ? Ship.CreateShipAtShipyard(P.Universe, q.ShipData.Name, Owner, launchPos)
                                       : Ship.CreateShipNearPlanet(P.Universe, q.ShipData.Name, Owner, P, true);

            q.Goal?.ReportShipComplete(shipAt);
            if (q.Goal is BuildConstructionShip || q.Goal is BuildOrbital)
            {
                shipAt.VanityName = q.ShipData.Name;
                shipAt.AI.SetPriorityOrder(true);
            }

            if (shipAt.IsFreighter)
            {
                shipAt.DownloadTradeRoutes(q.TradeRoutes);
                shipAt.TransportingFood        = true;
                shipAt.TransportingProduction  = true;
                shipAt.TransportingColonists   = true;
                shipAt.AllowInterEmpireTrade   = true; 
                shipAt.AreaOfOperation         = q.AreaOfOperation;
                shipAt.TransportingColonists  &= q.TransportingColonists;
                shipAt.TransportingFood       &= q.TransportingFood;
                shipAt.TransportingProduction &= q.TransportingProduction;
                shipAt.AllowInterEmpireTrade  &= q.AllowInterEmpireTrade;
                // the zone that ordered the refit gets its hull back marked. The id is looked
                // up rather than trusted - a zone can be dissolved while the yard works, and a
                // mark pointing at nothing would hide the hull from the empire and from every
                // zone at once. Assigned through the one writer, never by touching the field.
                TradeZone zone = shipAt.Loyalty?.GetTradeZoneById(q.TradeZoneId);
                if (zone != null)
                    shipAt.Loyalty.AssignFreighterToZone(shipAt, zone);
            }

            if (shipAt.ShipData.IsColonyShip)
            {
                float amount = shipAt.CargoSpaceFree.UpperBound(P.Population / 10);
                P.Population -= shipAt.LoadColonists(amount);
            }

            return true;
        }

        // ⚠ THE NEED FOR A TERRAFORMER OSCILLATES, and the queue used to flicker with it. Some
        // of its terms move while a terraformer works - fertility climbs, tiles stop being
        // terraformable - so a world sitting on the threshold answers yes one turn and no the
        // next: the governor queues the building, sends it to the back, then forward again, and
        // the player watches it appear and vanish (maintainer feedback, Roland's save).
        // The answer is HELD for ten turns once it has been true. The ordering is untouched; it
        // is the signal that stops blinking, which is where the fault was.
        // ⚠ zero on an old save means "never wanted", so it reads as before until the first yes.
        [StarData] float LastTerraformerWanted;
        const float TerraformerNeedHold = 1.0f; // StarDate advances 0.1 per turn

        bool TerraformerStillWanted()
        {
            if (P.AreTerraformersNeeded)
            {
                LastTerraformerWanted = P.Universe.StarDate;
                return true;
            }

            return P.Universe.StarDate - LastTerraformerWanted < TerraformerNeedHold;
        }

        // ★ THE ENTRY ACTUALLY BEING BUILT, and it is not always the first one. An entry may sit
        // in the queue with no tile yet - the player ordered a building for a square a biosphere
        // has not finished making habitable - and production must SKIP it rather than stall behind
        // it. The order the player arranged is kept: a waiting entry holds its place, and the pass
        // serves the first one it CAN. Walking backwards would loop; walking forwards cannot.
        // -1 when nothing in the queue can be built this turn (spec of 4 Sep).
        public int FirstBuildableIndex
        {
            get
            {
                for (int i = 0; i < ConstructionQueue.Count; ++i)
                    if (TryMakeBuildable(ConstructionQueue[i]))
                        return i;

                return -1;
            }
        }

        // An entry that is not a building is always ready. A building with a tile needs that tile
        // to still accept it; a building WITHOUT one takes the first that has become free - the
        // tile is chosen at construction, not at order time, which is what lets the entry wait
        // through a biosphere being built under it.
        bool TryMakeBuildable(QueueItem q)
        {
            if (!q.isBuilding)
                return true;

            // A terraformer whose colony plan is not far enough along cannot start: the budget
            // that pays for it is held at zero until the plan completes. It keeps its rank and
            // YIELDS ITS TURN, exactly like an entry with no tile - otherwise the whole queue
            // stalls behind it and nothing else is ever built, which is what the bench saw at
            // 600 (maintainer feedback: "reste en position 1 et tout est bloqué").
            if (WaitsForBlueprint(q))
                return false;

            if (q.pgs != null)
                return q.pgs.CanPlaceBuildingHere(q.Building);

            PlanetGridSquare where = null;
            if (!q.Building.AssignBuildingToTile(q.Building, ref where, P))
                return false;

            where.SetQueueItem(q);
            q.pgs = where;
            return true;
        }

        // An entry with no square yet. ⚠ PURE ON PURPOSE: the colony screen calls it to order the
        // rows it draws, and it runs on the UI thread - TryMakeBuildable would ASSIGN a tile from
        // there, which is a write into the simulation from the wrong thread.
        public static bool IsWaitingForTile(QueueItem q) => q.isBuilding && q.pgs == null;

        // A terraformer held back by its colony plan. Read by BOTH the dispatch above and the
        // colony screen, on purpose: what the queue SERVES and what the screen SHOWS have to
        // count the same thing, or the list stops being the list that is executed.
        public static bool WaitsForBlueprint(QueueItem q)
            => q.IsTerraformer && q.Planet?.TerraformerWaitsForBlueprint == true;

        // Everything that cannot start this turn, whatever the reason. ⚠ PURE, like its two parts.
        public static bool IsWaiting(QueueItem q) => IsWaitingForTile(q) || WaitsForBlueprint(q);

        // ★ ONE notion, read by everything that used to read the head: the entry production is
        // actually being spent on. Null when every entry is waiting for a tile.
        public QueueItem BuildingNow
        {
            get
            {
                int i = FirstBuildableIndex;
                return i < 0 ? null : ConstructionQueue[i];
            }
        }

        // Applies available production to production queue
        public void AutoApplyProduction(float surplusFromPlanet)
        {
            // surplus will be reset every turn and consumed at first opportunity
            SurplusThisTurn = surplusFromPlanet;
            if (ConstructionQueue.IsEmpty || P.IsSabotaged)
                return; // Massive sabotage to planetary facilities or no items

            // ⚠ the FIRST BUILDABLE entry, not the head: production spent on an entry with no
            // tile would be spent on something that cannot be placed, and the old code only found
            // that out at completion - after the cost was paid.
            int index = FirstBuildableIndex;
            if (index < 0)
                return; // everything in the queue is waiting for a tile this turn

            float percentToApply = P.RecentCombat ? 0.1f : 1f; // Ongoing combat is hindering logistics
            float limitSpentProd = P.LimitedProductionExpenditure(P.CurrentProductionToQueue);
            ApplyProductionToQueue(maxAmount: limitSpentProd * percentToApply, index, rushFees: false, immediate: false);
            TryPlayerRush();
        }

        void TryPlayerRush() // Apply rush if player marked items as continuous rush
        {
            if (!P.OwnerIsPlayer || Count == 0 || P.IsCrippled)
                return;

            // the rush follows the entry that is actually being built, not the head of the list
            int index = FirstBuildableIndex;
            if (index < 0)
                return;

            QueueItem item = ConstructionQueue[index];
            if (item.Rush || Owner.RushAllConstruction || P.RushConstruction)
            {
                float prodToRush = item.ProductionNeeded.UpperBound(P.ProdHere);
                if (prodToRush * GlobalStats.Defaults.RushCostPercentage + 1000 < P.Universe.Player.Money)
                {
                    RushProduction(index, prodToRush);
                }
            }
        }

        // @return TRUE if building was added to CQ,
        //         FALSE if `where` is occupied or if there is no free random tiles - except for a
        //         PLAYER order, which is queued with no tile and waits for one (spec of 4 Sep)
        public bool Enqueue(Building b, PlanetGridSquare where = null, bool playerAdded = false)
        {
            if ((b.Unique || b.BuildOnlyOnce) && P.BuildingBuiltOrQueued(b))
                return false; // unique building already built

            var qi = new QueueItem(P)
            {
                IsPlayerAdded   = playerAdded,
                isBuilding      = true,
                IsMilitary      = b.IsMilitary,
                IsTerraformer   = b.IsTerraformer,
                Building        = b,
                pgs             = where,
                Cost            = b.ActualCost(P.Owner),
                ProductionSpent = 0.0f,
                NotifyOnEmpty   = false,
                Rush            = P.Owner.RushAllConstruction,
                QType           = QueueItemType.Building
            };

            if (b.AssignBuildingToTile(b, ref where, P))
            {
                where.SetQueueItem(qi);
                qi.pgs = where; // reset PGS if we got a new one
                AddToQueueAndPrioritize(qi);
                P.RefreshBuildingsWeCanBuildHere();
                return true;
            }

            // ★ THE PLAYER'S OWN ORDER WAITS rather than being refused in silence. Ordering a
            // biosphere and the building meant to stand on it took two visits: the second could
            // not be queued until the first had finished, because the tile was not habitable yet.
            // The entry keeps its place with no tile and takes one as soon as one appears.
            // ⚠ the GOVERNOR keeps the placement check: it picks its tile deliberately, and
            // without that it would stack up waiting entries it never meant to order.
            if (playerAdded)
            {
                qi.pgs = null;  // waiting for a tile; the pass below skips it until it has one
                AddToQueueAndPrioritize(qi);
                P.RefreshBuildingsWeCanBuildHere();
                return true;
            }

            return false;
        }

        public void Enqueue(QueueItemType type, IShipDesign orbital, IShipDesign constructor, 
            float orbitalCost, bool rush, Goal goal = null)
        {
            if (goal != null && goal.PlanetBuildingAt == null)
                throw new InvalidOperationException($"CQ.Enqueue not allowed if Goal.PlanetBuildingAt is null!");

            float constructorCost = (constructor.GetCost(Owner)
                - GlobalStats.Defaults.ConstructionShipOrbitalDiscount).LowerBound(0);

            var qi = new QueueItem(P)
            {
                isShip        = true,
                isOrbital     = true,
                Goal          = goal,
                NotifyOnEmpty = false,
                DisplayName   = $"{constructor.Name} ({orbital.Name})",
                ShipData      = constructor,
                Cost          = orbitalCost + constructorCost,
                Rush          = P.Owner.RushAllConstruction || rush,
                QType         = type
            };

            if (goal != null) 
                goal.PlanetBuildingAt = P;

            AddToQueueAndPrioritize(qi);
        }

        public void Enqueue(IShipDesign orbitalRefit, IShipDesign constructor, float refitCost, Goal goal, bool rush)
        {
            float constructorCost = (constructor.GetCost(Owner)
                - GlobalStats.Defaults.ConstructionShipOrbitalDiscount).LowerBound(0);

            var qi = new QueueItem(P)
            {
                isShip        = true,
                isOrbital     = true,
                Goal          = goal,
                NotifyOnEmpty = false,
                DisplayName   = $"{constructor.Name} ({orbitalRefit.Name})",
                ShipData      = constructor,
                Cost          = refitCost + constructorCost,
                Rush          = rush || P.Owner.RushAllConstruction,
                QType         = QueueItemType.CombatShip
            };

            AddToQueueAndPrioritize(qi);
        }

        // tradeZoneId marks the hull for a trade zone before it exists: the yard carries the mark
        // and stamps it on the ship, the same path a refit already takes to come home to its zone.
        public void Enqueue(IShipDesign ship, QueueItemType type, Goal goal = null, bool notifyOnEmpty = true, string displayName = "", int tradeZoneId = 0)
        {
            if (goal != null && goal.PlanetBuildingAt == null)
                throw new InvalidOperationException($"CQ.Enqueue not allowed if Goal.PlanetBuildingAt is null!");

            var qi = new QueueItem(P)
            {
                isShip        = true,
                isOrbital     = ship.IsPlatformOrStation,
                Goal          = goal,
                ShipData      = ship,
                Cost          = GetShipCost(),
                NotifyOnEmpty = notifyOnEmpty,
                Rush          = P.Owner.RushAllConstruction,
                TradeZoneId   = tradeZoneId,
                QType         = type
            };  

            if (displayName.NotEmpty())
                qi.DisplayName = displayName;

            if (goal != null)
                goal.PlanetBuildingAt = P;

            AddToQueueAndPrioritize(qi);

            float GetShipCost()
            {
                if (!ship.IsSingleTroopShip)
                {
                    return ship.GetCost(Owner);
                }
                else // for when a player requisitions a single troop ship in a fleet
                {
                    Troop troopTemplate = Owner.GetUnlockedTroops().FindMax(troop => troop.SoftAttack);
                    if (troopTemplate != null)
                    {
                        return troopTemplate.ActualCost(P.Owner);
                    }
                    else
                    {
                        Log.Warning($"{Owner.Name} does not have any unlocked troops. Using troopship base cost.");
                        return ship.GetCost(Owner);
                    }
                }
            }
        }

        public void Enqueue(Troop template, QueueItemType type, Goal goal = null)
        {
            if (goal != null && goal.PlanetBuildingAt == null)
                throw new InvalidOperationException($"CQ.Enqueue not allowed if Goal.PlanetBuildingAt is null!");

            var qi = new QueueItem(P)
            {
                isTroop     = true,
                TroopType   = template.Name,
                Goal        = goal,
                Cost        = template.ActualCost(P.Owner),
                Rush        = P.Owner.RushAllConstruction,
                QType       = type
            };

            if (goal != null) 
                goal.PlanetBuildingAt = P;

            AddToQueueAndPrioritize(qi);
        }

        public void EnqueueRefitShip(QueueItem item)
        {
            AddToQueueAndPrioritize(item);
        }

        // Bumps a refit job to the front of the queue (for both player and AI) so the ship
        // gets back into service fast - unless the current front item is about to finish, in
        // which case the refit slots second so a nearly-complete build isn't bumped.
        // Caller must hold the ConstructionQueue lock.
        void PromoteRefitToFront(QueueItem item)
        {
            if (Count <= 1)
                return; // the refit is already the only/first item

            int refitIndex = ConstructionQueue.IndexOf(item);
            if (refitIndex < 0)
                return;

            // The "about to finish" window scales with ProductionPace, same as build costs do.
            float turnsThreshold = 5 * P.Universe.ProductionPace;
            int insertAt = TurnsToCompleteFirstItem() <= turnsThreshold ? 1 : 0;
            if (refitIndex != insertAt)
                MoveTo(insertAt, refitIndex);
        }

        // Turns the planet needs to finish the item currently at the front of the queue.
        // Stored production is dumped into it first, then per-turn income covers the rest.
        int TurnsToCompleteFirstItem()
        {
            QueueItem building = BuildingNow;
            if (building == null)
                return 0;

            float remaining = (building.ProductionNeeded - P.ProdHere).LowerBound(0);
            if (remaining <= 0)
                return 0; // the stockpile alone finishes it next turn

            float perTurn = P.CurrentProductionToQueue.LowerBound(0.01f);
            return (int)Math.Ceiling(remaining / perTurn);
        }

        void AddToQueueAndPrioritize(QueueItem item)
        {
            lock (ConstructionQueue)
            {
                ConstructionQueue.Add(item);
                QueueSnapshotDirty = true;
                if (!P.OwnerIsPlayer)
                {
                    int totalFreighters = Owner.TotalFreighters;
                    ConstructionQueue.Sort(q => q.GetAndUpdatePriorityForAI(P, totalFreighters));
                }
                else
                {
                    // (maintainer decision) The base game sends the terraformer to the BACK of the
                    // queue on every governor add, so a colony following a plan raises the whole
                    // plan first and the terraformer last. The two delays do not cost the same: a
                    // terraformer is a decades-long investment that removes itself once the work is
                    // done - it borrows a tile rather than taking one - while a plan's building
                    // pushed back costs a few turns of yield. So while the colony is still short of
                    // the terraformers the game asked for, it keeps its place. The count is already
                    // bounded and only one is ever built at a time, so this cannot run away.
                    if (P.Owner.AutoBuildTerraformers && P.Owner.data.Traits.TerraformingLevel > 0
                        && !item.IsPlayerAdded && !TerraformerStillWanted())
                        DePrioritizeTerraformer();

                    if (item.Rush && item.QType == QueueItemType.OrbitalUrgent)
                    {
                        MoveTo(0, Count - 1);
                    }
                    else
                    {
                        // Ludoal fork (maintainer spec): the ordered priority list. A prioritized
                        // item slots above everything ranked worse or unranked, below same-or-better
                        // ranks - FIFO within a category, better-ranked categories stay ahead.
                        // Insertion only: reordering the list never reshuffles queues already filled.
                        int rank = ConstructionPriorityRank(item);
                        if (rank >= 0)
                        {
                            int insertAt = 0;
                            while (insertAt < Count - 1)
                            {
                                int r = ConstructionPriorityRank(ConstructionQueue[insertAt]);
                                if (r < 0 || r > rank)
                                    break;
                                ++insertAt;
                            }
                            MoveTo(insertAt, Count - 1);
                        }
                    }
                }

                // Ship and orbital refits jump the queue so the ship returns to service fast.
                if (item.Goal is RefitShip or RefitOrbital)
                    PromoteRefitToFront(item);
            }
        }

        // a queue item's rank in the ordered Prioritization list; -1 = not prioritized
        int ConstructionPriorityRank(QueueItem item)
        {
            string key = item.QType switch
            {
                QueueItemType.Scout => "Explorers",
                QueueItemType.ColonyShip or QueueItemType.ColonyShipClaim => "Colonizers",
                QueueItemType.RoadNode => "Projectors",
                QueueItemType.Freighter => "Freighters",
                QueueItemType.Troop => "Troops",
                QueueItemType.CombatShip => "MilitaryShips",
                QueueItemType.Orbital => item.Goal is ProcessResearchStation ? "ResearchStations"
                                       : item.Goal is MiningOps ? "MiningStations" : null,
                _ => null,
            };
            return key == null ? -1 : P.Universe.P.ConstructionPriorities.IndexOf(key);
        }

        void Finish(QueueItem q, bool success)
        {
            if (success) Finish(q);
            else         Cancel(q);
        }

        void Finish(QueueItem q)
        {
            lock (ConstructionQueue)
            {
                ConstructionQueue.Remove(q);
                QueueSnapshotDirty = true;
            }
        }

        public bool Cancel(Building b)
        {
            lock (ConstructionQueue)
            {
                QueueItem item = ConstructionQueue.Find(q => q.Building == b);
                item?.SetCanceled();
                return item != null;
            }
        }

        public bool Cancel(Goal g)
        {
            lock (ConstructionQueue)
            {
                QueueItem item = ConstructionQueue.Find(q => q.Goal == g);
                item?.SetCanceled();
                return item != null;
            }
        }

        public void Cancel(QueueItem q, bool refund = true)
        {
            if (refund)
                P.ProdHere += q.ProductionSpent / 2;

            q.pgs?.RemoveQueueItem();
            if (q.Goal != null)
            {
                if (q.Goal is BuildConstructionShip || q.Goal is BuildOrbital)
                    Owner.AI.RemoveGoal(q.Goal);

                if (q.Goal is FleetGoal fg)
                {
                    fg.Fleet?.RemoveGoal(q.Goal);
                    Owner.AI.RemoveGoal(q.Goal);
                }

                if (q.Goal is RefitOrbital)
                    q.Goal.OldShip?.AI.ClearOrders();
            }

            lock (ConstructionQueue)
            {
                ConstructionQueue.Remove(q);
                QueueSnapshotDirty = true;
            }
            if (q.isBuilding)
                P.RefreshBuildingsWeCanBuildHere();
        }

        public void PrioritizeProjector(Vector2 buildPos)
        {
            for (int i = 0; i < ConstructionQueue.Count; ++i)
            {
                QueueItem q = ConstructionQueue[i];
                if (q.isShip 
                    && q.DisplayName != null
                    && q.DisplayName.Contains("Subspace Projector")
                    && q.Goal.BuildPosition == buildPos)
                {
                    MoveTo(0, i);
                    break;
                }
            }
        }

        /// <summary>
        /// Relevant for player who is using Governor, since there is no prioritization ofr buildings
        /// but we need to prefer buildings which are not terraformers.
        /// </summary>
        public void DePrioritizeTerraformer()
        {
            for (int i = 0; i < ConstructionQueue.Count; ++i)
            {
                QueueItem q = ConstructionQueue[i];
                if (q.IsTerraformer)
                {
                    MoveTo(Count-1, i);
                    break;
                }
            }
        }

        // Make sure Governors prioritize buildings again after a new relevant building was unlocked
        // TODO - adjust this for ColonyBlueprints
        public void RemoveGovernorQueuedBuildingsTechnUnlock()
        {
            if (P.GovernorOff)
                return;

            bool hasExclusiveBlueprints = P.Blueprints?.Exclusive == true;
            for (int i = ConstructionQueue.Count - 1; i >= 0; --i)
            {
                QueueItem q = ConstructionQueue[i];
                // ⚠ A TERRAFORMER IS NEVER TRADED FOR A "BETTER" CIVILIAN BUILDING. This sweep
                // drops a civilian building whenever something worthier can be built instead -
                // sound for a yield building, ruinous for a terraformer: the governor queues it,
                // this cancels it for the next warehouse, then queues it again once that is done,
                // and each round trip BURNS HALF the production already spent on it (Cancel
                // refunds one half). That is the appearing-and-vanishing terraformer, and it is
                // not the demotion everyone assumed - it never moved, it was destroyed and
                // rebuilt (maintainer feedback, bench 597).
                if (q.IsCivilianBuilding
                    && !q.IsTerraformer
                    && (!q.IsPlayerAdded || hasExclusiveBlueprints)
                    // an entry the PLAN names is never traded for a "better" one: the plan builds in
                    // its own order, this sweep judges by score, and the two almost never agree - so
                    // every technology cancelled the rank being raised and half its production with
                    // it, then the governor queued it again (audit, bench 603)
                    && P.Blueprints?.IsRequired(q.Building) != true
                    && q.ProductionSpent < q.ProductionNeeded * 0.9f
                    && P.BestCivilianBuildingToBuildDifferentThen(P.GetBuildingsCanBuild(), q.Building))
                {
                    Cancel(q.Building);
                }
            }
        }

        public void RefitShipsBeingBuilt(Ship oldShip, IShipDesign newShip)
        {
            float refitCost = oldShip.RefitCost(newShip);
            foreach (QueueItem q in ConstructionQueue)
            {
                if (q.isShip && q.ShipData.Name == oldShip.Name)
                {
                    float percentCompleted = q.ProductionSpent / q.ActualCost;
                    q.ShipData = newShip;
                    q.Cost = q.ProductionSpent <= 10
                           ? newShip.GetCost(Owner) 
                           : q.Cost + refitCost*P.ShipCostModifier;
                }
            }
        }

        public bool ContainsShipDesignName(string name) => ConstructionQueue.Any(q => q.isShip && q.ShipData.Name == name);
        public bool ContainsTroopWithGoal(Goal g) => ConstructionQueue.Any(q => q.isTroop && q.Goal == g);

        public bool CancelShipyard()
        {
            QueueItem shipyard = ConstructionQueue.LastOrDefault(q => q.isShip && q.ShipData.IsShipyard);
            if (shipyard != null)
            {
                Cancel(shipyard);
                return true;
            }
            return false;
        }

        // ★ THE GESTURE FOLLOWS THE ROW THE PLAYER SEES (audit, bench 603). The colony screen
        // sorts the queue so that entries waiting for a tile or a plan sink to the bottom; the
        // arrows and the drag acted on index +/- 1 of the REAL list, so with a waiting entry
        // ranked above, "move up" swapped with a row displayed at the bottom and nothing moved
        // on screen - one click in two did nothing. The shown order is recomputed here from the
        // very predicate the screen sorts on, so the model needs no reference to the screen.
        QueueItem[] ShownOrder() => ConstructionQueue.OrderBy(q => IsWaiting(q) ? 1 : 0).ToArray();

        // Moves `item` by `relativeChange` ROWS of the shown order: it lands, in the real list,
        // where the entry occupying the target row stands. Stable, like the screen's own sort.
        public void ReorderShown(QueueItem item, int relativeChange)
        {
            lock (ConstructionQueue)
            {
                int oldIndex = ConstructionQueue.IndexOf(item);
                if (oldIndex == -1 || relativeChange == 0)
                    return;

                QueueItem[] shown = ShownOrder();
                int row = System.Array.IndexOf(shown, item);
                int target = row + relativeChange;
                if (row < 0 || (uint)target >= (uint)shown.Length)
                    return;

                int newIndex = ConstructionQueue.IndexOf(shown[target]);
                if (newIndex < 0 || newIndex == oldIndex)
                    return;

                ConstructionQueue.Reorder(oldIndex, newIndex);
                QueueSnapshotDirty = true;
            }
        }

        public void Reorder(QueueItem item, int relativeChange)
        {
            lock (ConstructionQueue)
            {
                // When dragging an item, some items could be removed or moved
                // while the dragging is in process and before the scroll list is updated.
                // So we always need to double-check the itemIndex and newIndex
                int oldIndex = ConstructionQueue.IndexOf(item);
                if (oldIndex == -1)
                    return;

                int newIndex = oldIndex + relativeChange;
                if ((uint)newIndex < ConstructionQueue.Count)
                {
                    ConstructionQueue.Reorder(oldIndex, newIndex);
                    QueueSnapshotDirty = true;
                }
            }
        }

        public void Swap(int swapTo, int currentIndex)
        {
            var cq = ConstructionQueue;
            lock (cq)
            {
                swapTo = swapTo.Clamped(0, cq.Count - 1);
                currentIndex = currentIndex.Clamped(0, cq.Count - 1);

                (cq[swapTo], cq[currentIndex]) = (cq[currentIndex], cq[swapTo]);
                QueueSnapshotDirty = true;
            }
        }

        public void MoveTo(int moveTo, int currentIndex)
        {
            lock (ConstructionQueue)
            {
                QueueItem item = ConstructionQueue[currentIndex];
                ConstructionQueue.RemoveAt(currentIndex);
                ConstructionQueue.Insert(moveTo, item);
                QueueSnapshotDirty = true;
            }
        }

        public void MoveToAndContinuousRushFirstItem()
        {
            if (Empty)
                return;

            lock (ConstructionQueue)
            {
                if (Count > 1)
                    MoveTo(0, Count - 1);

                ConstructionQueue[0].Rush = true;
            }
        }

        public void SwitchRushAllConstruction(bool rush)
        {
            lock (ConstructionQueue)
                for (int i = 0; i < ConstructionQueue.Count; ++i)
                     ConstructionQueue[i].Rush = rush;
        }

        public void ClearQueue()
        {
            lock (ConstructionQueue)
            {
                ConstructionQueue.Clear();
                QueueSnapshotDirty = true;
            }
            foreach (PlanetGridSquare tile in P.TilesList)
                tile.RemoveQueueItem(); // Clear all planned buildings from tiles
        }

        public bool FirstItemCanFeedUs()
        {
            lock (ConstructionQueue)
            {
                QueueItem first = BuildingNow;
                if (first == null || !first.isBuilding)
                    return false;

                return P.NonCybernetic && first.Building.ProducesFood
                    || P.IsCybernetic && first.Building.ProducesProduction;
            }
        }
    }
}
