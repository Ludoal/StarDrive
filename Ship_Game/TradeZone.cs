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
        // berth. Bounded by BOTH ends, because a zone's freighters only carry between its own
        // colonies - imports it cannot supply from inside ask for nothing.
        //
        // Counted on the TOTAL slots rather than the free ones (bench 544): read on what is
        // left, the figure fell to nought exactly when the zone was being served, so it said
        // zero next to an Active of four. Beside Active it must be the demand, not the
        // remainder - the pair is only readable if one is the need and the other the answer.
        //
        // Deliberately WITHOUT a rotation term. Turning a backlog into a fleet size is the
        // queueing law, and it needs a measured arrival rate; inventing one here would produce a
        // plausible number that is wrong. The free slots are the game's own answer to how many
        // freighters fit, and they already carry a flux term of their own.
        //
        // Colonists are left out: their slots are counted in HEADS and never converted into
        // cargo loads, so adding them would mix two units in one total.
        public int RequiredFreighters(Empire owner)
        {
            int imports = 0, exports = 0;
            foreach (int id in Colonies)
            {
                Planet p = owner.Universe.GetPlanet(id);
                if (p == null || p.Owner != owner)
                    continue;

                imports += p.FoodImportSlots + p.ProdImportSlots;
                exports += p.FoodExportSlots + p.ProdExportSlots;
            }
            return imports < exports ? imports : exports;
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

                active += p.IncomingFoodFreighters + p.IncomingProdFreighters
                        + p.IncomingColonistsFreighters;
            }
            return active;
        }

        public override string ToString() => $"TradeZone {Name} ({Colonies.Count} colonies)";
    }
}
