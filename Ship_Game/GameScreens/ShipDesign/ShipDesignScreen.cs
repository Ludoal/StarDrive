using System;
using System.IO;
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
        // Ludoal fork: the two rows under the top bar - identity, then options. Held as fields so
        // pieces built later in LoadContent land on the same lines.
        int IdentityRowY;
        int OptionsRowY;

        public DesignShip DesignedShip { get; private set; }
        public ShipDesign CurrentDesign; // only Null during first time init, otherwise never Null, even in Hull Editor
        public ShipHull CurrentHull => CurrentDesign?.BaseHull; // can be null during first time init
        public DesignModuleGrid ModuleGrid;

        public string DesignOrHullName => CurrentDesign.Name;

        public EmpireUIOverlay EmpireUI;
        public string InitialDesign; // Ludoal fork: design to open with (battle sim return path)
        static string LastDesignThisSession; // Ludoal fork: reopen where we left off, this run only

        Vector3 CameraPos = new Vector3(0f, 0f, 1300f);
        // The pan target - HandleCameraMovement writes here, Update glides CameraPos
        // toward it (the XY twin of DesiredCamHeight), so the pan feels like Fleets' instead of raw
        Vector2 DesiredCamXY;
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
        UIButton BtnStripShip;       // Removes all modules but armor, shields and command modules
        UIButton BtnToggleOverlay;
        Submenu DesignTabs;   // Ludoal fork: the Design group's tab row, this screen being one tab
        // Ludoal fork: this page's real frame is its tab row's rect - the band excludes
        // exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => DesignTabs?.Rect ?? base.PageFrame;
        UIButton BtnArcs;            // weapon fire arcs overlay
        Rectangle SearchBar;

        ShipDesignInfoPanel InfoPanel;
        Submenu InfoSub;
        // Ludoal fork: the design-side twin of the module panel's obsolete button, same icon and
        // corner. Marking a design obsolete greys it in the browser and a filter hides them.
        UIButton ObsoleteDesign;
        // Ludoal fork (spec v4): the pinned design has no frame of its own — it only feeds the
        // delta lane of InfoPanel. This frame shows the design under the cursor instead.
        ShipDesignInfoPanel HoverPanel;
        Submenu HoverSub;
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
        UITextEntry ModuleFilter;          // Ludoal fork: same field, over the module list
        public string ModuleSearchText { get; private set; }
        // Ludoal fork: the browser's view toggles outlive the screen, which is rebuilt on every
        // open, and last as long as the game runs. They stay out of the config: ways of looking
        // at the list, not preferences.
        static bool ShowLockedDesigns;
        static bool HideObsoleteDesigns; // Ludoal fork: the browser's obsolete filter
        // Ludoal fork: ON = the two cartouches coexist, hover to the left of the active one.
        // OFF = one cartouche at that place: the active design, replaced by the hovered one for
        // as long as the cursor rests on a row. Stays out of the config; survives the session.
        static bool PinActiveDesign = true;
        UICheckBox PinActiveCheck;

        // Ludoal fork: the compact Active Design cartouche - the browser list's width, carrying
        // the flying overlay's stat set. Both cartouches swap their row sets with it;
        // ResizeCartouches applies a flip.
        static bool CompactActiveDesign;
        UICheckBox CompactActiveCheck;
        bool AppliedCompact; // the state the cartouches are actually built for

        // Ludoal fork: Full Screen drops the resolution-charter cap for the Shipyard only, so the
        // frame spans the whole display (still anchored on the rail) instead of the 1600x1080
        // footprint. Session-persistent; flipping it re-runs LoadContent so every panel and the
        // 3D projection rebuild on the new frame.
        public static bool FullScreenDesign; // public: Design Issues centres itself on the same frame
        // Ludoal fork: the hard pause is a Full Screen perk only - windowed, the Shipyard rides
        // the page-pause option like any other page
        protected override bool PageAlwaysPauses => FullScreenDesign;
        // Every dialog the Shipyard summons centres on ITS frame, not the display
        public Vector2 FrameCentre
        {
            get
            {
                Rectangle f = GameScreens.ScreenGroups.GroupFrame(ScreenWidth, ScreenHeight, FullScreenDesign);
                return new Vector2(f.CenterX(), f.CenterY());
            }
        }
        UICheckBox FullScreenCheck;
        // Ludoal fork: sweeping from one browser row to the next crosses a gap where nothing is
        // hovered. With Pin Active unchecked that gap lets the Active cartouche flash back into
        // its seat between every pair of rows, so a hover that ENDS is held for a moment before
        // the frame actually goes. Negative = not counting. Only the hover path sets it: loading
        // a design must still clear the frame at once, with no ghost.
        const float HoverLinger = 0.12f;
        float HoverLeftAt = -1f;
        // which groups were open before the last rebuild, so a filter change does not refold
        // the whole browser. Both group builders read it.
        readonly Array<string> ExpandedGroups = new();
        // which of the browser's designs are WIPs, so a row knows which affordances to carry
        readonly Array<string> WipDesigns = new();
        // Ludoal fork: how the browser groups its rows. By hull is the build view (a carcass and
        // what we already put on it); by role is the use view ("my carriers", wherever they are
        // built). Same list, same filters, one key change - a grouping mode, not a second list
        // with its own scroll, filter and selection to keep in step.
        static bool GroupByRole; // Ludoal fork: browser grouping tab, kept for the session
        const string DefaultBrowserFilter = "filter by name or hull...";
        const string DefaultModuleFilter = "filter modules...";
        // Ludoal fork: this screen uses the Hover cartouche instead of the flying hover overlay.
        // The overlay component itself lives on - the load/save popups and other screens still use it.

        public ShipModule HighlightedModule;
        SlotStruct ProjectedSlot;
        string ScreenToLaunch;

        public ShipModule ActiveModule;
        public ShipModule CompareModule; // Ludoal fork: pinned comparison module (Shift-click in the module list)
        public ShipModule HoveredListModule; // Ludoal fork (spec v4): module under the cursor in the list, feeds the Hover frame
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

        // "Edit this ship" arrives FROM a colony: a user close of the Shipyard goes back to it.
        // Tab switches and group jumps suppress the return - they land elsewhere, and the
        // reopened colony would bury their target.
        readonly Planet ReturnToColony;
        bool ReturnSuppressed;

        public ShipDesignScreen(UniverseScreen universe, EmpireUIOverlay empireUi, Planet returnToColony = null)
            : base(universe, toPause: universe)
        {
            ReturnToColony = returnToColony;
            ParentUniverse = universe;
            Name = "ShipDesignScreen";
            EmpireUI = empireUi;
            IsPopup = true; // Ludoal fork: the paused universe shows behind, dimmed - like the table screens
            // Ludoal fork: no fade in. A fade leaves this screen translucent while it builds, so
            // whatever sat under it in the stack shows through - e.g. opening the Shipyard with
            // Fleets already open would draw Fleets' cartouche for a moment. Nothing here reads
            // TransitionPosition, so a fade would only delay the screen, not animate it.
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
            // Ludoal fork: null means CANCEL, and it has to short-circuit here - CreateModuleListItem(null)
            // dereferences its argument.
            if (template == null)
            {
                CompareModule = null;
                return;
            }

            // Comparing a module with itself says nothing: every delta is zero. Refuse the pin
            // rather than show a frame full of blanks.
            if (ActiveModule != null && ActiveModule.UID == template.UID)
                return;

            if (CompareModule != null && CompareModule.UID == template.UID)
                CompareModule = null;
            else
                CompareModule = CreateModuleListItem(template);
        }

        // Ludoal fork: Shift-click in the browser pins a design, and the same design again unpins
        // it. The pinned design gets no frame — it only feeds the delta lane of the Active
        // cartouche, exactly as a pinned module does. Pinning the design already on the
        // workbench is refused: every delta would be zero.
        public void SetComparedDesign(IShipDesign design)
        {
            if (design != null && DesignedShip?.Name == design.Name)
                return; // comparing the working design with itself says nothing

            if (ComparedDesign != null && design != null && ComparedDesign.Name == design.Name)
                design = null; // re-pinning the same design unpins it

            ComparedDesign = design;

            if (design == null)
            {
                InfoPanel?.SetComparedDesign(null, null);
                return;
            }

            try
            {
                var ship = new DesignShip(ParentUniverse.UState, design as ShipDesign);
                ship.RecalculatePower();
                ship.ShipStatusChange();
                InfoPanel.SetComparedDesign(ship, design.Name);
            }
            catch (Exception e)
            {
                Log.Error(e, $"Compared design failed: {design.Name}");
                ComparedDesign = null;
                InfoPanel?.SetComparedDesign(null, null);
            }
        }

        // Ludoal fork: the design under the cursor in the browser, shown in the Hover cartouche.
        // Passing null hides it. This is the actual hide, and the one place that clears the linger.
        void HideHoveredDesign()
        {
            HoverLeftAt = -1f;
            if (HoverPanel == null)
                return;
            HoverPanel.SetActiveDesign(null);
            HoverSub.Visible = HoverPanel.Visible = false;
        }

        public void SetHoveredDesign(IShipDesign design)
        {
            if (HoverPanel == null)
                return;

            if (design == null || DesignedShip?.Name == design.Name)
            {
                // start the linger rather than hiding now; ResizeCartouches finishes the job.
                // If the frame is already down there is nothing to hold, so do not arm it.
                if (HoverSub.Visible && HoverLeftAt < 0f)
                    HoverLeftAt = 0f;
                return;
            }

            HoverLeftAt = -1f; // landed on a row: cancel any linger still counting down

            try
            {
                var ship = new DesignShip(ParentUniverse.UState, design as ShipDesign);
                ship.RecalculatePower();
                ship.ShipStatusChange();
                HoverPanel.SetActiveDesign(ship);
                HoverSub.Visible = HoverPanel.Visible = true;
            }
            catch (Exception e)
            {
                Log.Error(e, $"Hovered design failed: {design.Name}");
                HoverSub.Visible = HoverPanel.Visible = false;
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

            // Ludoal fork: picking up the very module that is pinned drops the pin, the same way
            // loading a design does on the browser side. Any OTHER module keeps the pin: swapping
            // the brush to weigh it against the pinned one is the whole point.
            if (CompareModule != null && CompareModule.UID == moduleUID)
                CompareModule = null;
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
            if (hullTemplate == null) // no design selected in the browser
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
            if (shipDesignTemplate == null) // no design selected in the browser
                return;

            // Ludoal fork: remember where we were, so reopening the shipyard lands on the design
            // we left. Static on purpose — the screen is rebuilt on every open, so the memory has
            // to outlive the instance; it dies with the process ("for this session").
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

            // Ludoal fork (spec v4): loading a design drops the pin — a comparison against a
            // ship that is no longer on the workbench is a ghost. SetActiveDesign cleared the
            // rows, so the shadow has to go with them.
            ComparedDesign = null;
            InfoPanel.SetComparedDesign(null, null);
            HideHoveredDesign(); // a load clears it outright: no linger, no ghost
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

        // Ludoal fork: the design cartouche is variable-geometry like the module one — it only
        // pays for the delta lanes while a design is actually pinned. It is resized here rather
        // than once at construction, so it can shrink back when the pin is dropped.
        //
        // ⚠ Frame and inner panel move TOGETHER, from one arithmetic: they are two blocks that
        // must agree, so they cannot each compute their own edge. And the frame's right edge is
        // the anchor — it stays on the browser's, the frame grows leftward — which is also what
        // keeps the Hover cartouche's own right edge glued to the Active frame's left.
        void ResizeCartouches(float deltaTime)
        {
            // Ludoal fork: unpinned, the active cartouche steps aside for the hovered one rather
            // than sharing the row with it. Decided here, every frame, from the hover frame's
            // own visibility.
            // the deferred hide from SetHoveredDesign, see HoverLinger
            if (HoverLeftAt >= 0f)
            {
                HoverLeftAt += deltaTime;
                if (HoverLeftAt >= HoverLinger)
                    HideHoveredDesign();
            }

            bool hoverTakesThePlace = !PinActiveDesign && HoverSub.Visible;
            InfoSub.Visible = InfoPanel.Visible = !hoverTakesThePlace;
            IssuesPanel.Visible = !hoverTakesThePlace;

            // ⚠ and its POSITION is settled here too, above the early return below: the hover
            // frame moves when the toggle flips, which has nothing to do with the delta lanes.
            // Left under that return it only runs on a pin or an unpin, so unpinned the Active
            // frame vanishes while the hover frame stays put on the left, over the browser.
            PlaceHoverCartouche(RectF.FromPoints(InfoSub.X, InfoSub.X + InfoSub.Width,
                                                 InfoSub.Y, InfoSub.Y + InfoSub.Height));

            bool wantDeltas = ComparedDesign != null;
            bool compactChanged = AppliedCompact != CompactActiveDesign;
            if (InfoPanel.HasDeltaLanes == wantDeltas && !compactChanged)
                return;

            InfoPanel.HasDeltaLanes = wantDeltas;

            if (compactChanged)
            {
                // the row sets swap wholesale - the shadow comparator follows inside
                // RebuildRows - and the hover frame changes width with them
                AppliedCompact = CompactActiveDesign;
                InfoPanel.Compact = CompactActiveDesign;
                InfoPanel.RebuildRows();
                HoverPanel.Compact = CompactActiveDesign;
                HoverPanel.RebuildRows();
                float hw = CompactActiveDesign ? ShipDesignInfoPanel.CompactFrameWidthFor(withPlan: true)
                         : ShipDesignInfoPanel.FrameWidthFor(withDeltas: false, withPlan: true);
                HoverSub.SetAbsSize(hw, HoverSub.Height);
                HoverSub.RequiresLayout = true;
                HoverPanel.SetAbsSize(hw - ShipDesignInfoPanel.Inset * 2f, HoverPanel.Height);
                HoverPanel.RequiresLayout = true;
            }

            // compact: the browser list's own width, deltas fitting inside it - the frame
            // never resizes on a pin, exactly like the module panel
            float w = CompactActiveDesign ? ModuleSelection.ListWidth
                    : ShipDesignInfoPanel.FrameWidthFor(wantDeltas, withPlan: false);
            // read off the element itself rather than through Rect: Submenu.Rect (RectF) shadows
            // UIElementV2.Rect (integer Rectangle), so going through it risks silently rounding
            // the anchor edge depending on which member the call site resolves to
            float right = InfoSub.X + InfoSub.Width;   // anchored edge
            var frame = RectF.FromPoints(right - w, right, InfoSub.Y, InfoSub.Y + InfoSub.Height);
            InfoSub.SetAbsPos(frame.X, frame.Y);
            InfoSub.SetAbsSize(frame.W, frame.H);
            InfoSub.RequiresLayout = true;      // SetAbsSize alone does not arm it

            InfoPanel.SetAbsPos(frame.X + ShipDesignInfoPanel.Inset, frame.Y + 26);
            InfoPanel.SetAbsSize(frame.W - ShipDesignInfoPanel.Inset * 2f, frame.H - 34);
            InfoPanel.RequiresLayout = true;   // SetAbsSize does not arm it, same as the frame

            // Design Completion / DESIGN ISSUES hang off the cartouche's left edge too — placed
            // once at construction, so they must move here when the frame grows leftward.
            IssuesPanel.SetAbsPos(frame.X, IssuesPanel.Y);

            // the obsolete button hangs off the frame's RIGHT edge, so it travels with it
            ObsoleteDesign.Pos.X = (int)(frame.X + frame.W - ObsoleteDesign.Width - 10);

            // (the Pin Active checkbox is anchored to the BROWSER's left edge, not to this
            // frame, so unlike the two elements above it does not travel when the frame grows)

            PlaceHoverCartouche(frame);
        }

        // One arithmetic for the hover cartouche's place, called from both ends of
        // ResizeCartouches: pinned it sits to the left of the Active frame, unpinned it takes
        // that frame's own place, same right edge. Two call sites that were meant to agree is
        // exactly the shape that drifts.
        void PlaceHoverCartouche(in RectF frame)
        {
            // Width, not Rect.W: Submenu.Rect is a RectF that shadows UIElementV2's integer
            // Rectangle Rect, and which one a call site resolves to is not worth relying on
            float hw = HoverSub.Width;
            float hoverRight = PinActiveDesign ? frame.X - 10f : frame.X + frame.W;
            var hover = RectF.FromPoints(hoverRight - hw, hoverRight, frame.Y, frame.Bottom);
            if (HoverSub.X.AlmostEqual(hover.X))
                return; // nothing moved: do not arm a relayout on every single frame

            HoverSub.SetAbsPos(hover.X, hover.Y);
            HoverSub.RequiresLayout = true;
            HoverPanel.SetAbsPos(hover.X + ShipDesignInfoPanel.Inset, hover.Y + 26); // same top as Active
            HoverPanel.RequiresLayout = true;
        }

        public override void Update(float fixedDeltaTime)
        {
            CameraPos.Z = CameraPos.Z.SmoothStep(DesiredCamHeight, 0.2f);
            // XY glides toward the pan target like Z toward the zoom target
            CameraPos.X = CameraPos.X.SmoothStep(DesiredCamXY.X, 0.2f);
            CameraPos.Y = CameraPos.Y.SmoothStep(DesiredCamXY.Y, 0.2f);
            ResizeCartouches(fixedDeltaTime);
            UpdateViewMatrix(CameraPos);

            UpdateShipyardLightOrbit(fixedDeltaTime);

            var simTime = new FixedSimTime(fixedDeltaTime);
            DesignedShip.SubLightAccelerate(100);
            DesignedShip.Velocity = new Vector2(0, 100);
            DesignedShip.UpdateThrusters(simTime);

            // Do NOT StartMusic here. This runs EVERY FRAME, and once the mixer force-stops the
            // track (cue limit) or PlayMusic returns DoNotPlay, StartMusic's guard
            // (CurrentMusic != name || Music.IsStopped) is true every frame - it re-fires forever,
            // stacking fade-out instances until the mixer saturates: music dies, only crackle left.
            // LoadContent already starts the theme once; that is enough.

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
            // Ludoal fork: the theme is a Full Screen perk - windowed, the universe's ambient
            // keeps playing under the workbench
            if (FullScreenDesign)
                ScreenManager.StartMusic("ShipyardTheme");
            AssignLightRig(LightRigIdentity.Shipyard);
            SetupShipyardLighting();

            ShipDesign lastWIP = ShipDesignWIP.GetLatestWipToLoad(Player);
            // Ludoal fork: coming back from the battle sim reopens the tested design
            // (it is SAVED, not a WIP — the lastWIP path misses it)
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

        // the two view toggles that carry a state read red while ON, blue while OFF
        ButtonStyle SymmetricDesignBtnStyle  => IsSymmetricDesignMode ? ButtonStyle.WideHostile : ButtonStyle.WideActive;
        ButtonStyle ArcsBtnStyle             => ShowAllArcs ? ButtonStyle.WideHostile : ButtonStyle.WideActive;

        void CreateGUI()
        {
            RemoveAll();

            // Ludoal fork: the module list runs DOWN TO the Active frame instead of stopping at
            // an arbitrary fraction of the screen. ModuleSelection owns that arithmetic — one
            // place decides where the list ends and the frame begins, so the two cannot drift apart.
            // Ludoal fork: the Shipyard tab of the Design group. Unlike the table screens, the
            // frame is a surround rather than a container - the 3D workbench, the module grid and
            // the two columns keep their own layout inside it.
            DesignTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.DesignTabTitles, 1,
                                                    OnDesignTabChanged, out Rectangle _, FullScreenDesign);
            // ⚠ CreateGUI runs BEFORE the hull restore - DesignedShip is still null here (the 358
            // crash; the line numbers lied about the order, the stack trace didn't). Nothing at this
            // point may touch the ship.

            // The tab frame is the container: every column bound is measured from it, with the
            // same 5px margin on all four sides. ModuleSelection carries the band so the two
            // columns and the four frames cannot each pick their own edge.
            const float BandPad = 5f;
            RectF tabClient = DesignTabs.ClientArea;
            ModuleSelection.BandTop    = tabClient.Y;
            ModuleSelection.BandBottom = tabClient.Bottom;
            ModuleSelection.BandLeft   = tabClient.X + BandPad;
            ModuleSelection.BandRight  = tabClient.Right - BandPad;

            // the centre rows start under the tab row rather than under the top bar
            ClassifCursor = new Vector2(tabClient.CenterX, tabClient.Y + 4);

            // The two centre rows: the design's IDENTITY (role, name, Save, Test Fight), then
            // everything that CONFIGURES it (stance, repair, hangar, carrier-only). Posed here,
            // before anything reads them, because the lists and their filter rows derive from
            // these two numbers - written once so they cannot drift apart.
            //
            // 52 rather than a tighter gap: the two dropdowns carry their caption ABOVE
            // themselves (Arial12Bold + 2), so the row needs a line of clearance.
            IdentityRowY = (int)ClassifCursor.Y + 6;
            OptionsRowY  = IdentityRowY + 52;

            // The columns are SIDE content: they start just under the close cross, not under the
            // centre rows, which sit between them. Each carries two lines above its list - filter
            // toggles, then search field.
            const float headerLine = 19f;
            float modListTop = ScreenGroups.GroupContentTop(DesignTabs.ClientArea) + 2 * headerLine;
            ModuleSelectComponent = Add(new ModuleSelection(this, new(ModuleSelection.BandLeft, modListTop),
                                        new(ModuleSelection.ListWidth, ModuleSelection.ListHeightFor(ScreenHeight, modListTop))));

            // Both centre rows are centred on the same arithmetic: a total width built from the
            // parts, halved off ScreenCenter. The stance block is 7 icons wide and two rows tall
            // (StanceButtons lays them 25px apart, then wraps back 3 columns), so it measures
            // 175x50, and its topLeft is what the rest of the options row aligns its top to.
            const int stanceCols = 7, stanceIcon = 25;
            const int stanceW = stanceCols * stanceIcon;   // 175

            if (HullEditMode || EnableDebugFeatures)
                HullEditor = Add(new HullEditorControls(this, ModuleSelectComponent.TopRight + new Vector2(50, 0)));

            // Top line: [ role ] [ name ] [ Save ] [ Test Fight ], centred as one block.
            const int idH = 26, idGap = 8;
            const int roleW = 110, nameW = 210, idBtnW = 120;
            const int idRowW = roleW + nameW + 2 * idBtnW + 3 * idGap;
            int idY = IdentityRowY;
            int idLeft = (int)ClassifCursor.X - idRowW / 2;

            DesignRoleRect = new Rectangle(idLeft, idY, roleW, idH);
            SearchBar      = new Rectangle(DesignRoleRect.Right + idGap, idY, nameW, idH);

            var topRow = AddList(new Vector2(SearchBar.Right + idGap, idY));
            topRow.LayoutStyle = ListLayoutStyle.ResizeList;
            topRow.Direction = new Vector2(+1, 0);
            topRow.Padding = new Vector2(idGap, 2f);

            BtnSaveAs = topRow.Add(ButtonStyle.WideHostile, GameText.SaveAs, click: b =>
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

                ScreenManager.AddScreen(new ShipDesignSaveScreen(this, DesignOrHullName, hullDesigner:HullEditMode) { CenterOn = FrameCentre });
            });
            BtnSaveAs.Tooltip = Localizer.Token(GameText.SaveShipDesignDesc);
            BtnSaveAs.Hotkey = InputBindings.FromString("Ctrl+S");
            // Ludoal fork: Load is gone - the browser on the right lists every design and loads on
            // double-click, so a modal picker on top of it was one door too many.
            var testFight = topRow.Add(ButtonStyle.Wide, "Test Fight", click: b =>
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
                    ScreenManager.AddScreen(new BattleSimEnemyPicker(ParentUniverse, design)); // Ludoal fork: pick the enemy first
                }
            });
            testFight.ClickSfx = "blip_click";
            testFight.Tooltip = "Battle simulator: fight a copy of this design in an arena (prototype)";

            BtnSaveAs.SetAbsSize(idBtnW, idH);
            testFight.SetAbsSize(idBtnW, idH);
            topRow.PerformLayout();

            // Each view toggle sits under the list it acts on: module placement on the left
            // column, the design's own overlays on the right. ModuleSelection owns the band they
            // live in (ToggleRowY / ToggleRowBand), so both lists shorten by the same amount and
            // the four frames keep their shared foot line.
            const int footGap = 8;
            float footY = ModuleSelection.ToggleRowY(ScreenHeight);
            float colW = ModuleSelection.ListWidth;
            // Measured off UIList rather than guessed: it starts at Pos + Padding and then
            // advances by (item size + Padding), so a pair spends the gap TWICE - once before
            // the first button, once between the two. Nothing after the second. With the row's
            // origin pulled back by one gap, two buttons of (colW - gap)/2 span the column
            // exactly.
            float footBtnW = (colW - footGap) * 0.5f;
            float footBtnH = ModuleSelection.ToggleRowH;

            // ⚠ UIList lays its first item at Pos + Padding, so an origin set to the column's edge
            // puts the pair one gap to the right of the list above it. Both rows start back by
            // that gap, and land flush with their column.
            var leftFoot = AddList(new Vector2(ModuleSelectComponent.LocalPos.X - footGap, footY));
            leftFoot.LayoutStyle = ListLayoutStyle.ResizeList;
            leftFoot.Direction = new Vector2(+1, 0);
            leftFoot.Padding = new Vector2(footGap, 2f);

            var rightFoot = AddList(new Vector2(ModuleSelection.BandRight - colW - footGap, footY));
            rightFoot.LayoutStyle = ListLayoutStyle.ResizeList;
            rightFoot.Direction = new Vector2(+1, 0);
            rightFoot.Padding = new Vector2(footGap, 2f);

            BtnStripShip = leftFoot.Add(ButtonStyle.WideActive, Localizer.Token(GameText.NormalDesign), click: b =>
            {
                OnStripShipToggle();
            });
            BtnStripShip.ClickSfx = "blip_click";
            BtnStripShip.Tooltip = Localizer.Token(GameText.StripsTheShipOfAny);

            BtnSymmetricDesign = leftFoot.Add(ButtonStyle.WideActive, Localizer.Token(GameText.SymmetricDesign), click: b =>
            {
                OnSymmetricDesignToggle();
            });
            BtnSymmetricDesign.ClickSfx = "blip_click";
            BtnSymmetricDesign.Tooltip = Localizer.Token(GameText.YouCanSwitchFromNormal);
            BtnSymmetricDesign.Hotkey  = InputBindings.FromString("M");
            BtnSymmetricDesign.Style   = SymmetricDesignBtnStyle;

            BtnArcs = rightFoot.Add(ButtonStyle.WideActive, "Arcs", click: b =>
            {
                ShowAllArcs = !ShowAllArcs;
                BtnArcs.Style = ArcsBtnStyle;
            });
            BtnArcs.ClickSfx = "blip_click";
            BtnArcs.Tooltip  = Localizer.Token(GameText.TogglesTheWeaponFireArc);
            BtnArcs.Hotkey   = InputBindings.FromString("Tab");
            BtnArcs.Style    = ArcsBtnStyle;

            BtnToggleOverlay = rightFoot.Add(ButtonStyle.WideActive, GameText.ToggleOverlay, click: b =>
            {
                ToggleOverlay = !ToggleOverlay;
            });
            BtnToggleOverlay.ClickSfx = "blip_click";

            // half a column each, so a pair spans exactly the list above it
            foreach (UIButton b in new[] { BtnStripShip, BtnSymmetricDesign, BtnArcs, BtnToggleOverlay })
                b.SetAbsSize(footBtnW, footBtnH);
            leftFoot.PerformLayout();
            rightFoot.PerformLayout();

            // Ludoal fork (spec v4): the right column is laid out from the LEFT COLUMN, not from
            // its own fractions. The browser ends where the module list ends, the strip sits
            // just under it, and the cartouche fills the rest down to the shared foot line — so
            // the two columns read as one row whatever the window size.
            // the top of both lists: the module column owns it, the browser follows
            float filterTop   = ModuleSelectComponent.LocalPos.Y;
            // the search line: one row of text sitting in the band between the options row and
            // the top of the two lists, so both fields land on it without a second arithmetic
            float searchY     = filterTop - 22f;
            // the browser starts on the SAME line as the module list: its toggles ride the
            // identity row and its search field shares the search line, so the band above it no
            // longer costs the two rows it used to reserve
            float colTop      = filterTop;
            // the same foot line the module frames land on, so the four read as one row
            float colBottom   = ModuleSelection.FramesBottom();
            // The right column mirrors the left one exactly: the cartouche keeps the module
            // frame's height, and the browser runs down to it with the module list's own gap, so
            // both columns' feet land on one line. Nothing else is allowed into that arithmetic —
            // the completion + issues strip is INDEPENDENT: it keeps its place above the
            // cartouche and is simply narrow enough not to reach under the browser.
            float cartoucheH  = ModuleSelectComponent.FrameHeight;
            float cartoucheY  = colBottom - cartoucheH; // colBottom already carries the margin
            // the browser runs down to the cartouche with the same gap the module list keeps
            // above its own frame — that is what puts the two columns' feet on one line
            // stops above the toggle row, not at the cartouche: the right column carries the same
            // band of buttons the left one does, and a list measured to the cartouche ran under it
            float listBottom  = cartoucheY - ModuleSelection.FrameGap - ModuleSelection.ToggleRowBand;
            // the completion line sits at local y 0, the issues button at 18 in Pirulen20
            float issuesH     = 18f + Fonts.Pirulen20.LineSpacing;
            // Ludoal fork: the strip moves INSIDE the cartouche, on its last line - above the
            // frame it bites into the browser list, and the cartouche has spare room at the
            // bottom. Completion and the issues button share the row, so it costs one line
            // rather than two.
            float issuesY     = cartoucheY + cartoucheH - issuesH - 6f;

            // Ludoal fork: same padding on both sides — the right column's frames stay flush
            // with the screen edge otherwise, while the left ones breathe.
            // Ludoal fork: same width as the module list on the other side of the screen.
            Vector2 hullSelSize = new(ModuleSelection.ListWidth, Math.Max(160f, listBottom - colTop));
            var hullSelectPos = new LocalPos(ModuleSelection.BandRight - hullSelSize.X, colTop);
            // Ludoal fork: the load popup's filters come WITH its list — dropping them would be
            // a regression, since one of them ("my designs only") is a persisted preference the
            // player may already have set. They sit above the frame rather than inside it:
            // pushing content into a SubmenuScrollList means rearranging its internal layout.
            // derived from the browser's own position, not recomputed from ScreenWidth: the
            // filter row sits above that frame and must move with it (it kept the old flush
            // origin when the column gained its right margin)
            float filterX = hullSelectPos.X;
            // the search field sits UNDER the toggles, directly above the list it filters; the
            // toggles ride the identity row instead (see below)
            BrowserFilter = Add(new UITextEntry(filterX + 4, searchY, hullSelSize.X - 8,
                                                Fonts.Arial12Bold, DefaultBrowserFilter));
            BrowserFilter.AutoCaptureOnKeys = true;
            BrowserFilter.AutoCaptureLoseFocusTime = 0.5f;
            // Ludoal fork: the standard framed search field, like Colony's building filter - a
            // bare underline reads as a stray line of text over the starfield.
            BrowserFilter.Background = new Submenu(new RectF(filterX, searchY - 3, hullSelSize.X, Fonts.Arial12Bold.LineSpacing + 6));
            BrowserFilter.Color = Colors.Cream;
            // Ludoal fork: the placeholder clears itself on click, instead of needing to be
            // deleted by hand before you can type.
            BrowserFilter.AutoClearTextOnInputCapture = true;
            BrowserFilter.OnTextChanged = (text) =>
            {
                BrowserFilterText = (text == DefaultBrowserFilter) ? null : text?.ToLower();
                RefreshHullSelectList();
            };

            // side by side on one row: stacked, the second one slipped under the frame's title tab
            // Ludoal fork: three toggles on the row, so it starts further left and runs past the
            // frame's left edge — deliberate: there is empty starfield there and nothing to
            // collide with, whereas the right side is the browser's own margin.
            // Ludoal fork: the module list gets its filter as a checkbox directly above it, where
            // the designs have theirs — close to the list it filters.
            // 26px ABOVE the list, not below its top edge: filterTop IS the list's own top, so
            // +26 would put the checkbox inside the list, drawing straight over it.
            // The filter toggles sit directly ABOVE their search field, with their list: a
            // checkbox draws itself around its centre and measures max(12, LineSpacing) tall, so
            // its top is pulled up by its own height plus the gap.
            float toggleH = Math.Max(12, Fonts.Arial12Bold.LineSpacing);
            float toggleY = searchY - toggleH - 6; // 2px higher, to air the row off the filter frame

            Checkbox(new Vector2(ModuleSelection.BandLeft + 6, toggleY),
                     () => IsFilterOldModulesMode,
                     (b) => { IsFilterOldModulesMode = b; ModuleSelectComponent.ResetActiveCategory(); },
                     "Hide obsolete", GameText.WhenToggledRedAnyModule);

            // The module list gets the same search field the browser has, on the line just above
            // it. Typing filters across every category, so a module can be found without knowing
            // which of the four tabs holds it.
            ModuleFilter = Add(new UITextEntry(ModuleSelection.BandLeft + 4, searchY, ModuleSelection.ListWidth - 8,
                                               Fonts.Arial12Bold, DefaultModuleFilter));
            ModuleFilter.AutoCaptureOnKeys = true;
            ModuleFilter.AutoCaptureLoseFocusTime = 0.5f;
            // Ludoal fork: the standard framed search field, like Colony's.
            ModuleFilter.Background = new Submenu(new RectF(ModuleSelection.BandLeft, searchY - 3, ModuleSelection.ListWidth, Fonts.Arial12Bold.LineSpacing + 6));
            ModuleFilter.Color = Colors.Cream;
            ModuleFilter.AutoClearTextOnInputCapture = true;
            ModuleFilter.OnTextChanged = (text) =>
            {
                ModuleSearchText = (text == DefaultModuleFilter) ? null : text?.ToLower();
                ModuleSelectComponent.ResetActiveCategory();
            };

            // Ludoal fork: the row is right-aligned on the frame's own right edge with a 5px
            // margin, rather than pushed left by a guessed amount.
            const float togglesWidth = 246f + 108f;   // three checkboxes, last one included
            float row3 = hullSelSize.X - togglesWidth - 5f;
            Checkbox(new Vector2(filterX + row3 + 6, toggleY),
                     () => !Player.Universe.P.ShowAllDesigns,
                     (b) => { Player.Universe.P.ShowAllDesigns = !b; RefreshHullSelectList(); },
                     "My designs only", "Show only the designs you created");

            Checkbox(new Vector2(filterX + row3 + 132, toggleY),
                     () => ShowLockedDesigns,
                     (b) => { ShowLockedDesigns = b; RefreshHullSelectList(); },
                     "Show locked", GameText.ShowEmpireLockedDesignsTip);

            Checkbox(new Vector2(filterX + row3 + 246, toggleY),
                     () => HideObsoleteDesigns,
                     (b) => { HideObsoleteDesigns = b; RefreshHullSelectList(); },
                     "Hide obsolete", "Hide the designs you have marked obsolete");

            // The grouping mode rides the frame's own title bar: Submenu carries tabs
            // natively, so it costs no pixel of the list and no third row of filters -
            // the two we have already fill the 52px above the frame at 1440.
            var hullSelectSub = Add(new SubmenuScrollList<ShipYardBrowserItem>(hullSelectPos, hullSelSize,
                                        new LocalizedText[] { "By Hull", "By Role" }));
            // rounded black background
            hullSelectSub.SetBackground(Colors.TransparentBlackFill);

            HullSelectList = hullSelectSub.List;
            // subscribed AFTER the list exists: Submenu selects tab 0 on its first Update, which
            // fires this — a handler armed any earlier would refresh a list that is still null
            hullSelectSub.OnTabChange = (tab) =>
            {
                GroupByRole = tab == 1;
                RefreshHullSelectList();
            };
            // Ludoal fork: restore the grouping the player left on. Submenu picks tab 0 on its
            // first Update when nothing is selected, so this has to be set before that runs -
            // and after the handler above is armed, or the tab would move without the list
            // following it.
            if (GroupByRole)
                hullSelectSub.SelectedIndex = 1;
            // single click selects (and shift-click pins for comparison), double click loads:
            // the load is the expensive gesture (mesh + modules + stats), so it stays deliberate
            HullSelectList.OnClick = OnBrowserItemClicked;
            HullSelectList.OnDoubleClick = OnBrowserItemDoubleClicked;
            HullSelectList.EnableItemHighlight = true;

            // hover preview: the same overlay the load popup used, so a design can be
            // inspected without paying for a load. Hull rows carry no design, and the
            // overlay hides itself when handed a null one.
            // Ludoal fork: hovering a design fills the Hover cartouche.
            HullSelectList.OnHovered = item => SetHoveredDesign(item?.Design);
            RefreshHullSelectList();
            hullSelectSub.PerformLayout();

            // The option row, centred like the identity line above it: the two dropdowns, the
            // carrier-only box and the stance block form one row whose total width is built from
            // the parts. Everything on it aligns its TOP with the stance block's top, which is
            // why the row's Y is the stance topLeft and not a font baseline.
            // Each dropdown carries its caption to its LEFT, so the row must reserve that width
            // too: measured in the font that draws it, never guessed.
            // Ludoal fork: the row reads Carrier Only, Repair, Stance, Hangar Type - the checkbox
            // first because it decides what the two dropdowns even mean. The captions are short
            // enough to fit the row at 1440 wide, each measured in the font that draws it.
            const int ddW = 125, ddHangarW = 150, ddH = 18, optGap = 20;
            int lblRepairW = (int)Fonts.Arial12Bold.TextWidth(RepairCaption) + TitleGap;
            int lblHangarW = (int)Fonts.Arial12Bold.TextWidth(HangarCaption) + TitleGap;
            // the checkbox measures itself the same way UICheckBox does: box (12) + padding (4) + text
            int carrierW = 12 + 4 + (int)Fonts.Arial12Bold.TextWidth("Carrier Only");
            int optRowW = carrierW + optGap + lblRepairW + ddW + optGap + lblHangarW + ddHangarW
                        + optGap + stanceW;
            int optY = OptionsRowY;
            int optX = (int)ClassifCursor.X - optRowW / 2;

            var carrierOnlyPos  = new Vector2(optX, optY);
            CarrierOnlyCheckBox = Checkbox(carrierOnlyPos,
                () => CurrentDesign?.IsCarrierOnly == true,
                (b) => { if (CurrentDesign != null) CurrentDesign.IsCarrierOnly = b; }, "Carrier Only", GameText.WhenMarkedThisShipCan);

            // Ludoal fork: measured off the checkbox rather than a reserved 110 - UICheckBox sizes
            // itself from its own text, so the slot was wider than the control and the gap before
            // Repair read larger than every other gap on the row.
            var dropdownRect = new Rectangle((int)(CarrierOnlyCheckBox.Right + optGap) + lblRepairW,
                                             optY, ddW, ddH);
            CategoryList = new CategoryDropDown(dropdownRect);
            foreach (ShipCategory item in Enum.GetValues(typeof(ShipCategory)).Cast<ShipCategory>())
                CategoryList.AddOption(item.ToString(), item);

            // Ludoal fork: Stance before Hangar Type. Stance is a block of icons rather than a
            // labelled dropdown, so it breaks the row's rhythm less in the middle than at the
            // end, where the widest thing would hang off the edge.
            OrdersButton = new DesignStanceButtons(this,
                new Vector2(dropdownRect.Right + optGap, optY));
            Add(OrdersButton);

            var hangarRect = new Rectangle(dropdownRect.Right + optGap + stanceW + optGap + lblHangarW,
                                           optY, ddHangarW, ddH);
            HangarOptionsList = new HangarDesignationDropDown(hangarRect);
            foreach (HangarOptions item in Enum.GetValues(typeof(HangarOptions)).Cast<HangarOptions>())
                HangarOptionsList.AddOption(item.ToString(), item);

            // DESIGN ISSUES sits UNDER the cartouche instead of in a narrow 200px column to its
            // left, so its text gets the full width. Both boxes use the bottom-up
            // geometry computed above, which is why the list stretches and these do not.
            // The cartouche's right edge follows the browser frame above it — one margin for the
            // whole column — and it grows LEFTWARD to a width that actually fits two columns of
            // labels: at the browser's own width a half-column left ~54px for titles like
            // "Total Module Slots", so the value landed in the middle of its own label.
            // 400 = 2 × (longest title ~105 + value 60) + gutter + frame margins.
            // Ludoal fork: the frame is sized BY ITS CONTENT, the module panel's way — two column
            // steps plus the panel's own margins — instead of a fraction of available space. Its
            // right edge stays on the browser's, which keeps the column's right margin, and it
            // grows leftward.
            // Ludoal fork: no comparison means no delta lanes, so the frame does not pay for them
            // either. One value feeds both the width and the panel's own reservation - they must
            // agree or the columns sit wrong inside the frame.
            // Ludoal fork: built at its NARROW size. The delta lanes are paid for only while a
            // design is actually pinned, and nothing is pinned when the screen opens -
            // ResizeCartouches widens it on the first pin and narrows it back on the last unpin.
            bool deltaLanes = false;
            float cartoucheW = CompactActiveDesign ? ModuleSelection.ListWidth
                             : ShipDesignInfoPanel.FrameWidthFor(withDeltas: deltaLanes, withPlan: false);
            AppliedCompact = CompactActiveDesign;
            var infoRect = RectF.FromPoints(hullSelectSub.Right - cartoucheW, hullSelectSub.Right,
                                            cartoucheY, cartoucheY + cartoucheH);
            // Ludoal fork: the stats live in a titled cartouche, same frame the module panel uses
            // ("Active Module"), so the two read as the same kind of object. The frame is added
            // first so it draws behind the values.
            var infoSub = Add(new Submenu(infoRect, "Active Design"));
            infoSub.SetBackground(Colors.TransparentBlackFill);

            // top-right of the frame, the same offsets the module panel uses
            int obsW = ResourceManager.Texture("NewUI/icon_queue_delete").Width;
            int obsH = ResourceManager.Texture("NewUI/icon_queue_delete").Height;
            var obsPos = new RectF(infoRect.X + infoRect.W - obsW - 10, infoRect.Y + 38, obsW, obsH);
            ObsoleteDesign = new UIButton(new UIButton.StyleTextures("NewUI/icon_queue_delete",
                                          "NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2"),
                                          new Vector2(obsW, obsH), "")
            {
                Tooltip = "Mark this design as obsolete",
                OnClick = OnObsoleteDesignClicked,
                ClickSfx = "sd_ui_accept_alt3", // AcceptClick, as this toggle always played
            };
            ObsoleteDesign.Rect = obsPos;
            void OnObsoleteDesignClicked(UIButton b)
            {
                Player.ToggleDesignObsolete(CurrentDesign.Name);
                RefreshHullSelectList(); // the browser greys the row straight away
            }

            // Ludoal fork: the inner panel takes the module frame's own left margin (10), so the
            // design name starts exactly where "Light Kinetic Cannon" does in its frame.
            const float Inset = ShipDesignInfoPanel.Inset;   // one number, read by all four sites
            var infoInner = RectF.FromPoints(infoRect.X + Inset, infoRect.Right - Inset,
                                             infoRect.Y + 26, infoRect.Bottom - 8);
            InfoPanel = Add(new ShipDesignInfoPanel(this, infoInner));
            InfoPanel.HasDeltaLanes = deltaLanes; // widened on the first pin, see ResizeCartouches
            InfoPanel.Compact = CompactActiveDesign; // the toggle survives the screen
            InfoSub = infoSub;
            // Ludoal fork: on the frame's tab row vertically, but bound to the BROWSER, not to
            // the frame - right-aligned on the browser's right edge, exactly as its module twin
            // sits on the right edge of ITS list. Bound to the list rather than the cartouche, it
            // also stops travelling when the cartouche grows leftward on a pin, and it survives
            // the frame it hides: unpinned, the checkbox is the only way back.
            // Pin Active stays on the ACTIVE frame's tab row - only Compact moves to the
            // browser's row, where the narrow frame cannot bite it.
            RectF listTab = hullSelectSub.Tabs[0].Rect;
            RectF infoTab = infoSub.Tabs[0].Rect;
            PinActiveCheck = Checkbox(new Vector2(hullSelectPos.X, infoTab.Y + 4),
                                      () => PinActiveDesign,
                                      (b) => { PinActiveDesign = b; },
                                      "Pin Active",
                                      "Keep the Active Design cartouche on screen while you hover the list.\n"
                                    + "Off: the hovered design takes its place, and it comes back when you look away.");
            // Ludoal fork: right-aligned on the browser's own right edge, the same way the module
            // twin hugs the right edge of its list. UICheckBox sizes itself in its constructor,
            // so the width is exact by the time this runs.
            // ⚠ off the ELEMENT's realized right edge - mixing a LocalPos into SetAbsPos here
            // reads as "not quite aligned".
            PinActiveCheck.SetAbsPos(hullSelectSub.Right - PinActiveCheck.Width,
                                     PinActiveCheck.Y);

            // Ludoal fork: the compact cartouche is LIVE - list-width, the flying overlay's stat
            // set, deltas wired. ResizeCartouches applies a flip.
            CompactActiveCheck = Checkbox(new Vector2(hullSelectPos.X, listTab.Y + 4),
                                          () => CompactActiveDesign,
                                          (b) => { CompactActiveDesign = b; },
                                          "Compact",
                                          "Show the Active Design cartouche in its compact form:\n"
                                        + "the browser's width, the hover overlay's stats.");
            // measured off Pin Active rather than a reserved slot, and spaced like the option row
            CompactActiveCheck.SetAbsPos(hullSelectSub.Right - CompactActiveCheck.Width,
                                         CompactActiveCheck.Y);

            // Ludoal fork: Full Screen toggle, on the identity row to the left of the hull name.
            // Its setter re-runs LoadContent because the frame, every anchored panel and the 3D
            // projection all rebuild from the new (uncapped) frame - none of them relayout on their own.
            FullScreenCheck = Checkbox(new Vector2(idLeft, IdentityRowY),
                                       () => FullScreenDesign,
                                       (b) =>
                                       {
                                           FullScreenDesign = b;
                                           // Keep the work across the flip. ReloadContent rebuilds via
                                           // LoadContent, whose hull-restore can fall back to the bare
                                           // hull - clone the design WITH its fitted modules and put it
                                           // back after the relayout.
                                           ShipDesign keep = CurrentDesign != null ? CloneCurrentDesign(CurrentDesign.Name) : null;
                                           ReloadContent();
                                           if (keep != null)
                                               ChangeHull(keep, zoomToHull: false);
                                           // The hard pause and the theme follow the toggle, not the ctor
                                           // - windowed hands both back
                                           if (FullScreenDesign)
                                               ClaimUniversePause(ParentUniverse);
                                           else
                                           {
                                               if (!GlobalStats.PauseOnPageOpen)
                                                   ReleaseUniversePause();
                                               ScreenManager.StartMusic("AmbientMusic");
                                           }
                                       },
                                       "Full Screen",
                                       "Expand the Shipyard to the whole display instead of the fixed\n"
                                     + "1600x1080 working size. Anchored on the rail either way.");
            // Right beside the role/name block, and centred on the identity row's height rather
            // than hanging from its top.
            FullScreenCheck.SetAbsPos(idLeft - FullScreenCheck.Width - idGap,
                                      IdentityRowY + (idH - (int)FullScreenCheck.Height) / 2);

            // Ludoal fork: the HOVER cartouche is the plain frame — no delta lane — showing
            // whatever design the cursor rests on in the browser, and it goes away when the
            // cursor leaves. The pinned design has no frame of its own: it only feeds the delta
            // lane of the Active cartouche.
            // same two columns, no delta lanes, plus the ship plan down its left edge
            float hoverW = CompactActiveDesign ? ShipDesignInfoPanel.CompactFrameWidthFor(withPlan: true)
                         : ShipDesignInfoPanel.FrameWidthFor(withDeltas: false, withPlan: true);
            var hoverRect = RectF.FromPoints(infoRect.X - hoverW - 10f, infoRect.X - 10f,
                                             infoRect.Y, infoRect.Bottom);
            HoverSub = Add(new Submenu(hoverRect, "Hovered Design"));
            HoverSub.SetBackground(Colors.TransparentBlackFill);

            // ⚠ 26, the SAME inner top as the Active frame: at 32 the hover title sits 6px
            // lower than its two siblings.
            var hoverInner = RectF.FromPoints(hoverRect.X + Inset, hoverRect.Right - Inset,
                                              hoverRect.Y + 26, hoverRect.Bottom - 8);
            HoverPanel = Add(new ShipDesignInfoPanel(this, hoverInner));
            HoverPanel.ShowShipPlan = true; // the module plan down its left edge
            HoverPanel.Compact = CompactActiveDesign;
            HoverSub.Visible = HoverPanel.Visible = false;

            // Ludoal fork: completion + issues sit ABOVE the cartouche tab, between the browser
            // and the frames, spanning the same width, left-aligned on the cartouche. Only as
            // WIDE as its own two lines, though — spanning the whole cartouche gives it a click
            // area that reaches under the browser list beside it. It is INDEPENDENT of the
            // column's arithmetic; nothing else should be shaped around it.
            var issuesRect = RectF.FromPoints(infoRect.X, infoRect.X + ShipDesignIssuesPanel.ContentWidth,
                                              issuesY, issuesY + issuesH);
            IssuesPanel = Add(new ShipDesignIssuesPanel(this, issuesRect));

            if (EnableDebugFeatures)
            {
                var debugUnlocks = Add(new ResearchDebugUnlocks(ParentUniverse, OnReloadAfterTechChange));
                debugUnlocks.SetAbsPos(10, 45);
            }

        }

        // Ludoal fork: the Design group's tabs. The Shipyard closes before the target opens -
        // its 3D preview shares the scene, and its input layer would leak through.
        void OnDesignTabChanged(int tab)
        {
            if (tab == 1)
                return; // already here

            GameAudio.EchoAffirmative();
            ReturnSuppressed = true; // a tab switch lands on a sibling, not back on the colony
            ExitScreen();
            if (tab == 0)
                ScreenManager.AddScreen(new FleetDesignScreen(ParentUniverse, EmpireUI));
            else
                ScreenManager.AddScreen(new BlueprintsScreen(ParentUniverse, ParentUniverse.Player));
        }

        // Ludoal fork: the visibility policy ported verbatim from the load popup. ShowAllDesigns
        // is a PERSISTED player preference — ignoring it would silently pour every faction's
        // designs on someone who chose to see only their own. Locked designs stay hidden by
        // default; the toggle comes with the filter bar.
        bool CanShowDesign(IShipDesign design)
        {
            // the browser's own text filter, matching name or hull like the popup's did
            if (BrowserFilterText.NotEmpty()
                && !design.Name.ToLower().Contains(BrowserFilterText)
                && !design.Hull.ToLower().Contains(BrowserFilterText))
                return false;

            if (!Player.Universe.P.ShowAllDesigns && !design.IsPlayerDesign)
                return false;

            // Ludoal fork: retired designs are hidden on demand.
            if (HideObsoleteDesigns && Player.IsDesignObsolete(design.Name))
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
            // Ludoal fork: the 3D workbench centres on the SHIPYARD WINDOW (frame capped at 1680),
            // not the whole screen - otherwise the ship drifts down and right at hi-res, where
            // screen centre no longer matches the frame's. The offset is computed once in
            // ScreenGroups so the Fleets surround uses the same arithmetic, and it reads the SAME
            // full-screen flag as the frame, so it recentres on the wider frame in Full Screen.
            Vector2 camOffset = ScreenGroups.GroupFrameCameraOffset(ScreenWidth, ScreenHeight, FullScreenDesign);
            // set shipyard's fov much lower to reduce parallax
            SetPerspectiveProjection(fovYdegrees: 20, maxDistance: 30000, offsetXY: camOffset);
            UpdateViewMatrix(CameraPos);
        }

        // Ludoal fork: one list grouped BY HULL, merging the role-grouped hull list and the
        // separate "load design" popup. Each hull group opens with its own bare hull as row #1,
        // then the designs built on it. The role shown on a design row is IShipDesign.Role (what
        // the fitted modules make it), a different field from the hull's own role — they never
        // disagree.
        // Ludoal fork: deleting a design from the browser uses the same two guards the load popup
        // used - DeleteShip and RemoveRelatedWiPs are public statics, DesignInQueue takes a
        // ShipDesignScreen.
        // Ludoal fork: one place builds a design row, so both grouping modes carry the same
        // affordances. Deleting is refused on read-only and from-save designs, exactly as the
        // load popup refused it.
        ShipYardBrowserItem NewDesignRow(IShipDesign design, bool hullInBadge)
        {
            bool isWip = WipDesigns.Contains(design.Name);

            // the techs this design still needs, and which of them are already queued - the
            // button only earns its place when researching would actually add something
            string[] missing = design.TechsNeeded.Filter(t => !Player.UnlockedTechs.Any(te => te.UID == t));
            string[] queued = missing.Filter(Player.Research.IsQueued);
            bool worthResearching = missing.Length > 0 && queued.Length < missing.Length;

            var row = new ShipYardBrowserItem(Player, design, isWip,
                onDelete: () => PromptDeleteDesign(design.Name),
                onResearch: worthResearching
                          ? () => PromptResearchDesign(design.Name, missing, queued)
                          : null,
                onDeleteAllWipVersions: isWip ? () => PromptDeleteAllWipVersions(design.Name) : null);
            row.ShowHullInBadge = hullInBadge;
            return row;
        }

        // Ludoal fork: queue the techs a design still needs, straight from its browser row.
        // Same shape as the deletions: nothing ported, AddTechToQueue and GetTechEntry are
        // already public.
        void PromptResearchDesign(string designName, string[] missingTechs, string[] alreadyQueued)
        {
            string ToNames(string[] techs)
            {
                var names = new Array<string>();
                foreach (string uid in techs)
                    if (Player.TryGetTechEntry(uid, out TechEntry te))
                        names.Add(te.Tech.Name.Text);
                return string.Join("\n", names);
            }

            string queued = alreadyQueued.Length > 0 ? $"Already in Queue:\n{ToNames(alreadyQueued)}\n\n" : "";
            string toAdd = ToNames(missingTechs.Filter(t => !alreadyQueued.Contains(t)));

            ScreenManager.AddScreen(new MessageBoxScreen(this,
                $"Confirm Research Missing Techs ({missingTechs.Length}) for {designName}:\n\n{queued} Will be added to Queue:\n{toAdd}")
            {
                Accepted = () =>
                {
                    foreach (TechEntry te in missingTechs.Select(t => Player.GetTechEntry(t)).Sorted(t => t.TechCost))
                        Player.Research.AddTechToQueue(te.UID);
                    GameAudio.EchoAffirmative();
                    RefreshHullSelectList();
                }
            });
        }

        // Ludoal fork: the shipyard is never without a design, so deleting the one on the
        // workbench falls back to a bare hull rather than leaving the screen holding a design
        // that no longer exists. Same fallback the screen uses when it opens with nothing to restore.
        void LoadDefaultDesign()
        {
            if (AvailableHulls.NotEmpty)
                ChangeHull(AvailableHulls[0], zoomToHull: false);
        }

        void PromptDeleteDesign(string designName)
        {
            if (ParentUniverse.UState.Ships.Any(sh => sh.Name == designName))
            {
                GameAudio.NegativeClick();
                ScreenManager.AddScreen(new MessageBoxScreen(this,
                    $"{designName} currently exists in the universe. You cannot delete a design with this name.",
                    MessageBoxButtons.Ok));
                return;
            }

            if (HelperFunctions.DesignInQueue(this, designName, out string playerPlanets))
            {
                GameAudio.NegativeClick();
                string why = playerPlanets.NotEmpty()
                    ? $"{designName} is in a build queue. You cannot delete this design name.\n Related planets: {playerPlanets}."
                    : $"{designName} currently exists in the universe (maybe another empire). You cannot delete this design name.";
                ScreenManager.AddScreen(new MessageBoxScreen(this, why, MessageBoxButtons.Ok));
                return;
            }

            ScreenManager.AddScreen(new MessageBoxScreen(this, $"Confirm Delete: {designName}")
            {
                Accepted = () =>
                {
                    bool wasOnTheBench = CurrentDesign?.Name == designName;
                    ResourceManager.DeleteShip(ParentUniverse.UState, designName);
                    GameAudio.EchoAffirmative();
                    if (wasOnTheBench)
                        LoadDefaultDesign(); // the screen always holds a design
                    RefreshHullSelectList();
                }
            });
        }

        void PromptDeleteAllWipVersions(string designName)
        {
            string prefix = ShipDesignWIP.GetWipShipNameAndNum(designName);
            ScreenManager.AddScreen(new MessageBoxScreen(this, $"Confirm Delete All WIP Versions: {prefix}")
            {
                Accepted = () =>
                {
                    // compared by PREFIX: every version of a design shares it
                    string onBench = CurrentDesign?.Name ?? "";
                    bool wasOnTheBench = onBench.StartsWith(prefix);
                    ShipDesignWIP.RemoveRelatedWiPs(ParentUniverse.UState, designName);
                    GameAudio.EchoAffirmative();
                    if (wasOnTheBench)
                        LoadDefaultDesign();
                    RefreshHullSelectList();
                }
            });
        }

        void RefreshHullSelectList()
        {
            // Ludoal fork: remember which groups were open. Rebuilding the list creates new row
            // objects, so their expanded state is lost and every category snaps shut otherwise.
            // Keyed by heading text, the only thing that survives the rebuild.
            ExpandedGroups.Clear();
            foreach (ShipYardBrowserItem row in HullSelectList.AllEntries)
                if (row.Expanded && row.HeaderText.NotEmpty())
                    ExpandedGroups.Add(row.HeaderText);

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

            // Ludoal fork: work-in-progress designs belong in the browser too. Read the same way
            // the load popup read them, and filed under their own hull like everything else.
            WipDesigns.Clear();
            foreach (FileInfo info in Dir.GetFiles(Dir.StarDriveUserData + "/WIP", "design"))
            {
                ShipDesign wip = ShipDesign.Parse(info);
                if (wip == null)
                    continue;
                if (!UnlockAllFactionDesigns && !Player.WeCanShowThisWIP(wip))
                    continue;
                if (BrowserFilterText.NotEmpty()
                    && !wip.Name.ToLower().Contains(BrowserFilterText)
                    && !wip.Hull.ToLower().Contains(BrowserFilterText))
                    continue;

                WipDesigns.Add(wip.Name);
                if (!designsByHull.TryGetValue(wip.Hull, out Array<IShipDesign> onHull))
                    designsByHull[wip.Hull] = onHull = new Array<IShipDesign>();
                onHull.Add(wip);
            }

            // Groups are the hull CLASSES of the tech tree (Fighter, Corvette, Frigate,
            // Freighter...), as the old hull list did. Inside a class, each hull opens with its
            // own bare row — that row carries the hull's name, e.g. "Fang Fighter" — and its
            // designs follow. Two levels is all the scroll list can do, which is why the class
            // is the heading and the hull is a row rather than a nested group.
            if (GroupByRole)
                BuildGroupsByRole(designsByHull);
            else
                BuildGroupsByHullClass(designsByHull);
        }

        // The build view: a hull class, then each hull with its own bare row followed by the
        // designs built on it. Two levels is all the scroll list can do, which is why the class
        // is the heading and the hull is a row rather than a nested group.
        void BuildGroupsByHullClass(Map<string, Array<IShipDesign>> designsByHull)
        {
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

                // Ludoal fork: a class the filter emptied is a heading promising rows that are
                // not there, so it is removed after the fact (added first and removed after,
                // rather than deferred: AddSubItem refuses to run on a header not in a list yet).
                if (group.NumSubItems == 0)
                {
                    HullSelectList.Remove(group);
                    continue;
                }

                // Ludoal fork: the filter reaches the hull rows too, and what survives is opened
                // so the matches are on screen rather than behind a fold.
                if (BrowserFilterText.NotEmpty() || ExpandedGroups.Contains(cls))
                    group.Expand(true);
            }
        }

        // The use view: group by what a ship IS FOR rather than what it is built on. A design's
        // role is the one its fitted modules express (Carrier, Colony, Scout); a bare hull has no
        // fitted modules, so it falls under its carcass role - which is exactly the answer to
        // "what could I build here". The two therefore stop being adjacent in this mode.
        void BuildGroupsByRole(Map<string, Array<IShipDesign>> designsByHull)
        {
            var byRole = new Map<string, Array<ShipYardBrowserItem>>();

            void Bucket(string role, ShipYardBrowserItem row)
            {
                if (!byRole.TryGetValue(role, out Array<ShipYardBrowserItem> rows))
                    byRole[role] = rows = new Array<ShipYardBrowserItem>();
                rows.Add(row);
            }

            foreach (ShipHull hull in AvailableHulls.Sorted(h => h.VisibleName))
            {
                if (HullMatchesFilter(hull))
                    Bucket(Localizer.GetRole(hull.Role, Player), new ShipYardBrowserItem(Player, hull));
            }

            // designsByHull is already filtered by CanShowDesign, so the same rows appear in
            // both modes - only their grouping changes
            foreach (Array<IShipDesign> designs in designsByHull.Values)
            {
                foreach (IShipDesign design in designs)
                    Bucket(Localizer.GetRole(design.Role, Player),
                           NewDesignRow(design, hullInBadge: true));
            }

            // filled by hand rather than from byRole.Keys: a Dictionary key collection satisfies
            // both Array's ICollection and IReadOnlyCollection constructors, so the call is
            // ambiguous
            var roles = new Array<string>();
            foreach (string role in byRole.Keys)
                roles.Add(role);
            roles.Sort();

            foreach (string role in roles)
            {
                var group = new ShipYardBrowserItem(Player, null, role);
                HullSelectList.AddItem(group);

                // ours first, then the strongest, then alphabetical - the load popup's order,
                // with the bare hulls leading since they are where a new design starts
                foreach (ShipYardBrowserItem row in byRole[role]
                             .OrderBy(r => !r.IsBareHull)
                             .ThenBy(r => r.Design == null ? "" : (r.Design.IsPlayerDesign ? "0" : "1"))
                             .ThenByDescending(r => r.Design?.BaseStrength ?? 0f)
                             .ThenBy(r => r.Design?.Name ?? r.Hull.VisibleName))
                {
                    group.AddSubItem(row);
                }

                if (BrowserFilterText.NotEmpty() || ExpandedGroups.Contains(role))
                    group.Expand(true);
            }
        }

        // the browser's text filter as it applies to a bare hull row
        bool HullMatchesFilter(ShipHull hull)
            => BrowserFilterText.IsEmpty()
            || hull.VisibleName.ToLower().Contains(BrowserFilterText)
            || hull.HullName.ToLower().Contains(BrowserFilterText);

        void AddHullAndItsDesigns(ShipYardBrowserItem group, ShipHull hull,
                                  Map<string, Array<IShipDesign>> designsByHull)
        {
            // the bare hull first: starting from an empty carcass is one row, not a mode
            if (HullMatchesFilter(hull))
                group.AddSubItem(new ShipYardBrowserItem(Player, hull));

            if (designsByHull.TryGetValue(hull.HullName, out Array<IShipDesign> designs))
            {

                // same order as the old load popup: our own designs first, then the
                // strongest, then alphabetical
                foreach (IShipDesign design in designs.OrderBy(d => !d.IsPlayerDesign)
                                                      .ThenByDescending(d => d.BaseStrength)
                                                      .ThenBy(d => d.Name))
                {
                    group.AddSubItem(NewDesignRow(design, hullInBadge: false));
                }
            }
        }
    }
}
