using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.AI;
using Ship_Game.Gameplay;
using Ship_Game.Ships;
using System;
using System.Collections.Generic;
using System.Linq;
using SDGraphics;
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Point = SDGraphics.Point;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    public sealed partial class ShipDesignScreen // refactored by Fat Bastard
    {
        public Point GridPosUnderCursor;
        public SlotStruct SlotUnderCursor;

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.BeginFrameRendering(elapsed, ref View, ref Projection);

            // Ludoal fork (bench 345): a popup now - dim the paused universe drawn behind it with the
            // table screens' veil, instead of the dead black backdrop it used to have.
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);

            // Ludoal fork: the starfield, the particles and the 3D workbench are clipped to the
            // tab frame - the screen is one tab of the Design group now, so its scene belongs
            // inside the frame rather than running under the top bar and past the edges. Scissor
            // is device state: it has to be turned off again before the UI pass, or every panel
            // drawn afterwards inherits the crop.
            RectF sceneClip = ScreenGroups.GroupSceneArea(DesignTabs.Rect, DesignTabs.ClientArea);
            Ship_Game.Graphics.RenderStates.EnableScissorTest(batch.GraphicsDevice, sceneClip);

            ParentUniverse.DrawStarField(ScreenManager.SpriteRenderer);
            ParentUniverse.Particles.Draw(View, Projection, nearView:true);
            // ⚠ Ludoal fork (bench 347): do NOT Update the universe's particles here. Since the
            // screen became a popup, the universe underneath is visible again and runs its OWN
            // Update (particles included), so updating them a second time from Draw advanced them
            // twice per frame and they piled up - the Shipyard-only slowdown Fleet never had
            // (Fleet does not touch particles). Draw them, let the universe update them.

            ScreenManager.RenderSceneObjects();

            if (ToggleOverlay)
            {
                // bench 360 (maintainer): the module tiles clip to the frame like the 3D hull does -
                // this pass now rides the scene scissor (the scissor rect is still set on the device;
                // the pinned rasterizer makes the batch honour it).
                batch.SafeBegin(SpriteBlendMode.AlphaBlend, sortImmediate:true,
                                Ship_Game.Graphics.RenderStates.ScissorEnabled);

                DrawEmptySlots(batch);
                DrawModules(batch);
                DrawUnpoweredTex(batch);
                DrawTacticalOverlays(batch);
                DrawModuleSelections();
                DrawProjectedModuleRect(batch);

                if (EnableDebugFeatures)
                {
                    DrawDebugDetails(batch);
                }

                batch.SafeEnd();
            }

            // Ludoal fork: the module brush and its firing arcs belong to the scene, so they
            // clip to the frame exactly like the fitted modules do. Independent of the module
            // overlay toggle, hence its own pass.
            if (ActiveModule != null && !ModuleSelectComponent.HitTest(Input))
            {
                batch.SafeBegin(SpriteBlendMode.AlphaBlend, sortImmediate:true,
                                Ship_Game.Graphics.RenderStates.ScissorEnabled);
                DrawActiveModule(batch);
                batch.SafeEnd();
            }

            // scissor off before the UI pass - the panels drawn from here on span the whole screen
            Ship_Game.Graphics.RenderStates.DisableScissorTest(batch.GraphicsDevice);

            batch.SafeBegin();
            DrawUi(batch, elapsed);

            base.Draw(batch, elapsed);

            // Ludoal fork: the obsolete-design button, drawn after the frame it sits on.
            // TexturedButton is not a UIElement, so it does not draw itself. Red when the design
            // is marked, exactly as the module panel colours its own.
            if (InfoSub.Visible && CurrentDesign != null)
            {
                ObsoleteDesign.BaseColor = Player.IsDesignObsolete(CurrentDesign.Name)
                                         ? Color.Red : Color.White;
                ObsoleteDesign.Draw(batch);
            }

            // Ludoal fork: the Design group's tab tooltip. No frame fill on this screen - the
            // starfield IS the workbench's background, so the surround is drawn as an outline
            // only and the 3D view keeps showing through.
            ScreenGroups.DrawDesignTabTip(DesignTabs, Input.CursorPosition);

            batch.SafeEnd();
            ScreenManager.EndFrameRendering();
        }

        void DrawDebugDetails(SpriteBatch batch)
        {
            // Draw Internal and External modules for the design
            foreach (ShipModule m in DesignedShip.Modules)
            {
                var rect = new RectF(ModuleGrid.GridPosToWorld(m.Pos), m.WorldSize);
                if (m.HasInternalRestrictions)
                    DrawRectangleProjected(rect, Color.Green);
                else if (m.IsExternal)
                    DrawRectangleProjected(rect, Color.Blue);
            }

            if (ShowAllArcs)
            {
                var shieldGridColor = Color.Cyan.Alpha(0.5f);
                var gridColor = Color.Gray.Alpha(0.5f);

                for (int y = 0; y < DesignedShip.Grid.Height; ++y)
                {
                    for (int x = 0; x < DesignedShip.Grid.Width; ++x)
                    {
                        var worldPos = ModuleGrid.GridPosToWorld(new Point(x,y));
                        var rect = new RectF(worldPos, new Vector2(16));

                        int numShields = DesignedShip.Grid.GetNumShieldsAt(x, y);
                        if (numShields > 0)
                        {
                            DrawRectangleProjected(rect, shieldGridColor);
                            DrawStringProjected(worldPos+new Vector2(1,1), 6, shieldGridColor, $"S:{numShields}");

                            ShipModule shield = DesignedShip.HitTestShields(rect.Center, 10f);
                            if (shield == null)
                            {
                                DrawCircleProjected(rect.Center, 10f, Color.Red);
                                DrawStringProjected(worldPos+new Vector2(1,6), 6, Color.Red, "Hit:false");
                            }
                        }
                        else
                        {
                            DrawRectangleProjected(rect, gridColor);
                        }
                    }
                }
            }

            DrawCrossHairProjected(ModuleGrid.GridPosToWorld(CurrentHull.GridCenter), 16f, Color.Red, 2f);
        }

        bool GetSlotForModule(ShipModule module, out SlotStruct slot)
        {
            slot = module == null ? null : ModuleGrid.SlotsList.FirstOrDefault(s => s.Module == module);
            return slot != null;
        }

        void DrawModuleSelections()
        {
            if (GetSlotForModule(HighlightedModule, out SlotStruct highlighted))
            {
                DrawRectangleProjected(highlighted.WorldRect, Color.DarkOrange, 1.25f);
                if (IsSymmetricDesignMode && GetMirrorSlotStruct(highlighted, out SlotStruct mirrored))
                    DrawRectangleProjected(mirrored.WorldRect, Color.DarkOrange.Alpha(0.66f), 1.25f);
            }
        }

        void DrawProjectedModuleRect(SpriteBatch batch)
        {
            if (ProjectedSlot == null || ActiveModule == null)
                return;

            bool fits = ModuleGrid.ModuleFitsAtSlot(ProjectedSlot, ActiveModule);
            DrawRectangleProjected(ProjectedSlot.GetWorldRectFor(ActiveModule), fits ? Color.LightGreen : Color.Red, 1.5f);

            if (IsSymmetricDesignMode && GetMirrorSlot(ProjectedSlot, ActiveModule, out MirrorSlot mirrored))
            {
                bool mirrorFits = ModuleGrid.ModuleFitsAtSlot(mirrored.Slot, ActiveModule);
                DrawRectangleProjected(mirrored.Slot.GetWorldRectFor(ActiveModule), mirrorFits 
                    ? Color.LightGreen.Alpha(0.66f) : Color.Red.Alpha(0.66f), 1.5f);
            }
        }

        void DrawEmptySlots(SpriteBatch batch)
        {
            SubTexture concreteGlass = ResourceManager.Texture("Modules/tile_concreteglass_1x1");

            foreach (SlotStruct slot in ModuleGrid.SlotsList)
            {
                RectF rect = ProjectToScreenRectF(slot.WorldRect);

                if (slot.Module != null)
                {
                    batch.Draw(concreteGlass, rect, Color.Gray);
                }
                else if (slot.Root.Module == null)
                {
                    bool valid = ActiveModule == null || slot.CanSlotSupportModule(ActiveModule);
                    Color activeColor = valid ? Color.LightGreen : Color.Red;
                    batch.Draw(concreteGlass, rect, activeColor);

                    if (!HullEditMode && DesignedShip.PwrGrid.IsPowered(slot.Pos))
                    {
                        Color yellow = ActiveModule != null ? new Color(Color.Yellow, 150).Premultiplied() : Color.Yellow;
                        batch.Draw(concreteGlass, rect, yellow);
                    }

                    string r = slot.HullRestrict.ToString();
                    DrawStringProjected(slot.WorldPos + new Vector2(8), 8f, Color.Navy, r, Fonts.Arial20Bold);
                }
            }
        }

        void DrawModules(SpriteBatch batch)
        {
            if (HullEditMode)
                return;
            foreach (SlotStruct slot in ModuleGrid.SlotsList)
            {
                if (slot.Module != null && slot.Tex != null)
                {
                    if (slot.Module.ModuleType == ShipModuleType.PowerConduit)
                    {
                        // get the module from the design ship, this is not the same as
                        // ModuleGrid.SlotsList modules :(
                        ShipModule m = DesignedShip.GetModuleAt(slot.Pos);
                        if (m != null)
                        {
                            slot.Tex = m.Powered ? ResourceManager.Texture(m.IconTexturePath + "_power") : m.ModuleTexture;
                        }
                    }
                    DrawModuleTex(slot.Module.ModuleRot, batch, slot, slot.WorldRect);
                }
            }
        }

        void DrawModuleTex(ModuleOrientation orientation, SpriteBatch batch, SlotStruct slot,
                           RectF moduleWorldRect, ShipModule template = null, float alpha = 1)
        {
            SpriteEffects effects = SpriteEffects.None;
            SubTexture texture = slot != null ? slot.Tex : ResourceManager.Texture(template.IconTexturePath);
            float xSize = moduleWorldRect.W;
            float ySize = moduleWorldRect.H;
            float rotation = 0f;

            bool rotatedTexture = (template ?? slot.Module).GetOrientedModuleTexture(ref texture, orientation);

            switch (orientation)
            {
                case ModuleOrientation.Left when !rotatedTexture:
                    moduleWorldRect.W = ySize; // swap width & height
                    moduleWorldRect.H = xSize;
                    moduleWorldRect.Y += ySize;
                    rotation = -RadMath.HalfPI;
                    break;
                case ModuleOrientation.Right when !rotatedTexture:
                    moduleWorldRect.W = ySize; // swap width & height
                    moduleWorldRect.H = xSize;
                    moduleWorldRect.X += xSize;
                    rotation = RadMath.HalfPI;
                    break;
                case ModuleOrientation.Rear when !rotatedTexture:
                    effects = SpriteEffects.FlipVertically;
                    break;
                case ModuleOrientation.Normal:
                case ModuleOrientation.Left:
                case ModuleOrientation.Right:
                    if (slot?.WorldPos.X >= 0f && slot.Module.ModuleType != ShipModuleType.PowerConduit)
                        effects = SpriteEffects.FlipHorizontally;
                    break;
            }

            RectF screenRect = ProjectToScreenRectF(moduleWorldRect);
            batch.Draw(texture, screenRect, Color.White.Alpha(alpha), rotation, Vector2.Zero, effects, 1f);
        }

        void DrawTacticalOverlays(SpriteBatch batch)
        {
            // if ShowAllArcs is enabled, then we can accidentally render
            // tactical overlays twice. This helps avoid that
            var alreadyDrawn = new HashSet<SlotStruct>();

            void DrawTacticalOverlays(SlotStruct s)
            {
                if (s.ModuleUID == null || s.Tex == null || alreadyDrawn.Contains(s))
                    return;

                alreadyDrawn.Add(s);

                if (s.Module.ShieldPowerMax > 0f)
                    DrawCircleProjected(s.Center, s.Module.ShieldHitRadius, Color.LightGreen);

                if (s.Module.ModuleType == ShipModuleType.Turret && Input.LeftMouseHeld())
                {
                    DrawFireArcText(s);
                    if (IsSymmetricDesignMode)
                        ToolTip.ShipYardArcTip();
                }

                DrawHangarShipText(s.Module, s.WorldRect);
                DrawWeaponArcs(batch, s);

                if (IsSymmetricDesignMode && GetMirrorSlotStruct(s, out SlotStruct mirrored))
                {
                    DrawTacticalOverlays(mirrored);
                }
            }

            // we need to draw highlighted module first to get correct focus color
            foreach (SlotStruct s in ModuleGrid.SlotsList)
                if (s.Module == HighlightedModule)
                    DrawTacticalOverlays(s);

            if (ShowAllArcs) // draw all the rest
            {
                foreach (SlotStruct s in ModuleGrid.SlotsList)
                    DrawTacticalOverlays(s);
            }
        }

        void DrawUnpoweredTex(SpriteBatch batch)
        {
            if (HullEditMode)
                return;

            var unPowered = ResourceManager.Texture("UI/lightningBolt");
            foreach (SlotStruct slot in ModuleGrid.SlotsList)
            {
                ShipModule m = slot.Module;
                if (m == null || m == HighlightedModule && Input.LeftMouseHeld() && m.ModuleType == ShipModuleType.Turret)
                    continue;
                
                if (m.PowerDraw > 0f
                    && m.ModuleType != ShipModuleType.PowerConduit
                    && !DesignedShip.PwrGrid.IsPowered(slot.Pos))
                {
                    ProjectToScreenCoordsF(slot.Center, new Vector2(10), out Vector2 pos, out Vector2 size);
                    var screenRect = new RectF(pos - size/2, size);
                    batch.DrawDropShadowImage(screenRect, unPowered, Color.White);
                    batch.Draw(unPowered, screenRect, ApplyCurrentAlphaToColor(Color.Red));
                }
            }
        }

        void DrawFireArcText(SlotStruct slot)
        {
            Color fill = Color.Black.Alpha(0.33f);
            Color edge = (slot.Module == HighlightedModule) ? Color.DarkOrange : fill;
            DrawRectangleProjected(slot.WorldRect, edge, fill);
            DrawStringProjected(slot.Center, 0, 1, Color.Orange, slot.Module.TurretAngle.ToString());
        }

        void DrawHangarShipText(ShipModule m, in RectF worldRect)
        {
            if (m.ModuleType != ShipModuleType.Hangar)
                return;

            Color color = Color.Black.Alpha(0.33f);
            Color textC = ShipBuilder.GetHangarTextColor(m.HangarShipUID);
            DrawRectangleProjected(worldRect, textC, color);

            if (ResourceManager.GetShipTemplate(m.HangarShipUID, out Ship hangarShip))
            {
                DrawStringProjected(worldRect.Center, 6, textC, hangarShip.Name, Fonts.Arial20Bold, shadow:true, center:true);
            }
        }

        public void DrawWeaponArcs(SpriteBatch batch, SlotStruct slot)
        {
            Weapon w = slot.Module.InstalledWeapon;
            if (w != null)
            {
                DrawWeaponArcs(batch, this, w, slot.Module, slot.Center, 500f, 0f, slot.Module.TurretAngle);
            }
        }

        void DrawWeaponArcs(SpriteBatch batch, ShipModule module, Vector2 moduleWorldPos, float shipFacing, int turretAngle)
        {
            Weapon w = module.InstalledWeapon;
            if (w != null)
            {
                Vector2 moduleWorldCenter = moduleWorldPos + module.WorldSize * 0.5f;
                DrawWeaponArcs(batch, this, module.InstalledWeapon, ActiveModule, 
                               moduleWorldCenter, 500f, shipFacing, turretAngle);
            }
        }

        // @note This is reused in DebugInfoScreen as well
        public static void DrawWeaponArcs(SpriteBatch batch, GameScreen screen, Weapon w, ShipModule m,
                                          Vector2 moduleWorldCenter, float worldSize, float shipFacing, int turretAngle)
        {
            if (w.Tag_Bomb)
                return; 

            Color color;
            if (w.Tag_Cannon && !w.Tag_Energy)        color = new Color(255, 255, 0, 255);
            else if (w.Tag_Cannon)                    color = new Color(0, 255, 0, 255);
            else if (!w.IsBeam)                       color = new Color(255, 0, 0, 255);
            else                                      color = new Color(0, 0, 255, 255);

            screen.ProjectToScreenCoords(moduleWorldCenter, 0f, worldSize,
                                         out Vector2d posOnScreen, out double sizeOnScreen);
            Vector2 pos = posOnScreen.ToVec2f();
            float size = (float)sizeOnScreen;

            SubTexture arcTexture = ResourceManager.GetArcTexture(m.FieldOfFire.ToDegrees());

            var texOrigin = new Vector2(250f, 250f);
            var rect = new RectF(pos, new Vector2(size));

            float facing = shipFacing + ((float)turretAngle).ToRadians();
            batch.Draw(arcTexture, rect, color.Alpha(0.75f), facing, texOrigin, SpriteEffects.None, 1f);

            Vector2 direction = facing.RadiansToDirection();
            Vector2 start     = pos;
            Vector2 end = start + direction * size;
            batch.DrawLine(start, start.LerpTo(end, 0.45f), color.Alpha(0.25f), 3);

            end = start + direction * size;
            
            Vector2 textPos = start.LerpTo(end, 0.16f);
            float textRot   = facing + RadMath.HalfPI;
            Vector2 offset  = direction.RightVector() * 6f;
            if (direction.X > 0f)
            {
                textRot -= RadMath.PI;
                offset = -offset;
            }

            string rangeText = $"Range: {w.BaseRange.String(0)}";
            float textWidth  = Fonts.Arial8Bold.TextWidth(rangeText);
            batch.DrawString(Fonts.Arial8Bold, rangeText, textPos + offset, color.Alpha(0.3f),
                             textRot, new Vector2(textWidth / 2, 10f));
        }

        void DrawActiveModule(SpriteBatch batch)
        {
            ShipModule template = ResourceManager.GetModuleTemplate(ActiveModule.UID);

            Vector2 moduleWorldPos = CursorWorldPosition2D.Rounded();
            Vector2 moduleWorldSize = ActiveModule.WorldSize;
            var worldRect = new RectF(moduleWorldPos, moduleWorldSize);

            DrawModuleTex(ActiveModule.ModuleRot, batch, null, worldRect, template);
            DrawWeaponArcs(batch, ActiveModule, moduleWorldPos, 0, ActiveModule.TurretAngle);
            DrawHangarShipText(ActiveModule, worldRect);

            if (IsSymmetricDesignMode)
            {
                Vector2 mirrorWorldPos = GetMirrorWorldPos(moduleWorldPos, moduleWorldSize);
                if (!MirroredModulesTooClose(mirrorWorldPos, moduleWorldPos, moduleWorldSize))
                {
                    ModuleOrientation orientation = GetMirroredOrientation(ActiveModule.ModuleRot);

                    var mirrorWorldRect = new RectF(mirrorWorldPos, moduleWorldSize);
                    DrawModuleTex(orientation, batch, null, mirrorWorldRect, template, 0.5f);

                    int turretAngle = GetMirroredTurretAngle(ActiveModule.TurretAngle);
                    DrawWeaponArcs(batch, ActiveModule, mirrorWorldPos, 0f, turretAngle);
                    DrawHangarShipText(ActiveModule, mirrorWorldRect);
                }
            }

            if (ActiveModule.ShieldPowerMax > 0f)
            {
                DrawShieldCircle(template, moduleWorldPos, moduleWorldSize);
            }
        }

        void DrawShieldCircle(ShipModule moduleTemplate, Vector2 moduleWorldPos, Vector2 moduleWorldSize)
        {
            Vector2 moduleCenter = moduleWorldPos + moduleWorldSize*0.5f;
            DrawCircleProjected(moduleCenter, moduleTemplate.ShieldHitRadius, Color.LightGreen);

            if (IsSymmetricDesignMode)
            {
                Vector2 mirrorCenter = GetMirrorWorldPos(moduleWorldPos, moduleWorldSize) + moduleWorldSize*0.5f;
                DrawCircleProjected(mirrorCenter, moduleTemplate.ShieldHitRadius, Color.LightGreen.Alpha(0.5f));
            }
        }

        // TODO: Is this used anywhere?
        void DrawHullBonuses(ref Vector2 cursor, float cost)
        {
            HullBonus bonus = CurrentDesign.Bonuses;
            if (bonus.Hull.NotEmpty()) //Added by McShooterz: Draw Hull Bonuses
            {
                if (bonus.ArmoredBonus != 0 || bonus.ShieldBonus != 0
                    || bonus.SensorBonus != 0 || bonus.SpeedBonus != 0
                    || bonus.CargoBonus != 0 || bonus.DamageBonus != 0
                    || bonus.FireRateBonus != 0 || bonus.RepairBonus != 0
                    || bonus.CostBonus != 0)
                {
                    DrawString(cursor, Color.Orange, Localizer.Token(GameText.HullBonus), Fonts.Verdana14Bold);
                    cursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                }

                void HullBonus(ref Vector2 bCursor, float stat, in LocalizedText text)
                {
                    if (stat > 0 || stat < 0)
                        return;
                    DrawString(bCursor, Color.Orange, $"{stat * 100f}%  {text.Text}", Fonts.Verdana12);
                    bCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                }
                HullBonus(ref cursor, bonus.ArmoredBonus, GameText.ArmorProtection);
                HullBonus(ref cursor, bonus.ShieldBonus, "Shield Strength");
                HullBonus(ref cursor, bonus.SensorBonus, GameText.ArmorProtection);
                HullBonus(ref cursor, bonus.SpeedBonus, GameText.MaxSpeed);
                HullBonus(ref cursor, bonus.CargoBonus, GameText.CargoSpace2);
                HullBonus(ref cursor, bonus.DamageBonus, "Weapon Damage");
                HullBonus(ref cursor, bonus.FireRateBonus, GameText.FireRate);
                HullBonus(ref cursor, bonus.RepairBonus, GameText.RepairRate);
                HullBonus(ref cursor, bonus.CostBonus, GameText.CostReduction);
            }
        }

        // Ludoal fork: the label sits on its OWN dropdown rather than on a screen fraction - the two
        // used to be written separately and drifted apart the moment either moved. Arial12: these
        // are secondary options, not headings.
        //
        // To the LEFT of the field and centred on it, so the whole options row reads as one line
        // with the carrier-only checkbox. The label measures itself, which is what keeps it clear
        // of the field whatever the string.
        public const int TitleGap = 6;   // between a dropdown caption and its field

        // Ludoal fork: one source for each caption - the layout reserves its width from the same
        // string DrawTitle paints, so a reworded label cannot leave the row measured for the old one.
        public const string RepairCaption = "Repair";
        public const string HangarCaption = "Hangar Type";
        static void DrawTitle(SpriteBatch batch, in Rectangle dropdown, string title)
        {
            Graphics.Font font = Fonts.Arial12Bold;
            var pos = new Vector2(dropdown.X - font.TextWidth(title) - TitleGap,
                                  dropdown.CenterY() - font.LineSpacing / 2);
            // the panels' own label grey (maintainer bench 304) - orange read louder than
            // everything around it
            batch.DrawString(font, title, pos, new Color(168, 172, 178));
        }

        void DrawUi(SpriteBatch batch, DrawTimes elapsed)
        {
            EmpireUI.Draw(batch);
            CategoryList.Draw(batch, elapsed);

            // TODO: these should be split into separate parts
            DrawTitle(batch, new Rectangle((int)CategoryList.X, (int)CategoryList.Y,
                                          (int)CategoryList.Width, (int)CategoryList.Height), RepairCaption);
            DrawTitle(batch, new Rectangle((int)HangarOptionsList.X, (int)HangarOptionsList.Y,
                                          (int)HangarOptionsList.Width, (int)HangarOptionsList.Height), HangarCaption);
            HangarOptionsList.Draw(batch, elapsed);

            // Ludoal fork: the design's identity plates, drawn in the reworked screens' grammar
            // (dark fill, brass rule). They appear at their place rather than sliding in.
            var plate = new Color(14, 12, 9).Alpha(0.92f);
            var rule  = new Color(118, 102, 67, 255).Premultiplied();

            Rectangle r = DesignRoleRect;
            batch.FillRectangle(r, plate);
            batch.DrawRectangle(r, rule);
            var cursor = new Vector2(r.X + 8, r.Y + r.Height / 2 - Fonts.Arial20Bold.LineSpacing / 2);
            batch.DrawString(Fonts.Arial20Bold, Localizer.GetRole(Role, Player), cursor, Colors.Cream);

            r = SearchBar;
            batch.FillRectangle(r, plate);
            batch.DrawRectangle(r, rule);

            string name = DesignOrHullName;
            Graphics.Font font = Fonts.Arial20Bold.TextWidth(name) <= (SearchBar.Width - 10)
                               ? Fonts.Arial20Bold : Fonts.Arial12Bold;
            var cursor1 = new Vector2(r.X + 8, r.Y + r.Height / 2 - font.LineSpacing / 2);
            batch.DrawString(font, name, cursor1, ShipSaved || IsEmptyHull ? Color.White : Color.OrangeRed);
        }
    }
}
