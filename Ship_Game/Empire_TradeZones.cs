using System.Collections.Generic;
using System.Linq;
using SDGraphics;
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
        // ⚠ an explicit MARKER, never an inferred state: the conversion below happens exactly
        // once per game, and "no ship carries a filter" is also what a converted game looks like -
        // the two are indistinguishable without this.
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

        // ⚠ a dissolved zone RELEASES its hulls. A freighter pointing at a zone that is gone would
        // be enclosed by nothing and served by no one - the one state this design must never
        // leave behind.
        public void RemoveTradeZone(TradeZone zone)
        {
            ReleaseZoneFreighters(zone);
            TradeZones.Remove(zone);
        }

        // ★ Clearing Exclusive RELEASES the hulls, exactly as dissolving the zone does. Their zone
        // mark keeps them out of TotalFreighters and out of the idle pool, so a zone that stops
        // claiming them - MemberFreighters is empty with Exclusive off - would strand them:
        // counted by nobody, and shrinking the reserve, which is a share of TotalFreighters.
        // Setting the flag costs nothing, so only the clearing branch does any work.
        public void SetZoneExclusive(TradeZone zone, bool exclusive)
        {
            if (zone.Exclusive && !exclusive)
                ReleaseZoneFreighters(zone);

            zone.Exclusive = exclusive;
        }

        void ReleaseZoneFreighters(TradeZone zone)
        {
            if (zone.Id == 0)
                return;

            foreach (Ship s in OwnedShips)
                if (s.TradeZoneId == zone.Id)
                    AssignFreighterToZone(s, null);
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

        // ★★ THE ONE NEED. Every screen and the requisition read this and nothing else, so they
        // cannot drift apart: a figure computed twice is two figures.
        //
        // What a colony needs is what its own import rule says it needs - the rule the DISPATCH
        // obeys when it decides whether to send a hull. So this asks the colony rather than
        // reckoning on its behalf: one owner per good, and the book cannot fall out of step with
        // the traffic it describes. Each rule reasons about both halves of the question - what the
        // world burns while a run is in the air, and the room its store still has - and the trip
        // is MEASURED, not estimated: every planet keeps a moving average of its delivery times.
        //
        // ⚠ COLONISTS HAVE NO FLOW. Nothing in the game says how many colonists a world consumes
        // in a turn - their slots are a fullness ratio capped at five, an appetite rather than a
        // rate. Their term is therefore the base game's own ceiling, kept in the total because a
        // zone that wants people still needs hulls to carry them; it is a ceiling among flows, and
        // the tooltip is where that is said.
        public float RunsNeeded(Planet p, Goods goods, bool beforeServing = false)
        {
            // ★★ THE BOOK KEEPS NO ARITHMETIC OF ITS OWN. It ASKS the colony the same question the
            // dispatch asks, through the same function, so the two cannot drift apart: what the
            // dispatch serves, the book counts.
            if (goods == Goods.Food)
                return p.GetFoodImportSlots(beforeServing);

            if (goods == Goods.Production)
                return p.GetProdImportSlots(beforeServing);

            return p.ColonistsImportSlots; // a ceiling, not a flow - see above
        }

        static int ExportSlotsOf(Planet p, Goods goods)
            => goods == Goods.Food       ? p.FoodExportSlots
             : goods == Goods.Production ? p.ProdExportSlots
             : p.ColonistsExportSlots;

        // Can this zone put this good on a hull at all? An enclave loads among its own worlds;
        // a soft zone loads on the common ground. ⚠ the common ground here is the plain one -
        // colonies outside every enclave - never CommonExportGround, which reads the very needs
        // this pass is still writing.
        bool ZoneSupplies(TradeZone zone, Array<Planet> colonies, Array<Planet> common, Goods goods)
            => ExportSupply(zone.Exclusive ? colonies : common, goods) > 0;

        // What a set of worlds can send of one good.
        public int ExportSupply(Array<Planet> exporters, Goods goods)
        {
            int supply = 0;
            for (int i = 0; i < exporters.Count; ++i)
                supply += ExportSlotsOf(exporters[i], goods);

            return supply;
        }

        // What a PERIMETER wants, in whole hulls: what its importers burn, and nothing else.
        // Rounded UP - half a run still takes a hull.
        //
        // ⚠ NO CEILING HERE. Bounding the need by the available supply inside this function makes
        // one number do two jobs and the display job loses: a zone with no source left would read
        // "Required 0", which means "I want nothing". The ceiling lives at the one place that needs
        // it - the dispatch quota in MeasureZoneNeeds - where the supply is a STOCK walked in list
        // order: an exporter promised to one zone is not there for the next, and the set it is
        // taken on is the set the DISPATCH searches.
        //
        // beforeServing: the same book read one step earlier - what the perimeter wants BEFORE the
        // cargo now in the air is counted against it. Only the freighters overlay asks for it, and
        // only because it prints that very cargo as its numerator: net it out on one side while
        // adding it up on the other and the pair crosses over. One function, one definition.
        public int PerimeterNeed(Array<Planet> importers, Goods goods, bool beforeServing = false)
        {
            float need = 0;
            for (int i = 0; i < importers.Count; ++i)
                need += RunsNeeded(importers[i], goods, beforeServing);

            int whole = (int)need;
            if (need > whole)
                ++whole;

            return whole;
        }

        // ★ THE ONE BOOK OF NEED. Zones are read IN LIST ORDER, which is the dispatch priority the
        // player arranged, against a ledger of what each colony has already promised this turn. A
        // world shared by two zones is therefore counted once - by the zone ranked first - and the
        // next zone sees what is left, nought if everything was taken. Overlap stays legal (rings
        // of zones around a shared homeworld are a real design); what is forbidden is counting the
        // same berth twice and requisitioning a hull for each count.
        //
        // Idempotent and cheap, so a screen that needs the figure before the turn has run may call
        // it rather than read a nought it cannot tell from a measured zero.
        public void MeasureZoneNeeds()
        {
            // ⚠ a colony's consumption is served ONCE, by the best-ranked zone that names it; the
            // zones below see it at nought. Two zones asking for the same world's food would
            // requisition twice for a single delivery, so the book is kept while walking the list,
            // whose order IS the priority the player arranged.
            // ★★ THE LEDGER IS PER WORLD AND PER GOOD, never per world alone. A world may import
            // production from one enclave and people from another - two needs, two zones - and a
            // ledger kept per world would hand the first zone the world entire and lose the second
            // need in silence. One notion, one owner, and the notion is the PAIR.
            var servedFood = new HashSet<int>();
            var servedProd = new HashSet<int>();
            var servedCol  = new HashSet<int>();
            var stationLedger = new Map<int, int>();
            // ★★ TWO PASSES, AND THE ORDER IS THE POINT. The common loading ground is defined by
            // which enclaves are already served, so every zone's RAW need has to be written before
            // any ceiling is taken - otherwise the ground is drawn from last turn's book, or from
            // nothing at all on the first turn.
            var zoneColonies = new Array<Planet>[TradeZones.Count];
            var wantFood = new Array<Planet>[TradeZones.Count];
            var wantProd = new Array<Planet>[TradeZones.Count];
            var wantCol  = new Array<Planet>[TradeZones.Count];
            // read once: a zone's ability to supply a good does not change while we walk the list
            Array<Planet> plainCommon = ColoniesOutsideExclusiveZones();

            // PASS ONE, ROUND ONE - each zone claims the worlds it can actually SUPPLY: an
            // enclave from its own colonies, a soft zone from the common ground it loads on.
            // ⚠ this only DEPARTS two zones over one world, so the one able to serve it takes it.
            // It must never make a need disappear - see round two.
            for (int zi = 0; zi < TradeZones.Count; ++zi)
            {
                TradeZone zone = TradeZones[zi];
                var colonies = new Array<Planet>();
                foreach (int id in zone.Colonies)
                {
                    Planet p = Universe.GetPlanet(id);
                    if (p == null || p.Owner != this)
                        continue;

                    colonies.Add(p);  // the zone's own worlds - an enclave's far end
                }

                zoneColonies[zi] = colonies;
                wantFood[zi] = new Array<Planet>();
                wantProd[zi] = new Array<Planet>();
                wantCol[zi]  = new Array<Planet>();

                bool canFood = ZoneSupplies(zone, colonies, plainCommon, Goods.Food);
                bool canProd = ZoneSupplies(zone, colonies, plainCommon, Goods.Production);
                bool canCol  = ZoneSupplies(zone, colonies, plainCommon, Goods.Colonists);

                foreach (Planet p in colonies)
                {
                    if (canFood && servedFood.Add(p.Id)) wantFood[zi].Add(p);
                    if (canProd && servedProd.Add(p.Id)) wantProd[zi].Add(p);
                    if (canCol  && servedCol.Add(p.Id))  wantCol[zi].Add(p);
                }
            }

            // ★★ ROUND TWO - A NEED NOBODY CAN SERVE IS STILL A NEED. A world no zone claimed for
            // a good falls to the first zone in the list holding it, so what the dispatch serves
            // is what the book counts. ⚠ filtering the need by the capacity to serve it prints
            // "Need 0" beside colonies the dispatch is importing for (bench 592).
            for (int zi = 0; zi < TradeZones.Count; ++zi)
                foreach (Planet p in zoneColonies[zi])
                {
                    if (servedFood.Add(p.Id)) wantFood[zi].Add(p);
                    if (servedProd.Add(p.Id)) wantProd[zi].Add(p);
                    if (servedCol.Add(p.Id))  wantCol[zi].Add(p);
                }

            // ★ TWO FIGURES, TWO TRADES. The RAW need is what the importers burn - what the
            // screens show, and what any rule asking "is this zone served" must read. The CAPPED
            // one, below, is the dispatch quota, bounded by what the ground it searches can send.
            for (int zi = 0; zi < TradeZones.Count; ++zi)
            {
                TradeZone zone = TradeZones[zi];
                zone.NeedFood      = PerimeterNeed(wantFood[zi], Goods.Food);
                zone.NeedProd      = PerimeterNeed(wantProd[zi], Goods.Production);
                zone.NeedColonists = PerimeterNeed(wantCol[zi], Goods.Colonists);
                // the overlay's half of the same book, on the very same sets of colonies
                zone.NeedFoodBeforeServing      = PerimeterNeed(wantFood[zi], Goods.Food, beforeServing: true);
                zone.NeedProdBeforeServing      = PerimeterNeed(wantProd[zi], Goods.Production, beforeServing: true);
                zone.NeedColonistsBeforeServing = PerimeterNeed(wantCol[zi], Goods.Colonists, beforeServing: true);
                // and the sets themselves, read by the counters paired with these needs
                zone.CountsFood.Clear();      foreach (Planet p in wantFood[zi]) zone.CountsFood.Add(p.Id);
                zone.CountsProd.Clear();      foreach (Planet p in wantProd[zi]) zone.CountsProd.Add(p.Id);
                zone.CountsColonists.Clear(); foreach (Planet p in wantCol[zi])  zone.CountsColonists.Add(p.Id);
            }

            // ⚠ THE CEILING IS TAKEN ON THE SET THE DISPATCH SEARCHES, and the two regimes do not
            // search the same ground: an EXCLUSIVE zone loads among its own colonies, a SOFT one
            // on the common ground - which includes the enclaves whose own imports of that good
            // are covered, since an enclave owns its hulls and not its harvests.
            //
            // The COMMON ground is a stock walked in the list's own order - what one soft zone has
            // been promised, the next cannot be. An enclave's own capacity is not shared with
            // anyone by construction, so it is read fresh and taken whole.
            int foodLeft = ExportSupply(CommonExportGround(Goods.Food), Goods.Food);
            int prodLeft = ExportSupply(CommonExportGround(Goods.Production), Goods.Production);
            int colLeft  = ExportSupply(CommonExportGround(Goods.Colonists), Goods.Colonists);

            // PASS TWO - the dispatch quota, and the stock it is taken out of.
            for (int zi = 0; zi < TradeZones.Count; ++zi)
            {
                TradeZone zone = TradeZones[zi];
                Array<Planet> colonies = zoneColonies[zi];

                int capFood, capProd, capCol;
                if (zone.Exclusive)
                {
                    // its own ground, shared with nobody, so read fresh and taken whole
                    capFood = zone.NeedFood.UpperBound(ExportSupply(colonies, Goods.Food));
                    capProd = zone.NeedProd.UpperBound(ExportSupply(colonies, Goods.Production));
                    capCol  = zone.NeedColonists.UpperBound(ExportSupply(colonies, Goods.Colonists));
                }
                else
                {
                    capFood = zone.NeedFood.UpperBound(foodLeft);
                    capProd = zone.NeedProd.UpperBound(prodLeft);
                    capCol  = zone.NeedColonists.UpperBound(colLeft);
                    // taken out of the common stock: what this zone has been PROMISED is not there
                    // for the next one that asks - and the promise is the CAPPED figure, never the
                    // raw one, or a need nobody can serve would empty the stock on paper
                    foodLeft -= capFood;
                    prodLeft -= capProd;
                    colLeft  -= capCol;
                }

                int need = capFood + capProd + capCol;
                int raw  = zone.NeedFood + zone.NeedProd + zone.NeedColonists;

                // a station's hunger is its own - two zones naming the same body would ask for the
                // same run, so it keeps a book of its own
                foreach (Ship station in zone.Stations(this))
                {
                    Planet body = station.GetTether();
                    int key = body?.Id ?? 0;
                    int open = AI.CountGoals(g => g.IsSupplyingGoodsToStationStationGoal(station));
                    stationLedger.TryGetValue(key, out int taken);
                    int left = (open - taken).LowerBound(0);
                    need += left;
                    raw  += left;   // a station's hunger is real in both books
                    stationLedger[key] = taken + left;
                }

                zone.MeasuredNeed = need;
                zone.RawNeed = raw;
            }
        }

        // ★ THE ONE WRITER of a zone's edit, called from both doors: the Trade page's form and
        // the same form opened from a colony. An empty selection deletes the zone; a null zone
        // creates one.
        public TradeZone ApplyZoneEdit(TradeZone zone, Array<Planet> chosen, int quota, string name,
                                       bool exclusive, CargoPriority priority)
        {
            if (chosen.IsEmpty)
            {
                if (zone != null)
                    RemoveTradeZone(zone);
                return null;
            }

            zone ??= AddTradeZone(chosen[0]);

            zone.Colonies.Clear();
            foreach (Planet p in chosen)
                zone.Add(p);

            zone.Quota = quota;
            SetZoneExclusive(zone, exclusive);
            zone.Priority = priority;
            // an empty box keeps the name the zone already had, rather than leaving it nameless
            if (name.NotEmpty() && name != zone.Name)
                zone.ChangeName(name);

            return zone;
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
        // decision, not a display preference, and lives here rather than in the screen.
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

        // ⚠ "is this world inside ANY exclusive zone?" - a question about the SET, never about an
        // owner. A colony may sit in several zones of either regime: rings of exclusive zones
        // sharing a homeworld hub are a supported design, and the dispatch does not mind - a
        // planet's free slots close on whatever is already inbound, whoever sent it, so two zones
        // serving one world never deliver twice. Only the NEED must not be counted twice, and the
        // shared ledger below settles that, not a ban on the overlap.
        public TradeZone GetExclusiveZone(Planet planet)
            => TradeZones.Find(z => z.Exclusive && z.Serves(planet));

        // ⚠ A WORLD MAY SIT IN SEVERAL ENCLAVES, so "the" enclave holding it is not a question
        // with one answer. Anything asking whether a world is still owed something must ask them
        // ALL: one satisfied zone does not speak for a hungry one.
        // Is this world still WAITING for this good? An open import berth with nothing on its way.
        // ⚠ not the same question as "does it want any": a world served every turn wants something
        // every turn, and that answer keeps the door shut for every living enclave (bench 593).
        static bool StillWaiting(Planet p, Goods goods)
            => goods == Goods.Food       ? p.FreeFoodImportSlots > 0
             : goods == Goods.Production ? p.FreeProdImportSlots > 0
             : p.FreeColonistImportSlots > 0;

        bool AnyExclusiveZoneWaits(Planet planet, Goods goods)
        {
            for (int i = 0; i < TradeZones.Count; ++i)
            {
                TradeZone z = TradeZones[i];
                if (!z.Exclusive || !z.Serves(planet))
                    continue;

                foreach (Planet p in z.ColonyPlanets(this))
                    if (StillWaiting(p, goods))
                        return true;
            }

            return false;
        }

        // ★ THE COMMON LOADING GROUND, AND IT IS PER GOOD. An exclusive zone owns its HULLS, not
        // its harvests (bench 589): once its own imports of a good are covered, its colonies lend
        // that good's surplus to the realm, which comes to fetch it with its OWN hulls - a surplus
        // rotting in an enclave's store serves nobody.
        //
        // ⚠ IT READS THE RAW NEED, NEVER MeasuredNeed. A zone whose ground has run dry has a quota
        // of nought, and reading that calls a starving enclave "served" and takes its food away.
        // ⚠ and it is the LOADING end only: nothing here opens an enclave to deliveries.
        public Array<Planet> CommonExportGround(Goods goods)
        {
            var colonies = new Array<Planet>();
            for (int i = 0; i < OwnedPlanets.Count; ++i)
            {
                if (!AnyExclusiveZoneWaits(OwnedPlanets[i], goods))
                    colonies.Add(OwnedPlanets[i]);
            }

            return colonies;
        }

        // ★ THE COLONIES THE COMMON PASS MAY DELIVER TO: everything outside an exclusive zone.
        // An exclusive zone is served by the hulls it requisitioned and by nothing else, so its
        // worlds leave the empire's own dispatch - a world served by both keeps its berths
        // closed against the very freighters the zone took for it (maintainer feedback).
        public Array<Planet> ColoniesOutsideExclusiveZones()
        {
            var colonies = new Array<Planet>();
            for (int i = 0; i < OwnedPlanets.Count; ++i)
                if (GetExclusiveZone(OwnedPlanets[i]) == null)
                    colonies.Add(OwnedPlanets[i]);

            return colonies;
        }

        // Ludoal fork (player feedback): what stands on a body, when anything of ours does.
        // A mining rig or a research post orbits a body that is nobody's colony, so it never shows
        // up in GetPlanets() - and a zone may name it all the same, because a zone names BODIES.
        // A picker listing colonies and station bodies side by side must say which is which: they
        // are named the same way and behave nothing alike.
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
        // Ludoal fork (maintainer feedback): the two PER-SHIP trade filters - the area of operation
        // and the manual trade routes - become EXCLUSIVE ZONES on the first turn of a game that
        // carries them, and their editors go with them. ⚠ the conversion CLEARS the per-ship state
        // rather than offering a choice, so no hull keeps refusing a trade through an editor the
        // player has no way to open.
        //
        // Hulls are grouped by the SET OF PLANETS their filter resolves to, not by the rectangle:
        // ten freighters sharing a perimeter make one zone. Rectangles that merely overlap are NOT
        // merged - that would draw a zone the player never drew.
        //
        // ⚠ WHAT IS LOST, and the notice says it in full: an area of operation is a SHAPE and it
        // welcomes colonies founded inside it later. A zone is a LIST, taken at this instant.
        void ConvertLegacyTradeFiltersToZones()
        {
            // ⚠ two hulls whose perimeters SHARE a colony cannot become two exclusive zones - a
            // colony has one owner. They are MERGED rather than one of them silently losing the
            // shared world, so every colony keeps every hull that serves it. Disjoint perimeters
            // stay separate: overlapping rectangles alone are never a reason to merge.
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
                    // the per-ship filters are cleared here, so the zone is the only state left
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
                    // is named. Only a world that stopped being ours leaves (player feedback).
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
