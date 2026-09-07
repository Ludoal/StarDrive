using System;
using System.Collections.Generic;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;

namespace Ship_Game
{
    // Ludoal fork (maintainer feedback): a trade zone is a NAMED LIST OF COLONIES that the
    // empire's freighters serve as a set. It carries no geometry: the dispatch pairs planets,
    // never surfaces, so a list of colonies is the native primitive - the same shape a ship's
    // own TradeRoutes already had, promoted from a scribble on one hull to an empire asset.
    //
    // Quota 0 means Auto: the zone's need is MEASURED rather than ordered. Any other number is
    // a standing order from the player.
    [StarDataType]
    public class TradeZone
    {
        // Ludoal fork (maintainer feedback): a STABLE number the hulls can name. Not the zone's
        // position in the list - that order is the dispatch priority and the player reorders it.
        // Zero means "not numbered yet": ids start at 1, so a zone restored from a save that
        // predates them is numbered by the housekeeping on its first turn, and no hull can point
        // at 0 since 0 means "no zone" on the other side too.
        [StarData] public int Id { get; private set; }
        // EXCLUSIVE: the zone owns its hulls instead of borrowing a share of the turn. The empire
        // keeps the fleet (build, scrap, refit) - an exclusive zone requisitions, it does not breed.
        [StarData] public bool Exclusive;
        // CargoPriority as an int: one of our own enumerations never enters the save graph.
        [StarData] public int PriorityValue;
        [StarData] public string Name { get; private set; }
        [StarData] public Array<int> Colonies { get; private set; } = new();
        [StarData] public int Quota;

        // Ludoal fork (maintainer feedback, Roland Johansen): the hulls this zone HOLDS BACK for
        // its stations - lent to it, unspent, and kept out of the rest of the turn. The stations
        // standing on its member bodies draw here rather than on the common pool, which is what
        // putting one in a zone buys: it is served on the zone's budget instead of taking the
        // first idle hull the moment it is hungry.
        // Never serialized - a fact about this turn, and a save reloads into a fresh pass.
        public Ship[] LentThisTurn = Array.Empty<Ship>();

        // ★ WHAT THIS ZONE MAY ASK FOR, once the zones above it in the list have taken their share
        // of any colony they hold in common. The need keeps ONE book (Lek, 31 Aug): the delivery
        // side already worked that way - a planet's free slots close on whatever is inbound,
        // whoever sent it - and this is the same law applied to the READING of the need. Without
        // it a world named by two zones is counted twice and requisitioned for twice, while only
        // one of the two can ever deliver. Not serialized: a fact of the turn.
        public int MeasuredNeed;
        // the same book split by good. The overlay shows the three lines the table sums, and
        // it READS them here rather than computing them again - that is the whole point of
        // one need: two screens that compute cannot agree, two screens that read cannot differ.
        // Never serialized either: a fact of the turn.
        public int NeedFood, NeedProd, NeedColonists;
        // The same three, read one step earlier: before the cargo already in the air was netted
        // out of them. ONLY the freighters overlay uses these, because it prints that cargo as its
        // numerator - see PerimeterNeed's beforeServing. Same function, same set of colonies, one
        // step apart, so the two can never drift into separate definitions. Not serialized either.
        public int NeedFoodBeforeServing, NeedProdBeforeServing, NeedColonistsBeforeServing;
        // ★ THE SAME BOOK, UNCAPPED. MeasuredNeed above is the dispatch QUOTA: it is bounded by
        // what the ground the dispatch searches can actually send, because a run needs a berth at
        // both ends. This one is what the importers burn, whatever anyone can do about it.
        // ⚠ they were one number until the bench of 6 Sep, and a zone whose supply had run dry
        // read "Required 0" - "I need nothing" written exactly like "nobody can serve me", which
        // are the two most different states a zone can be in. Any rule asking "is this zone
        // served" must read THIS one; the capped figure would call a starving zone satisfied.
        // Not serialized either: a fact of the turn.
        public int RawNeed;
        // ★ THE WORLDS THIS ZONE ACTUALLY COUNTS, per good - the ledger's answer, written by
        // MeasureZoneNeeds. A world named by two zones is counted by the better-ranked one, and
        // everything that pairs a figure with the need must read THESE sets, never Colonies:
        // ActiveFreighters and the overlay summed over every colony named, so a shared world's
        // traffic showed in the zone whose need did not count it - "3 / 0" (audit, bench 603).
        // Not serialized: a fact of the turn.
        public readonly HashSet<int> CountsFood = new(), CountsProd = new(), CountsColonists = new();
        public bool Counts(Planet p, Goods goods)
            => goods == Goods.Food       ? CountsFood.Contains(p.Id)
             : goods == Goods.Production ? CountsProd.Contains(p.Id)
             : CountsColonists.Contains(p.Id);


        [StarDataConstructor] TradeZone() { }

        public TradeZone(string name)
        {
            Name = name;
        }

        public void ChangeName(string newName) => Name = newName;
        public void SetId(int id) => Id = id;

        // ⚠ TradeFirst is refused here, not merely left out of the picker: it lifts the FOREIGN
        // pass above production and colonists at the scale of the empire, and a zone serves no
        // foreign planet. A save carrying it - hand-edited, or written by a later change of mind -
        // reads as Auto rather than as a lever with nothing at the end of it.
        public CargoPriority Priority
        {
            get
            {
                var p = (CargoPriority)PriorityValue;
                return p == CargoPriority.TradeFirst ? CargoPriority.Auto : p;
            }
            set => PriorityValue = value == CargoPriority.TradeFirst ? 0 : (int)value;
        }

        // The hulls that belong to this zone. Exclusive only: a soft zone borrows a share of the
        // turn and owns nothing, which is the whole difference between the two regimes.
        public Array<Ship> MemberFreighters(Empire owner)
        {
            var members = new Array<Ship>();
            if (!Exclusive || Id == 0)
                return members;

            foreach (Ship s in owner.OwnedShips)
                if (s.IsFreighter && s.TradeZoneId == Id)
                    members.Add(s);

            return members;
        }

        public bool Serves(Planet planet) => Colonies.Contains(planet.Id);

        // The stations standing on this zone's member bodies. A station is a SHIP tethered to a
        // body, so the zone names the body and the fleet answers with what stands on it - which
        // is also why a mineable or researchable body may be a member while owning nothing.
        public Array<Ship> Stations(Empire owner)
        {
            var stations = new Array<Ship>();
            foreach (Ship s in owner.OwnedShips)
            {
                if (!s.IsMiningStation && !s.IsResearchStation || !s.IsTethered)
                    continue;

                Planet body = s.GetTether();
                if (body != null && Colonies.Contains(body.Id))
                    stations.Add(s);
            }
            return stations;
        }
        public void Add(Planet planet) => Colonies.AddUnique(planet.Id);
        public void Remove(Planet planet) => Colonies.Remove(planet.Id);

        // A zone with no colony would serve EVERYWHERE, not nowhere: the route filter reads an
        // empty list as "no restriction". So an empty zone is never a valid state - it is
        // dissolved rather than kept as an enclosure that encloses nothing.
        public bool IsEmpty => Colonies.IsEmpty;
        public int NumColonies => Colonies.Count;

        // the colonies as planets, for the dispatch which works on planet lists
        public Array<Planet> ColonyPlanets(Empire owner)
        {
            var planets = new Array<Planet>();
            foreach (int id in Colonies)
            {
                Planet p = owner.Universe.GetPlanet(id);
                if (p != null && p.Owner == owner)
                    planets.Add(p);
            }
            return planets;
        }

        // ⚠ the lessons the old per-zone measure carried, and they now govern MeasureZoneNeeds
        // on the empire: the unit is the freighter BERTH, counted on the IMPORT side and on the
        // TOTAL slots - never the free ones, which fall to nought exactly when a zone is being
        // served (bench 544), and never bounded by the export side, which belongs to an enclosed
        // zone and never limited this one (bench 546). No rotation term: turning a backlog into a
        // fleet size needs a measured arrival rate, and inventing one produces a plausible number
        // that is wrong.

        // What this zone's stations are asking for, in berths. A station's hunger is already
        // expressed by its open supply goal, so the zone COUNTS those goals rather than testing
        // the hunger again - the goal stays the one signal, the zone only pays for it.
        public int StationDemand(Empire owner)
        {
            int berths = 0;
            foreach (Ship station in Stations(owner))
                berths += owner.AI.CountGoals(g => g.IsSupplyingGoodsToStationStationGoal(station));

            return berths;
        }

        // The freighters already converging on this zone. The colonies count what is inbound for
        // their own slot arithmetic, so this is a sum rather than a new count.
        public int ActiveFreighters(Empire owner)
        {
            int active = 0;
            foreach (int id in Colonies)
            {
                Planet p = owner.Universe.GetPlanet(id);
                if (p == null || p.Owner != owner)
                    continue;

                // the same three the demand counts, on the SAME sets the demand counted them on -
                // a world another zone counts for a good does not show its hulls here either
                if (CountsFood.Contains(id))      active += p.IncomingFoodFreighters;
                if (CountsProd.Contains(id))      active += p.IncomingProdFreighters;
                if (CountsColonists.Contains(id)) active += p.IncomingColonistsFreighters;
            }

            // ⚠ a run to a STATION lands on no colony's counter, so it would never show up above -
            // and the demand beside this figure DOES count stations. Two numbers a player reads
            // side by side must count the same set, or the pair says nothing (bench 556).
            Array<Ship> stations = Stations(owner);
            if (stations.NotEmpty)
            {
                foreach (Ship s in owner.OwnedShips)
                {
                    if (!s.IsFreighter || s.AI == null)
                        continue;

                    Ship target = s.AI.TradeTargetStation;
                    if (target != null && stations.Contains(target))
                        ++active;
                }
            }

            return active;
        }

        public override string ToString() => $"TradeZone {Name} ({Colonies.Count} colonies)";
    }
}
