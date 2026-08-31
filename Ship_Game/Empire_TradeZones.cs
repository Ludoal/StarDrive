using System.Collections.Generic;
using System.Linq;
using SDUtils;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public partial class Empire
    {
        // Ludoal fork: the zones live on the empire, the way patrol plans do - a zone is an asset
        // of the realm, not a property of the ship that happened to draw it.
        [StarData] public Array<TradeZone> TradeZones { get; private set; } = new();
        // ⚠ an explicit MARKER, never the state of an old flag: the conversion below must happen
        // exactly once per game, and "no ship carries a filter any more" is also what a converted
        // game looks like - the two are indistinguishable without this.
        [StarData] public bool LegacyTradeFiltersConverted;
        // ⚠ raised by the TURN, shown by the SCREEN: the conversion runs on the simulation thread
        // and a modal must not be summoned from there. Serialized, so a game saved between the
        // two never loses the notice.
        [StarData] public bool TradeZoneNoticePending;

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

        // ★ A colony belongs to AT MOST ONE exclusive zone (maintainer, 31 Aug). Exclusivity means
        // a single owner: a hull has one zone, a colony has one plan, and a colony has one
        // exclusive zone. Two owners would have both dispatching to the same world while the
        // perimeter counted it once, following whichever zone the lookup happened to meet first.
        // SOFT zones may still overlap freely - they own nothing, they borrow a share of the turn,
        // and two priorities serving one colony is a thing a player can mean.
        public TradeZone GetExclusiveZone(Planet planet)
            => TradeZones.Find(z => z.Exclusive && z.Serves(planet));

        // Called when a zone takes a colony, or becomes exclusive with colonies already on it.
        public void ClaimColoniesExclusively(TradeZone owner)
        {
            if (!owner.Exclusive)
                return;

            foreach (TradeZone other in TradeZones)
            {
                if (other == owner || !other.Exclusive)
                    continue;

                for (int i = other.Colonies.Count - 1; i >= 0; --i)
                    if (owner.Colonies.Contains(other.Colonies[i]))
                        other.Colonies.RemoveAt(i);
            }
        }

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
        // Ludoal fork (maintainer, 31 Aug '26): the two PER-SHIP trade filters - the area of
        // operation and the manual trade routes - become EXCLUSIVE ZONES on the first turn of a
        // game that carries them, and their editors go with them. Converting rather than offering
        // is the maintainer's call, and it buys what an offer could not: the old state DIES, so no
        // hull goes on refusing a trade because of an editor the player can no longer open.
        //
        // Hulls are grouped by the SET OF PLANETS their filter resolves to, not by the rectangle:
        // ten freighters sharing a perimeter make one zone. Rectangles that merely overlap are NOT
        // merged - that would draw a zone the player never drew.
        //
        // ⚠ WHAT IS LOST, and the notice says it in full: an area of operation was a SHAPE and it
        // welcomed colonies founded inside it later. A zone is a LIST, taken at this instant.
        void ConvertLegacyTradeFiltersToZones()
        {
            // ⚠ two hulls whose perimeters SHARE a colony cannot become two exclusive zones - a
            // colony has one owner. They are MERGED rather than one of them silently losing the
            // shared world: after the merge every colony is still served by every hull that used
            // to serve it, which is the conservative half of the trade. Disjoint perimeters stay
            // separate, so overlapping rectangles are still never merged for the sake of it.
            var owner = new Map<int, int>();   // planet id -> the id that leads its group
            var shipIds = new Map<Ship, Array<int>>();
            foreach (Ship s in OwnedShips)
            {
                if (!s.IsFreighter)
                    continue;

                Array<int> ids = LegacyFilterPlanetIds(s);
                if (ids.IsEmpty)
                    continue;

                shipIds[s] = ids;
                int lead = GroupLeadOf(owner, ids[0]);
                foreach (int id in ids)
                    MergeGroups(owner, lead, GroupLeadOf(owner, id));
            }

            var hulls = new Map<int, Array<Ship>>();
            var colonies = new Map<int, Array<int>>();
            foreach (KeyValuePair<Ship, Array<int>> pair in shipIds)
            {
                int lead = GroupLeadOf(owner, pair.Value[0]);
                if (!hulls.TryGetValue(lead, out Array<Ship> group))
                {
                    hulls[lead] = group = new Array<Ship>();
                    colonies[lead] = new Array<int>();
                }

                group.Add(pair.Key);
                foreach (int id in pair.Value)
                    colonies[lead].AddUnique(id);
            }

            foreach (KeyValuePair<int, Array<Ship>> group in hulls)
            {
                Array<int> ids = colonies[group.Key];
                TradeZone zone = AddTradeZone(Universe.GetPlanet(ids[0]));
                foreach (int id in ids)
                {
                    Planet p = Universe.GetPlanet(id);
                    if (p != null)
                        zone.Add(p);
                }

                zone.Exclusive = true;
                foreach (Ship s in group.Value)
                {
                    AssignFreighterToZone(s, zone);
                    // the filters die here: this is the whole reason to convert rather than offer
                    s.TradeRoutes.Clear();
                    s.AreaOfOperation.Clear();
                }
            }

            if (hulls.Count > 0)
                TradeZoneNoticePending = true;
        }

        // A plain union-find over planet ids: two perimeters that share a world end up under one
        // lead, and the lead names the zone they will become.
        static int GroupLeadOf(Map<int, int> owner, int id)
        {
            while (owner.TryGetValue(id, out int up) && up != id)
                id = up;

            owner[id] = id;
            return id;
        }

        static void MergeGroups(Map<int, int> owner, int a, int b)
        {
            if (a != b)
                owner[b] = a;
        }

        // What a hull's old filter actually named, as planet ids. Routes are already ids; an area
        // of operation is a shape, so it is resolved to the colonies standing inside it NOW.
        Array<int> LegacyFilterPlanetIds(Ship s)
        {
            var ids = new Array<int>();
            if (s.TradeRoutes != null)
                foreach (int id in s.TradeRoutes)
                    if (Universe.GetPlanet(id) != null)
                        ids.AddUnique(id);

            if (s.AreaOfOperation.NotEmpty)
            {
                foreach (Planet p in OwnedPlanets)
                    foreach (Rectangle ao in s.AreaOfOperation)
                        if (ao.HitTest(p.Position))
                        {
                            ids.AddUnique(p.Id);
                            break;
                        }
            }

            return ids;
        }

        public void RefreshTradeZones()
        {
            // once, on the first turn of a game that still carries the per-ship filters
            if (isPlayer && !LegacyTradeFiltersConverted)
            {
                LegacyTradeFiltersConverted = true;
                ConvertLegacyTradeFiltersToZones();
            }

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
