using System;
using SDGraphics;
using Ship_Game.Audio;
using Ship_Game.Fleets;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    public partial class UniverseScreen
    {
        public Vector3d GetNewCameraPos(Vector3d currentCamPos3d, Vector2 targetScreenPos, double desiredZ)
        {
            double currentZ = currentCamPos3d.Z;
            if (currentZ.AlmostEqual(desiredZ))
                return currentCamPos3d; // already there

            Vector3d targetWorldPos = UnprojectToWorldPosition3D(targetScreenPos);
            targetWorldPos.Z = desiredZ;
            return targetWorldPos;
        }

        Vector3d GetCameraPosFromCursorTarget(InputState input, double desiredCamZ)
        {
            Vector2 targetScreenPos = input.CursorPosition;
            Vector3d newPos = GetNewCameraPos(CamPos, targetScreenPos, desiredCamZ);

            // TODO: this happens quite rarely, but if it does, it's game-breaking
            if (double.IsNaN(newPos.X) || double.IsNaN(newPos.Y) || double.IsNaN(newPos.Z))
            {
                Log.Error($"New CameraPos NaN! CamPos:{CamPos} targetScreenPos:{targetScreenPos} desiredCamZ:{desiredCamZ}");

                // TODO: this is here to avoid a fatal View matrix corruption
                CamPos = new(0, 0, desiredCamZ);
                Matrix cameraMatrix = Matrices.CreateLookAtDown(CamPos.X, CamPos.Y, -CamPos.Z);
                SetViewMatrix(cameraMatrix);
                return CamPos;
            }

            // this decides how fast we zoom-average towards new camera position
            const double NewPosRate = 0.5;
            double newX = (newPos.X*NewPosRate + CamPos.X*(1.0 - NewPosRate));
            double newY = (newPos.Y*NewPosRate + CamPos.Y*(1.0 - NewPosRate));
            return new(newX, newY, desiredCamZ);
        }

        public void ViewToShip(Ship ship)
        {
            if (ship == null)
                return;

            SetSelectedShip(ship);

            ShipToView = ship;
            AdjustCamTimer = 1.0f;
            transitionElapsedTime = 0.0f;
            CamDestination.Z = CamDestination.Z.UpperBound(GetZfromScreenState(UnivScreenState.PlanetView));
            snappingToShip = true;
            ViewingShip = true;
        }

        public void SnapViewColony(Planet p, bool combatView)
        {
            ShowShipNames = false;
            bool doReturnToShip = ViewingShip;
            SetSelectedPlanet(p);
            if (p == null)
                return;

            if (combatView && Debug)
            {
                OpenCombatMenu(p);
                return;
            }

            if (!p.System.IsExploredBy(Player))
            {
                GameAudio.NegativeClick();
            }
            else
            {
                bool flag = false;
                if (p.Owner == Player && combatView ||
                    p.Owner != Player && Player.data.MoleList.Any(m => m.PlanetId == p.Id) && combatView)
                {
                    OpenCombatMenu(p);
                    return;
                }

                foreach (Mole mole in Player.data.MoleList)
                {
                    if (mole.PlanetId == p.Id)
                    {
                        flag = true;
                        break;
                    }
                }

                // Ludoal fork: Planet View removed — the selection cartouche carries its info.
                // Double-click: colony view on real colonies (incl. mole vision), combat view
                // when tactically visible, otherwise just the camera snap.
                if ((p.Owner == Player || flag || Debug) && p.Owner != null)
                {
                    // Ludoal fork (spec: colony-as-tab): a map-opened colony rides the GALAXY
                    // group's row with origin -1 - Esc closes to the map, as it always did.
                    // A MOLE's host opens on the DIPLOMACY row with Espionage as the origin
                    // instead (bench 397): its reader came from espionage territory, and Esc
                    // goes back there. ⚠ A list screen arms its own seat for this planet
                    // before calling the snap - that arming wins, don't demote it.
                    if (HostedTabTitle != p.Name)
                    {
                        if (p.Owner != Player && flag)
                            HostColonyTab(p, GameScreens.ScreenGroups.Group.Diplomacy,
                                          (int)MainDiplomacyScreen.Tab.Espionage);
                        else
                            HostColonyTab(p, GameScreens.ScreenGroups.Group.Galaxy, -1);
                    }
                    // Ludoal fork (migration, bench 386): a STACKED page like every tab - no
                    // mount, no camera anchoring, the map simply keeps living underneath.
                    // The planet STAYS selected - its cartouche shows through (bench 396).
                    SetSelectedPlanet(p);
                    ReturnToListScreen = null;
                    ReturnToListGroup  = GameScreens.ScreenGroups.Group.None;
                    // any page still open closes first (bench 397): the cartouche eye can fire
                    // with a page up, and the colony must not bury it under a foreign tab row
                    ScreenManager.ExitAllAbove(this);
                    ScreenManager.AddScreen(new ColonyScreen(this, p, EmpireUI));
                    return;
                }
                else if (combatView && p.Habitable
                                    && p.IsExploredBy(Player)
                                    && (p.WeAreInvadingHere(Player) || !Player.DifficultyModifiers.HideTacticalData
                                                                    || p.System.OwnerList.Contains(Player)
                                                                    || p.OurShipsCanScanSurface(Player)))
                {
                    OpenCombatMenu(p); // snaps the view itself
                    return;
                }

                SnapViewTo(new(p.Position.X, p.Position.Y, GetZfromScreenState(UnivScreenState.PlanetView)), 5f, 2f); // Ludoal fork: 2500 was nose-on-the-planet; PlanetView is the named level for this
            }
        }

        public void SnapViewTo(Vector3d worldPos, float duration, float adjustCamTimer = 2f)
        {
            CamDestination = worldPos;
            AdjustCamTimer = adjustCamTimer;

            transitionStartPosition = CamPos;
            transitionElapsedTime = 0.0f;
            transDuration = duration;
        }

        public void SnapViewSystem(SolarSystem s, Planet p, UnivScreenState camHeight, bool select = true)
        {
            double z = GetZfromScreenState(camHeight);
            SnapViewTo(new(s.Position.X, s.Position.Y + 400f, z), 5f, 2f);

            bool doReturnToShip = ViewingShip;
            if (select) // Ludoal fork: notification snaps pass false — a selection the
                SetSelectedSystem(s, p); // player never made kept the exploded view armed on dezoom
            returnToShip = doReturnToShip;
        }

        public void SnapViewShip(Ship s)
        {
            ShowShipNames = false;
            SetSelectedShip(s);
            if (s == null)
                return;

            SnapViewTo(new(s.Position.X, s.Position.Y + 400, 2500), 5f, 2f);
            LookingAtPlanet = false;
            // an immobile ship (station/platform) has nothing to chase: engaging follow
            // mode on it traps the camera (field report: could not zoom out from a
            // station-built notification). Snap and select only.
            if (s.IsPlatformOrStation)
                return;
            ShipToView = s;
            snappingToShip = true;
            ViewingShip = true;
        }

        public void SnapViewFleet(Fleet fleet)
        {
            ViewingShip = false;
            AdjustCamTimer = 0.5f;
            CamDestination = fleet.AveragePosition().ToVec3d(CamDestination.Z);

            if (CamPos.Z <= (int)UnivScreenState.DetailView)
                CamDestination.Z = GetZfromScreenState(UnivScreenState.DetailView);
        }

        void ViewSystem(SolarSystem s)
        {
            SnapViewTo(new(s.Position, 147000f), 3f, 1f);
        }

        void ViewPlanet(Planet p, UnivScreenState zoomLevel)
        {
            SetSelectedPlanet(p);
            SnapViewTo(new(p.Position, GetZfromScreenState(zoomLevel)), 3f, 1f);
        }

        void ViewFleet(Fleet f, UnivScreenState zoomLevel)
        {
            SnapViewTo(new(f.AveragePosition(), GetZfromScreenState(zoomLevel)), 3f, 1f);
        }

        void AdjustCamera(float elapsedTime)
        {
            if (ShipToView == null)
                ViewingShip = false;

            #if DEBUG
                float minCamHeight = 400.0f;
            #else
                float minCamHeight = Debug ? 1337.0f : 400.0f;
            #endif

            AdjustCamTimer -= elapsedTime;
            if (ViewingShip && !snappingToShip && ShipToView != null)
            {
                UState.CamPos.X = ShipToView.Position.X;
                UState.CamPos.Y = ShipToView.Position.Y;
                UState.CamPos.Z = UState.CamPos.Z.SmoothStep(CamDestination.Z, 0.2);
                if (UState.CamPos.Z < minCamHeight)
                    UState.CamPos.Z = minCamHeight;
                // Ludoal fork (wishlist #1): keep the free-camera destination in tow —
                // it went stale during the chase, so every exit path glided the camera
                // back to the pre-chase position instead of staying at the ship.
                CamDestination.X = ShipToView.Position.X;
                CamDestination.Y = ShipToView.Position.Y;
            }

            if (AdjustCamTimer > 0.0)
            {
                if (ShipToView == null)
                    snappingToShip = false;

                transitionElapsedTime += elapsedTime;
                double amount = Math.Pow(transitionElapsedTime / (double)transDuration, 0.7);

                if (snappingToShip && ShipToView != null)
                {
                    CamDestination.X = ShipToView.Position.X;
                    CamDestination.Y = ShipToView.Position.Y;
                    CamPos = CamPos.SmoothStep(CamDestination, amount);

                    if (AdjustCamTimer - elapsedTime <= 0f)
                    {
                        ViewingShip = true;
                        transitionElapsedTime = 0.0f;
                        AdjustCamTimer = -1f;
                        snappingToShip = false;
                    }
                }
                else
                {
                    CamPos = CamPos.SmoothStep(CamDestination, amount);

                    if (transitionElapsedTime > transDuration ||
                        CamPos.ToVec2f().Distance(CamDestination.ToVec2f()) < 50f &&
                        Math.Abs(CamPos.Z - CamDestination.Z) < 50f)
                    {
                        transitionElapsedTime = 0.0f;
                        AdjustCamTimer = -1f;
                    }
                }
                if (UState.CamPos.Z < minCamHeight)
                    UState.CamPos.Z = minCamHeight;
            }
            else if (LookingAtPlanet && SelectedPlanet != null)
            {
                UState.CamPos.X = UState.CamPos.X.SmoothStep(SelectedPlanet.Position.X, 0.2);
                UState.CamPos.Y = UState.CamPos.Y.SmoothStep(SelectedPlanet.Position.Y + 400f, 0.2);
            }
            else if (!ViewingShip) // regular free camera movement in Universe
            {
                UState.CamPos = UState.CamPos.SmoothStep(CamDestination, 0.2);
                if (UState.CamPos.Z < minCamHeight)
                    UState.CamPos.Z = minCamHeight;
            }

            UState.CamPos.X = UState.CamPos.X.Clamped(-UState.Size, +UState.Size);
            UState.CamPos.Y = UState.CamPos.Y.Clamped(-UState.Size, +UState.Size);
            UState.CamPos.Z = UState.CamPos.Z.Clamped(minCamHeight, MaxCamHeight);

            //Log.Write(ConsoleColor.Green, $"CamPos {CamPos.X:0.00} {CamPos.Y:0.00} {CamPos.Z:0.00}  Dest {CamDestination.X:0.00} {CamDestination.Y:0.00} {CamDestination.Z:0.00}");

            var newViewState = UnivScreenState.DetailView;
            foreach (UnivScreenState state in Enum.GetValues(typeof(UnivScreenState)))
            {
                if (CamPos.Z <= GetZfromScreenState(state))
                {
                    newViewState = state;
                    break;
                }
            }

            // We reset the Perspective Matrix because at close zoom levels
            // we need to reduce the MaxDistance of the Projection matrix
            // Otherwise our screen projection is extremely inaccurate due to float errors
            if (viewState != newViewState)
            {
                viewState = newViewState;

                const double maxDetailNebulaDist = 15_000_000;
                double maxDistance = maxDetailNebulaDist;
                switch (newViewState)
                {
                    case UnivScreenState.DetailView: maxDistance += (int)UnivScreenState.ShipView; break;
                    case UnivScreenState.ShipView:   maxDistance += (int)UnivScreenState.PlanetView; break;
                    case UnivScreenState.PlanetView: maxDistance += (int)UnivScreenState.SystemView; break;
                    case UnivScreenState.SystemView: maxDistance += (int)UnivScreenState.SectorView; break;
                    case UnivScreenState.SectorView: maxDistance += (int)UnivScreenState.GalaxyView; break;
                    case UnivScreenState.GalaxyView: maxDistance += maxDetailNebulaDist; break;
                }

                //Log.Info($"View: {newViewState} MaxDistance: {maxDistance}  CamHeight: {CamPos.Z}");
                ProjMaxDistance = maxDistance;
                ApplyUniverseProjection();
            }
            // bench 392 (maintainer): a page opening/closing, a resize, OR a switch to a panel of
            // a DIFFERENT width all move the viewport offset without touching the view state -
            // re-lay the projection whenever the offset VALUE changes (not just its existence, or
            // wide->short panel kept the old shift), so the map recentres to the current panel.
            else if (PageViewportOffset() != AppliedPageOffset)
            {
                ApplyUniverseProjection();
            }
        }

        // bench 390 (maintainer): the last maxDistance the view-state ladder chose. Kept so a
        // page opening/closing can re-lay the projection with the viewport offset WITHOUT
        // changing the zoom clamp - only the frustum's off-centre moves.
        double ProjMaxDistance = 15_000_000 + (int)UnivScreenState.GalaxyView;
        Vector2 AppliedPageOffset; // the exact offset the current projection was built for (bench 392)

        // bench 391 (maintainer): with a page open on a wide display the map's visible band is
        // the strip LEFT of the open panel plus what sits right of it; centre the view there.
        // Target: x = mid of the free band right of the OPEN PANEL'S own right edge (short panels
        // leave more room than the old fixed 1680), y = mid of the left band between the top
        // bar's bottom and the minimap's top. Off under 1920 or with no page open. Returned as a
        // fraction of the screen (frame-centre-minus-screen-centre), the shape offsetXY speaks.
        public Vector2 PageViewportOffset()
        {
            if (ScreenWidth < 1920 || !OpenGroupPage(out GameScreen page))
                return default;
            float panelRight = page.PageFrame.Right; // the actual open panel's width (bench 391)
            float targetX = (ScreenWidth + panelRight) * 0.5f;
            float barBottom = EmpireUIOverlay.BarTop + EmpireUIOverlay.BarH;
            float minimapTop = ScreenHeight - 256 - 10; // mmHousing.Y (LoadContent)
            float targetY = (barBottom + minimapTop) * 0.5f;
            return new Vector2((targetX - ScreenWidth * 0.5f) / ScreenWidth,
                               (targetY - ScreenHeight * 0.5f) / ScreenHeight);
        }

        // the top-most group screen (or hosted colony) in the stack, when a page is open
        bool OpenGroupPage(out GameScreen page)
        {
            var stack = ScreenManager.Screens;
            for (int i = stack.Count - 1; i >= 0; --i)
                if (GameScreens.ScreenGroups.GroupOf(stack[i]) != GameScreens.ScreenGroups.Group.None)
                {
                    page = stack[i];
                    return true;
                }
            page = null;
            return false;
        }

        public void ApplyUniverseProjection()
        {
            Vector2 offset = PageViewportOffset();
            AppliedPageOffset = offset;
            SetPerspectiveProjection(maxDistance: ProjMaxDistance, offsetXY: offset);
        }

        public void InputZoomToShip()
        {
            GameAudio.AcceptClick();
            if (SelectedShip != null)
            {
                ViewToShip(SelectedShip);
            }
            else if (SelectedPlanet != null)
            {
                ViewPlanet(SelectedPlanet, UnivScreenState.PlanetView);
            }
            else if (SelectedSystem != null)
            {
                ViewSystem(SelectedSystem);
            }
            else if (SelectedFleet != null)
            {
                ViewFleet(SelectedFleet, UnivScreenState.PlanetView);
            }
        }

        public void InputZoomOut()
        {
            GameAudio.AcceptClick();
            AdjustCamTimer = 1f;
            transitionElapsedTime = 0f;
            CamDestination.X = CamPos.X;
            CamDestination.Y = CamPos.Y;
            CamDestination.Z = 4200000f;
        }

        void DefaultZoomPoints()
        {
            snappingToShip = false;
            ViewingShip = false;
            if (CamPos.Z < GetZfromScreenState(UnivScreenState.GalaxyView) &&
                CamPos.Z > GetZfromScreenState(UnivScreenState.SectorView))
            {
                AdjustCamTimer = 1f;
                transitionElapsedTime = 0f;
                CamDestination = new(CamPos.X, CamPos.Y, 1175000.0);
            }
            else if (CamPos.Z > GetZfromScreenState(UnivScreenState.ShipView))
            {
                AdjustCamTimer = 1f;
                transitionElapsedTime = 0f;
                CamDestination = new(CamPos.X, CamPos.Y, 147000.0);
            }
            else if (viewState < UnivScreenState.SystemView)
            {
                CamDestination = new(CamPos.X, CamPos.Y, GetZfromScreenState(UnivScreenState.SystemView));
            }
        }

        // Ludoal fork (wishlist): leave the planet panel but STAY at the planet on
        // the main map (the normal dismiss flies the camera back to where it was).
        // Keeps the previous zoom level, at the planet's position, planet selected.
        public void ClosePlanetPanelStayHere()
        {
            if (workersPanel == null)
                return;
            Planet p = workersPanel.P;
            LookingAtPlanet = false;
            SnapToPlanetStayHere(p);
        }

        // Ludoal fork (migration, bench 386): the stay-here landing with the planet passed
        // in - the stacked colony has no mount this could read. One camera arithmetic for
        // the eye gesture and the combat panel's own close.
        public void SnapToPlanetStayHere(Planet p)
        {
            SetSelectedPlanet(p);
            returnToShip = false;
            CamDestination = new Vector3d(p.Position.X, p.Position.Y,
                                          GetZfromScreenState(UnivScreenState.PlanetView)); // aligned with the planet-snap standard (was 2500, too strong)
            AdjustCamTimer = 1f;
            transitionElapsedTime = 0f;
        }

        // Ludoal fork (bench 388): the table single-click - select the subject on the map
        // and pan to it at the CURRENT zoom, cartouche showing through the band. Zooming
        // onto the subject is the cartouche's own business (click-to-cartouche spec).
        public void PanToKeepZoom(in Vector2 pos)
        {
            CamDestination = new Vector3d(pos.X, pos.Y, CamPos.Z);
            AdjustCamTimer = 1f;
            transitionElapsedTime = 0f;
        }

        public void PanToPlanetKeepZoom(Planet p)
        {
            SetSelectedPlanet(p);
            returnToShip = false;
            PanToKeepZoom(p.Position);
        }

        public void PanToShipKeepZoom(Ship s)
        {
            SetSelectedShip(s);
            PanToKeepZoom(s.Position);
        }

        public void PanToSystemKeepZoom(SolarSystem s)
        {
            SetSelectedSystem(s);
            PanToKeepZoom(s.Position);
        }

        void ToggleViewingShip()
        {
            // Ludoal fork (wishlist #1): ViewToShip sets ViewingShip=true itself —
            // the old unconditional flip flipped it back OFF right after arming,
            // leaving only the initial snap (chase died if anything interrupted it)
            if (!ViewingShip)
                ViewToShip(SelectedShip);
            else
                ViewingShip = false;
        }

        void ToggleCinematicMode()
        {
            if (!IsCinematicModeEnabled)
            {
                CinematicModeTextTimer = 3;
            }
            IsCinematicModeEnabled = !IsCinematicModeEnabled;
        }
    }
}