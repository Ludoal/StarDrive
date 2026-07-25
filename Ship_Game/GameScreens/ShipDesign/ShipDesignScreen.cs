using System;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.AI.CombatTactics.UI;
using Ship_Game.Audio;
using Ship_Game.Gameplay;
using Ship_Game.GameScreens;
using Ship_Game.GameScreens.ShipDesign;
using Ship_Game.GameScreens.Universe.Debug;
using Ship_Game.Ships;
using Ship_Game.UI;
using Ship_Game.Universe;
using SynapseGaming.LightingSystem.Lights;
// SunBurn + MonoGame both define DirectionalLight; we want SunBurn's here.
using DirectionalLight = SynapseGaming.LightingSystem.Lights.DirectionalLight;
using Point = SDGraphics.Point;
using Rectangle = SDGraphics.Rectangle;

using RectF = SDGraphics.RectF;
using Vector2 = SDGraphics.Vector2;
using Vector3 = SDGraphics.Vector3;

namespace Ship_Game
{
    public enum ModuleOrientation
    {
        Normal, Left, Right, Rear
    }

    public sealed partial class ShipDesignScreen : GameScreen
    {
        public UniverseScreen ParentUniverse;
        public Empire Player => ParentUniverse.Player;
        public DesignStanceButtons OrdersButton;

        public DesignShip DesignedShip { get; private set; }
        public ShipDesign CurrentDesign; // only Null during first time init, otherwise never Null, even in Hull Editor
        public ShipHull CurrentHull => CurrentDesign?.BaseHull; // can be null during first time init
        public DesignModuleGrid ModuleGrid;

        public string DesignOrHullName => CurrentDesign.Name;

        public EmpireUIOverlay EmpireUI;
        public string InitialDesign; // Ludoal fork: design to open with (battle sim return path)
        static string LastDesignThisSession; // Ludoal fork: reopen where we left off, this run only

        Vector3 CameraPos = new Vector3(0f, 0f, 1300f);
        float DesiredCamHeight = 1300f;
        Vector2 StartDragPos;

        // Slow ambient orbit of the Key/Fill/Back rig around the world Y axis
        // (~105s per revolution at 0.06 rad/s). Each frame Update recomputes
        // each light's Direction by rotating its captured initial direction.
        DirectionalLight ShipyardKey, ShipyardFill, ShipyardBack;
        Microsoft.Xna.Framework.Vector3 ShipyardKeyDir0, ShipyardFillDir0, ShipyardBackDir0;
        float ShipyardLightOrbitAngle;
        const float ShipyardLightOrbitSpeed = 0.06f;

        readonly Array<ShipHull> AvailableHulls = new Array<ShipHull>();
        UIButton BtnSaveAs;
        UIButton BtnSymmetricDesign; // Symmetric Module Placement Feature by Fat Bastard
        UIButton BtnFilterModules;   // Filter Absolute Modules
        UIButton BtnStripShip;       // Removes all modules but armor, shields and command modules
        GenericButton ArcsButton;
        Rectangle SearchBar;
        Rectangle BottomSep;
        Rectangle BlackBar;

        ShipDesignInfoPanel InfoPanel;
        ShipDesignInfoPanel ComparedPanel; // Ludoal fork: the pinned design, beside the active one
        Submenu ComparedSub;
        ShipDesignIssuesPanel IssuesPanel;

        // this contains module selection list and active module selection info
        public ModuleSelection ModuleSelectComponent { get; private set; }
        // Ludoal fork: this list is now the merged browser — hull groups, each opening
        // with its bare hull, then the designs built on it. Name kept until the merge
        // is proven in game, then it becomes BrowserList.
        ScrollList<ShipYardBrowserItem> HullSelectList;
        public IShipDesign ComparedDesign; // shift-clicked design, pinned for comparison
        UITextEntry BrowserFilter;         // Ludoal fork: the load popup's filters, rehoused
        string BrowserFilterText;
        bool ShowLockedDesigns;
        const string DefaultBrowserFilter = "filter by name or hull...";
        ShipInfoOverlayComponent ShipInfoOverlay; // hover preview, same component the load popup used

        public ShipModule HighlightedModule;
        SlotStruct ProjectedSlot;
        string ScreenToLaunch;

        public ShipModule ActiveModule;
        public ShipModule CompareModule; // Ludoal fork: pinned comparison module (Shift-click in the module list)
        CategoryDropDown CategoryList;
        HangarDesignationDropDown HangarOptionsList;

        bool ShowAllArcs;
        public bool ToggleOverlay = true;
        bool ShipSaved = true;
        public bool HullEditMode;
        HullEditorControls HullEditor;

        // Used in Developer Sandbox to load any design
        bool UnlockAllFactionDesigns;

        // Used in Dev SandBox to enable some special debug features
        public bool EnableDebugFeatures;

        public RoleName Role => DesignedShip.DesignRole;
        Rectangle DesignRoleRect;

        public HangarOptions HangarDesignation => HangarOptionsList.ActiveValue;

        public bool IsSymmetricDesignMode => GlobalStats.SymmetricDesign; // Ludoal fork: player preference (config), no longer per-save

        public bool IsFilterOldModulesMode
        {
            get => ParentUniverse.UState.P.FilterOldModules;
            set => ParentUniverse.UState.P.FilterOldModules = value;
        }
          
        struct MirrorSlot
        {
            public SlotStruct Slot;
            public ModuleOrientation ModuleRot;
            public int TurretAngle;
        }

        public ShipDesignScreen(UniverseScreen universe, EmpireUIOverlay empireUi) : base(universe, toPause: universe)
        {
            ParentUniverse = universe;
            Name = "ShipDesignScreen";
            EmpireUI = empireUi;
            TransitionOnTime = 2f;
            HullEditMode = false;
            UnlockAllFactionDesigns = universe is DeveloperUniverse;
            EnableDebugFeatures = universe is DeveloperUniverse || universe.Debug;
        }

        void ReorientActiveModule(ModuleOrientation orientation)
        {
            if (ActiveModule == null)
                return;
            ShipModule template = ResourceManager.GetModuleTemplate(ActiveModule.UID);
            ActiveModule.SetModuleRotation(template.XSize, template.YSize, 
                                           orientation, ShipModule.DefaultFacingFor(orientation));
        }

        // Ludoal fork: Shift-click in the module list pins a module for side-by-side
        // comparison with the Active Module panel; same module again unpins it.
        public void SetCompareModule(ShipModule template)
        {
            if (CompareModule != null && template != null && CompareModule.UID == template.UID)
                CompareModule = null;
            else
                CompareModule = CreateModuleListItem(template);
        }

        // Ludoal fork: Shift-click in the browser pins a design beside the active one, and the
        // same design again unpins it — the exact gesture SetCompareModule gives modules. The
        // try/catch is inherited from the hover overlay, which needed it on real designs.
        public void SetComparedDesign(IShipDesign design)
        {
            if (ComparedDesign != null && design != null && ComparedDesign.Name == design.Name)
                design = null; // re-pinning the same design unpins it

            ComparedDesign = design;

            if (design == null)
            {
                ComparedPanel?.SetActiveDesign(null);
                if (ComparedSub != null) ComparedSub.Visible = false;
                if (ComparedPanel != null) ComparedPanel.Visible = false;
                return;
            }

            try
            {
                var ship = new DesignShip(ParentUniverse.UState, design as ShipDesign);
                ship.RecalculatePower();
                ship.ShipStatusChange();
                ComparedPanel.SetActiveDesign(ship);
                ComparedSub.Visible = ComparedPanel.Visible = true;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Compared design failed: {design.Name}");
                ComparedDesign = null;
                ComparedSub.Visible = ComparedPanel.Visible = false;
            }
        }

        public ShipModule CreateModuleListItem(ShipModule template)
        {
            return CreateDesignModule(template.UID, ModuleOrientation.Normal, 0, DynamicHangarOptions.DynamicLaunch.ToString());
        }

        public ShipModule CreateDesignModule(string uid, ModuleOrientation moduleRot, int turretAngle, string hangarShipUID)
        {
            if (!ResourceManager.GetModuleTemplate(uid, out ShipModule moduleTemplate))
                return null; // this module UID doesn't exist anymore
            UniverseState us = ParentUniverse.UState;
            return ShipModule.CreateDesignModule(us, uid, moduleRot, turretAngle, hangarShipUID, CurrentHull);
        }

        // spawn a new active module under cursor
        // WARNING: must use Module UID string here, otherwise we can get incorrect XSIZE/YSIZE due to Orientations
        void SpawnActiveModule(string moduleUID, ModuleOrientation moduleRot, int turretAngle, string hangarShipUID)
        {
            ActiveModule = CreateDesignModule(moduleUID, moduleRot, turretAngle, hangarShipUID);
        }

        void ResetActiveModule()
        {
            ActiveModule?.UninstallModule();
            ActiveModule = null;
        }
        
        public void SetActiveModule(string moduleUID, ModuleOrientation moduleRot, int turretAngle, string hangarShipUID)
        {
            SpawnActiveModule(moduleUID, moduleRot, turretAngle, hangarShipUID);
            HighlightedModule = null;
        }

        class SlotInstall
        {
            public readonly SlotStruct Slot;
            public readonly ShipModule Mod;
            bool CanInstall;
            public SlotInstall() {}
            public SlotInstall(SlotStruct slot, ShipModule mod)
            {
                Slot = slot;
                Mod = mod;
            }
            public bool UpdateCanInstallTo(DesignModuleGrid grid)
            {
                if (Slot == null)
                    return false;
                if (!grid.ModuleFitsAtSlot(Slot, Mod))
                {
                    GameAudio.NegativeClick();
                    return false;
                }
                CanInstall = !Slot.IsSame(Mod, Mod.ModuleRot, Mod.TurretAngle);
                return CanInstall;
            }
            public void TryInstallTo(DesignModuleGrid designGrid)
            {
                if (CanInstall)
                    designGrid.InstallModule(Slot, Mod);
            }
        }

        SlotInstall CreateMirrorInstall(SlotInstall install)
        {
            if (IsSymmetricDesignMode && GetMirrorSlot(install.Slot, install.Mod, out MirrorSlot mirrored))
            {
                // @warning in order to get correct XSIZE/YSIZE, we MUST use Module Template UID here
                ShipModule mModule = CreateDesignModule(install.Mod.UID, mirrored.ModuleRot, mirrored.TurretAngle, install.Mod.HangarShipUID);
                return new SlotInstall(mirrored.Slot, mModule);
            }
            return new SlotInstall();
        }

        void InstallActiveModule(SlotInstall active)
        {
            SlotInstall mirror = CreateMirrorInstall(active);
            bool canInstall  = active.UpdateCanInstallTo(ModuleGrid);
                 canInstall |= mirror.UpdateCanInstallTo(ModuleGrid);
            if (canInstall)
            {
                ModuleGrid.StartUndoableAction();
                {
                    active.TryInstallTo(ModuleGrid);
                    mirror.TryInstallTo(ModuleGrid);
                }
                
                OnDesignChanged();
                ShipModule m = active.Mod;
                SpawnActiveModule(m.UID, m.ModuleRot, m.TurretAngle, m.HangarShipUID);
            }
        }

        void ReplaceModulesWith(SlotStruct slot, ShipModule template)
        {
            if (!slot.IsModuleReplaceableWith(template))
            {
                GameAudio.NegativeClick();
                return;
            }

            ModuleGrid.StartUndoableAction();

            string replacementId = slot.Module.UID;
            foreach (SlotStruct replaceAt in ModuleGrid.SlotsList)
            {
                if (replaceAt.ModuleUID == replacementId)
                {
                    ShipModule m = CreateDesignModule(template.UID, replaceAt.Module.ModuleRot, 
                                                      replaceAt.Module.TurretAngle, ActiveModule.HangarShipUID);
                    ModuleGrid.InstallModule(replaceAt, m);
                }
            }
            
            OnDesignChanged();
        }

        void DeleteModuleAtSlot(SlotStruct slot)
        {
            if (slot.Module == null && slot.Parent == null)
                return;

            ModuleGrid.StartUndoableAction();
            ShipSaved = false;
            if (IsSymmetricDesignMode)
            {
                if (GetMirrorSlotStruct(slot, out SlotStruct mirrored))
                {
                    ModuleGrid.ClearSlots(mirrored.Root, mirrored.Root.Module);
                }
            }
            ModuleGrid.ClearSlots(slot.Root, slot.Root.Module);
            OnDesignChanged();
        }

        void StripModules()
        {
            ModuleGrid.StartUndoableAction();
            for (int i = 0; i < ModuleGrid.SlotsList.Count; i++)
            {
                SlotStruct slot = ModuleGrid.SlotsList[i];
                ShipModule module = slot.Module;
                if (module != null && module.Deflection <= 0 &&
                    !module.Is(ShipModuleType.Armor) && !module.Is(ShipModuleType.Engine) &&
                    !module.Is(ShipModuleType.Shield) && !module.Is(ShipModuleType.Command) &&
                    !module.Is(ShipModuleType.PowerPlant) && !module.Is(ShipModuleType.PowerConduit))
                {
                    ModuleGrid.ClearSlots(slot.Root, slot.Root.Module);
                    ShipSaved = false;
                }
            }

            OnDesignChanged();
        }

        void RemoveVisibleMesh()
        {
            // can be null at first load
            DesignedShip?.RemoveSceneObject();
        }

        void CreateSOFromCurrentHull()
        {
            DesignedShip.CreateSceneObject();
        }

        public void UpdateHullWorldPos()
        {
            DesignedShip.ShowSceneObjectAt(DesignedShip.Position, 0);
        }

        // @param zoomToHull whether to use the zoom-to-hull animation, not needed in some cases
        public void ChangeHull(ShipHull hullTemplate, bool zoomToHull = true)
        {
            if (hullTemplate == null) // if ShipDesignLoadScreen has no selected design
                return;

            if (HullEditMode)
                hullTemplate = hullTemplate.GetClone();

            // In Debug, show the modder HullName=`Misc/HaulerSmall` instead of VisibleName=`Small Freighter`
            string name = ParentUniverse.Debug ? hullTemplate.HullName : hullTemplate.VisibleName;
            ChangeHull(new ShipDesign(hullTemplate, name), zoomToHull: zoomToHull);
        }

        // @param zoomToHull whether to use the zoom-to-hull animation, not needed in some cases
        public void ChangeHull(IShipDesign shipDesignTemplate, bool zoomToHull = true)
        {
            if (shipDesignTemplate == null) // if ShipDesignLoadScreen has no selected design
                return;

            // Ludoal fork: remember where we were, so reopening the shipyard lands on the
            // design we left. Static on purpose — the screen is rebuilt on every open, so the
            // memory has to outlive the instance, and it dies with the process, which is
            // exactly what "for this session" means (Ludo's call, the persisted version can
            // come later).
            LastDesignThisSession = shipDesignTemplate.Name;

            RemoveVisibleMesh();
            ShipDesign cloned = shipDesignTemplate.GetClone(shipDesignTemplate.Name);
            ModuleGrid = new DesignModuleGrid(this, cloned);
            CurrentDesign = cloned;
            DesignedShip = new DesignShip(ParentUniverse.UState, cloned); // TODO: make a mini-verse in Shipyard

            InstallModulesFromDesign(cloned);
            CreateSOFromCurrentHull();
            BindListsToActiveHull();

            if (!HullEditMode)
            {
                OrdersButton.ResetButtons(CurrentDesign);
                UpdateCarrierShip();
            }

            // force modules list to reset itself, so if we change from Battleship to Fighter
            // the available modules list is adjusted correctly
            ModuleSelectComponent.SelectedIndex = -1;
            if (zoomToHull)
                ZoomCameraToEncloseHull();

            // TODO: remove DesignIssues from this page
            InfoPanel.SetActiveDesign(DesignedShip);
            IssuesPanel.SetActiveDesign(DesignedShip);
            ShipSaved = DesignedShip.Modules.Length > 0;
        }

        public void UpdateDesignedShip(bool forceUpdate)
        {
            DesignedShip.UpdateDesign(ModuleGrid.CopyModulesList(), forceUpdate);
        }

        void InstallModulesFromDesign(ShipDesign design)
        {
            Point offset = design.BaseHull.GridCenter.Sub(design.GridInfo.Center);

            foreach (DesignSlot slot in design.GetOrLoadDesignSlots())
            {
                Point pos = slot.Pos.Add(offset);
                if (!ModuleGrid.Get(pos, out SlotStruct targetSlot))
                {
                    Log.Warning($"DesignModuleGrid failed to find Slot at {pos}");
                    continue;
                }

                ShipModule m = CreateDesignModule(slot.ModuleUID, slot.ModuleRot, slot.TurretAngle, slot.HangarShipUID);
                if (ModuleGrid.ModuleFitsAtSlot(targetSlot, m, logFailure: true))
                    ModuleGrid.InstallModule(targetSlot, m);
            }

            ModuleGrid.SaveDebugGrid();

            OnDesignChanged(showRoleChangeTip: false, playSound: false);
            ResetActiveModule();
        }

        public void OnDesignChanged(bool showRoleChangeTip = true, bool playSound = true)
        {
            var oldRole = Role;
            UpdateDesignedShip(forceUpdate:false);

            if (showRoleChangeTip && Role != oldRole)
                RoleData.CreateDesignRoleToolTip(Role, DesignRoleRect, true, Input.CursorPosition);

            ShipSaved = false;
            BtnSaveAs.Text = Localizer.Token(HullEditMode || IsGoodDesign() || IsEmptyHull 
                ? GameText.SaveAs
                : GameText.SaveWIP);

            if (playSound)
                GameAudio.SmallServo();
        }

        bool IsEmptyHull => CurrentDesign.UniqueModuleUIDs.Length == 0;

        // true if this module can never fit into the module grid
        public bool CanNeverFitModuleGrid(ShipModule module)
        {
            foreach (SlotStruct slot in ModuleGrid.SlotsList)
            {
                if (ModuleGrid.ModuleFitsAtSlot(slot, module))
                    return false;
                ShipModule tiltedModule = CreateDesignModule(module.UID, ModuleOrientation.Right, 0, null);
                if (ModuleGrid.ModuleFitsAtSlot(slot, tiltedModule))
                    return false;
            }
            return true;
        }

        public override void Update(float fixedDeltaTime)
        {
            CameraPos.Z = CameraPos.Z.SmoothStep(DesiredCamHeight, 0.2f);
            UpdateViewMatrix(CameraPos);

            UpdateShipyardLightOrbit(fixedDeltaTime);

            var simTime = new FixedSimTime(fixedDeltaTime);
            DesignedShip.SubLightAccelerate(100);
            DesignedShip.Velocity = new Vector2(0, 100);
            DesignedShip.UpdateThrusters(simTime);

            ScreenManager.StartMusic("ShipyardTheme");

            base.Update(fixedDeltaTime);
        }

        void UpdateShipyardLightOrbit(float dt)
        {
            if (ShipyardKey == null) return;
            ShipyardLightOrbitAngle += ShipyardLightOrbitSpeed * dt;
            var yaw = Microsoft.Xna.Framework.Matrix.CreateRotationY(ShipyardLightOrbitAngle);
            ShipyardKey.Direction  = Microsoft.Xna.Framework.Vector3.Normalize(Microsoft.Xna.Framework.Vector3.Transform(ShipyardKeyDir0,  yaw));
            ShipyardFill.Direction = Microsoft.Xna.Framework.Vector3.Normalize(Microsoft.Xna.Framework.Vector3.Transform(ShipyardFillDir0, yaw));
            ShipyardBack.Direction = Microsoft.Xna.Framework.Vector3.Normalize(Microsoft.Xna.Framework.Vector3.Transform(ShipyardBackDir0, yaw));
        }

        public override void LoadContent()
        {
            Log.Info("ShipDesignScreen.LoadContent");

            UpdateAvailableHulls();
            CreateGUI();
            InitializeCamera();
            ScreenManager.StartMusic("ShipyardTheme");
            AssignLightRig(LightRigIdentity.Shipyard);
            SetupShipyardLighting();

            ShipDesign lastWIP = ShipDesignWIP.GetLatestWipToLoad(Player);
            // Ludoal fork: coming back from the battle sim reopens the tested design
            // (it is SAVED, not a WIP — the lastWIP path missed it, field report 45.38)
            if (!string.IsNullOrEmpty(InitialDesign)
                && ResourceManager.Ships.GetDesign(InitialDesign, out Ships.IShipDesign bsDesign))
                ChangeHull(bsDesign);
            else if (lastWIP != null)
                ChangeHull(lastWIP); // unsaved work outranks a clean reload: never lose it
            else if (LastDesignThisSession != null
                     && ResourceManager.Ships.GetDesign(LastDesignThisSession, out Ships.IShipDesign lastSeen))
                ChangeHull(lastSeen);
            else
                ChangeHull(AvailableHulls[0]);
        }

        // §4.6.B(b) follow-up: Pre-migration the shipyard scene was lit by a
        // SunBurn `.lightRig` content file (now stub'd, MIGRATION_LIMITATIONS
        // entry #7). Without it, AssignLightRig clears all lights and
        // submits none — high-spec materials like Cordrazine read flat-black
        // because there's no direction for the half-vector specular peak to
        // align against. Mirrors BasicEffect's classic 3-directional Key /
        // Fill / Back rig (matches the canonical SunBurn shipyard look:
        // strong key from upper-front, warm fill from below-rear, cool rim
        // from upper-side). All 3 lights contribute specular so the binder
        // can give the shader 3 chances to fire `pow(N·H, SpecularPower)` on
        // each visible hull pixel.
        void SetupShipyardLighting()
        {
            ShipyardKey = new DirectionalLight
            {
                Name         = "Shipyard Key",
                Direction    = new Vector3(-0.5265408f, -0.5735765f, -0.6275069f),
                DiffuseColor = new Vector3(1f, 1f, 1f),
                Intensity    = 1.75f,
                Enabled      = true,
            };
            ShipyardKeyDir0 = ShipyardKey.Direction;
            AddLight(ShipyardKey, dynamic: false);

            ShipyardFill = new DirectionalLight
            {
                Name         = "Shipyard Fill",
                Direction    = new Vector3(0.7198464f, 0.3420201f, 0.6040227f),
                DiffuseColor = new Vector3(0.85f, 0.88f, 0.92f),
                Intensity    = 0.8f,
                Enabled      = true,
            };
            ShipyardFillDir0 = ShipyardFill.Direction;
            AddLight(ShipyardFill, dynamic: false);

            ShipyardBack = new DirectionalLight
            {
                Name         = "Shipyard Back",
                Direction    = new Vector3(0.4545195f, -0.7660444f, 0.4545195f),
                DiffuseColor = new Vector3(0.3231373f, 0.3607844f, 0.3937255f),
                Intensity    = 0.5f,
                Enabled      = true,
            };
            ShipyardBackDir0 = ShipyardBack.Direction;
            AddLight(ShipyardBack, dynamic: false);
            // Neutral white ambient at moderate strength — replaces the
            // SceneInstance default `(40,60,110) × 0.75` violet that washes
            // dark hulls into purple. The shipyard is a fixed-camera 3D view
            // where the user expects the hull's *real* color to show.
            AddLight(new AmbientLight
            {
                Name         = "Shipyard Ambient",
                DiffuseColor = Color.White.ToVector3(),
                Intensity    = 0.15f,
            }, dynamic: false);
        }

        void OnReloadAfterTechChange()
        {
            UpdateAvailableHulls();
            RefreshHullSelectList();
            ModuleSelectComponent.ResetActiveCategory();
            UpdateDesignedShip(forceUpdate:true);
        }

        ButtonStyle SymmetricDesignBtnStyle  => IsSymmetricDesignMode ? ButtonStyle.Military : ButtonStyle.BigDip;
        ButtonStyle FilterModulesBtnStyle    => IsFilterOldModulesMode ? ButtonStyle.Military : ButtonStyle.BigDip;

        void CreateGUI()
        {
            RemoveAll();

            ModuleSelectComponent = Add(new ModuleSelection(this, new(5, LowRes ? 45 : 100), new(305, (ScreenHeight-100)*0.45f)));

            BlackBar = new Rectangle(0, ScreenHeight - 70, 3000, 70);
            ClassifCursor = new Vector2(ScreenWidth * .5f,ResourceManager.Texture("EmpireTopBar/empiretopbar_btn_132px").Height + 10);

            float ordersBarX = ClassifCursor.X - 15;
            var ordersBarPos = new Vector2(ordersBarX, ClassifCursor.Y + 20);
            OrdersButton = new DesignStanceButtons(this, ordersBarPos);
            Add(OrdersButton);

            if (HullEditMode || EnableDebugFeatures)
                HullEditor = Add(new HullEditorControls(this, ModuleSelectComponent.TopRight + new Vector2(50, 0)));

            UIList bottomListRight = AddList(new Vector2(ScreenWidth - 250f, ScreenHeight - 50f));
            bottomListRight.LayoutStyle = ListLayoutStyle.ResizeList;
            bottomListRight.Direction = new Vector2(-1, 0);
            bottomListRight.Padding = new Vector2(16f, 2f);
            BtnSaveAs = bottomListRight.Add(ButtonStyle.Medium, GameText.SaveAs, click: b =>
            {
                bool isGoodDesign = IsGoodDesign();
                if (!HullEditMode && !isGoodDesign)
                {
                    if (!ShipSaved)
                        SaveWIP();
                    else
                        GameAudio.NegativeClick();

                    return;
                    // ScreenManager.AddScreen(new MessageBoxScreen(this, Localizer.Token(GameText.ThisShipDesignIsInvalid)));
                    //return;*/
                }

                ScreenManager.AddScreen(new ShipDesignSaveScreen(this, DesignOrHullName, hullDesigner:HullEditMode));
            });
            BtnSaveAs.Tooltip = Localizer.Token(GameText.SaveShipDesignDesc);
            BtnSaveAs.Hotkey = InputBindings.FromString("Ctrl+S");
            bottomListRight.Add(ButtonStyle.Medium, GameText.Load, click: b =>
            {
                if (HullEditMode)
                    ScreenManager.AddScreen(new MessageBoxScreen(this, "Load Design is not available in Hull Edit Mode"));
                else
                    ScreenManager.AddScreen(new ShipDesignLoadScreen(this, UnlockAllFactionDesigns));
            });
            bottomListRight.Add(ButtonStyle.Medium, GameText.ToggleOverlay, click: b =>
            {
                ToggleOverlay = !ToggleOverlay;
            }).ClickSfx = "blip_click";
            BtnSymmetricDesign = bottomListRight.Add(ButtonStyle.Medium, Localizer.Token(GameText.SymmetricDesign), click: b =>
            {
                OnSymmetricDesignToggle();
            });
            BtnSymmetricDesign.ClickSfx = "blip_click";
            BtnSymmetricDesign.Tooltip = Localizer.Token(GameText.YouCanSwitchFromNormal);
            BtnSymmetricDesign.Hotkey  = InputBindings.FromString("M");
            BtnSymmetricDesign.Style   = SymmetricDesignBtnStyle;



            UIList bottomListLeft = AddList(new Vector2(50f, ScreenHeight - 50f));
            bottomListLeft.LayoutStyle = ListLayoutStyle.ResizeList;
            bottomListLeft.Direction = new Vector2(+1, 0);
            bottomListLeft.Padding = new Vector2(16f, 2f);

            BtnStripShip = bottomListLeft.Add(ButtonStyle.Medium, Localizer.Token(GameText.NormalDesign), click: b =>
            {
                OnStripShipToggle();
            });
            BtnStripShip.ClickSfx = "blip_click";
            BtnStripShip.Tooltip = Localizer.Token(GameText.StripsTheShipOfAny);

            BtnFilterModules = bottomListLeft.Add(ButtonStyle.Medium, Localizer.Token(GameText.OmitOldModules), click: b =>
            {
                OnFilterModuleToggle();
            });
            BtnFilterModules.ClickSfx = "blip_click";
            BtnFilterModules.Tooltip  = GameText.WhenToggledRedAnyModule;
            BtnFilterModules.Style    = FilterModulesBtnStyle;

            // Ludoal fork (battle simulator): test the current design in a 1v1 arena.
            // Left list: in 1920 the right list overlaps the build number (field report 45.37).
            var testFight = bottomListLeft.Add(ButtonStyle.Medium, "Test Fight", click: b =>
            {
                if (HullEditMode)
                    ScreenManager.AddScreen(new MessageBoxScreen(this, "Test Fight is not available in Hull Edit Mode"));
                else if (CurrentDesign == null || !ShipSaved || !IsGoodDesign())
                    ScreenManager.AddScreen(new MessageBoxScreen(this, "Save a valid design first (command module required)"));
                else
                {
                    // 45.23 field result: the shipyard must CLOSE before the arena opens —
                    // its 3D preview model haunted the arena at the origin (shared scene),
                    // and its input layer leaked through. Design is saved: exit is silent.
                    string design = CurrentDesign.Name;
                    ExitScreen();
                    ScreenManager.AddScreen(new BattleSimEnemyPicker(ParentUniverse, design)); // Ludoal fork S2: pick the enemy first
                }
            });
            testFight.ClickSfx = "blip_click";
            testFight.Tooltip = "Battle simulator: fight a copy of this design in an arena (prototype)";

            SearchBar = new Rectangle((int)ScreenCenter.X, (int)bottomListRight.Y, 210, 25);
            BottomSep = new Rectangle(BlackBar.X, BlackBar.Y, BlackBar.Width, 1);

            // Ludoal fork: the right column is laid out from the BOTTOM UP. The boxes whose
            // height is dictated by their content take it — the stats cartouche, then the
            // issues strip under it — and the browser list absorbs whatever is left (Ludo's
            // call: the list is the extensible one). The two content heights are first
            // calibrations, clamped to a fraction of the band so a short screen still leaves
            // a usable list instead of a sliver.
            // the filter row lives in the band ABOVE the browser frame, so the frame starts lower
            float filterTop   = ModuleSelectComponent.LocalPos.Y;
            float colTop      = filterTop + 52f;
            float colBottom   = BlackBar.Y;
            float colBand     = colBottom - colTop;
            // the cartouche grew downward by two lines, taken from the issues strip (Ludo's
            // call): the strip only carries the completion line and the issues button, and
            // the fullest designs were spilling their last block out of the frame
            float issuesH     = Math.Min(58f, colBand * 0.10f);   // completion line + issues button
            float cartoucheH  = Math.Min(374f, colBand * 0.56f);  // fullest case: a station, two columns
            float cartoucheY  = colBottom - issuesH - cartoucheH;

            Vector2 hullSelSize = new(SelectSize(260, 280, 320), Math.Max(160f, cartoucheY - 10 - colTop));
            var hullSelectPos = new LocalPos(ScreenWidth - hullSelSize.X, colTop);
            // Ludoal fork: the load popup's filters come WITH its list — dropping them would be
            // a regression, since one of them ("my designs only") is a persisted preference the
            // player may already have set. They sit above the frame rather than inside it:
            // pushing content into a SubmenuScrollList means rearranging its internal layout.
            float filterX = ScreenWidth - hullSelSize.X;
            BrowserFilter = Add(new UITextEntry(filterX + 4, filterTop, hullSelSize.X - 8,
                                                Fonts.Arial12Bold, DefaultBrowserFilter));
            BrowserFilter.AutoCaptureOnKeys = true;
            BrowserFilter.AutoCaptureLoseFocusTime = 0.5f;
            BrowserFilter.OnTextChanged = (text) =>
            {
                BrowserFilterText = (text == DefaultBrowserFilter) ? null : text?.ToLower();
                RefreshHullSelectList();
            };

            // side by side on one row: stacked, the second one slipped under the frame's title tab
            Checkbox(new Vector2(filterX + 6, filterTop + 26),
                     () => !Player.Universe.P.ShowAllDesigns,
                     (b) => { Player.Universe.P.ShowAllDesigns = !b; RefreshHullSelectList(); },
                     "My designs only", "Show only the designs you created");

            Checkbox(new Vector2(filterX + 132, filterTop + 26),
                     () => ShowLockedDesigns,
                     (b) => { ShowLockedDesigns = b; RefreshHullSelectList(); },
                     "Show locked", GameText.ShowEmpireLockedDesignsTip);

            var hullSelectSub = Add(new SubmenuScrollList<ShipYardBrowserItem>(hullSelectPos, hullSelSize, "Hulls & Designs"));
            // rounded black background
            hullSelectSub.SetBackground(Colors.TransparentBlackFill);

            HullSelectList = hullSelectSub.List;
            // single click selects (and shift-click pins for comparison), double click loads:
            // the load is the expensive gesture (mesh + modules + stats), so it stays deliberate
            HullSelectList.OnClick = OnBrowserItemClicked;
            HullSelectList.OnDoubleClick = OnBrowserItemDoubleClicked;
            HullSelectList.EnableItemHighlight = true;

            // hover preview: the same overlay the load popup used, so a design can be
            // inspected without paying for a load. Hull rows carry no design, and the
            // overlay hides itself when handed a null one.
            ShipInfoOverlay = Add(new ShipInfoOverlayComponent(this, ParentUniverse.UState));
            HullSelectList.OnHovered = item => ShipInfoOverlay.ShowToLeftOf(item?.Pos ?? Vector2.Zero, item?.Design);
            RefreshHullSelectList();
            hullSelectSub.PerformLayout();

            var dropdownRect = new Rectangle((int)(ScreenWidth * 0.375f), (int)ClassifCursor.Y + 25, 125, 18);

            CategoryList = new CategoryDropDown(dropdownRect);
            foreach (ShipCategory item in Enum.GetValues(typeof(ShipCategory)).Cast<ShipCategory>())
                CategoryList.AddOption(item.ToString(), item);

            var hangarRect = new Rectangle((int)(ScreenWidth * 0.65f), (int)ClassifCursor.Y + 25, 150, 18);
            HangarOptionsList = new HangarDesignationDropDown(hangarRect);
            foreach (HangarOptions item in Enum.GetValues(typeof(HangarOptions)).Cast<HangarOptions>())
                HangarOptionsList.AddOption(item.ToString(), item);

            var carrierOnlyPos  = new Vector2(dropdownRect.X - 200, dropdownRect.Y);
            CarrierOnlyCheckBox = Checkbox(carrierOnlyPos,
                () => CurrentDesign?.IsCarrierOnly == true,
                (b) => { if (CurrentDesign != null) CurrentDesign.IsCarrierOnly = b; }, "Carrier Only", GameText.WhenMarkedThisShipCan);

            ArcsButton = new GenericButton(new Vector2(HullSelectList.X - 32, 97f), "Arcs", Fonts.Pirulen20, Fonts.Pirulen16)
            {
                ToggleOnColor = Color.DarkOrange,
                ButtonStyle = GenericButton.Style.Shadow,
            };

            // DESIGN ISSUES sits UNDER the cartouche (Ludo's call) instead of in a narrow 200px
            // column to its left, so its text gets the full width. Both boxes use the bottom-up
            // geometry computed above, which is why the list stretches and these do not.
            // The cartouche's right edge follows the browser frame above it — one margin for the
            // whole column — and it grows LEFTWARD to a width that actually fits two columns of
            // labels: at the browser's own width a half-column left ~54px for titles like
            // "Total Module Slots", so the value landed in the middle of its own label.
            // 400 = 2 × (longest title ~105 + value 60) + gutter + frame margins.
            float cartoucheW = Math.Max(hullSelectSub.Width, 400f);
            var infoRect = RectF.FromPoints(hullSelectSub.Right - cartoucheW, hullSelectSub.Right,
                                            cartoucheY, cartoucheY + cartoucheH);
            // Ludoal fork: the stats live in a titled cartouche now, same frame the module
            // panel uses ("Active Module"), so the two read as the same kind of object.
            // The frame is added first so it draws behind the values.
            var infoSub = Add(new Submenu(infoRect, "Active Design"));
            infoSub.SetBackground(Colors.TransparentBlackFill);

            var infoInner = RectF.FromPoints(infoRect.X + 12, infoRect.Right - 12,
                                             infoRect.Y + 32, infoRect.Bottom - 8);
            InfoPanel = Add(new ShipDesignInfoPanel(this, infoInner));

            // Ludoal fork: the COMPARED design sits beside the active one, exactly as the
            // Compared Module panel sits beside the Active Module — and like it, it is wider by
            // a delta lane. Hidden until a design is shift-clicked in the browser.
            var comparedRect = RectF.FromPoints(infoRect.X - (cartoucheW + 100f) - 10f, infoRect.X - 10f,
                                                infoRect.Y, infoRect.Bottom);
            ComparedSub = Add(new Submenu(comparedRect, "Compared Design"));
            ComparedSub.SetBackground(Colors.TransparentBlackFill);

            var comparedInner = RectF.FromPoints(comparedRect.X + 12, comparedRect.Right - 12,
                                                 comparedRect.Y + 32, comparedRect.Bottom - 8);
            ComparedPanel = Add(new ShipDesignInfoPanel(this, comparedInner));
            ComparedPanel.CompareAgainst = InfoPanel;
            ComparedSub.Visible = ComparedPanel.Visible = false;

            var issuesRect = RectF.FromPoints(infoRect.X, infoRect.Right,
                                              infoRect.Bottom + 6, colBottom);
            IssuesPanel = Add(new ShipDesignIssuesPanel(this, issuesRect));

            if (EnableDebugFeatures)
            {
                var debugUnlocks = Add(new ResearchDebugUnlocks(ParentUniverse, OnReloadAfterTechChange));
                debugUnlocks.SetAbsPos(10, 45);
            }

            CloseButton(ScreenWidth - 27, 75);
        }

        // Ludoal fork: the visibility policy ported verbatim from the load popup, so the
        // merged list shows exactly what the popup showed. ShowAllDesigns is a PERSISTED
        // player preference — ignoring it would silently pour every faction's designs on
        // someone who had chosen to see only their own. Locked designs stay hidden, which
        // is the popup's default state; its toggle comes with the filter bar.
        bool CanShowDesign(IShipDesign design)
        {
            // the browser's own text filter, matching name or hull like the popup's did
            if (BrowserFilterText.NotEmpty()
                && !design.Name.ToLower().Contains(BrowserFilterText)
                && !design.Hull.ToLower().Contains(BrowserFilterText))
                return false;

            if (!Player.Universe.P.ShowAllDesigns && !design.IsPlayerDesign)
                return false;

            if (UnlockAllFactionDesigns) // developer universe: everything is visible
                return !design.Deleted;

            return !design.Deleted
                && !design.IsShipyard
                // "show locked" is simplified against the popup: it checked an eligible-empires
                // set built from the major empires; here it is simply "a hull we have unlocked"
                && (Player.WeCanBuildThis(design) || (ShowLockedDesigns && Player.IsHullUnlocked(design.Hull)))
                && (!design.IsSubspaceProjector || EnableDebugFeatures)
                && (!design.IsDysonSwarmController || EnableDebugFeatures)
                && (!design.IsUnitTestShip || EnableDebugFeatures)
                && ResourceManager.ShipRoles.TryGetValue(design.Role, out ShipRole sr) && !sr.Protected;
        }

        void UpdateAvailableHulls()
        {
            AvailableHulls.Clear();

            if (UnlockAllFactionDesigns)
            {
                foreach (ShipHull hull in ResourceManager.Hulls)
                {
                    hull.ReloadIfNeeded();
                    AvailableHulls.Add(hull);
                }
            }
            else
            {
                string[] hulls = Player.GetUnlockedHulls();
                foreach (string hull in hulls)
                {
                    if (ResourceManager.Hull(hull, out ShipHull hullData))
                    {
                        if (!hullData.IsShipyard || ParentUniverse.Debug)
                        {
                            hullData.ReloadIfNeeded();
                            AvailableHulls.Add(hullData);
                        }
                    }
                }
            }
        }

        void InitializeCamera()
        {
            // set shipyard's fov much lower to reduce parallax
            SetPerspectiveProjection(fovYdegrees: 20, maxDistance: 30000);
            UpdateViewMatrix(CameraPos);
        }

        // Ludoal fork: one list grouped BY HULL, replacing the old role-grouped hull list
        // and the separate "load design" popup. Each hull group opens with its own bare
        // hull, so starting from an empty carcass is row #1, then the designs built on it.
        // The role shown on a design row is IShipDesign.Role (what the fitted modules make
        // it), which is a different field from the hull's own role — they never disagree.
        void RefreshHullSelectList()
        {
            HullSelectList.Reset();

            // designs indexed by the hull they are built on, so each group is one lookup
            var designsByHull = new Map<string, Array<IShipDesign>>();
            foreach (Ship ship in ResourceManager.Ships.Ships)
            {
                IShipDesign design = ship.ShipData;
                if (!CanShowDesign(design))
                    continue;

                if (!designsByHull.TryGetValue(design.Hull, out Array<IShipDesign> onHull))
                    designsByHull[design.Hull] = onHull = new Array<IShipDesign>();
                onHull.Add(design);
            }

            // Groups are the hull CLASSES of the tech tree (Fighter, Corvette, Frigate,
            // Freighter...), as the old hull list did. Inside a class, each hull opens with its
            // own bare row — that row carries the hull's name, e.g. "Fang Fighter" — and its
            // designs follow. Two levels is all the scroll list can do, which is why the class
            // is the heading and the hull is a row rather than a nested group.
            var classes = new Array<string>();
            foreach (ShipHull hull in AvailableHulls)
            {
                string cls = Localizer.GetRole(hull.Role, Player);
                if (!classes.Contains(cls))
                    classes.Add(cls);
            }
            classes.Sort();

            foreach (string cls in classes)
            {
                var group = new ShipYardBrowserItem(Player, null, cls);
                HullSelectList.AddItem(group);

                foreach (ShipHull hull in AvailableHulls.Sorted(h => h.VisibleName))
                {
                    if (Localizer.GetRole(hull.Role, Player) != cls)
                        continue;

                    AddHullAndItsDesigns(group, hull, designsByHull);
                }
            }
        }

        void AddHullAndItsDesigns(ShipYardBrowserItem group, ShipHull hull,
                                  Map<string, Array<IShipDesign>> designsByHull)
        {
            // the bare hull first: starting from an empty carcass is one row, not a mode
            group.AddSubItem(new ShipYardBrowserItem(Player, hull));

            if (designsByHull.TryGetValue(hull.HullName, out Array<IShipDesign> designs))
            {

                // same order as the old load popup: our own designs first, then the
                // strongest, then alphabetical
                foreach (IShipDesign design in designs.OrderBy(d => !d.IsPlayerDesign)
                                                      .ThenByDescending(d => d.BaseStrength)
                                                      .ThenBy(d => d.Name))
                {
                    group.AddSubItem(new ShipYardBrowserItem(Player, design, isWIP: false));
                }
            }
        }
    }
}
