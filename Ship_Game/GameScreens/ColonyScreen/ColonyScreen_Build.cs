using System.Linq;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    public partial class ColonyScreen
    {
        bool ResetBuildableList;
        string FilterItemsText;

        readonly string BuildingsTabText = Localizer.Token(GameText.Buildings); // BUILDINGS
        readonly string ShipsTabText = Localizer.Token(GameText.Ships); // SHIPS
        readonly string TroopsTabText = Localizer.Token(GameText.Troops); // TROOPS

        void OnBuildableTabChanged(int tabIndex)
        {
            PlayerDesignsToggle.Visible    = BuildableTabs.IsSelected(ShipsTabText);
            BuildableList.EnableDragOutEvents = BuildableTabs.IsSelected(BuildingsTabText);
            ResetBuildableList = true;
        }

        void OnPlayerDesignsToggleClicked(ToggleButton button)
        {
            Universe.P.ShowAllDesigns = !Universe.P.ShowAllDesigns;
            PlayerDesignsToggle.IsToggled = !Universe.P.ShowAllDesigns;
            ResetBuildableList = true;
        }

        void ResetBuildableTabs()
        {
            int selected = BuildableTabs.SelectedIndex;

            BuildableTabs.ClearTabs();
            // TROOPS before SHIPS: SHIPS carries the designs toggle at its right, so it closes the row.
            BuildableTabs.AddTab(BuildingsTabText);
            if (P.CanBuildInfantry) BuildableTabs.AddTab(TroopsTabText);
            if (P.HasSpacePort)     BuildableTabs.AddTab(ShipsTabText);

            // The designs toggle rides just right of the SHIPS tab, wherever the row ends -
            // tab rects are valid as soon as AddTab returns.
            if (PlayerDesignsToggle != null && BuildableTabs.Tabs.Count > 0)
            {
                RectF last = BuildableTabs.Tabs[BuildableTabs.Tabs.Count - 1].Rect;
                PlayerDesignsToggle.Pos = new Vector2(last.Right + 6, BuildableTabs.Y + 1);
                PlayerDesignsToggle.PerformLayout(); // its word position derives from Pos
            }

            BuildableTabs.SelectedIndex = selected;
        }

        void UpdateBuildAndConstructLists(float elapsedTime)
        {
            if (P.HasSpacePort     && !BuildableTabs.ContainsTab(ShipsTabText) ||
                P.CanBuildInfantry && !BuildableTabs.ContainsTab(TroopsTabText))
            {
                ResetBuildableTabs();
            }

            if (BuildableTabs.IsSelected(BuildingsTabText))  
            {
                var buildingsCanBuild = P.GetBuildingsCanBuild();
                ResetBuildableList |= BuildableList.NumEntries != buildingsCanBuild.Count;

                string filter = FilterBuildableItems.Text.ToLower();
                if (ResetBuildableList || FilterItemsText != filter) 
                {
                    FilterItemsText = filter;
                    Building[] buildings = P.GetBuildingsCanBuild().Sorted(b => b.Name);
                    if (filter.NotEmpty())
                        buildings = buildings.Filter(b => b.Name.ToLower().Contains(filter));

                    BuildableList.SetItems(buildings.Select(b => new BuildableListItem(this, b)));
                }
            }
            else if (BuildableTabs.IsSelected(ShipsTabText))
            {
                // NOTE: Ships list is hierarchical, so checking if buildable ships list
                //       changed is also more complicated
                TryPopulateBuildableShips();
            }
            else if (BuildableTabs.IsSelected(TroopsTabText))
            {
                string[] troopTypes = P.Owner.GetTroopsWeCanBuild();
                ResetBuildableList |= BuildableList.NumEntries != troopTypes.Length;
                if (ResetBuildableList)
                {
                    Troop[] troopTemplates = troopTypes.Select(ResourceManager.GetTroopTemplate);
                    BuildableList.SetItems(troopTemplates.Select(t => new BuildableListItem(this, t)));
                }
            }

            if (!ConstructionQueue.IsDragging)
            {
                // Snapshot once under lock: the sim thread mutates the live queue while we read it.
                QueueItem[] queue = P.ConstructionQueueSnapshot;
                if (!ConstructionQueue.AllEntries.Select(item => item.Item).EqualElements(queue))
                {
                    var newItems = queue.Select(qi => new ConstructionQueueScrollListItem(qi));
                    ConstructionQueue.SetItems(newItems);
                }
            }
            ResetBuildableList = false;
        }

        class ShipCategory
        {
            public string Name;
            public readonly Array<IShipDesign> Ships = new();
            public int Size;
            public override string ToString() => $"Category {Name} Size={Size} Count={Ships.Count}";
        }

        ShipCategory[] BuildableShipHierarchy = Empty<ShipCategory>.Array;

        bool BuildableShipsChanged(ShipCategory[] newHierarchy)
        {
            return !BuildableShipHierarchy.EqualElements(newHierarchy, (catA, catB) =>
            {
                return catA.Name == catB.Name
                    && catA.Ships.EqualElements(catB.Ships, (shipA, shipB) => shipA.Name == shipB.Name);
            });
        }

        void TryPopulateBuildableShips()
        {
            IShipDesign[] buildableShips = Empty<IShipDesign>.Array;

            // enable all ships in the sandbox
            if (Universe.Debug && Universe.Screen is DeveloperUniverse)
            {
                buildableShips = ResourceManager.Ships.Designs.ToArr();
            }
            else if (P.Owner != null)
            {
                buildableShips = P.Owner.ShipsWeCanBuildSnapshot
                    .Filter(ship => (ship.IsBuildableByPlayer(Universe.Player) && Universe.Screen.Player.WeCanBuildThis(ship) || Universe.Debug)
                                    && !ship.IsResearchStation
                                    && !ship.IsMiningStation
                                    && !ship.IsConstructor
                                    && !ship.IsSubspaceProjector
                                    && !ship.IsDysonSwarmController);
            }

            string filter = FilterBuildableItems.Text.ToLower();
            if (filter.IsEmpty() && FilterItemsText.NotEmpty())
            {
                FilterItemsText = "";
                ResetBuildableList = true; // filter is empty so revert back to Ship Categories
            }

            if (filter.NotEmpty() && (ResetBuildableList || FilterItemsText != filter))
            {
                FilterItemsText = filter;
                var shipList = buildableShips.Filter(s => s.Name.ToLower().Contains(filter));
                shipList = shipList.SortedDescending(s => s.BaseStrength);
                BuildableList.SetItems(shipList.Select(s => new BuildableListItem(this, s)));
                return;
            }

            var categoryMap = new Map<string, ShipCategory>();

            foreach (IShipDesign ship in buildableShips)
            {
                string name = Localizer.GetRole(ship.Role, P.Owner);
                if (!categoryMap.TryGetValue(name, out ShipCategory c))
                {
                    c = new(){ Name = name, Size = ship.SurfaceArea };
                    categoryMap.Add(name, c);
                }
                c.Ships.Add(ship);
            }

            // first sort the categories by name:
            ShipCategory[] categories = categoryMap.Values.Sorted(c => c.Name);
            foreach (ShipCategory category in categories)
            {
                category.Ships.Sort((a, b) => // rank better ships as first:
                {
                    float diff = b.BaseStrength - a.BaseStrength;
                    if (diff.NotEqual(0)) return (int)diff;
                    return string.CompareOrdinal(b.Name, a.Name);
                });
            }

            if (ResetBuildableList || BuildableShipsChanged(categories))
            {
                BuildableShipHierarchy = categories;
                BuildableList.Reset();

                // and then sort each ship category individually by Strength
                foreach (ShipCategory category in categories)
                {
                    // and add to Build list
                    BuildableListItem catHeader = BuildableList.AddItem(new(this, category.Name));
                    foreach (IShipDesign ship in category.Ships)
                        catHeader.AddSubItem(new BuildableListItem(this, ship, !ship.IsShipyard));
                }
            }
        }

        void OnBuildableItemDoubleClicked(BuildableListItem item)
        {
            if (P.Owner != Player && !P.Universe.Debug)
                return;

            item.BuildIt(1);
        }

        // bench 447: ship info lives in the Description pane now, not a floating box -
        // and a PINNED ship holds the pane against hover changes
        bool PinnedShipHeld => PinnedBuildable?.Ship != null || PinnedQueue?.Item.isShip == true;
        Rectangle DescriptionPane => new(PFacilities.Rect.X, PFacilities.Rect.Y + 30,
                                         PFacilities.Rect.Width, PFacilities.Rect.Height - 35);

        void OnBuildableHoverChange(BuildableListItem item)
        {
            if (item?.Ship != null && !PinnedShipHeld)
                ShipInfoOverlay.ShowInRect(DescriptionPane, item.Ship);
            else if (!PinnedShipHeld && item?.Ship == null)
                ShipInfoOverlay.Hide();
        }

        void OnBuildableListDrag(BuildableListItem item, DragEvent evt, bool outside)
        {
            if (evt != DragEvent.End)
                return;

            if (outside && item != null) // TODO: somehow `item` can be null, not sure how it happens
            {
                Building b = item.Building;
                if (b != null)
                {
                    PlanetGridSquare tile = P.FindTileUnderMouse(Input.CursorPosition);
                    if (tile != null && Build(b, tile))
                        return;
                }
                // a genuine drop attempt outside the list that found no valid tile
                GameAudio.NegativeClick();
            }
            // released INSIDE the list: not a build attempt - the 75ms DragBeginDelay arms
            // a "drag" on any ordinary click, and this fall-through was buzzing every row
            // click on top of the click sound (bench 459 double-buzz, reported twice)
        }

        void OnConstructionItemReorder(ConstructionQueueScrollListItem item, int relativeChange)
        {
            P.Construction.Reorder(item.Item, relativeChange);
        }

        void OnConstructionItemHovered(ConstructionQueueScrollListItem item)
        {
            if (item != null && item.Item.isShip && !PinnedShipHeld)
                ShipInfoOverlay.ShowInRect(DescriptionPane, item.Item.ShipData);
            else if (!PinnedShipHeld && item?.Item.isShip != true)
                ShipInfoOverlay.Hide();
        }

        public bool Build(Building b, PlanetGridSquare where = null)
        {
            if (P.Construction.Enqueue(b, where, true))
            {
                GameAudio.AcceptClick();
                ClearItemsFilter();
                return true;
            }
            GameAudio.NegativeClick();
            return false;
        }

        public void Build(IShipDesign ship, int repeat = 1)
        {
            // Orbitals are added via marshalled goals that don't apply until the sim thread runs,
            // so the limit check can't see what we queued earlier in this same loop. Track it locally.
            int orbitalsQueued = 0;
            for (int i = 0; i < repeat; i++)
            {
                if (P.IsOutOfOrbitalsLimit(ship, orbitalsQueued))
                {
                    GameAudio.NegativeClick();
                    return;
                }

                if (ship.IsPlatformOrStation || ship.IsShipyard)
                {
                    P.AddOrbital(ship);
                    orbitalsQueued++;
                }
                else
                {
                    P.Construction.Enqueue(ship, QueueItem.PlayerQueueTypeFor(ship));
                }
            }

            GameAudio.AcceptClick();
        }

        public void Build(Troop troop, int repeat = 1)
        {
            for (int i = 0; i < repeat; i++)
            {
                P.Construction.Enqueue(troop, QueueItemType.Troop);
            }

            GameAudio.AcceptClick();
        }

        void ClearItemsFilter()
        {
            if (FilterItemsText.IsEmpty())
                return;

            FilterItemsText    = "";
            ResetBuildableList = true;
            FilterBuildableItems.Clear();
        }

        void OnClearFilterClick(UIButton b)
        {
            ClearItemsFilter();
        }
    }
}
