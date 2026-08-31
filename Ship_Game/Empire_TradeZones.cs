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
            zone.SetId(NextTradeZoneId());
            if (seed != null)
                zone.Add(seed);

            TradeZones.Add(zone);
            return zone;
        }

        // ⚠ a dissolved zone RELEASES its hulls. A freighter pointing at a zone that no longer
        // exists would be enclosed by nothing and served by no one - the one state this design
        // must never leave behind.
        public void RemoveTradeZone(TradeZone zone)
        {
            if (zone.Id != 0)
                foreach (Ship s in OwnedShips)
                    if (s.TradeZoneId == zone.Id)
                        s.TradeZoneId = 0;

            TradeZones.Remove(zone);
        }

        // Ids are handed out above the highest one in use, never reused: a number freed by a
        // dissolved zone must not come back and adopt the hulls of its predecessor.
        int NextTradeZoneId()
        {
            int max = 0;
            foreach (TradeZone z in TradeZones)
                if (z.Id > max)
                    max = z.Id;

            return max + 1;
        }

        public TradeZone GetTradeZoneById(int id)
            => id == 0 ? null : TradeZones.Find(z => z.Id == id);

        // ★ THE ONE WRITER of a freighter's membership. Three doors lead here - the Ships page,
        // the Trade page and the cargo's own Zone button - and a rule applied at one of them must
        // be applied at all three, which only holds if there is a single place to apply it.
        // Passing null releases the hull.
        public void AssignFreighterToZone(Ship freighter, TradeZone zone)
        {
            if (freighter == null || !freighter.IsFreighter)
                return;

            freighter.TradeZoneId = zone?.Id ?? 0;
        }

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
        // What stands on a body, when anything of ours does. A picker that lists colonies and
        // station bodies side by side must say which is which: they are named the same way and
        // behave nothing alike.
        public string StationKindOn(Planet body)
        {
            foreach (Ship s in OwnedShips)
            {
                if (!s.IsTethered || s.GetTether() != body)
                    continue;

                if (s.IsMiningStation)   return Localizer.Token(GameText.TzMiningStation);
                if (s.IsResearchStation) return Localizer.Token(GameText.TzResearchStation);
            }
            return "";
        }

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
                // a zone restored from a save older than the numbering has none: it gets one here,
                // on the first turn, before anything can point at it
                if (zone.Id == 0)
                    zone.SetId(NextTradeZoneId());
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
                    RemoveTradeZone(zone);
            }
        }
    }
}
