using SDUtils;
using Ship_Game.Data.Serialization;

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
        [StarData] public string Name { get; private set; }
        [StarData] public Array<int> Colonies { get; private set; } = new();
        [StarData] public int Quota;

        [StarDataConstructor] TradeZone() { }

        public TradeZone(string name)
        {
            Name = name;
        }

        public void ChangeName(string newName) => Name = newName;

        public bool Serves(Planet planet) => Colonies.Contains(planet.Id);
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
            return imports;
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
            return active;
        }

        public override string ToString() => $"TradeZone {Name} ({Colonies.Count} colonies)";
    }
}
