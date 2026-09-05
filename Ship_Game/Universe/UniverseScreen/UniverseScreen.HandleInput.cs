using Ship_Game.AI;
using Ship_Game.Debug;
using Ship_Game.Ships;
using System;
using System.Collections.Generic;
using System.Linq;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Fleets;
using Ship_Game.GameScreens;
using Ship_Game.GameScreens.FleetDesign;
using Ship_Game.Spatial;
using Keys = SDGraphics.Input.Keys;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public partial class UniverseScreen
    {

        public ClickableSpaceBuildGoal[] ClickableBuildGoals = Empty<ClickableSpaceBuildGoal>.Array;
        // Ludoal fork (wishlist): drag-move of an in-progress deep space build - the
        // held marker and the ghost position the draw follows
        public Commands.Goals.DeepSpaceBuildGoal DraggingBuildGoal;
        public Vector2 DraggingBuildGoalWorldPos;
        readonly Array<ClickableFleet> ClickableFleetsList = new();

        RectF SelectionBox = new(-1, -1, 0, 0);

        // Ludoal fork (spec: interactive band): the minimap's navigation, extracted so the
        // visible-band entry and the map-side path share the one arithmetic.
        public bool HandleMinimapNavigation(InputState input)
        {
            if (!MinimapDisplayRect.HitTest(input.CursorPosition) || SelectingWithBox)
                return false;
            HandleCameraZoomScrolling(input);
            if (input.LeftMouseDown)
            {
                // ⚠ the SAME projection the MiniMap draws with, asked of it - two separate
                // formulas for one mapping put a click off target.
                Vector2 c = Minimap.MapCentre;
                float scale = Minimap.MapScale;
                CamDestination.X = (input.CursorPosition.X - c.X) / scale;
                CamDestination.Y = (input.CursorPosition.Y - c.Y) / scale;
                snappingToShip = false;
                ViewingShip = false;
            }
            return true;
        }

        // Ludoal fork (spec: interactive band): the map answers a curated set of gestures
        // while a PAGE is open, LIMITED to the visible band outside the page's frame.
        // Reached through the live top bar's shared path - never via the main HandleInput,
        // whose hosted-seat bookkeeping assumes no input arrives under stacked pages.
        // v1 gestures: the minimap (overlay buttons and navigation) and the wheel zoom.
        // Ludoal fork: a page draws controls OUTSIDE the frame it declares - the bottom-right
        // buttons that sit over the minimap's corner. The map answers only where it is really
        // visible, so a pixel the player sees a control on belongs to the page, frame or not.
        // Ludoal fork: everything the page owns at this pixel - its declared frame, and the
        // controls it draws outside that frame (the bottom-right buttons over the minimap).
        static bool PageOwnsPixel(GameScreen caller, Vector2 pos)
            => caller.PageFrame.HitTest(pos) || CallerDrawsHere(caller, pos)
            || caller.AboveHitTest(pos); // an open list stands over this pixel: the page owns it

        static bool CallerDrawsHere(GameScreen caller, Vector2 pos)
        {
            var elements = caller.GetElements();
            for (int i = 0; i < elements.Count; ++i)
            {
                UIElementV2 e = elements[i];
                if (e.Visible && e.HitTest(pos))
                    return true;
            }
            return false;
        }

        public bool HandleVisibleBandInput(InputState input, GameScreen caller)
        {
            // (maintainer feedback) the cartouches and their order buttons answer BEFORE the
            // page-frame gate. They draw ON TOP of the open page and reach into the reserved
            // corner (a ship cartouche's SECOND order row climbs above the first, into the
            // page's own rect) - gated by the frame, that top row's clicks and tooltips would
            // be eaten by the table. HandleGUIClicks also arms the tooltips, so it has to run
            // regardless of where the cursor sits over the cartouche stack.
            if (HandleGUIClicks(input, caller))
                return true;
            if (PageOwnsPixel(caller, input.CursorPosition))
                return false; // the page owns this pixel: frame, or a control it drew
            if (Minimap != null && Minimap.HandleInput(input))
                return true;
            if (HandleMinimapNavigation(input))
                return true;
            // (maintainer feedback, bench 442) the two minimap-seated windows stay LIVE
            // beside an open page, like the minimap they dock to. Their handlers run first
            // (buttons, tooltips); an open window's rect then owns the cursor either way,
            // so a click on the window body cannot fall through to box-select or the map
            // click resolver below.
            if (ExoticBonusesWindow.AcceptsBandInput &&
                (ExoticBonusesWindow.HandleInput(input) || ExoticBonusesWindow.HitTest(input.CursorPosition)))
                return true;
            if (FreighterUtilizationWindow.AcceptsBandInput &&
                (FreighterUtilizationWindow.HandleInput(input) || FreighterUtilizationWindow.HitTest(input.CursorPosition)))
                return true;
            // (maintainer feedback) the band carries the map's OWN input suite, in the
            // main flow's order - the exploded system view, then box-select, then the click
            // resolver. The double-click colony-open is allowed: the map door works here too.
            if (HandleSelectionBox(input))
                return true;
            if (input.LeftMouseClick && LeftClickOnClickableItem(input))
                return true;
            HandleMiddleMousePan(input);
            HandleCameraZoomScrolling(input);
            return false;
        }

        // caller: the page this pass runs under, null on the map's own path. The cartouches
        // must answer BEFORE the page gate (they draw on top of it), but the minimap must
        // NOT - it lives UNDER the page, so a pixel the page owns is never its business.
        bool HandleGUIClicks(InputState input, GameScreen caller = null)
        {
            bool captured = DeepSpaceBuildWindow.HandleInput(input);
            if (caller == null || !PageOwnsPixel(caller, input.CursorPosition))
                captured |= HandleMinimapNavigation(input);

            // a control the page draws ABOVE this pixel answers before the cartouches - the
            // rule the containers already use inside a screen, one storey up. This gate gets
            // the tooltips too: HandleGUIClicks arms them, so a list covering a cartouche
            // would otherwise be clickable with the cartouche's tip still showing through.
            // Everywhere the page has nothing standing above, the cartouches keep their
            // priority - an order row climbing into the page's rect still answers.
            bool pageStandsAbove = caller != null && caller.AboveHitTest(input.CursorPosition);

            // @note Make sure HandleInputs are called here
            if (!LookingAtPlanet && !pageStandsAbove)
            {
                captured |= SelectedShip != null && ShipInfoUIElement.HandleInput(input);
                captured |= SelectedPlanet != null && pInfoUI.HandleInput(input);
                captured |= SelectedSystem != null && sInfoUI.HandleInput(input); // Ludoal fork (wishlist)
                captured |= SelectedShipList != null && shipListInfoUI.HandleInput(input);
            }

            if (SelectedSystem == null)
            {
                SystemInfoOverlay.SelectionTimer = 0.0f;
            }
            else
            {
                captured |= !LookingAtPlanet && SystemInfoOverlay.HandleInput(input);
            }

            if (NotificationManager.HandleInput(input))
                return true;

            // @todo Why are these needed??
            captured |= ShipsInCombat.Rect.HitTest(input.CursorPosition);
            captured |= PlanetsInCombat.Rect.HitTest(input.CursorPosition);

            return captured;
        }

        bool HandleInputNotLookingAtPlanet(InputState input)
        {
            if (HandleBuildGoalDrag(input))
                return true;
            if (input.DeepSpaceBuildWindow)       InputOpenDeepSpaceBuildWindow();
            if (input.FTLOverlay)                 ShowingFTLOverlay         = ToggleUIComponent("sd_ui_accept_alt3", ShowingFTLOverlay);
            if (input.RangeOverlay)               ShowingRangeOverlay       = ToggleUIComponent("sd_ui_accept_alt3", ShowingRangeOverlay);
            if (input.InfluenceOverlay)           ShowingInfluenceOverlay   = ToggleUIComponent("sd_ui_accept_alt3", ShowingInfluenceOverlay);
            if (input.GravityWellOverlay)         ShowingGravityWellOverlay = ToggleUIComponent("sd_ui_accept_alt3", ShowingGravityWellOverlay);
            if (input.VisionOverlay)              ShowingVisionOverlay      = ToggleUIComponent("sd_ui_accept_alt3", ShowingVisionOverlay);
            if (input.CodexHelp)                  HandleCodexHelp();
            if (input.BlueprintsSceen)            ScreenManager.AddScreen(new BlueprintsScreen(this, Player));
            if (input.EmpirePatrolsScreen)        ScreenManager.AddScreen(new EmpirePatrolsScreen(this, Player));
            if (input.ImportantEventsScreen)      ScreenManager.AddScreen(new ImportantEventsScreen(this)); // Ludoal fork: F7

            // Ludoal fork (bench 427): keyboard colony navigation - leaf through the
            // player's colonies (selection + pan at constant zoom), Home snaps to the capital.
            // bench 429 (maintainer feedback): the tour is SPATIAL - systems ordered by
            // distance from the homeworld's system, planets by orbit within each system,
            // so a system is finished before the tour jumps to the next one. And the tour
            // resumes from the currently selected colony, wherever the mouse left it.
            if (input.PrevColony || input.NextColony)
            {
                Planet[] tour = Player.SpatialColonyOrder(); // bench 431: ONE arithmetic with the Colonies table
                if (tour.Length > 0)
                {
                    int current = SelectedPlanet != null ? Array.IndexOf(tour, SelectedPlanet) : -1;
                    if (current >= 0)
                        ColonyCycleIndex = current;
                    ColonyCycleIndex = (ColonyCycleIndex + (input.NextColony ? 1 : -1) + tour.Length) % tour.Length;
                    Planet cycleTo = tour[ColonyCycleIndex];
                    SetSelectedPlanet(cycleTo);
                    PanToPlanetKeepZoom(cycleTo);
                    GameAudio.AcceptClick();
                }
            }
            if (input.GoToCapital && Player.Capital != null)
            {
                SetSelectedPlanet(Player.Capital);
                PanToPlanetKeepZoom(Player.Capital);
                GameAudio.AcceptClick();
            }
            // Ludoal fork (maintainer feedback): H opens the Policies tab of the Empire group.
            // The screen closes on H or right-click. Automation keeps its own binding row but
            // ships unbound - it is opened from the tab row.
            if (input.PoliciesWindow && !Debug)
                ScreenManager.AddScreen(new PoliciesScreen(this));
            if (input.AutomationWindow && !Debug)
                ScreenManager.AddScreen(new AutomationScreen(this));
            if (input.ExoticBonusesWindow) ExoticBonusesWindow.ToggleVisibility();
            if (input.FreighterUtilWindow) FreighterUtilizationWindow.ToggleVisibility();
            if (input.PlanetListScreen)  ScreenManager.AddScreen(new PlanetListScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.ExoticListScreen)  ScreenManager.AddScreen(new ExoticSystemsListScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.ShipListScreen)    ScreenManager.AddScreen(new ShipListScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.TroopListScreen)   ScreenManager.AddScreen(new TroopListScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.ColonyOverviewScreen) OpenEmpireColonyTab(); // Ludoal fork: F8, the Empire Colony tab
            if (input.FleetDesignScreen) ScreenManager.AddScreen(new FleetDesignScreen(this, EmpireUI, "sd_ui_accept_alt3"));
            if (input.ZoomToShip) InputZoomToShip();
            if (input.ZoomOut)    InputZoomOut();
            // Ludoal fork (wishlist): Escape no longer jumps the zoom between fixed
            // levels at the current camera XY — it read as a random center-zoom.
            // Deliberate zooming keeps its own keys (ZoomToShip / ZoomOut / wheel).
            // if (input.Escaped)    DefaultZoomPoints();
            if (input.Tab && !input.LeftCtrlShift) ShowShipNames = !ShowShipNames;

            HandleCameraZoomScrolling(input);
            HandleShipSelectionAndOrders();

            if (input.LeftMouseDoubleClick && HandleDoubleClickShipsAndSolarObjects(input))
                return true;

            if (!LookingAtPlanet)
            {
                if (HandleSelectionBox(input))
                    return true;

                if (HandlePieMenu(input))
                    return true;

                if (input.LeftMouseClick && LeftClickOnClickableItem(input))
                    return true;
            }

            if (Debug)
                HandleDebugEvents(input);

            return false;
        }

        void HandleCodexHelp()
        {
            string uid = ToolTip.GetActiveCodexUid();

            GameAudio.TacticalPause();
            // OpenAt before AddScreen: ScreenManager queues the screen for the
            // next tick, so we stash PendingUid and LoadContent flushes it.
            // Ludoal fork: with no codex tooltip active, F1 opens the codex at its
            // root — same as the Help (?) button — instead of doing nothing.
            var codex = new Codex.CodexScreen(this);
            if (uid != null)
                codex.OpenAt(uid);
            ScreenManager.AddScreen(codex);
        }

        void HandleDebugEvents(InputState input)
        {
            Empire player = Player;

            if (input.EmpireToggle) 
                player  = input.RemnantToggle ? UState.Remnants : UState.Corsairs;

            if (input.SpawnShip)
                Ship.CreateShipAtPoint(UState, "Bondage-Class Mk IIIa Cruiser", player, CursorWorldPosition2D);

            if (input.SpawnFleet2) HelperFunctions.DebugCreateFleetAt(UState, "Fleet 2", player, CursorWorldPosition2D);
            if (input.SpawnFleet1) HelperFunctions.DebugCreateFleetAt(UState, "Fleet 1", player, CursorWorldPosition2D);

            if (SelectedShip != null)
            {
                if (input.DebugKillShip) // 'X' or 'Delete'
                {
                    // Apply damage as a percent of module health to all modules.
                    var damage = input.IsShiftKeyDown ? 0.9f : 1f;
                    SelectedShip.DebugDamage(damage);
                }

                if (input.BlowExplodingModule) // "N" key
                {
                    if (input.IsShiftKeyDown)
                        SelectedShip.DebugBlowSmallestExplodingModule();
                    else
                        SelectedShip.DebugBlowBiggestExplodingModule();
                }
            }
            else if (SelectedPlanet != null && input.DebugKillShip)
            {
                foreach (string troopType in ResourceManager.TroopTypes)
                    if (ResourceManager.TryCreateTroop(troopType, UState.Remnants, out Troop t))
                        t.TryLandTroop(SelectedPlanet);
            }

            if (input.SpawnRemnant)
                UState.Remnants.Remnants.DebugSpawnRemnant(input, CursorWorldPosition2D);

            if (input.ToggleSpatialManagerType)
                UState.Spatial.ToggleSpatialType();

            if (input.IsShiftKeyDown && input.KeyPressed(Keys.B))
                StressTestShipLoading();
        }

        void HandleInputLookingAtPlanet(InputState input)
        {
            if (input.Tab)
                ShowShipNames = !ShowShipNames;

            var colonyScreen = workersPanel as ColonyScreen;
            bool dismiss = (input.Escaped || input.RightMouseClick) && colonyScreen?.ClickedTroop == false;
            if (dismiss || !workersPanel.IsActive)
            {
                // Ludoal fork (maintainer feedback): opened from a list screen, closing goes back
                // to that list. Taken before it is called: reopening the screen runs its own
                // setup, and a hook still standing would send the NEXT close there too.
                Action back = ReturnToListScreen;
                ReturnToListScreen = null;
                ReturnToListGroup  = GameScreens.ScreenGroups.Group.None;

                // Ludoal fork (spec: colony-as-tab): closing the hosted panel closes its TAB -
                // back to the panel it was opened from, or to the map when it came from there
                // (origin -1). The seat clears first: the reopened screen must build the stock
                // row, without the tab that just died.
                if (back == null && HostedTabTitle != null)
                {
                    var hostedGroup = HostedTabGroup;
                    int origin = HostedTabOrigin;
                    ClearHostedTab();
                    if (origin >= 0)
                        back = () => ScreenManager.AddScreen(
                            GameScreens.ScreenGroups.TabOf(hostedGroup, origin, this));
                }

                AdjustCamTimer = 1f;
                if (returnToShip)
                {
                    ViewingShip = true;
                    returnToShip = false;
                    snappingToShip = true;
                    CamDestination.Z = transitionStartPosition.Z;
                }
                else
                {
                    CamDestination = transitionStartPosition;
                    SetSelectedPlanet(workersPanel.P);
                }
                transitionElapsedTime = 0f;
                LookingAtPlanet = false;
                // Ludoal fork: the colony may HOLD the pause it inherited from the list that
                // opened it, and this close path never calls ExitScreen - give the simulation
                // back here. When going back to a list, the reopened screen takes the pause
                // again with its own toPause in this same stack, so no tick runs in between.
                workersPanel.ReleaseUniversePause();
                back?.Invoke();
            }
        }

        void OnFleetButtonClicked(FleetButton b)
        {
            Fleet f = Player?.GetFleetOrNull(b.FleetKey);
            if (f != null)
            {
                SetSelectedFleet(f);

                if (Input.LeftMouseDoubleClick)
                {
                    SnapViewFleet(f);
                }
            }
        }
        
        void OnFleetHotKeyPressed(FleetButton b)
        {
            // this can be null if a new fleet is being created
            Fleet selectedFleet = Player.GetFleetOrNull(b.FleetKey);

            if (Input.ReplaceFleet)
            {
                CreateNewFleet(selectedFleet, b.FleetKey);
            }
            else if (Input.AddToFleet)
            {
                AddShipsToExistingFleet(selectedFleet, b.FleetKey);
            }
            else
            {
                ShowSelectedFleetInfo(selectedFleet, b.FleetKey);
            }
        }

        public void ToggleDebugWindow() // toggle Debug Window overlay
        {
            if (DebugWin == null)
                DebugWin = Add(new DebugInfoScreen(this));
            else
                HideDebugWindow();
        }

        public void HideDebugWindow()
        {
            DebugWin?.RemoveFromParent();
            DebugWin = null;
        }

        public override bool HandleInput(InputState input)
        {
            Input = input;

            // Ludoal fork (spec: colony-as-tab): the hosted tab dies WITH its group. The
            // universe only receives input once nothing is stacked above it - so a seat
            // still armed here, with no colony panel up, means its group was closed to the
            // map and the tab goes with it. (Every in-group path opens the next screen in
            // the same frame, before input ever returns to the universe.)
            if (HostedTabTitle != null && !LookingAtPlanet)
                ClearHostedTab();

            if (input.PauseGame && !GlobalStats.TakingInput)
                EmpireUI.TogglePause(); // one owner for the pause gesture

            if (input.DebugMode)
            {
                UState.SetDebugMode(!UState.Debug);

                foreach (SolarSystem solarSystem in UState.Systems)
                {
                    solarSystem.SetExploredBy(Player);
                    foreach (Planet planet in solarSystem.PlanetList)
                        planet.SetExploredBy(Player);

                    solarSystem.UpdateFullyExploredBy(Player);
                }

                if (!UState.P.UseLegacyEspionage)
                {
                    if (UState.Debug)
                    {
                        foreach (Empire empire in UState.ActiveEmpires)
                            empire.SetCanBeScannedByPlayer(true);
                    }
                }
            }

            if (Debug)
            {
                if (input.ShowDebugWindow)
                {
                    ToggleDebugWindow();
                }

                if (input.GetMemory)
                {
                    GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                }
            }

            // ensure universe has the correct light rig
            ResetLighting(forceReset: false);

            HandleEdgeDetection(input);
            UpdateVisibleShields();

            if (HandleDragAORect(input))
                return true;

            if (HandleTradeRoutesDefinition(input))
                return true;

            // Handle new UIElementV2 items
            if (base.HandleInput(input))
                return true;

            UpdateSelectedShips();
            if (HandlePrevSelectedShipChange(input))
                return true;

            // fbedard: Set camera chase on ship (Ctrl+middle; plain middle stays
            // the map-drag gesture — field report 45.40, plain-click chase fought it)
            if (input.MiddleMouseClick)
            {
                if (ViewingShip)
                    ToggleViewingShip(); // exit easily
                else if (input.IsCtrlKeyDown) // only enter if ctrl key is down
                    ToggleViewingShip();
            }

            if (input.CinematicMode)
                ToggleCinematicMode();

            ShowTacticalCloseup = input.TacticalIcons;

            if (input.QuickSave && !IsSaving)
            {
                SaveDuringNextUpdate($"Quicksave, {Player.data.Traits.Name}, {UState.StarDate.String()}");
            }

            if (input.UseRealLights)
            {
                UseRealLights = !UseRealLights; // toggle real lights
                ResetLighting(forceReset: true);
            }
            if (input.ShowExceptionTracker)
            {
                UState.Paused = true;
                Log.OpenURL(GlobalStats.VanillaDefaults.URL);
            }

            HandleGameSpeedChange(input);

            if (!LookingAtPlanet)
            {
                if (HandleGUIClicks(input))
                    return true;
            }
            else
            {
                ClearSelectedItems();
                // Ludoal fork: the minimap navigates and the wheel zooms beside the colony too.
                // Its overlay buttons already ride the base.HandleInput pass above - only the
                // gestures the colony path lacks.
                if (HandleMinimapNavigation(input))
                    return true;
                if (!GameScreens.ScreenGroups.GroupFrame(ScreenWidth, ScreenHeight).HitTest(input.CursorPosition))
                    HandleCameraZoomScrolling(input);
            }

            if (input.ScrapShip && (SelectedItem != null && SelectedItem.AssociatedGoal.Owner == Player))
                OnScrapSelectedItem();

            // Ludoal fork: the Cancel button on the build cartouche
            if (SelectedItem != null && SelectedItem.AssociatedGoal.Owner == Player
                && DsbCancelRect.HitTest(input.CursorPosition))
            {
                ToolTip.CreateTooltip(GameText.UhCancelConstructionTooltip, "Delete", null); // hotkey rendered in the game's standard style
                if (input.LeftMouseClick)
                {
                    GameAudio.AffirmativeClick();
                    OnScrapSelectedItem();
                    return true;
                }
            }

            // (the combat counters' visibility is the Draw side's business - nothing about the
            // map changes when a panel opens)

            if (LookingAtPlanet && workersPanel.HandleInput(input))
                return true;

            if (IsActive && EmpireUI.HandleInput(input))
                return true;

            if (!LookingAtPlanet)
            {
                if (HandleInputNotLookingAtPlanet(input))
                    return true;
            }
            else
            {
                HandleInputLookingAtPlanet(input);
            }

            return false;
        }

        protected override GameCursor GetCurrentCursor()
        {
            if (IsCinematicModeEnabled)
                return GameCursors.Cinematic;

            // bench 451: a grabbable build marker (or one in hand) wears the hand cursor
            if (DraggingBuildGoal != null
                || (DeepSpaceBuildWindow.Visible && GetSpaceBuildGoalUnderCursor() != null))
                return GameCursors.Hand;

            if (SelectedFleet != null || SelectedShip != null || SelectedShipList.NotEmpty)
            {
                MoveOrder mo = ShipCommands.GetMoveOrderType();
                if (mo.IsSet(MoveOrder.AddWayPoint))
                {
                    if (mo.IsSet(MoveOrder.Aggressive)) return GameCursors.AggressiveNav;
                    if (mo.IsSet(MoveOrder.StandGround)) return GameCursors.StandGroundNav;
                    return GameCursors.RegularNav;
                }
                else
                {
                    if (mo.IsSet(MoveOrder.Aggressive)) return GameCursors.Aggressive;
                    if (mo.IsSet(MoveOrder.StandGround)) return GameCursors.StandGround;
                    return GameCursors.Regular;
                }
            }
            return GameCursors.Regular;
        }

        void CreateNewFleet(Fleet selectedFleet, int index)
        {
            // clear the fleet if no ships selected and pressing Ctrl + NumKey[1-9]
            if (SelectedShipList.IsEmpty)
            {
                selectedFleet?.Reset(fleeIfInCombat: false);
                return;
            }

            // else: we have selected some ships, delete old fleet
            selectedFleet?.Reset(fleeIfInCombat: false, clearOrders: false);

            // create new fleet
            Fleet fleet = CreateNewFleet(index, SelectedShipList);
            if (fleet != null) 
                SetSelectedFleet(fleet);
        }

        void AddShipsToExistingFleet(Fleet selectedFleet, int index)
        {
            if (SelectedShipList.IsEmpty)
            {
                GameAudio.NegativeClick();
                return;
            }

            Fleet fleet;
            if (selectedFleet?.Ships.Count > 0)
            {
                // create a list of ships that are not part of the target fleet.
                var newShips = SelectedShipList.Filter(s => s.Fleet != selectedFleet && s.CanBeAddedToFleets());
                if (newShips.Length == 0) // nothing to add
                {
                    GameAudio.NegativeClick();
                    return;
                }

                fleet = AddShipsToFleet(selectedFleet, newShips);
            }
            else
            {
                fleet = CreateNewFleet(index, SelectedShipList);
            }

            SetSelectedFleet(fleet);

            if (fleet.Name.IsEmpty() || fleet.Name.Contains("Fleet"))
                fleet.Name = Fleet.GetDefaultFleetName(index);

            fleet.Update(FixedSimTime.Zero /*paused during init*/);
        }

        // Fleet hotkey double-tap detection: a single press of the fleet key just
        // selects (no camera jump); a second press of the SAME key within the
        // window snaps the camera to the fleet.
        int LastFleetKeyPressed = -1;
        int LastFleetKeyPressTickMs;
        const int FleetKeyDoubleTapWindowMs = 500;

        void ShowSelectedFleetInfo(Fleet selectedFleet, int fleetKey)
        {
            // nothing selected
            if (selectedFleet == null || selectedFleet.Ships.IsEmpty)
                return;

            int nowMs = Environment.TickCount;
            bool isDoubleTap = LastFleetKeyPressed == fleetKey
                            && (nowMs - LastFleetKeyPressTickMs) < FleetKeyDoubleTapWindowMs
                            && SelectedFleet == selectedFleet;

            SetSelectedFleet(selectedFleet);
            GameAudio.FleetClicked();

            if (isDoubleTap && SelectedFleet != null)
            {
                SnapViewFleet(SelectedFleet);
                LastFleetKeyPressed = -1; // require a fresh pair for the next zoom
            }
            else
            {
                LastFleetKeyPressed = fleetKey;
                LastFleetKeyPressTickMs = nowMs;
            }
        }

        void UpdateVisibleShields()
        {
            Array<Shield> shields = new();

            Ship[] ships = UState.Objects.VisibleShips;
            for (int i = 0; i < ships.Length; i++)
            {
                Ship ship = ships[i];
                if (ship.Active && ship.ShieldMax > 0f && ship.IsVisibleToPlayerInMap && !ship.IsLaunching)
                {
                    shields.AddRange(ship.GetActiveShields().Select(s => s.Shield));
                }
            }

            // TODO: this needs to be rewritten
            Shields.SetVisibleShields(shields.ToArr());

            if (viewState <= UnivScreenState.SectorView)
            {
                Array<Shield> visibleShields = new();

                Planet[] planets = UState.GetVisiblePlanets();
                foreach (Planet planet in planets)
                {
                    if (planet.Shield != null && planet.IsExploredBy(Player))
                        visibleShields.Add(planet.Shield);
                }

                Shields.SetVisiblePlanetShields(visibleShields.ToArr());
            }
        }

        bool CanClickOnShip(SpatialObjectBase go)
        {
            return go is Ship { InPlayerSensorRange: true } ship
                // feature: if we're zoomed OUT a lot, ignore subspace projector clicks
                && (!ship.IsSubspaceProjector || CamPos.Z <= 1_200_000.0);
        }

        public Ship[] GetVisibleShipsInScreenRect(in RectF screenRect, int maxResults = 1024)
        {
            AABoundingBox2D worldRect = UnprojectToWorldRect(new(screenRect));
            SearchOptions opt = new(worldRect, GameObjectType.Ship)
            {
                MaxResults = maxResults,
                SortByDistance = true, // only care about closest results
                FilterFunction = CanClickOnShip
            };
            return UState.Spatial.FindNearby(ref opt).FastCast<SpatialObjectBase, Ship>();
        }

        public Ship[] GetVisibleEnemyShipsInScreen()
        {
            Ship[] Ships = GetVisibleShipsInScreenRect(new RectF(0,0, new Vector2(ScreenArea.X, ScreenArea.Y)));
            return Ships.Filter(s => !s.IsInWarp && Player.IsEmpireAttackable(s.Loyalty, s));
        }

        Ship FindClickedShip(InputState input)
        {
            // Workaround for #254 (Matrix.Invert precision loss at CamPos.Z in the millions).
            // The old spatial-search approach built the world click rect by unprojecting the
            // cursor pixel ± a screen-pixel radius. On epic maps Unproject diverges from Project
            // by ~17 pixels / ~500 world units (float matrix invert amplifies error after
            // perspective division), so the rect was offset from the ship's actual position and
            // the spatial search returned 0 even when the cursor was visually on the icon.
            // We instead project each candidate ship forward to screen and compare in pixel
            // space — Project alone is fine, only the invert step loses precision.
            //
            // Per-ship click radius: take whichever is larger — the ship's actual on-screen
            // radius (close zoom: many pixels) or the icon-mode floor (far zoom: ~12 px).
            // Without this the click box is fixed at 12 px and you can only select close-up
            // ships by clicking near their geometric center.
            //
            // No prefilter: iterating 5000 ships with a Vector2 distance check is sub-
            // millisecond and avoids the same Unproject-precision trap on cursor→world.
            // Also no InFrustum gate: a Project per candidate already validates on-screen
            // position, and InFrustum can be momentarily stale in the camera-move window
            // before UpdateVisibleObjects re-runs.
            const float MarginPx = 4f;

            Vector2 cursor = input.CursorPosition;
            Ship best = null;
            float bestDistPx = float.MaxValue;

            Ship[] ships = UState.Ships;
            for (int i = 0; i < ships.Length; i++)
            {
                Ship ship = ships[i];
                if (!ship.Active || !ship.InPlayerSensorRange)
                    continue;
                if (ship.IsSubspaceProjector && CamPos.Z > 1_200_000.0)
                    continue;

                ProjectToScreenCoords(ship.Position, ship.Radius,
                                      out Vector2d shipScreen, out double shipScreenRadius);
                // per-candidate: ships and stations have separate icon-size settings,
                // and the click radius must match what is actually drawn
                float iconClickRadiusPx = (16f + Ship.IconSizeSetting(ship)) * 0.5f + MarginPx;
                float threshold = Math.Max(iconClickRadiusPx, (float)shipScreenRadius + MarginPx);
                float distPx = cursor.Distance(shipScreen.ToVec2f());
                if (distPx <= threshold && distPx < bestDistPx)
                {
                    best = ship;
                    bestDistPx = distPx;
                }
            }
            return best;
        }

        Planet FindPlanetUnderCursor()
        {
            // Mitigates #254 on the planet click path: a wider screen-pixel radius gives more
            // forgiveness against the same Unproject precision drift that affected ships.
            // Was: UnprojectToWorldSize(16) - 500, which subtracted a magic number that did
            // nothing useful at large world coords (where the value is huge) and over-trimmed
            // at small ones (forcing the LowerBound floor). Removed the subtraction and
            // widened the source pixel size for click forgiveness on large maps.
            float searchRadius = UnprojectToWorldSize(sizeOnScreen: 24).LowerBound(100);

            Vector3d worldPos = UnprojectToWorldPosition3D(Input.CursorPosition, ZPlane: 2500);
            Planet p = UState.FindPlanetAt(worldPos.ToVec2f(), searchRadius: searchRadius);
            return p != null && p.System.IsExploredBy(Player) ? p : null;
        }

        // should be called for >= SectorView
        SolarSystem FindSolarSystemUnderCursor()
        {
            // convert cam Z pos at high zoom to a relative [0.0, 1.0] linear range
            float minZ = 1_500_000f;
            float maxZ = 2_500_000f;
            float relZ = MathExt.LerpInverse((float)CamPos.Z, minZ, maxZ).Clamped(0, 1);

            // convert the relative Z to a solar hit radius
            float hitRadius = 5_000f.LerpTo(50_000f, relZ);

            // Mitigates #254 on the sun click path. Large maps allow MaxCamHeight up to
            // CAM_MAX (15M), well past maxZ. Without continued scaling the sun icon visually
            // grows but the hit radius stops at 50_000, so clicks on the icon's edge miss.
            // Scale linearly with CamPos.Z above maxZ to keep the hit zone in step with the
            // icon size and absorb the Unproject precision drift at extreme zoom.
            if (CamPos.Z > maxZ)
                hitRadius *= (float)(CamPos.Z / maxZ);

            return UState.FindSolarSystemAt(CursorWorldPosition2D, hitRadius: hitRadius);
        }

        Fleet CheckFleetClicked()
        {
            foreach(ClickableFleet clickableFleet in ClickableFleetsList)
                if (Input.CursorPosition.InRadius(clickableFleet.ScreenPos, clickableFleet.ClickRadius))
                    return clickableFleet.fleet;
            return null;
        }

        // Ludoal fork (wishlist): hit-test the suns at close zoom
        SolarSystem FindSunUnderCursorClose(Vector2 cursor)
        {
            var systems = UState.Systems;
            for (int i = 0; i < systems.Count; i++)
            {
                SolarSystem s = systems[i];
                ProjectToScreenCoords(s.Position, 30000f, out Vector2d pos, out double radius);
                float r = (float)Math.Max(radius, 24.0);
                if (cursor.InRadius(pos.ToVec2f(), r))
                    return s;
            }
            return null;
        }

        // Ludoal fork (wishlist): grab a build marker with a held left button, slide it,
        // release to commit - Escape cancels. The gesture lives with the markers: only
        // while the DSB window shows them.
        bool HandleBuildGoalDrag(InputState input)
        {
            if (DraggingBuildGoal == null)
            {
                if (!DeepSpaceBuildWindow.Visible || !input.LeftMouseHeldDown || !input.LeftMouseHeld(0.15f))
                    return false;
                ClickableSpaceBuildGoal[] goals = ClickableBuildGoals;
                for (int i = 0; i < goals.Length; ++i)
                {
                    ClickableSpaceBuildGoal c = goals[i];
                    if (c.HitTest(input.StartLeftHold)
                        && c.AssociatedGoal is Commands.Goals.DeepSpaceBuildGoal bg
                        && bg.Owner == Player)
                    {
                        DraggingBuildGoal = bg;
                        GameAudio.BuildItemClicked();
                        break;
                    }
                }
                if (DraggingBuildGoal == null)
                    return false;
            }
            if (input.Escaped) // cancel: the site stays where it was
            {
                DraggingBuildGoal = null;
                return true;
            }
            DraggingBuildGoalWorldPos = UnprojectToWorldPosition(input.CursorPosition);
            if (input.LeftMouseUp)
            {
                DraggingBuildGoal.MoveBuildPos(DraggingBuildGoalWorldPos);
                UpdateClickableItems(); // the marker follows without waiting a tick
                GameAudio.AffirmativeClick();
                DraggingBuildGoal = null;
            }
            return true; // while dragging, the gesture owns the cursor
        }

        ClickableSpaceBuildGoal GetSpaceBuildGoalUnderCursor()
        {
            var goals = ClickableBuildGoals;
            for (int i = 0; i < goals.Length; ++i)
            {
                ClickableSpaceBuildGoal goal = goals[i];
                if (Input.CursorPosition.InRadius(goal.ScreenPos, goal.Radius))
                    return goal;
            }
            return null;
        }

        bool HandlePieMenu(InputState input)
        {
            if (input.ShipPieMenu)
            {
                if (!pieMenu.Visible)
                {
                    if (SelectedPlanet != null)
                        LoadPieMenuNodesForPlanet(SelectedPlanet);
                    else if (SelectedShip is { IsHangarShip: false, IsConstructor: false } s)
                        LoadPieMenuShipNodes(s);
                }
                else
                {
                    pieMenu.Hide();
                }
                return true;
            }

            if (pieMenu.Visible)
            {
                pieMenu.HandleInput(input);
                return true; // always capture input from pie menu
            }
            return false;
        }

        bool UnselectableShip(Ship ship = null)
        {
            ship ??= SelectedShip;
            if (!ship.IsConstructor && !ship.IsSupplyShuttle)
                return false;

            GameAudio.NegativeClick();
            return true;
        }

        bool SelectShipClicks(InputState input)
        {
            Ship ship = FindClickedShip(input);
            if (ship != null)
            {
                if (SelectedShipList.Count > 0 && input.IsShiftKeyDown)
                {
                    // remove existing ship?
                    if (SelectedShipList.RemoveRef(ship))
                    {
                        UpdateSelectedShips();
                        return true;
                    }

                    // ok, no, add a new ship instead?
                    bool added = SelectedShipList.AddUniqueRef(ship);
                    UpdateSelectedShips();
                    return added;
                }

                SetSelectedShip(ship);
                return true;
            }
            return false;
        }

        bool SelectPlanetClicks(InputState input)
        {
            Planet planet = FindPlanetUnderCursor();
            if (planet != null)
            {
                if (input.LeftMouseDoubleClick)
                {
                    // (maintainer feedback) a MOLE planet opens its colony (mole vision)
                    // like an owned one - combatView must be false so it doesn't detour to
                    // the combat menu before the snap's own mole branch can answer
                    bool mole = false;
                    if (planet.Owner != Player)
                        foreach (Mole m in Player.data.MoleList)
                            if (m.PlanetId == planet.Id) { mole = true; break; }
                    SnapViewColony(planet, planet.Owner != Player && !mole && !Debug);
                    SelectionBox = new();
                }
                else
                {
                    SetSelectedPlanet(planet);
                    GameAudio.PlanetClicked();
                }
                return true;
            }
            return false;
        }

        bool LeftClickOnClickableItem(InputState input)
        {
            Project.Started = false;

            if (viewState >= UnivScreenState.SectorView)
            {
                SolarSystem system = FindSolarSystemUnderCursor();
                if (system != null)
                {
                    SetSelectedSystem(system);
                    GameAudio.MouseOver();
                    return true;
                }

                // in SectorView, always prefer selecting planets
                if (SelectPlanetClicks(input))
                    return true;
            }

            Fleet fleet = CheckFleetClicked();
            if (fleet != null)
            {
                SetSelectedFleet(fleet);
                GameAudio.FleetClicked();
                return true;
            }

            if (SelectShipClicks(input))
            {
                GameAudio.ShipClicked();
                return true;
            }

            // in SystemView, prefer ship clicks over planet clicks
            if (viewState < UnivScreenState.SectorView)
            {
                if (SelectPlanetClicks(input))
                    return true;
            }

            // Ludoal fork: the sun itself is clickable up close — star cartouche.
            // Extended to SectorView (bench 420): the far path's world-space hit radius
            // bottoms out at close zoom, and an unexplored system has no planets to
            // rescue the click - explored ones only felt clickable through theirs. The
            // screen-space sun test has no such dead zone.
            if (viewState <= UnivScreenState.SectorView)
            {
                SolarSystem sun = FindSunUnderCursorClose(input.CursorPosition);
                if (sun != null)
                {
                    SetSelectedSystem(sun);
                    GameAudio.MouseOver();
                    return true;
                }
            }

            ClickableSpaceBuildGoal goal = GetSpaceBuildGoalUnderCursor();
            if (goal != null)
            {
                SetSelectedItem(goal);
                GameAudio.BuildItemClicked();
                return true;
            }

            if (!input.IsShiftKeyDown && !input.IsAltKeyDown && !input.IsCtrlKeyDown)
            {
                ClearSelectedItems(clearFlags: false);
                // Ludoal fork: deselecting on empty space also decouples the chase camera
                ViewingShip = false;
            }
            return false;
        }

        bool HandleSelectionBox(InputState input)
        {
            if (input.LeftMouseHeld(0.1f)) // we started dragging selection box
            {
                SelectionBox = input.LeftHold.GetSelectionBox();
                SelectingWithBox = true;
                return true;
            }

            if (!SelectingWithBox) // mouse released, but we weren't selecting
                return false;

            if (SelectingWithBox) // trigger! mouse released after selecting
                SelectingWithBox = false;

            var ships = GetAllShipsInArea(SelectionBox, input, out Fleet fleet);
            if (ships.NotEmpty)
            {
                SetSelectedFleet(fleet, ships);
            }

            SelectionBox = new(0, 0, -1, -1);
            return true;
        }

        static bool IsCombatShip(Ship ship)
        {
            return NonCombatShip(ship) == false;
        }

        static bool NonCombatShip(Ship ship)
        {
            return ship != null
                && (ship.ShipData.Role <= RoleName.freighter 
                    || ship.ShipData.ShipCategory == ShipCategory.Civilian 
                    || ship.DesignRole == RoleName.troop
                    || ship.Weapons.Count == 0 && !ship.Carrier.HasFighterBays
                    || ship.AI.State == AIState.Colonize);
        }

        Array<Ship> GetAllShipsInArea(in RectF screenArea, InputState input, out Fleet fleet)
        {
            fleet = null;
            Ship[] potentialShips = GetVisibleShipsInScreenRect(screenArea);
            if (potentialShips.Length == 0)
                return new();

            bool hasCombatShips = potentialShips.Any(IsCombatShip);

            // TODO: These are not documented to the players
            bool addToSelection = input.IsShiftKeyDown;
            bool ctrlSelect     = input.IsCtrlKeyDown;
            bool selectAll      = ctrlSelect || !hasCombatShips;
            bool nonPlayer      = input.IsAltKeyDown || !potentialShips.Any(s => s.Loyalty.isPlayer);
            bool onlyPlayer     = !nonPlayer && potentialShips.Any(s => s.Loyalty.isPlayer);

            var ships = new Array<Ship>();
            if (addToSelection)
                ships.AddRange(SelectedShipList);

            foreach (Ship ship in potentialShips)
            {
                if       (onlyPlayer && ship.Loyalty.isPlayer) ships.AddUnique(ship);
                else if  (nonPlayer && !ship.Loyalty.isPlayer) ships.AddUnique(ship);
            }

            if (onlyPlayer && !selectAll && fleet == null) // Need to remove non combat ship.
            {
                ships.RemoveAll(NonCombatShip);
            }

            if (onlyPlayer && !ctrlSelect && !hasCombatShips)
            {
                // if we selected a bunch of civilian ships, but some of them are troop transports
                // then discard all ships that aren't troop transports.
                // upstream issue 298: count only the player's own selected ships — an ENEMY
                // transport in the box used to poison this and strip the whole selection.
                // And Ctrl means 'everything of mine': the preference filter yields to it.
                bool hasTroopTransports = ships.Any(s => s.IsSingleTroopShip);
                if (hasTroopTransports)
                    ships.RemoveAll(s => !s.IsSingleTroopShip);
            }

            if (onlyPlayer && ships.Count > 0 && ships.First.Fleet != null)
            {
                Fleet groupFleet = ships.Any(s => s.Fleet != ships.First.Fleet) ? null : ships.First.Fleet;
                if (groupFleet != null && groupFleet.Ships.Count == ships.Count)
                    fleet = groupFleet; // All the fleet was selected
            }

            return ships;
        }

        public void UpdateClickableItems()
        {
            var buildGoals = new Array<ClickableSpaceBuildGoal>();
            EmpireAI playerAI = Player.AI;

            // ToArray() used for thread safety
            foreach (Goal goal in playerAI.Goals.ToArr())
            {
                if (goal.IsDeploymentGoal)
                {
                    ProjectToScreenCoords(goal.BuildPosition, 100f, out Vector2d buildPos, out double clickableRadius);
                    buildGoals.Add(new ClickableSpaceBuildGoal
                    {
                        ScreenPos = buildPos.ToVec2f(),
                        BuildPos = goal.BuildPosition,
                        Radius = (float)(clickableRadius + 10),
                        UID = goal.ToBuild.Name,
                        AssociatedGoal = goal
                    });
                }
            }
            ClickableBuildGoals = buildGoals.ToArray();
        }

        bool HandleTradeRoutesDefinition(InputState input)
        {
            if (!DefiningTradeRoutes)
                return false;

            DefiningTradeRoutes = !DefiningAO;
            HandleCameraZoomScrolling(input); // allow exclusive scrolling during Trade Route define
            if (!LookingAtPlanet && HandleGUIClicks(input))
                return true;

            if (input.LeftMouseClick || input.RightMouseClick)
                InputPlanetsForTradeRoutes(input); // add or remove a planet from the list

            if (SelectedShip == null || input.Escaped) // exit the trade routes mode
            {
                DefiningTradeRoutes = false;
                return true;
            }
            return true;
        }

        void InputPlanetsForTradeRoutes(InputState input)
        {
            if (viewState > UnivScreenState.SystemView)
                return;

            Planet planet = FindPlanetUnderCursor();
            if (planet != null)
            {
                if (input.LeftMouseClick)
                {
                    if (SelectedShip.AddTradeRoute(planet))
                        GameAudio.AcceptClick();
                    else
                        GameAudio.NegativeClick();
                }
                else
                {
                    SelectedShip.RemoveTradeRoute(planet);
                    GameAudio.AffirmativeClick();
                }
            }
        }

        bool HandleDragAORect(InputState input)
        {
            if (!DefiningAO)
                return false;

            DefiningAO = !DefiningTradeRoutes;
            HandleCameraZoomScrolling(input); // allow exclusive scrolling during AO define
            if (!LookingAtPlanet && HandleGUIClicks(input))
                return true;

            if (input.RightMouseClick) // erase existing AOs
            {
                Vector2 cursorWorld = UnprojectToWorldPosition(input.CursorPosition);
                SelectedShip.AreaOfOperation.RemoveFirst(ao => ao.HitTest(cursorWorld));
                return true;
            }

            // no ship selection? abort
            // Easier out from defining an AO. Used to have to left and Right click at the same time.    -Gretman
            if (SelectedShip == null || input.Escaped)
            {
                DefiningAO = false;
                return true;
            }

            if (input.LeftMouseHeld(0.1f))
            {
                Vector2 start = UnprojectToWorldPosition(input.StartLeftHold);
                Vector2 end   = UnprojectToWorldPosition(input.EndLeftHold);
                AORect = new Rectangle((int)Math.Min(start.X, end.X),  (int)Math.Min(start.Y, end.Y), 
                                       (int)Math.Abs(end.X - start.X), (int)Math.Abs(end.Y - start.Y));
            }
            else if ((AORect.Width+AORect.Height) > 1000 && input.LeftMouseReleased)
            {
                if (AORect.Width >= 5000 && AORect.Height >= 5000)
                {
                    GameAudio.EchoAffirmative();
                    SelectedShip.AreaOfOperation.Add(AORect);
                }
                else
                {
                    GameAudio.NegativeClick(); // eek-eek! AO not big enough!
                }
                AORect = Rectangle.Empty;
            }
            return true;
        }

        bool HandleDoubleClickShipsAndSolarObjects(InputState input)
        {
            if (viewState <= UnivScreenState.SystemView)
            {
                Planet planet = FindPlanetUnderCursor();
                if (planet != null)
                {
                    GameAudio.SubBassWhoosh();
                    SnapViewColony(planet, planet.Owner != Player && !Debug);
                    return true;
                }
            }

            if (SelectMultipleShipsByClickingOnShip(input))
                return true;

            if (viewState >= UnivScreenState.SectorView)
            {
                SolarSystem system = FindSolarSystemUnderCursor();
                if (system != null)
                {
                    if (system.IsExploredBy(Player))
                    {
                        GameAudio.SubBassWhoosh();
                        ViewSystem(system);
                    }
                    else
                    {
                        GameAudio.NegativeClick();
                    }
                    return true;
                }
            }

            return false;
        }

        bool SelectMultipleShipsByClickingOnShip(InputState input)
        {
            Ship clicked = FindClickedShip(input);
            if (clicked == null)
                return false;

            Array<Ship> selected = new() { clicked };

            Ship[] ships = UState.Objects.VisibleShips;
            foreach (Ship ship in ships)
            {
                if (clicked == ship || ship.Loyalty != clicked.Loyalty)
                    continue;

                bool sameHull   = ship.BaseHull == clicked.BaseHull;
                bool sameRole   = ship.DesignRole == clicked.DesignRole;
                bool sameDesign = ship.Name == clicked.Name;

                // TODO: These are not documented to the players
                if (input.SelectSameDesign) // Ctrl+Alt+DoubleClick
                {
                    if (sameDesign)
                        selected.AddUnique(ship);
                }
                else if (input.SelectSameRoleAndHull) // Ctrl+DoubleClick
                {
                    if (sameRole && sameHull)
                        selected.AddUnique(ship);
                }
                else if (input.SelectSameHull) // Alt+DoubleClick
                {
                    if (sameHull)
                        selected.AddUnique(ship);
                }
                else // simple DoubleClick, select Same Role
                {
                    if (sameRole)
                        selected.AddUnique(ship);
                }
            }

            SetSelectedShipList(selected, fleet: null);
            return true;
        }

        void CyclePlanetsInCombat(UIButton b)
        {
            if (Player.EmpirePlanetCombat > 0)
            {
                Planet planetToView = null;
                int planetIdx = 0;

                // try to select the next planet which is in combat
                foreach (SolarSystem system in UState.Systems)
                {
                    foreach (Planet p in system.PlanetList)
                    {
                        if (p.IsExploredBy(Player) && p.RecentCombat)
                        {
                            if (p.Owner?.isPlayer == true || p.Troops.WeHaveTroopsHere(UState.Player))
                            {
                                if (planetIdx == nextPlanetCombat)
                                    planetToView = p;
                                ++planetIdx;
                            }
                        }
                    }
                }

                ++nextPlanetCombat;
                if (nextPlanetCombat >= Player.EmpirePlanetCombat)
                    nextPlanetCombat = 0;

                if (planetToView != null)
                {
                    SetSelectedPlanet(planetToView);
                    SnapViewTo(new(planetToView.Position, 9000.0), 5f, 2f);
                    LookingAtPlanet = false;
                }
            }
        }

        void OnScrapSelectedItem()
        {
            Player.AI.RemoveGoal(SelectedItem.AssociatedGoal);

            bool found = false;
            var ships = Player.OwnedShips;
            foreach (Ship ship in ships)
            {
                if (ship.IsConstructor && ship.AI.OrderQueue.NotEmpty)
                {
                    for (int i = 0; i < ship.AI.OrderQueue.Count; ++i)
                    {
                        if (ship.AI.OrderQueue[i].Goal == SelectedItem.AssociatedGoal)
                        {
                            found = true;
                            ship.AI.OrderScrapShip();
                            break;
                        }
                    }
                }
            }

            if (!found)
            {
                foreach (Planet planet in Player.GetPlanets())
                {
                    foreach (QueueItem qi in planet.ConstructionQueue)
                    {
                        if (qi.Goal == SelectedItem.AssociatedGoal)
                        {
                            qi.IsCancelled = true; // cancel on next SBProduction update
                        }
                    }
                }
            }

            if (ClickableBuildGoals.ContainsRef(SelectedItem))
            {
                GameAudio.BlipClick();
            }

            ClearSelectedItems();
        }

        Fleet CreateNewFleet(int fleetId, IReadOnlyList<Ship> ships)
        {
            if (ships.Count == 0 || !ships.Any(s => s.CanBeAddedToFleets()))
                return null;

            Fleet newFleet = Player.CreateFleet(fleetId, null);
            AddShipsToFleet(newFleet, ships);
            return newFleet;
        }

        Fleet AddShipsToFleet(Fleet fleet, IReadOnlyList<Ship> ships)
        {
            if (ships.Count != 0)
            {
                ClearShipFleetsWithDataNodes(ships);
                fleet.AddShips(ships);

                fleet.SetCommandShip(null);
                fleet.AutoArrange(); // arrange new ships into formation
                fleet.Update(FixedSimTime.Zero/*paused during init*/);

                GameAudio.FleetClicked();
                return fleet;
            }
            return fleet;
        }

        // to handle the case where a ship is being reassigned,
        // the original datanodes must be cleared as well, which is only necessary
        // during reassignment
        void ClearShipFleetsWithDataNodes(IReadOnlyList<Ship> ships)
        {
            foreach (Ship ship in ships.Filter(s => s.CanBeAddedToFleets()))
            {
                // remove the DataNode
                ship.Fleet?.DataNodes.RemoveFirst(n => n.Ship == ship);
                ship.ClearFleet(returnToManagedPools: false, clearOrders: false);
            }
        }

        Vector2 StartDragPos;

        // Ludoal fork: the middle-drag pan, ONE arithmetic shared by the map path and the
        // visible band. Ctrl+middle stays the chase gesture and returns early here, or a
        // held chase click would fall into the pan and kill the snap.
        public void HandleMiddleMousePan(InputState input)
        {
            if (input.IsCtrlKeyDown)
                return;
            if (input.MiddleMouseClick)
                StartDragPos = input.CursorPosition;
            if (input.MiddleMouseHeld())
            {
                float worldWidthOnScreen = (float)VisibleWorldRect.Width;
                float dx = input.CursorPosition.X - StartDragPos.X;
                float dy = input.CursorPosition.Y - StartDragPos.Y;
                StartDragPos = input.CursorPosition;
                CamDestination.X += -dx * worldWidthOnScreen * 0.001f;
                CamDestination.Y += -dy * worldWidthOnScreen * 0.001f;
                snappingToShip = false;
                ViewingShip    = false;
                CamDestination.X = CamDestination.X.Clamped(-UState.Size, UState.Size);
                CamDestination.Y = CamDestination.Y.Clamped(-UState.Size, UState.Size);
            }
        }

        void HandleEdgeDetection(InputState input)
        {
            if (LookingAtPlanet)
                return;

            if (input.OpenScreenSaveMenu)
                ScreenManager.AddScreen(new SaveGameScreen(this));

            float worldWidthOnScreen = (float)VisibleWorldRect.Width;

            float x = input.CursorX, y = input.CursorY;
            float outer = -50f;
            float inner = +5.0f;
            float minLeft = outer, maxLeft = inner;
            float minTop  = outer, maxTop  = inner;
            float minRight  = ScreenWidth  - inner, maxRight  = ScreenWidth  - outer;
            float minBottom = ScreenHeight - inner, maxBottom = ScreenHeight - outer;

            bool InRange(float pos, float min, float max)
            {
                return min <= pos && pos <= max;
            }

            bool enableKeys = !ViewingShip;
            bool arrowKeys = Debug == false;

            HandleMiddleMousePan(input);
            if (input.IsCtrlKeyDown || !input.MiddleMouseHeld())
            {
                if (ShipInfoUIElement.IsHandlingNameInput)
                    return; // don't pan the camera if the ship name area is being edited

                bool enableMousePanning = !GlobalStats.DisableScreenPanning;
                if (enableMousePanning && InRange(x, minLeft, maxLeft) || (enableKeys && input.KeysLeftHeld(arrowKeys)))
                {
                    CamDestination.X -= 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
                if (enableMousePanning && InRange(x, minRight, maxRight) || (enableKeys && input.KeysRightHeld(arrowKeys)))
                {
                    CamDestination.X += 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
                if (enableMousePanning && InRange(y, minTop, maxTop) || (enableKeys && input.KeysUpHeld(arrowKeys)))
                {
                    CamDestination.Y -= 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
                if (enableMousePanning && InRange(y, minBottom, maxBottom) || (enableKeys && input.KeysDownHeld(arrowKeys) && !input.IsCtrlKeyDown))
                {
                    CamDestination.Y += 0.008f * worldWidthOnScreen;
                    snappingToShip = false;
                    ViewingShip    = false;
                }
            }

            CamDestination.X = CamDestination.X.Clamped(-UState.Size, UState.Size);
            CamDestination.Y = CamDestination.Y.Clamped(-UState.Size, UState.Size);
        }

        void HandleCameraZoomScrolling(InputState input)
        {
            if (AdjustCamTimer >= 0f)
                return;

            double scrollAmount = 1000.0;
            double camDestZ = CamDestination.Z;

            if (input.ScrollOut || input.BButtonHeld)
            {
                // gradually adjust scroll-out based on CamPos.Z
                if      (camDestZ >= 5_000_000) scrollAmount = 2000_000;
                if      (camDestZ >= 1200_000) scrollAmount = 1000_000;
                else if (camDestZ >= 600_000)  scrollAmount = 400_000;
                else if (camDestZ >= 250_000)  scrollAmount = 96_000; // 250_000: SystemView
                else if (camDestZ >= 100_000)  scrollAmount = 40_000;
                else if (camDestZ >= 35_000)   scrollAmount = 20_000; // 35_000: PlanetView
                else if (camDestZ >= 15_000)   scrollAmount = 7_000;  // 15_000: ShipView
                else if (camDestZ >= 7_000)    scrollAmount = 4_000;  // 7_000:  DetailView
                else if (camDestZ >= 3_000)    scrollAmount = 1_500;

                CamDestination.Z = (camDestZ + scrollAmount).Clamped(MinCamHeight, MaxCamHeight);
                //Log.Info($"scrollAmount: {scrollAmount}  Z={CamDestination.Z}");

                // turbo zoom out when Ctrl key is down
                if (input.IsCtrlKeyDown)
                {
                    // zoom out in two stages
                    CamDestination.Z = camDestZ < 55000.0 ? 60000.0 : MaxCamHeight;
                    AdjustCamTimer = 1f; // animated camera transition over 1sec
                    transitionElapsedTime = 0f;
                }
            }
            else if (input.ScrollIn || input.YButtonHeld)
            {
                // gradually adjusts scroll-in based on CamPos.Z
                if      (camDestZ >= 3200_000) scrollAmount = 1800_000;
                else if (camDestZ >= 1200_000) scrollAmount = 400_000;
                else if (camDestZ >= 600_000)  scrollAmount = 150_000;
                else if (camDestZ >= 300_000)  scrollAmount = 96_000;
                else if (camDestZ >= 100_000)  scrollAmount = 44_000;
                else if (camDestZ >= 60_000)   scrollAmount = 24_000;
                else if (camDestZ >= 35_000)   scrollAmount = 15_000; // 35_000: PlanetView
                else if (camDestZ >= 15_000)   scrollAmount = 7_500;  // 15_000: ShipView
                else if (camDestZ >= 7_000)    scrollAmount = 3_500;  // 7_000:  DetailView
                else if (camDestZ >= 3_000)    scrollAmount = 1_500;  // 7_000:  DetailView

                CamDestination.Z = (camDestZ - scrollAmount).Clamped(MinCamHeight, MaxCamHeight);
                //Log.Info($"scrollAmount: {scrollAmount}  Z={CamDestination.Z}");

                // turbo zoom in when Ctrl key is down
                if (input.IsCtrlKeyDown && camDestZ > 10000.0)
                {
                    CamDestination.Z = camDestZ <= 65000.0 ? 10000.0 : 60000.0;
                }

                // if we're not view-following a ship, adjust the camera towards target
                if (!ViewingShip)
                {
                    //fbedard: add a scroll on selected object
                    if ((!input.IsShiftKeyDown && GlobalStats.ZoomTracking) || (input.IsShiftKeyDown && !GlobalStats.ZoomTracking))
                        CamDestination = GetZoomTrackingTarget(input, CamDestination.Z);
                    else
                        CamDestination = GetCameraPosFromCursorTarget(input, CamDestination.Z);
                }
            }
        }

        Vector3d GetZoomTrackingTarget(InputState input, double camDestZ)
        {
            if (SelectedShip is { Active: true })
                return new(SelectedShip.Position, camDestZ);

            if (SelectedPlanet != null)
                return new(SelectedPlanet.Position, camDestZ);

            if (SelectedFleet != null && SelectedFleet.Ships.NotEmpty)
                return new(SelectedFleet.AveragePosition(), camDestZ);

            if (SelectedShipList.NotEmpty && SelectedShipList[0]?.Active == true)
                return new(SelectedShipList[0].Position, camDestZ);

            return GetCameraPosFromCursorTarget(input, camDestZ);
        }

        public bool IsShipUnderFleetIcon(Ship ship, Vector2 screenPos, float fleetIconScreenRadius)
        {
            foreach (ClickableFleet clickableFleet in ClickableFleetsList)
                if (clickableFleet.fleet == ship.Fleet && screenPos.InRadius(clickableFleet.ScreenPos, fleetIconScreenRadius))
                    return true;
            return false;
        }
    }
}