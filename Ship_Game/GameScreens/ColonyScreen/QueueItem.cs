using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Commands.Goals;
using Ship_Game.UI; // UITable.FitText: the shared ellipsis cut

namespace Ship_Game
{
    public delegate void QueueItemCompleted(bool success);

    [StarDataType]
    public class QueueItem
    {
        // Ludoal fork (bench 526): the queue type a PLAYER-CHOSEN ship is filed under.
        // ConstructionPriorityRank sorts by it, so the ship must carry its real role: scouts and
        // colony ships file as themselves, never as military. ⚠ the colony screen and the
        // empire-wide picker both call this - they must file the same ship identically.
        public static QueueItemType PlayerQueueTypeFor(IShipDesign ship)
            => ship.IsColonyShip           ? QueueItemType.ColonyShip
             : ship.Role == RoleName.scout ? QueueItemType.Scout
             : ship.IsFreighter            ? QueueItemType.Freighter
             :                               QueueItemType.CombatShip;

        [StarData] public Planet Planet;
        [StarData] public bool isBuilding;
        [StarData] public bool IsMilitary; // Military building
        [StarData] public bool IsTerraformer; 
        [StarData] public bool isShip;
        [StarData] public bool isOrbital;
        [StarData] public bool isTroop;
        [StarData] public IShipDesign ShipData;
        [StarData] public Building Building;
        [StarData] public string TroopType;
        [StarData] public Array<int> TradeRoutes = new();
        [StarData] public Array<Rectangle> AreaOfOperation = new();
        // Ludoal fork (bench 583): a refit keeps the hull inside the trade zone that paid for it -
        // the yard stamps the zone back onto the rebuilt freighter.
        [StarData] public int TradeZoneId;
        [StarData] public PlanetGridSquare pgs;
        [StarData] public string DisplayName;
        [StarData] public float Cost;
        [StarData] public float ProductionSpent;
        [StarData] public Goal Goal;
        [StarData] public int Priority;
        [StarData] public QueueItemType QType;
        [StarData] public float PriorityBonus { get; private set; } // Gets bigger as the queue is prioritized
        [StarData] public bool Rush;
        [StarData(DefaultValue = true)] public bool NotifyOnEmpty = true;
        [StarData] public bool IsPlayerAdded = false;
        [StarData(DefaultValue = true)] public bool TransportingColonists  = true;
        [StarData(DefaultValue = true)] public bool TransportingFood       = true;
        [StarData(DefaultValue = true)] public bool TransportingProduction = true;
        [StarData(DefaultValue = true)] public bool AllowInterEmpireTrade  = true;

        public bool IsCivilianBuilding => isBuilding && !IsMilitary;
        public Rectangle rect;
        public Rectangle removeRect;

        // production still needed until this item is finished
        public float ProductionNeeded => ActualCost - ProductionSpent;

        // is this item finished constructing?
        public bool IsComplete => ProductionSpent.GreaterOrEqual(ActualCost); // float imprecision

        // if TRUE, this QueueItem will be cancelled during next production queue update.
        // Saved: a cancel clicked while paused must survive a save taken before the next turn.
        [StarData] public bool IsCancelled;

        // production passes in a row during which this entry could never be placed; not saved,
        // a reload simply starts the count again (SBProduction.DropHopelessEntries)
        public byte HopelessPasses;

        public QueueItem() { }

        public QueueItem(Planet planet)
        {
            Planet = planet;
        }

        public void SetCanceled(bool state = true) => IsCancelled = state;

        public void DrawAt(UniverseState us, SpriteBatch batch, Vector2 at, int nameRoom = 0)
        {
            var r = new Rectangle((int)at.X, (int)at.Y, 29, 30);
            var tCursor = new Vector2(at.X + 40f, at.Y);
            var pbRect = new Rectangle((int)tCursor.X, (int)tCursor.Y + Fonts.Arial12Bold.LineSpacing + 4, 150, 18);
            var pb = new ProgressBar(pbRect, ActualCost, ProductionSpent);
            Graphics.Font font = Fonts.Arial10;

            // A caller whose row ends in a control on the NAME's own line - the colonies list
            // puts its CR box there - passes the room the name may take, and the shared cut
            // gives it an ellipsis on a whole word. 0 = the whole line is the name's, which is
            // what the colony screen's own queue hands over.
            string Fit(string s) => nameRoom > 0 ? UITable.FitText(Fonts.Arial12Bold, s, nameRoom) : s;

            if (isBuilding)
            {
                batch.Draw(Building.IconTex, r);
                string shownName = Fit(DrawnName);
                batch.DrawString(Fonts.Arial12Bold, shownName, tCursor, Color.White);
                // ★ an entry with no tile SAYS SO, and shows no progress bar: it is not stalled,
                // it has yielded its turn and takes a square the moment one frees up. A bar at
                // nought would read as a fault (bench 597).
                if (pgs == null)
                {
                    float nameW = Fonts.Arial12Bold.TextWidth(shownName);
                    batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.CqWaitingForTile),
                                     new Vector2(tCursor.X + nameW + 6, tCursor.Y), Color.Gray);
                }
                else if (IsTerraformer && Planet?.TerraformerWaitsForBlueprint == true)
                {
                    // Same shape as the tile wait above, and mutually exclusive with it: the entry
                    // holds its square, it just cannot start until the plan is far enough along.
                    // Without the words the player sees a queued building that never moves.
                    float nameW = Fonts.Arial12Bold.TextWidth(shownName);
                    batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.CqWaitingForBlueprint),
                                     new Vector2(tCursor.X + nameW + 6, tCursor.Y), Color.Gray);
                }
                else
                {
                    pb.Draw(batch);
                }
            }
            else if (isShip)
            {
                batch.Draw(ShipData.Icon, r);
                batch.DrawString(Fonts.Arial12Bold, Fit(DrawnName), tCursor, Color.White);
                pb.Draw(batch);
            }
            else if (isTroop)
            {
                Troop template = ResourceManager.GetTroopTemplate(TroopType);
                template.Draw(us, batch, r);
                batch.DrawString(Fonts.Arial12Bold, Fit(DrawnName), tCursor, Color.White);
                pb.Draw(batch);
            }

            if (Rush)
            {
                var rushCursor = new Vector2(at.X + 200f, at.Y + 22);
                batch.DrawString(font, Localizer.Token(GameText.BpContinuousRush), rushCursor, Color.IndianRed);
            }
        }

        public float ActualCost
        {
            get
            {
                float cost = Cost;
                if (isShip && !ShipData.IsSingleTroopShip)
                    cost *= Planet.ShipCostModifier; // single troop ships do not get shipyard bonus

                return (int)cost; // FB - int to avoid float issues in release which prevent items from being complete
            }
        }

        // The name DrawAt paints, before any cut. ⚠ ONE owner: a caller that clips the name
        // needs the whole string back for its tooltip, and rebuilding it there would give the
        // fleet suffix two authors. Not DisplayText - that one is a lookup key (it is matched
        // against "Subspace Projector") and carries no fleet suffix.
        public string DrawnName
        {
            get
            {
                if (isBuilding)
                    return Building.TranslatedName.Text;
                if (isTroop)
                    return TroopType;
                if (!isShip && !isOrbital)
                    return ""; // same guard DisplayText carries: no hull, no name to read

                string name = DisplayName.IsEmpty() ? ShipData.Name : DisplayName;
                return Goal is FleetGoal fg && fg.Fleet != null ? $"{name} ({fg.Fleet.Name})" : name;
            }
        }

        public string DisplayText
        {
            get
            {
                if (isBuilding)
                    return Building.TranslatedName.Text;
                if (isShip || isOrbital)
                    return DisplayName ?? ShipData.Name;
                if (isTroop)
                    return TroopType;
                return "";
            }
        }

        // This also increases the priority bonus of the item. So it will be bumped up in the list a little next time
        public float GetAndUpdatePriorityForAI(Planet planet,int totalFreighters)
        {
            float priority = 5000;
            Empire owner = planet.Owner;
            switch (QType)
            {
                case QueueItemType.OrbitalUrgent:
                case QueueItemType.ColonyShipClaim: priority = 0;                                                                               break;
                case QueueItemType.Building:        priority = planet.PrioritizeColonyBuilding(Building);                                       break;
                case QueueItemType.Troop:           priority = 0.2f + owner.AI.DefensiveCoordinator.TroopsToTroopsWantedRatio * 5;              break;
                case QueueItemType.Scout:           priority = owner.GetPlanets().Count * 0.02f;                                                break;
                case QueueItemType.Orbital:         priority = 1 + (owner.TotalOrbitalMaintenance / owner.AI.DefenseBudget.LowerBound(1) * 10); break;
                case QueueItemType.RoadNode:        priority = 0.5f + owner.AI.SpaceRoadsManager.NumOnlineSpaceRoads * 0.1f;                    break;
                case QueueItemType.ColonyShip: 
                    priority = (owner.GetPlanets().Count * (owner.IsExpansionists ? 0.005f : 0.01f));
                    if (Goal != null && !Goal.TargetPlanet.System.HasPlanetsOwnedBy(owner))
                        priority -= 0.5f;
                    
                    break;
                case QueueItemType.Freighter: 
                    priority =  totalFreighters < owner.GetPlanets().Count*1.5f ? 0 : 0.5f * totalFreighters / owner.FreighterCap;
                    break;
                case QueueItemType.CombatShip:      
                    priority = (owner.TotalWarShipMaintenance / owner.AI.BuildCapacity.LowerBound(1));
                    if (owner.IsMilitarists)
                        priority *= 0.5f;
                    break;
            }

            if (owner.IsAtWarWithMajorEmpire)
            {
                switch (QType)
                {
                    case QueueItemType.Troop:
                    case QueueItemType.CombatShip: priority *= 0.25f; PriorityBonus += 0.2f;  break;
                    case QueueItemType.Orbital:    priority *= 0.66f; break;
                }
            }

            switch (QType)
            {
                case QueueItemType.Scout:
                case QueueItemType.ColonyShip: PriorityBonus += 0.1f;  break;
                case QueueItemType.Building:   PriorityBonus += 0.15f; break;
                case QueueItemType.Freighter:  PriorityBonus += 0.2f;  break;
                default:                       PriorityBonus += 0.05f; break;
            }

            if (DisplayText.Contains("Subspace Projector") || Rush)
                PriorityBonus += 1f;

            PriorityBonus += ProductionSpent / Cost.LowerBound(1);
            return (priority - PriorityBonus);
        }

        public override string ToString() => $"QueueItem DisplayText={DisplayText}";
    }

    public enum QueueItemType
    {
        ColonyShip,
        ColonyShipClaim, // change to ColonyShipPriority when spinning a savegame version
        Freighter,
        Scout,
        Troop,
        CombatShip,
        Building,
        Orbital,
        OrbitalUrgent,
        RoadNode,
        SwarmController
    }

}
