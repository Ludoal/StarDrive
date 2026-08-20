using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public partial class ColonyScreen
    {
        int PFacilitiesPlayerTabSelected;
        // bench 444: the description pins of the two right-column lists (LIST tab pattern)
        BuildableListItem PinnedBuildable;
        ConstructionQueueScrollListItem PinnedQueue;

        // Gets the item which we want to use for detail info text
        object GetHoveredDetailItem(InputState input)
        {
            // bench 444: a clicked row PINS the description - while a pin is set, hover
            // must not steal the panel (the pin's whole point, bench 429). A pin whose
            // row left its list (filter, tab switch, item built) dies with it.
            if (PinnedBuildable != null)
            {
                bool alive = false;
                foreach (BuildableListItem e in BuildableList.AllEntries)
                    if (e == PinnedBuildable) { alive = true; break; }
                if (!alive) ClearListPins();
                else return (object)PinnedBuildable.Building ?? (object)PinnedBuildable.Troop ?? PinnedBuildable.Ship;
            }
            if (PinnedQueue != null)
            {
                bool alive = false;
                foreach (ConstructionQueueScrollListItem e in ConstructionQueue.AllEntries)
                    if (e == PinnedQueue) { alive = true; break; }
                if (!alive) ClearListPins();
                else if (PinnedQueue.Item.Building != null) return PinnedQueue.Item.Building;
                else if (PinnedQueue.Item.TroopType != null) return ResourceManager.GetTroopTemplate(PinnedQueue.Item.TroopType);
                else if (PinnedQueue.Item.isShip) return PinnedQueue.Item.ShipData;
            }

            // TODO: replace with a popup window
            if (BuildableList.HitTest(input.CursorPosition))
            {
                foreach (BuildableListItem e in BuildableList.AllEntries)
                {
                    if (e.Hovered)
                    {
                        if (e.Building != null) return e.Building;
                        if (e.Troop != null) return e.Troop;
                        if (e.Ship != null) return e.Ship; // ship: blank text, the overlay seats in the pane
                    }
                }
            }
            else if (ConstructionQueue.HitTest(input.CursorPosition))
            {
                foreach (ConstructionQueueScrollListItem e in ConstructionQueue.AllEntries)
                {
                    if (e.Hovered)
                    {
                        if (e.Item.Building != null) return e.Item.Building;
                        if (e.Item.TroopType != null) return ResourceManager.GetTroopTemplate(e.Item.TroopType);
                        if (e.Item.isShip) return e.Item.ShipData;
                    }
                }
            }

            // bench 429: a pin FREEZES the panel - while one is set, wandering over other
            // tiles must not steal the description (that is the pin's whole point)
            if (SubColonyGrid.SelectedIndex == 0 && PinnedBuilt == null) // tiles are only visible in the MAP view
            {
                foreach (PlanetGridSquare pgs in P.TilesList)
                {
                    if (pgs.TroopsAreOnTile)
                    {
                        for (int i = 0; i < pgs.TroopsHere.Count; ++i)
                            if (pgs.TroopsHere[i].ClickRect.HitTest(input.CursorPosition))
                                return pgs.TroopsHere[i];
                    }
                }

                foreach (PlanetGridSquare pgs in P.TilesList)
                    if (pgs.ClickRect.HitTest(input.CursorPosition))
                        return pgs;
            }
            else if (LastBuiltHover != null)
            {
                // LIST view: the hovered row's tile, live - OnHovered clears it the moment
                // the cursor leaves the row (bench 426)
                return LastBuiltHover;
            }

            // no live hover in EITHER view: the click-pinned building holds the panel
            // (bench 427: the pin works from MAP tiles too, so it must serve MAP's misses)
            if (PinnedBuilt != null)
                return PinnedBuilt;

            return null; // default: use planet description text
        }

        public void OnPFacilitiesTabChange(int tabindex)
        {
            // Using PlayerSelectedTab here to be able to return to the tab the player selected when there is no Detail Info item.
            // So if the player selected the trade tab, then viewed a planet tile and then moved the cursor away, the trade tab will be set again
            if (DetailInfo == null)
                PFacilitiesPlayerTabSelected = tabindex;
        }

        public override bool HandleInput(InputState input)
        {
            // Ludoal fork: Esc, right-click and the close cross all close the colony's tab
            // WITH the seat routing - intercepted before the base popup dismiss and the
            // child pass, which would exit bare.
            // A right-click landing on the COLONY frame belongs to its content - the tile
            // scrap prompt, the list rows - never to "close the page". The close intercept
            // ran before the tiles ever saw the click and ate the scrap gesture (bench 419).
            // bench 447: ALT+click on a queue ROW sends it to the TOP - Ctrl never reaches
            // the game intact from the maintainer's Mac (the VM turns Ctrl+click into a
            // right-click), so the gesture moves to Alt and right-click keeps its dismiss.
            if (input.IsAltKeyDown && input.LeftMouseClick
                && ConstructionQueue.HitTest(input.CursorPosition))
            {
                foreach (ConstructionQueueScrollListItem e in ConstructionQueue.AllEntries)
                {
                    if (e.Hovered)
                    {
                        QueueItem toTop = e.Item;
                        GameAudio.AcceptClick();
                        P.Universe.Screen.RunOnSimThread(() =>
                        {
                            int index = P.ConstructionQueue.IndexOf(toTop);
                            if (index > 0)
                                P.Construction.MoveTo(0, index);
                        });
                        break;
                    }
                }
                return true; // over the queue, the gesture is the list's - never the dismiss
            }

            bool rightClickOnColonyFrame = input.RightMouseClick && SubColonyGrid.Rect.HitTest(input.CursorPosition);
            if ((CanEscapeFromScreen && (input.Escaped || (input.RightMouseClick && !ClickedTroop && !rightClickOnColonyFrame)))
                || (input.LeftMouseClick && CloseBtn.Rect.HitTest(input.CursorPosition)))
            {
                GameAudio.EchoAffirmative();
                CloseColonyPage();
                return true;
            }

            // Ludoal fork: the live top bar and the visible band, like every page - the
            // universe's own input does not run under a stacked colony.
            if (Eui.HandleInput(input, caller: this))
                return true;

            // the COLONY frame's own tab row (MAP | LIST) - the frame is drawn by hand,
            // so its input is served by hand too
            if (SubColonyGrid.HandleInput(input))
                return true;

            // the description elevator (bench 429): the wheel walks the bottom panel's
            // content - pinned or not - from anywhere on the page except the lists, which
            // keep their own scroll. Clamped to the content measured at the last draw.
            if ((input.ScrollIn || input.ScrollOut) && DescriptionPaneUp
                && !(BuiltList.Visible && BuiltList.HitTest(input.CursorPosition))
                && !BuildableList.HitTest(input.CursorPosition)
                && !ConstructionQueue.HitTest(input.CursorPosition))
            {
                if (input.ScrollIn)  DescriptionScroll = (DescriptionScroll - 48f).LowerBound(0f);
                else                 DescriptionScroll = (DescriptionScroll + 48f).UpperBound(MaxDescriptionScroll);
                return true;
            }

            // always get the currently hovered item
            DetailInfo = GetHoveredDetailItem(input);

            // If there is a detail info, display the Description TAB, else display last tab the player selected.
            PFacilities.SelectedIndex = DetailInfo == null ? PFacilitiesPlayerTabSelected : 2; // Ludoal fork: Description is index 2 since the Stats+ tab

            if (!FilterBuildableItems.HandlingInput && !PlanetName.HandlingInput &&  HandleCycleColoniesLeftRight(input))
                return true;

            FilterBuildableItemsLabel.Color = FilterBuildableItems.HandlingInput ? Color.White : Color.Gray;
            P.UpdateIncomes();

            // We are monitoring AI Colonies
            if (P.Owner != Player && !Log.HasDebugger)
            {
                // The read-only early-out skips base.HandleInput, so the close cross - an
                // Add()ed element - never gets served on an infiltrated colony (right-click
                // works: that path belongs to the universe, not this panel). Serve the cross
                // explicitly before handing the rest of the input back.
                if (CloseBtn.HandleInput(input))
                    return true;
                // Input not captured, let Universe Screen manager what happens
                return false;
            }

            // tiles only exist on screen in the MAP view - the LIST view's rows are
            // Add()ed children, served by the base pass below
            if (SubColonyGrid.SelectedIndex == 0 && HandleTroopSelect(input))
                return true;

            // The COLONY frame consumes EVERY right-click in its perimeter, whatever the
            // outcome - scrap prompt, or a no-op on an empty/non-scrappable tile. Without
            // this the unconsumed click flowed down to the base popup dismiss and closed
            // the page bare; the same gesture at the same spot must never close-or-not
            // depending on the tile's state (bench 420).
            if (rightClickOnColonyFrame)
                return true;

            // update all Added UI elements
            if (base.HandleInput(input))
                return true;

            if (HandleExportImportButtons(input))
                return true;

            return false;
        }

        bool HandleTroopSelect(InputState input)
        {
            ClickedTroop = false;
            foreach (PlanetGridSquare pgs in P.TilesList)
            {
                if (!pgs.ClickRect.HitTest(MousePos))
                {
                    pgs.Highlighted = false;
                }
                else
                {
                    if (!pgs.Highlighted)
                    {
                        GameAudio.ButtonMouseOver();
                    }

                    pgs.Highlighted = true;
                }

                if (pgs.TroopsAreOnTile)
                {
                    for (int i = 0; i < pgs.TroopsHere.Count; ++i)
                    {
                        Troop troop = pgs.TroopsHere[i];
                        if (troop.ClickRect.HitTest(MousePos))
                        {
                            if (input.RightMouseClick && troop.Loyalty == Player)
                            {
                                Ship troopShip = troop.Launch(pgs);
                                if (troopShip != null)
                                {
                                    GameAudio.TroopTakeOff();
                                    ClickedTroop = true;
                                }
                                else
                                {
                                    GameAudio.NegativeClick();
                                }
                            }

                            return true;
                        }
                    }
                }
            }

            if (!ClickedTroop && (P.OwnerIsPlayer || P.Universe.Debug))
            {
                foreach (PlanetGridSquare pgs in P.TilesList)
                {
                    if (pgs.ClickRect.HitTest(input.CursorPosition))
                    {
                        var bRect = new Rectangle(pgs.ClickRect.X + pgs.ClickRect.Width / 2 - 32,
                            pgs.ClickRect.Y + pgs.ClickRect.Height / 2 - 32, 50, 50);
                        if (pgs.BuildingOnTile && bRect.HitTest(input.CursorPosition) && Input.RightMouseClick)
                        {
                            if (pgs.Building.Scrappable)
                                PromptScrapBuilding(pgs); // shared with the LIST view's delete button

                            ClickedTroop = true;
                            return true;
                        }

                        // bench 427: the pin principle reaches MAP - left-click on a building
                        // tile pins/unpins it like a LIST row click (the bio corner keeps its
                        // own gesture below)
                        var bioCorner = new Rectangle(pgs.ClickRect.X, pgs.ClickRect.Y, 20, 20);
                        if (pgs.BuildingOnTile && bRect.HitTest(input.CursorPosition)
                            && Input.LeftMouseClick && !bioCorner.HitTest(input.CursorPosition))
                        {
                            PinnedBuilt = PinnedBuilt == pgs ? null : pgs;
                            DescriptionScroll = 0f;
                            GameAudio.AcceptClick();
                            ClickedTroop = true;
                            return true;
                        }

                        var bioRect = new Rectangle(pgs.ClickRect.X,pgs.ClickRect.Y, 20, 20);
                        if (pgs.Biosphere
                            && bioRect.HitTest(input.CursorPosition) && (Input.RightMouseClick|| Input.LeftMouseClick))
                        {
                            BioToScrap     = pgs;
                            string message = Localizer.Token(GameText.DoYouWishToScrap);
                            var messageBox = new MessageBoxScreen(P.Universe.Screen, message);
                            messageBox.Accepted = ScrapBioAccepted;
                            ScreenManager.AddScreen(messageBox);
                            ClickedTroop = true;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        void OnChangeColony(int change)
        {
            // bench 432: the arrows walk the SHARED spatial order (SpatialColonyOrder),
            // like the Colonies table and the keyboard tour - never a private list
            Planet[] planets = P.Owner.SpatialColonyOrder();
            int newIndex = System.Array.IndexOf(planets, P) + change;
            if (newIndex >= planets.Length) newIndex = 0;
            else if (newIndex < 0) newIndex = planets.Length - 1;

            Planet nextOrPrevPlanet = planets[newIndex];
            if (nextOrPrevPlanet != P)
            {
                // Ludoal fork: the walk re-arms the hosted seat FIRST - the fresh screen's
                // constructor reads it, so the row rebuilds with the new planet's name on the
                // tab, same group, same Esc origin.
                UniverseScreen u = Universe.Screen;
                if (u.HostedTabTitle != null)
                    u.HostColonyTab(nextOrPrevPlanet, u.HostedTabGroup, u.HostedTabOrigin);
                u.PanToPlanetKeepZoom(nextOrPrevPlanet); // the walk pans, no zoom
                ExitScreen();
                ScreenManager.AddScreen(new ColonyScreen(u, nextOrPrevPlanet, Eui,
                    GovernorDetails.CurrentTabIndex, PFacilitiesPlayerTabSelected,
                    SubColonyGrid.SelectedIndex)); // the walk keeps the MAP/LIST choice
            }
        }

        // the Home button - straight to the capital, the arrows' walk mechanics
        void GoToHomeworld()
        {
            if (!Player.GetCurrentCapital(out Planet home) || home == P)
                return;
            UniverseScreen u = Universe.Screen;
            if (u.HostedTabTitle != null)
                u.HostColonyTab(home, u.HostedTabGroup, u.HostedTabOrigin);
            u.PanToPlanetKeepZoom(home);
            ExitScreen();
            ScreenManager.AddScreen(new ColonyScreen(u, home, Eui,
                GovernorDetails.CurrentTabIndex, PFacilitiesPlayerTabSelected));
        }

        bool HandleCycleColoniesLeftRight(InputState input)
        {
            bool canView = (P.Universe.Debug || P.OwnerIsPlayer);
            if (canView && (input.Left || input.Right))
            {
                int change = input.Left ? -1 : +1;
                OnChangeColony(change);
                return true; // planet changed, ColonyScreen will be replaced
            }

            return false;
        }

        bool HandleExportImportButtons(InputState input)
        {
            // auto-supplies: a greyed list is read-only - the Auto checkbox owns QUI decides
            if (P.FoodManual && FoodDropDown.r.HitTest(input.CursorPosition) && input.LeftMouseClick)
            {
                FoodDropDown.Toggle();
                GameAudio.AcceptClick();
                P.FS = (Planet.GoodState) ((int) P.FS + (int) Planet.GoodState.IMPORT);
                if (P.FS > Planet.GoodState.EXPORT)
                    P.FS = Planet.GoodState.STORE;
                return true;
            }

            if (P.ProdManual && ProdDropDown.r.HitTest(input.CursorPosition) && input.LeftMouseClick)
            {
                ProdDropDown.Toggle();
                GameAudio.AcceptClick();
                P.PS = (Planet.GoodState) ((int) P.PS + (int) Planet.GoodState.IMPORT);
                if (P.PS > Planet.GoodState.EXPORT)
                    P.PS = Planet.GoodState.STORE;
                return true;
            }

            // Ludoal fork (wishlist): the colonist flow cycles Stay -> Bring in -> Resettle
            if (P.ColonistsManual && ColonistsDropDown.r.HitTest(input.CursorPosition) && input.LeftMouseClick)
            {
                ColonistsDropDown.Toggle();
                GameAudio.AcceptClick();
                P.CS = P.CS >= Planet.GoodState.EXPORT ? Planet.GoodState.STORE : (Planet.GoodState)((int)P.CS + 1);
                return true;
            }
            return false;
        }
    }
}
