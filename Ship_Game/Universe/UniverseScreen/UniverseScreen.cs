using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.AI;
using Ship_Game.Debug;
using Ship_Game.Ships;
using SynapseGaming.LightingSystem.Core;
using SynapseGaming.LightingSystem.Lights;
using SynapseGaming.LightingSystem.Rendering;
using SynapseGaming.LightingSystem.Shadows;
using System;
using System.Threading;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.GameScreens;
using Ship_Game.Universe;
using Ship_Game.Fleets;
using Ship_Game.GameScreens.FleetDesign;
using Ship_Game.Graphics;
using Ship_Game.Graphics.Particles;
using Matrix = SDGraphics.Matrix;
using Vector2 = SDGraphics.Vector2;
using Vector3 = SDGraphics.Vector3;
using Rectangle = SDGraphics.Rectangle;
using BoundingFrustum = Microsoft.Xna.Framework.BoundingFrustum;
using Ship_Game.ExtensionMethods;

namespace Ship_Game
{
    public partial class UniverseScreen : GameScreen
    {
        // The non-visible state of the Universe
        public readonly UniverseState UState;

        public string StarDateString => UState.StarDate.StarDateString();
        public float LastAutosaveTime = 0;
        public float LastAutosaveStarDate = 0; // Ludoal fork: turn-based autosave anchor

        public Background bg;

        public Array<Bomb> BombList  = new();
        readonly AutoResetEvent DrawCompletedEvt = new(false);
        bool LoggedGeneralUIDrawError; // Ludoal fork: log the first UI-draw failure only

        public const double MinCamHeight = 450.0;
        protected double MaxCamHeight;
        public Vector3d CamDestination;
        public Vector3d CamPos { get => UState.CamPos; set => UState.CamPos = value; }
        public Vector3d transitionStartPosition;

        public bool ViewingShip = false;
        public float transDuration = 3f;
        public float SelectedSomethingTimer = 3f;

        public bool ShowTacticalCloseup { get; private set; }
        public bool Debug => UState.Debug;
        public DebugModes DebugMode => UState.DebugMode;

        public PieMenu pieMenu;
        PieMenuNode planetMenu;
        PieMenuNode shipMenu;

        public ParticleManager Particles;

        public Background3D bg3d;
        public Empire Player => UState.Player;
        public string PlayerLoyalty => Player.data.Traits.Name;

        public UnivScreenState viewState { get => UState.ViewState; set => UState.ViewState = value; }
        public bool LookingAtPlanet;
        public bool snappingToShip;
        public bool returnToShip;
        // Ludoal fork (bench 191): when a colony was opened from a LIST screen (Economy,
        // Empire, Troops), closing it goes back to that list rather than to the map (maintainer feedback).
        // What is remembered is how to REOPEN it, not which one it was: the three screens have
        // three different constructors, and an enum here would be one more thing to keep in
        // step with them. Cleared as soon as it is used, and by any colony opened from the map.
        public Action ReturnToListScreen;
        // the exited list screen's group frame + tab row, drawn as a dimmed silhouette
        // under a list-opened colony - origin tab still selected. Written at every
        // arming site; only read while ReturnToListScreen is non-null.
        public Submenu ReturnToListTabs;
        // Ludoal fork (maintainer feedback): the group the exited list screen belonged to, so the
        // top bar keeps that group's button lit while a list-opened colony is up (the list screen
        // itself has left the stack). Armed alongside ReturnToListScreen; None when it clears.
        public GameScreens.ScreenGroups.Group ReturnToListGroup = GameScreens.ScreenGroups.Group.None;

        // Ludoal fork: the HOSTED tab's state - the successor of the trio above (spec:
        // colony-as-tab, maintainer decision: the mechanism is universal, the colony is
        // only its first subject; a ship or a troop panel rides the same seat later).
        // A group's screens are born and die at every tab swap; only the universe survives
        // them, so it carries what rides a group's row: the tab's title, HOW to (re)open
        // its panel (never which type it was - the ReturnToListScreen philosophy), which
        // group hosts it, and the tab index Esc returns to (-1 = opened from the map,
        // where Esc closes to the map). One hosted seat: a new subject replaces the old.
        public string HostedTabTitle;                 // null = no hosted tab
        public Action OpenHostedTabPanel;             // how the tab's click reopens its panel
        public GameScreens.ScreenGroups.Group HostedTabGroup = GameScreens.ScreenGroups.Group.None;
        public int HostedTabOrigin = -1;

        // Ludoal fork: arm the hosted seat for a colony - the panel-open mirrors the
        // map-open block (Camera.cs) except it must NOT clear the tab state: the tab is
        // precisely what is being opened. Camera anchoring identical - the panel covers
        // the map. The title follows the planet; the colony arrows re-arm on navigation.
        // Ludoal fork (maintainer): the Empire group's colony tab is PERMANENT - it remembers
        // the last colony viewed there, the capital by default. The seat above is transient
        // (per-visit, any group); this survives seat clears and group jumps.
        public Planet EmpireColonyPlanet;
        public Planet EmpireColonyDefault
        {
            get
            {
                if (EmpireColonyPlanet != null && EmpireColonyPlanet.Owner == Player)
                    return EmpireColonyPlanet;
                return Player.GetCurrentCapital(out Planet capital) ? capital : null;
            }
        }

        // the permanent Colony tab's opener - activating it pans (no zoom) to the planet
        public void OpenEmpireColonyTab()
        {
            Planet p = EmpireColonyDefault;
            if (p == null)
                return;
            HostColonyTab(p, GameScreens.ScreenGroups.Group.Empire, -1);
            PanToPlanetKeepZoom(p); // selects the planet too - the cartouche shows through
            ScreenManager.AddScreen(new ColonyScreen(this, p, EmpireUI));
        }

        public void HostColonyTab(Planet p, GameScreens.ScreenGroups.Group group, int originTab)
        {
            if (group == GameScreens.ScreenGroups.Group.Empire)
                EmpireColonyPlanet = p; // the permanent tab follows the last colony viewed
            HostedTabTitle = p.Name;
            HostedTabGroup = group;
            HostedTabOrigin = originTab;
            // Ludoal fork (migration, bench 386): the colony is a STACKED page now, like
            // every tab - one code path for input, pause, band and closing.
            OpenHostedTabPanel = () =>
            {
                SetSelectedPlanet(p); // stays selected - the cartouche shows through (bench 396)
                ScreenManager.AddScreen(new ColonyScreen(this, p, EmpireUI));
            };
        }

        public void ClearHostedTab()
        {
            HostedTabTitle = null;
            OpenHostedTabPanel = null;
            HostedTabGroup = GameScreens.ScreenGroups.Group.None;
            HostedTabOrigin = -1;
        }

        public EmpireUIOverlay EmpireUI;
        public BloomComponent bloomComponent;
        public DistortionComponent distortionComponent;
        // §3.8.B: shadow-map depth pre-pass. Sits next to the post-process
        // components since it has the same lifetime contract (LoadContent
        // here, dispose on UnloadGraphics). Unlike Bloom/Distortion this
        // is a PRE-pass — SceneInterface.RenderScene drives it before the
        // lit pass, so the wiring here is just construction + handing the
        // component off to ScreenManager's SceneInterface.
        public Ship_Game.Graphics.ShadowMapComponent shadowMapComponent;
        // §3.7 step 2: reusable scratch list for the per-frame distortion-source
        // build. ShieldManager.BuildDistortionSources appends; capacity matches
        // DistortionComponent.MaxShields so the typical case allocates nothing.
        readonly System.Collections.Generic.List<DistortionComponent.DistortionSource> DistortionSources
            = new(DistortionComponent.MaxShields);
        public Texture2D FogMap;
        // Phase 3.7 step 3: ping-pong fog-of-war RTs. Pre-migration code did
        // `FogMap = fogMapTarget.GetTexture()` which returned a SEPARATE
        // snapshot texture (XNA 3.1 RenderTarget2D semantics). MonoGame removed
        // that — RenderTarget2D *is* a Texture2D, so the migrated `FogMap = rt`
        // made source and destination of UpdateFogMap the same memory. Reading
        // a texture that's currently bound as the active RT is undefined under
        // D3D11, breaking persistent exploration. Ping-pong restores the
        // separate-source-and-destination invariant: render front→back, swap.
        RenderTarget2D FogMapTargetA;
        RenderTarget2D FogMapTargetB;
        public RenderTarget2D MainTarget;
        public RenderTarget2D BorderRT;
        RenderTarget2D LightsTarget;
        // §3.7 step 1: bloom output RT. Allocated only when RenderBloom is on;
        // bloomComponent processes MainTarget into this, and the fog-of-war
        // composite then sources from here instead of MainTarget.
        RenderTarget2D PostBloomTarget;
        // §3.7 step 2: distortion output RT. Allocated when RenderShieldDistortion
        // is on. We can't alias-safely write back to MainTarget/PostBloomTarget —
        // the PS samples its source, so destination MUST be a separate RT. When
        // no shield is actively hit, the pass is skipped and the composite reads
        // from the prior stage instead.
        RenderTarget2D PostDistortTarget;

        #pragma warning disable CA2213 // managed by Content Manager
        public Effect basicFogOfWarEffect;
        #pragma warning restore CA2213

        public Rectangle SelectedStuffRect;
        public NotificationManager NotificationManager;
        public ShieldManager Shields;
        public Rectangle MinimapDisplayRect;
        public Rectangle mmShowBorders;
        public Rectangle mmHousing;
        public AnomalyManager anomalyManager;
        public ShipInfoUIElement ShipInfoUIElement;
        public PlanetInfoUIElement pInfoUI;
        public StarInfoUIElement sInfoUI; // Ludoal fork (wishlist): star cartouche
        public SolarsystemOverlay SystemInfoOverlay;
        public ShipListInfoUIElement shipListInfoUI;
        public VariableUIElement vuiElement;
        Rectangle DsbCancelRect; // Ludoal fork (wishlist): cancel button on the build cartouche
        public MiniMap Minimap { get; private set; }
        bool loading;
        public float transitionElapsedTime;

        // @note Initialize with a default frustum for UnitTests
        public BoundingFrustum Frustum = new(Matrix.CreateTranslation(1000000, 1000000, 0));

        float MusicCheckTimer;
        public Ship ShipToView;
        public float AdjustCamTimer;
        public ExoticBonusesWindow ExoticBonusesWindow;
        public FreighterUtilizationWindow FreighterUtilizationWindow;
        public bool DefiningAO; // are we defining a new AO?
        public bool DefiningTradeRoutes; // are we defining  trade routes for a freighter?
        public Rectangle AORect; // used for showing current AO Rect definition

        // Ludoal fork: the five map overlays live in UState so the selection rides the save;
        // these properties keep every call site (hotkeys, minimap buttons) unchanged.
        public bool ShowingFTLOverlay         { get => UState.ShowFTLOverlay;         set => UState.ShowFTLOverlay = value; }         // F4
        public bool ShowingInfluenceOverlay   { get => UState.ShowInfluenceOverlay;   set => UState.ShowInfluenceOverlay = value; }   // F2
        public bool ShowingGravityWellOverlay { get => UState.ShowGravityWellOverlay; set => UState.ShowGravityWellOverlay = value; } // F5
        public bool ShowingVisionOverlay      { get => UState.ShowVisionOverlay;      set => UState.ShowVisionOverlay = value; }      // F3
        public bool ShowingRangeOverlay       { get => UState.ShowRangeOverlay;       set => UState.ShowRangeOverlay = value; }       // F6

        /// <summary>
        /// Toggles Cinematic Mode (no UI) on or off
        /// </summary>
        bool IsCinematicModeEnabled = false;
        float CinematicModeTextTimer = 3f;

        /// <summary>
        /// Conditions to suppress diplomacy screen popups
        /// </summary>
        public bool CanShowDiplomacyScreen => UState.CanShowDiplomacyScreen && !IsCinematicModeEnabled;

        public DeepSpaceBuildingWindow DeepSpaceBuildWindow;
        public DebugInfoScreen DebugWin;
        public bool ShowShipNames;
        bool UseRealLights = true;
        bool SelectingWithBox;

        public PlanetScreen workersPanel;
        int SelectorFrame;

        public UIButton ShipsInCombat;
        public UIButton PlanetsInCombat;
        public int lastshipcombat   = 0;
        public int nextPlanetCombat = 0;

        ShipMoveCommands ShipCommands;

        // for really specific debugging
        public int SimTurnId;

        // To avoid double-loading universe thread when
        // graphics setting changes cause 
        bool IsUniverseInitialized;

        public bool IsViewingCombatScreen(Planet p) => LookingAtPlanet && workersPanel is CombatScreen cs && cs.P == p;
        // Ludoal fork (migration, bench 386): the colony is a stacked page - ask the stack
        public bool IsViewingColonyScreen(Planet p)
        {
            var stack = ScreenManager.Screens;
            for (int i = 0; i < stack.Count; ++i)
                if (stack[i] is ColonyScreen cs && cs.P == p && !stack[i].IsExiting)
                    return true;
            return false;
        }

        /// <summary>
        /// RADIUS of the universe, Stars are generated within XY range [-universeRadius, +universeRadius]
        /// </summary>
        public UniverseScreen(UniverseParams settings, float universeRadius) : base(null, toPause: null)
        {
            UState = new UniverseState(this, settings, universeRadius);
            Initialize();
        }

        public UniverseScreen(UniverseState state) : base(null, toPause: null) // load game
        {
            UState = state;
            UState.OnUniverseScreenLoaded(this);
            loading = true;
            Initialize();
        }

        void Initialize()
        {
            UState.EvtOnShipRemoved += Objects_OnShipRemoved;
            Name = "UniverseScreen";
            CanEscapeFromScreen = false;

            ShipCommands = new ShipMoveCommands(this);
            DeepSpaceBuildWindow = new DeepSpaceBuildingWindow(this);
        }

        void Objects_OnShipRemoved(Ship ship)
        {
            void RemoveShip()
            {
                if (SelectedShip == ship)
                    SelectedShip = null;
                SelectedShipList.RemoveRef(ship);
            }
            RunOnNextFrame(RemoveShip);
        }

        // NOTE: this relies on MaxCamHeight and UniverseSize
        public void ResetLighting(bool forceReset) // Ludoal fork: public for the battle sim arena
        {
            if (!forceReset && ScreenManager.LightRigIdentity == LightRigIdentity.UniverseScreen)
                return;

            if (!UseRealLights)
            {
                AssignLightRig(LightRigIdentity.UniverseScreen);
                return;
            }

            RemoveLighting();
            ScreenManager.LightRigIdentity = LightRigIdentity.UniverseScreen;

            float globalLightRad = (float)(UState.Size * 2 + MaxCamHeight * 10);
            float globalLightZPos = (float)(MaxCamHeight * 10);
            AddLight("Global Fill Light", new Vector2(0, 0), 0.7f, globalLightRad, Color.White, -globalLightZPos, fillLight: false, shadowQuality: 0f);
            AddLight("Global Back Light", new Vector2(0, 0), 0.6f, globalLightRad, Color.White, +globalLightZPos, fillLight: false, shadowQuality: 0f);

            // Phase B refactor: scene-wide AmbientLight feeds SharedFx via
            // LightingEffectBinder so every SO (ships, planets, asteroids,
            // launching ships, debris) gets a consistent shadow-floor without
            // per-object PrimaryLight* setup. The "sun" itself comes from the
            // existing per-system Key/LocalFill/OverSaturationKey PointLights:
            // LightingEffectBinder picks the closest system's 3 lights and
            // populates the 3 PointLight slots in MeshLighting.fx. The shader
            // recomputes light direction per-pixel from world position, with
            // smooth-quadratic radius falloff per light — automatic per-ship
            // parallax + faithful SunBurn-style multi-light contributions.
            // Ambient at 0.06× white matches the pre-refactor per-SO ambient
            // floor (PrimaryLightColor * 0.06 in the old contrast pass).
            AddLight(new AmbientLight
            {
                Name = "Universe Ambient",
                DiffuseColor = Color.White.ToVector3(),
                Intensity = 0.06f,
            }, dynamic: false);

            foreach (SolarSystem system in UState.Systems)
                ResetSolarSystemLights(system);
        }

        public void ResetSolarSystemLights(SolarSystem system)
        {
            system.Lights.Clear();
            Color color     = system.Sun.LightColor;
            float intensity = system.Sun.LightIntensity;
            float radius    = system.Sun.Radius;
            // §4.6.B(b) test: Key Z bumped from -5500 → -50000 to steepen the
            // toLight vector at the play plane. With the sun close to ship z=0,
            // the light direction was nearly grazing — half-vector specular
            // peaks barely landed on top hull faces. Moving the sun far above
            // gives a more uniform overhead-light direction across the active
            // system.
            //
            // Scene-light radius decoupled from sun.Radius (the sprite size):
            // SolarSystem.MinRadius is 150k, but most suns set Radius in the
            // 40k–100k range — at d=150k, the shader's `1 - (d/R)^2` falloff
            // clamps to zero and ships on the far side of the system get no
            // sun light at all (only MinAmbient). 215k gives ~51% sun
            // intensity at the system edge (1 - (150/215)^2) while still
            // tapering toward the rim, and treats binaries (R=40k–50k
            // sprite) the same as normal stars (R=100k). The
            // OverSaturationKey stays tied to sun.Radius intentionally —
            // it's a small near-sun overbright, not a system-wide light.
            const float SystemLightRadius = 215_000f;
            var light1 = AddLight("Key",               system, intensity,         SystemLightRadius, color, -50000);
            var light2 = AddLight("OverSaturationKey", system, intensity * 5.00f, radius * 0.05f,    color, -1500);
            var light3 = AddLight("LocalFill",         system, intensity * 0.55f, SystemLightRadius, Color.White, 0);
            //AddLight("Back", system, intensity * 0.5f , radius, color, 2500, fallOff: 0, fillLight: true);
            system.Lights.Add(light1);
            system.Lights.Add(light2);
            system.Lights.Add(light3);
        }

        void RemoveLighting()
        {
            ScreenManager.RemoveAllLights();
        }

        PointLight AddLight(string name, SolarSystem system, float intensity, float radius, Color color, float zpos, float fallOff = 1f, bool fillLight = false)
        {
            return AddLight($"{system.Name} - {system.Sun.Id} - {name}", system.Position, intensity, radius, color,
                            zpos, fillLight: fillLight, fallOff:fallOff, shadowQuality:0f);
        }

        protected PointLight AddLight(string name, Vector2 source, float intensity, float radius, Color color,
                            float zpos, bool fillLight, float fallOff = 0, float shadowQuality = 1) // Ludoal fork: protected for the battle sim arena
        {
            var light = new PointLight
            {
                Name                = name,
                DiffuseColor        = color.ToVector3(),
                Intensity           = intensity,
                ObjectType          = ObjectType.Static, // RedFox: changed this to Static
                FillLight           = fillLight,
                Radius              = radius,
                Position            = new Vector3(source, zpos),
                Enabled             = true,
                FalloffStrength     = fallOff,
                ShadowPerSurfaceLOD = true,
                ShadowQuality = shadowQuality
            };

            if (shadowQuality > 0f)
                light.ShadowType = ShadowType.AllObjects;

            light.World = Matrix.CreateTranslation((Vector3)light.Position);
            AddLight(light, dynamic:false);
            return light;
        }

        public override void LoadContent()
        {
            Log.Write(ConsoleColor.Cyan, "UniverseScreen.LoadContent");
            RemoveAll();
            UnloadGraphics();

            UState.ResearchRootUIDToDisplay = GlobalStats.Defaults.ResearchRootUIDToDisplay;

            NotificationManager = new(ScreenManager, this);

            Shields = new(this);

            InitializeCamera(); // ResetLighting requires MaxCamHeight
            ResetLighting(forceReset: true);
            LoadGraphics();

            InitializeUniverse();
        }

        // So this should be the absolute max height for the camera
        // And this also defines the limit to Perspective Matrix's MaxDistance
        // The bigger Perspective project MaxDistance is, the less accurate our screen coordinates
        public const double CAM_MAX = 15_000_000;

        void InitializeCamera()
        {
            float univSizeOnScreen = 10f;

            MaxCamHeight = CAM_MAX;
            SetPerspectiveProjection(maxDistance: CAM_MAX);

            while (univSizeOnScreen < (ScreenWidth + 50))
            {
                float univRadius = UState.Size / 2f;
                var camMaxToUnivCenter = Matrices.CreateLookAtDown(-univRadius, univRadius, MaxCamHeight);

                Vector3 univTopLeft  = new Vector3(
                    Viewport.Project(Vector3.Zero, Projection, camMaxToUnivCenter, Matrix.Identity)
                );
                Vector3 univBotRight = new Vector3(
                    Viewport.Project(new Vector3(UState.Size * 1.25f, UState.Size * 1.25f, 0.0f), Projection, camMaxToUnivCenter, Matrix.Identity)
                );
                univSizeOnScreen = Math.Abs(univBotRight.X - univTopLeft.X);
                if (univSizeOnScreen < (ScreenWidth + 50))
                    MaxCamHeight -= 0.1 * MaxCamHeight;
            }

            if (MaxCamHeight > CAM_MAX)
                MaxCamHeight = CAM_MAX;

            if (!loading)
            {
                // Ludoal fork: a planet-less universe (battle simulator arena) has no
                // colony to frame — start the camera at the origin instead of crashing.
                var planets = Player.GetPlanets();
                CamPos = new Vector3d(planets.Count > 0 ? planets[0].Position : Vector2.Zero, 2750);
            }

            CamDestination = CamPos;
        }

        void InitializeUniverse()
        {
            if (IsUniverseInitialized)
                return;

            IsUniverseInitialized = true;
            CreateStartingShips();
            InitializeSolarSystems();

            foreach (Empire empire in UState.Empires)
            {
                empire.InitEmpireFromSave(UState);
            }

            WarmUpShipsForLoad();

            if (UState.StarDate.AlmostEqual(1000)) // Run once to get all empire goals going
            {
                Array<Empire> updated = UpdateEmpires(FixedSimTime.Zero);
                EndOfTurnUpdate(updated, FixedSimTime.Zero);
            }
            CreateUniverseSimThread();
        }

        void CreateUniverseSimThread()
        {
            if (!CreateSimThread)
                return;
            SimThread = new Thread(UniverseSimMonitored);
            SimThread.Name = "Universe.SimThread";
            SimThread.IsBackground = false; // RedFox - make sure ProcessTurns runs with top priority
            SimThread.Start();
        }

        void InitializeSolarSystems()
        {
            anomalyManager = new();

            foreach (SolarSystem system in UState.Systems)
            {
                foreach (Anomaly anomaly in system.AnomaliesList)
                {
                    if (anomaly.type == "DP")
                    {
                        anomalyManager.AnomaliesList.Add(new DimensionalPrison(UState, system.Position + anomaly.Position));
                    }
                }

                foreach (Empire empire in UState.ActiveEmpires)
                {
                    system.UpdateFullyExploredBy(empire);
                }

                foreach (Planet planet in system.PlanetList)
                {
                    planet.InitializePlanetMesh();
                    planet.UpdatePlanetStatsByRecalculation();
                }
            }
        }

        void CreateStartingShips()
        {
            // not a new game or load game at stardate 1000 
            if (UState.StarDate > 1000f || UState.Ships.Length > 0)
                return;

            foreach (Empire empire in UState.MajorEmpires)
            {
                // Ludoal fork: an empire without colonies (battle simulator arena)
                // has no home planet to spawn starting ships around — skip it.
                if (empire.GetPlanets().Count == 0)
                    continue;

                Planet homePlanet = empire.GetPlanets()[0];
                string colonyShip = empire.data.DefaultColonyShip;
                string startingScout = empire.data.StartingScout;
                string freighter = empire.data.DefaultSmallTransport;
                string starterShip = empire.data.Traits.Prototype == 0
                                   ? empire.data.StartingShip
                                   : empire.data.PrototypeShip;

                //if starting ship is a station - make it orbit the planet
                Ship createdStartingShip = Ship.CreateShipNearPlanet(UState, starterShip, empire, homePlanet, true);
                if (createdStartingShip != null && (createdStartingShip.MaxFTLSpeed == 0 || createdStartingShip.MaxSTLSpeed == 0))
                {
                    createdStartingShip.Position = homePlanet.Position.GenerateRandomPointOnCircle(500 + homePlanet.Radius, UState.Random);
                    createdStartingShip.TetherToPlanet(homePlanet);
                }
                Ship.CreateShipNearPlanet(UState, colonyShip, empire, homePlanet, true);
                Ship startingFrieghter = Ship.CreateShipNearPlanet(UState, freighter, empire, homePlanet, true);
                if (startingFrieghter != null) // FB - wa for new frieghter since this is done onShipComplete in sbproduction
                {
                    startingFrieghter.TransportingProduction = true;
                    startingFrieghter.TransportingFood       = true;
                    startingFrieghter.TransportingColonists  = true;
                    startingFrieghter.AllowInterEmpireTrade  = true;
                }

                for (int i = 0; i < 1 + empire.data.Traits.ExtraStartingScouts; i++)
                    Ship.CreateShipNearPlanet(UState, startingScout, empire, homePlanet, true);
            }
        }

        // Ludoal fork (maintainer spec): the minimap housing scales with an options slider.
        // One seat for everything the widget anchors - the housing, the click target, the
        // border toggle, and the two combat counters above the frame - so a live change from
        // the options screen cannot leave them drifted apart. ⚠ the CLICK target is the map's
        // own rect, asked of the MiniMap: a separate hand-measured rect drifted the moment the
        // frame was reworked, and clicks panned the camera off-target.
        public void SeatMinimap()
        {
            const int minimapOffSet = 14;
            // 10px off both edges, the margin the overlay tabs and every reworked frame keep
            const int mmMargin = 10;
            float mult = GlobalStats.MinimapSizeMult.Clamped(1f, 2f);
            int mmW = (int)((276 + minimapOffSet) * mult);
            int mmH = (int)(256 * mult);
            mmHousing = new Rectangle(GameBase.ScreenWidth - mmW - mmMargin,
                                      GameBase.ScreenHeight - mmH - mmMargin, mmW, mmH);
            Minimap?.RemoveFromParent();
            Minimap = Add(new MiniMap(this, mmHousing));
            MinimapDisplayRect = Minimap.MapRect;
            mmShowBorders = new Rectangle(MinimapDisplayRect.X, MinimapDisplayRect.Y - 25, 32, 32);

            if (ShipsInCombat != null)
            {
                Rectangle mmap = Minimap.MapRect;
                int mmFrameL = mmap.X - 6, mmFrameR = mmap.Right + 6;
                int counterW = (mmFrameR - mmFrameL - 6) / 2;
                int counterY = mmHousing.Y - 30;
                ShipsInCombat.Rect   = new Rectangle(mmFrameL, counterY, counterW, 24);
                PlanetsInCombat.Rect = new Rectangle(mmFrameR - counterW, counterY, counterW, 24);
            }
            // the utility overlays hang off the minimap frame - they follow it (bench 406)
            ExoticBonusesWindow?.SeatByMinimap();
            FreighterUtilizationWindow?.SeatByMinimap();
        }

        void LoadGraphics()
        {
            var device  = ScreenManager.GraphicsDevice;
            int width   = GameBase.ScreenWidth;
            int height  = GameBase.ScreenHeight;

            Particles = new ParticleManager(TransientContent);

            if (GlobalStats.DrawStarfield)
            {
                bg = new Background(this, device);
            }

            if (GlobalStats.DrawNebulas)
            {
                bg3d = new Background3D(this, device);
            }

            Frustum = new BoundingFrustum(ViewProjection);
            SeatMinimap();
            ExoticBonusesWindow = Add(new ExoticBonusesWindow(this));
            FreighterUtilizationWindow = Add(new FreighterUtilizationWindow(this));

            // Ludoal fork: reopen the utility windows the save had open. Done here because
            // LoadContent also runs on a device reset, so a resize keeps them open too.
            // Freighters/Exotic are exclusive - the else-if enforces it even on a bad save.
            if (UState.ShowDeepSpaceBuildWindow && !DeepSpaceBuildWindow.Visible)
                DeepSpaceBuildWindow.InitializeAndShow();
            if (UState.ShowExoticBonusesWindow)
                ExoticBonusesWindow.ToggleVisibility(playSound: false);
            else if (UState.ShowFreighterUtilWindow)
                FreighterUtilizationWindow.ToggleVisibility(playSound: false);


            // Ludoal fork: 10px off the left and bottom edges, the margin the whole reworked
            // interface keeps - every info cartouche (ship, system, planet, star, fleet list)
            // derives from this one rect, so they all move together.
            SelectedStuffRect = new Rectangle(10, height - 257, 407, 247); // visible frame = 247-26 shave = 221, the minimap frame's height
            DsbCancelRect = new Rectangle(SelectedStuffRect.X + 25, SelectedStuffRect.Y + 150, 182, 25); // Ludoal fork
            ShipInfoUIElement = new ShipInfoUIElement(SelectedStuffRect, ScreenManager, this);
            SystemInfoOverlay = new SolarsystemOverlay(SelectedStuffRect, ScreenManager, this);
            pInfoUI           = new PlanetInfoUIElement(SelectedStuffRect, ScreenManager, this);
            sInfoUI           = new StarInfoUIElement(SelectedStuffRect, ScreenManager, this); // Ludoal fork
            shipListInfoUI    = new ShipListInfoUIElement(SelectedStuffRect, ScreenManager, this);
            vuiElement        = new VariableUIElement(SelectedStuffRect, ScreenManager, this);
            EmpireUI          = new EmpireUIOverlay(Player, device, this);

            if (GlobalStats.RenderBloom)
            {
                bloomComponent = new BloomComponent(device, TransientContent);
                bloomComponent.LoadContent();
            }
            if (GlobalStats.RenderShieldDistortion)
            {
                distortionComponent = new DistortionComponent(device, TransientContent);
                distortionComponent.LoadContent();
            }
            // Shadow infrastructure (Phase 3.8.B — ShadowMapComponent +
            // RunShadowPrePass + receiver shader path) is intentionally NOT
            // attached on the universe screen. StarDrive's universe view is
            // effectively coplanar — ships float at ~the same Z, the sun is far
            // enough to act as a near-directional light from above, and there is
            // no terrain receiver. The pre-pass produces no visible benefit but
            // does produce real artifacts: ComputeCasterBounds includes the
            // planet itself as a caster, so the ortho light frustum stretches
            // huge, and the planet samples the shadow map at UVs that fall
            // inside the frustum footprint — painting a hard rectangle of
            // shadow on the planet surface where the cruiser geometry projects.
            // The plumbing stays in place for any future scene (hangar floor,
            // planet-surface combat, 3D fleet view) that genuinely benefits;
            // attaching is a per-screen decision, not a global on/off.
            // GlobalStats.RenderShadows is preserved as a setting for that
            // future use.

            // §4.6 #1.b regression fix: MainTarget MUST PreserveContents because the
            // shadow pre-pass in SunBurnStubs.RenderScene swaps to ShadowMap and back
            // mid-frame. With DiscardContents, the rebind wipes the already-drawn
            // RenderBackdrop output (nebula + stars + clouds), leaving a black scene
            // under the ship meshes when zoomed in close enough that ships have
            // non-zero bounds. Other RTs (Border, Lights, FogMap, PostBloom,
            // PostDistort) are explicitly cleared at the start of their write passes,
            // so DiscardContents is fine for them.
            MainTarget   = RenderTargets.Create(device, RenderTargetUsage.PreserveContents);
            LightsTarget = RenderTargets.Create(device);
            BorderRT     = RenderTargets.Create(device);
            if (GlobalStats.RenderBloom)
                PostBloomTarget = RenderTargets.Create(device);
            if (GlobalStats.RenderShieldDistortion)
                PostDistortTarget = RenderTargets.Create(device);

            NotificationManager.ReSize();

            CreateFogMap(TransientContent, device);
            CreatePieMenu();

            FTLManager.LoadContent(this);

            // ⚠ derived from the minimap FRAME (asked of the MiniMap), not from screen-foot
            // constants: the two counters span the frame's width, half each (maintainer), and
            // sit just above the widget, clear of its icon bands.
            Rectangle mmap = Minimap.MapRect;
            int mmFrameL = mmap.X - 6, mmFrameR = mmap.Right + 6;   // the painted rule's edges
            int counterW = (mmFrameR - mmFrameL - 6) / 2;
            int counterY = mmHousing.Y - 30;
            ShipsInCombat = ButtonMediumMuted(mmFrameL, counterY, "Ships: 0");
            ShipsInCombat.Size = new Vector2(counterW, 24);
            ShipsInCombat.DynamicText = () =>
            {
                ShipsInCombat.Style = Player.EmpireShipCombat > 0 ? ButtonStyle.Medium : ButtonStyle.MediumMuted;
                return $"Ships: {Player.EmpireShipCombat}";
            };
            ShipsInCombat.Tooltip = "Cycle through ships not in fleet that are in combat";
            ShipsInCombat.OnClick = ShipsInCombatClick;
            Add(ShipsInCombat);

            PlanetsInCombat = ButtonMediumMuted(mmFrameR - counterW, counterY, "Planets: 0");
            PlanetsInCombat.Size = new Vector2(counterW, 24);
            PlanetsInCombat.DynamicText = () =>
            {
                PlanetsInCombat.Style = Player.EmpirePlanetCombat > 0 ? ButtonStyle.Medium : ButtonStyle.MediumMuted;
                return $"Planets: {Player.EmpirePlanetCombat}";
            };
            PlanetsInCombat.OnClick = CyclePlanetsInCombat;
            PlanetsInCombat.Tooltip = "Cycle through planets that are in combat";

            RectF leftRect = new(25, 60, 200, 500); // 5 right (bench 307): the Patrol badge kissed the edge
            Add(new FleetButtonsList(leftRect, this, this,
                onClick: OnFleetButtonClicked,
                onHotKey: OnFleetHotKeyPressed,
                isSelected: (b) => SelectedFleet?.Key == b.FleetKey
            ));
        }

        void ShipsInCombatClick(UIButton b)
        {
            int nbrship = 0;
            if (lastshipcombat >= Player.EmpireShipCombat)
                lastshipcombat = 0;
            var ships = Player.OwnedShips;
            foreach (Ship ship in ships)
            {
                if (ship.Fleet != null || ship.OnLowAlert || ship.IsHangarShip || ship.IsHomeDefense || !ship.Active)
                    continue;
                if (nbrship == lastshipcombat)
                {
                    ViewToShip(ship);
                    lastshipcombat++;
                    break;
                }

                nbrship++;
            }
        }

        void CreateFogMap(Data.GameContentManager content, GraphicsDevice device)
        {
            EnsureFogMapRenderTargets(device);

            // Clear both ping-pong RTs to fully transparent (no exploration yet).
            device.SetRenderTarget(FogMapTargetA);
            device.Clear(Color.Transparent);
            device.SetRenderTarget(FogMapTargetB);
            device.Clear(Color.Transparent);
            device.SetRenderTarget(null);
            FogMap = FogMapTargetA;

            // Ludoal fork: the fog map is fully derivable now (explored systems restamp
            // every frame), so saved bytes are not loaded — this also purges the ship
            // wakes baked into older saves. Saves still write the bytes (revert-safe).
            if (false && UState.FogMapBytes != null)
            {
                // Load saved alpha mask into the front RT so the next UpdateFogMap
                // call samples it. FromAlphaOnly returns a stand-alone Texture2D
                // (rgb=alpha for premul correctness); blit it onto FogMapTargetA.
                Texture2D loaded = content.RawContent.TexImport.FromAlphaOnly(UState.FogMapBytes);
                UState.FogMapBytes = null;
                if (loaded != null)
                {
                    using var sb = new Microsoft.Xna.Framework.Graphics.SpriteBatch(device);
                    device.SetRenderTarget(FogMapTargetA);
                    device.Clear(Color.Transparent);
                    sb.Begin(blendState: Microsoft.Xna.Framework.Graphics.BlendState.Opaque);
                    sb.Draw(loaded, new Rectangle(0, 0, 512, 512), Color.White);
                    sb.End();
                    device.SetRenderTarget(null);
                    loaded.Dispose();
                }
            }

            basicFogOfWarEffect = content.Load<Effect>("Effects/BasicFogOfWar");
        }

        public override void UnloadContent()
        {
            UState.Paused = true;

            if (StarDriveGame.Instance != null) // don't show in tests
                Log.Write(ConsoleColor.Cyan, "UniverseScreen.UnloadContent");

            ScreenManager.UnloadSceneObjects();
            // destroy SceneObjects for everything
            UState.RemoveSceneObjects();
            base.UnloadContent();
        }

        public override void Update(float fixedDeltaTime)
        {
            // (bench 386 migration: the colony is a stacked page - the top bar closes it on a
            // group jump like any page, and the seat still dies where the universe regains input)

            if (LookingAtPlanet)
                workersPanel?.Update(fixedDeltaTime);

            // Ludoal fork: the utility windows ride the save like the overlays - their open
            // state is polled so no toggle site is ever missed.
            UState.ShowDeepSpaceBuildWindow = DeepSpaceBuildWindow.Visible;
            UState.ShowExoticBonusesWindow  = ExoticBonusesWindow?.IsOpen == true;
            UState.ShowFreighterUtilWindow  = FreighterUtilizationWindow?.IsOpen == true;

            DeepSpaceBuildWindow.Update(fixedDeltaTime);
            pieMenu.Update(fixedDeltaTime);
            SelectedSomethingTimer -= fixedDeltaTime;

            if (++SelectorFrame > 299)
                SelectorFrame = 0;

            ScreenManager.StartMusic("AmbientMusic");
            NotificationManager.Update(fixedDeltaTime);

            GameAudio.Update3DSound(new Vector3((float)CamPos.X, (float)CamPos.Y, (float)CamPos.Z));

            ScreenManager.UpdateSceneObjects(fixedDeltaTime);
            EmpireUI.Update(fixedDeltaTime);
            UpdateSelectedItems(GameBase.Base.Elapsed);

            base.Update(fixedDeltaTime);
        }

        void UpdateSelectedItems(UpdateTimes elapsed)
        {
            if (ShowSystemInfoOverlay)
            {
                SystemInfoOverlay.Update(elapsed);
            }
            // Ludoal fork (field report 45.44): the clickable build goals froze while paused, so
            // a DSB under construction could not be selected. We fixed it here, on the UI thread;
            // upstream took the diagnosis (PR #356) but rewrote the fix onto the SIM thread,
            // because reading GoalsList from here reopens the torn-read race their fixes_24 had
            // closed. The patch 47 merge brought their version in and left ours in place, so the
            // refresh ran on both threads at once - the exact race their rewrite exists to avoid.
            // Theirs owns it now: see ProcessSimulationTurns in UniverseScreen.UpdateGame.cs.

            if (ShowPlanetInfo)
            {
                pInfoUI.Update(elapsed);
            }
            else if (ShowStarInfo)
            {
                sInfoUI.Update(elapsed); // Ludoal fork (wishlist)
            }
            else if (ShowShipInfo)
            {
                ShipInfoUIElement.Update(elapsed);
            }
            else if (ShowShipList)
            {
                shipListInfoUI.Update(elapsed);
            }
            else if (ShowFleetInfo)
            {
                shipListInfoUI.Update(elapsed);
            }
        }

        public void OnPlayerDefeated()
        {
            // TODO Post-1.60: StarDriveGame.EndingGame() lived in the deleted XNA wrapper;
            // restore once we wire equivalent shutdown hooks on MonoGame's Game class.
            // Low priority — current path still ends the game correctly via screen-stack
            // unwind; the missing hook only affects the legacy graceful-shutdown contract.
            UState.GameOver = true;
            UState.Paused = true;
            UState.Objects.Clear();
            HelperFunctions.CollectMemory();
            ScreenManager.AddScreen(new YouLoseScreen(this));
            UState.Paused = false;
        }

        public void OnPlayerWon(LocalizedText title = default)
        {
            UState.GameOver = true;
            ScreenManager.AddScreen(new YouWinScreen(this, title));
        }

        void UnloadGraphics()
        {
            if (MainTarget == null)
                return;
            if (!GlobalStats.IsUnitTest)
                Log.Write(ConsoleColor.Cyan, "Universe.UnloadGraphics");
            Mem.Dispose(ref bloomComponent);
            Mem.Dispose(ref distortionComponent);
            // Detach from ScreenManager BEFORE disposing so SceneInterface.
            // RenderScene can't run a depth pass against a disposed RT
            // during the teardown frame.
            ScreenManager?.AttachShadowMap(null);
            Mem.Dispose(ref shadowMapComponent);
            Mem.Dispose(ref bg);
            FogMap = null; // alias of FogMapTargetA/B; the RTs own the lifetime
            Mem.Dispose(ref FogMapTargetA);
            Mem.Dispose(ref FogMapTargetB);
            Mem.Dispose(ref MainTarget);
            Mem.Dispose(ref BorderRT);
            Mem.Dispose(ref LightsTarget);
            Mem.Dispose(ref PostBloomTarget);
            Mem.Dispose(ref PostDistortTarget);
            Mem.Dispose(ref Particles);
            Mem.Dispose(ref Shields);
            Mem.Dispose(ref ExoticBonusesWindow);
            Mem.Dispose(ref FreighterUtilizationWindow);
            Mem.Dispose(ref DebugWin);
            Mem.Dispose(ref workersPanel);
        }

        protected override void Dispose(bool disposing)
        {
            // Only walk managed children on an explicit Dispose. From the finalizer
            // (disposing == false), Ships / UState / module slots etc. may already
            // have been finalized in undefined GC order, so reaching into them AVs
            // (e.g. Ship.RemoveFromUniverseUnsafe -> BombBays.Clear()). UState and
            // its children all have their own finalizers and will clean themselves
            // up; we just don't drive that walk from here.
            if (disposing)
            {
                UnloadGraphics();

                anomalyManager = null;
                BombList.Clear();
                PendingSimThreadActions.Dispose();
                NotificationManager?.Clear();
                SelectedShipList = new();

                DrawCompletedEvt.Dispose();
                UState.Dispose();
            }

            base.Dispose(disposing);
        }

        public override void ExitScreen()
        {
            if (IsDisposed)
                return; // already exited and disposed

            IsExiting = true;
            UState.Paused = true;

            Thread processTurnsThread = SimThread;
            SimThread = null;
            DrawCompletedEvt.Set(); // notify processTurnsThread that we're terminating

            // Wait for the in-flight simulation turn to finish before Dispose() below tears
            // down UState (which disposes every Planet/Ship). A single turn is bounded, but a
            // heavy one (e.g. an empire federation AbsorbEmpire) on a memory-pressured machine
            // can take far longer than the old 250ms timeout. Disposing underneath a running
            // turn makes the sim thread dereference a disposed planet (NRE in ProcessTurns).
            // Use a generous timeout that still guards against a genuinely stuck thread.
            if (processTurnsThread != null && !processTurnsThread.Join(10_000))
                Log.Warning("UniverseScreen.ExitScreen: sim thread did not stop within 10s; tearing down anyway");

            RemoveLighting();
            ScreenManager.StopMusic();

            ClearSelectedItems();
            ShipToView = null;

            EmpireHullBonuses.Clear();
            ClickableFleetsList.Clear();

            base.ExitScreen();
            Dispose(); // will call virtual Dispose(bool disposing) and UnloadGraphics()

            HelperFunctions.CollectMemory();
            // make sure we reset the latest savegame attachment
            Log.ConfigureStatsReporter(null);
        }

        // When user or automation AI orders a deep space build goal
        // Then these are used to visualize it to players
        public class ClickableSpaceBuildGoal
        {
            public Vector2 ScreenPos;
            public Vector2 BuildPos;
            public float Radius;
            public string UID;
            public Goal AssociatedGoal;
            public bool HitTest(Vector2 touch) => touch.InRadius(ScreenPos, Radius);
        }

        struct ClickableFleet
        {
            public Fleet fleet;
            public Vector2 ScreenPos;
            public float ClickRadius;
        }
        public enum UnivScreenState
        {
            DetailView = 7000,
            ShipView   = 15000,
            PlanetView = 35000,
            SystemView = 250000,
            SectorView = 1775000, // from 250_001 to 1_775_000
            GalaxyView
        }

        public double GetZfromScreenState(UnivScreenState screenState)
        {
            if (screenState == UnivScreenState.GalaxyView)
                return MaxCamHeight;
            return (double)screenState;
        }
    }
}

