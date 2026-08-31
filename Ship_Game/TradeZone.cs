using System;
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
        // STRICT: the zone owns its hulls instead of borrowing a share of the turn. The empire
        // keeps the fleet (build, scrap, refit) - a strict zone requisitions, it does not breed.
        [StarData] public bool Strict;
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

        // The hulls that belong to this zone. Strict only: a soft zone borrows a share of the
        // turn and owns nothing, which is the whole difference between the two regimes.
        public Array<Ship> MemberFreighters(Empire owner)
        {
            var members = new Array<Ship>();
            if (!Strict || Id == 0)
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

        // What the zone can put to work, in the game's own unit: a trade slot is a freighter
        // berth. Counted on its IMPORT side, on the TOTAL slots.
        //
        // Two corrections, both from bench 546 where Active read higher than Required:
        //  - it was bounded by both ends, min(import, export). That bound belongs to an
        //    ENCLOSED zone, whose freighters only carry between its own colonies. A zone of
        //    this regime borrows from the common pool and imports from the whole empire, so
        //    what it can employ is its import capacity - the export side never limited it.
        //  - the free slots were read first (bench 544), which fell to nought exactly when the
        //    zone was being served. Beside Active the figure must be the demand, not the
        //    remainder: the pair only reads if one is the need and the other the answer.
        //
        // Deliberately WITHOUT a rotation term. Turning a backlog into a fleet size is the
        // queueing law, and it needs a measured arrival rate; inventing one here would produce a
        // plausible number that is wrong.
        //
        // ⚠ colonists were left out of both figures on the reading that their slots count HEADS.
        // That is true of the EXPORT side alone (GetColonistsExportSlots returns the population
        // itself); the IMPORT side returns 1 to 5 BERTHS, the very unit food and production use.
        // Since only import slots are counted here, there was never a unit to reconcile - the
        // generalisation was mine, and it stood in this comment as a reason not to do a thing
        // that had no obstacle.
        public int RequiredFreighters(Empire owner)
        {
            int imports = 0;
            foreach (int id in Colonies)
            {
                Planet p = owner.Universe.GetPlanet(id);
                if (p == null || p.Owner != owner)
                    continue;

                imports += p.FoodImportSlots + p.ProdImportSlots + p.ColonistsImportSlots;
            }

            return imports + StationDemand(owner);
        }

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

                // the same three the demand counts - a pair only reads if both sides hold the
                // same goods, and all three are counted in berths on the import side
                active += p.IncomingFoodFreighters + p.IncomingProdFreighters
                        + p.IncomingColonistsFreighters;
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
