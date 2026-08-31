using System.Linq;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;

namespace Ship_Game
{
    public partial class Empire
    {
        // Ludoal fork: the zones live on the empire, the way patrol plans do - a zone is an asset
        // of the realm, not a property of the ship that happened to draw it.
        [StarData] public Array<TradeZone> TradeZones { get; private set; } = new();

        public TradeZone AddTradeZone(Planet seed)
        {
            var zone = new TradeZone(GetNewTradeZoneName(seed?.Name ?? Name));
            if (seed != null)
                zone.Add(seed);

            TradeZones.Add(zone);
            return zone;
        }

        public void RemoveTradeZone(TradeZone zone) => TradeZones.Remove(zone);

        // The list order IS the priority: when two zones want more freighters than the pool
        // holds, the one placed first is served first. Moving a zone is therefore a game
        // decision, not a display preference - which is why it lives here and not in the screen.
        public void MoveTradeZone(TradeZone zone, bool up)
        {
            int i = TradeZones.IndexOf(zone);
            int j = up ? i - 1 : i + 1;
            if (i < 0 || j < 0 || j >= TradeZones.Count)
                return;

            TradeZones[i] = TradeZones[j];
            TradeZones[j] = zone;
        }

        string GetNewTradeZoneName(string basis)
        {
            string baseName = $"{basis} Trade";
            string uniqueName = baseName;
            int suffix = 1;
            while (TradeZones.Any(z => z.Name == uniqueName))
                uniqueName = $"{baseName}-{suffix++}";

            return uniqueName;
        }

        public TradeZone GetTradeZone(Planet planet) => TradeZones.Find(z => z.Serves(planet));

        // Ludoal fork (maintainer feedback, Roland Johansen): the bodies our STATIONS stand on.
        // A mining rig or a research post orbits a body that is nobody's colony, so it never
        // shows up in GetPlanets() - and a zone may name it all the same, because a zone names
        // BODIES and what stands on them is the fleet's business.
        public Array<Planet> StationBodies()
        {
            var bodies = new Array<Planet>();
            foreach (Ship s in OwnedShips)
            {
                if (!s.IsMiningStation && !s.IsResearchStation || !s.IsTethered)
                    continue;

                Planet body = s.GetTether();
                if (body != null && body.Owner != this)
                    bodies.AddUnique(body);
            }
            return bodies;
        }

        // Housekeeping, the twin of RefreshTradeRoutes on a ship: a colony that stops being ours
        // leaves the zones that named it, and a zone left without a single colony is dissolved -
        // an empty list reads as "everywhere" downstream, so it must not survive.
        public void RefreshTradeZones()
        {
            for (int i = TradeZones.Count - 1; i >= 0; --i)
            {
                TradeZone zone = TradeZones[i];
                for (int j = zone.Colonies.Count - 1; j >= 0; --j)
                {
                    Planet planet = Universe.GetPlanet(zone.Colonies[j]);
                    // ⚠ a member is not always a colony: a mineable or researchable body carries a
                    // STATION and owns nothing, so the ownership test would evict it the turn it
                    // was named. Only a world that stopped being ours leaves (Roland's ask).
                    if (planet == null
                        || !planet.IsMineable && !planet.IsResearchable && planet.Owner != this)
                        zone.Colonies.RemoveAt(j);
                }

                if (zone.IsEmpty)
                    TradeZones.RemoveAt(i);
            }
        }
    }
}
