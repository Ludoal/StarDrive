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
        // Colonists are left out of BOTH figures: their slots are counted in HEADS and never
        // converted into cargo loads, so counting them on one side only made Active outrun
        // Required on its own.
        public int RequiredFreighters(Empire owner)
        {
            int imports = 0;
            foreach (int id in Colonies)
            {
                Planet p = owner.Universe.GetPlanet(id);
                if (p == null || p.Owner != owner)
                    continue;

                imports += p.FoodImportSlots + p.ProdImportSlots;
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

                // food and production only, the same two the demand counts: a colonist run has
                // no counterpart in Required, and counting it here alone made the pair lie
                active += p.IncomingFoodFreighters + p.IncomingProdFreighters;
            }
            return active;
        }

        public override string ToString() => $"TradeZone {Name} ({Colonies.Count} colonies)";
    }
}
