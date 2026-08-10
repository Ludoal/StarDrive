using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Ship_Game.AI;
using Ship_Game.Fleets;
using Ship_Game.Ships;
using SDGraphics;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry

namespace Ship_Game
{
    public sealed partial class FleetDesignScreen
    {
        Color NeonGreen = new (0, 255, 0, 70);

        (Vector2 pos, float radius) GetPosAndRadiusOnScreen(Vector2 fleetOffset, float radius)
        {
            Vector2 pos1 = ProjectToScreenPos(new Vector3(fleetOffset, 0f));
            Vector2 pos2 = ProjectToScreenPos(new Vector3(fleetOffset.PointFromAngle(90f, radius), 0f));
            float radiusOnScreen = pos1.Distance(pos2) + 10f;
            return (pos1, radiusOnScreen);
        }

        Vector2 ProjectToScreenPos(in Vector3 worldPos)
        {
            var p = new Vector3(Viewport.Project(worldPos, Projection, View, Matrix.Identity));
            return new Vector2(p.X, p.Y);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.BeginFrameRendering(elapsed, ref View, ref Projection);

            // Ludoal fork: the starfield and the fleet grid are clipped to the tab frame - the
            // screen is one tab of the Design group now, so its scene stays inside the frame.
            // Scissor is device state: it goes off again before the UI pass, or the panels drawn
            // afterwards inherit the crop.
            RectF sceneClip = ScreenGroups.GroupSceneArea(DesignTabs.Rect, DesignTabs.ClientArea);
            Ship_Game.Graphics.RenderStates.EnableScissorTest(batch.GraphicsDevice, sceneClip);
            Universe.DrawStarField(ScreenManager.SpriteRenderer);

            batch.SafeBegin(SpriteBlendMode.AlphaBlend, Ship_Game.Graphics.RenderStates.ScissorEnabled);
            {
                DrawGrid(batch);
                DrawSelectedNodeSensorRange(batch);
                DrawHoveredNodes(batch);
                DrawSelectedNodes(batch);
                DrawFleetManagementIndicators(batch);
                // the node icons belong to the scene, so they clip with the grid (bench 343: they
                // spilled outside the frame when drawn in the unclipped UI pass)
                if (SelectedFleet != null)
                    foreach (FleetDataNode node in SelectedFleet.DataNodes)
                        DrawFleetNode(batch, node);

                // the placement brush belongs to the scene too (maintainer feedback): drawn in
                // the unclipped UI pass it followed the cursor over the side panels and past
                // the frame's edges
                if (ActiveShipDesign != null)
                    DrawActiveShipDesign(batch);

                if (SelectionBox.W > 0)
                    batch.DrawRectangle(SelectionBox, Color.Green);
            }
            batch.SafeEnd();

            // render 3D. Ludoal fork (maintainer bench 343): the 3D sprite and the tactical icon are
            // MUTUALLY EXCLUSIVE on the same zoom threshold (5000) - the sprite showed
            // unconditionally while the icon only showed past 5000, so above it BOTH drew and the
            // ships flickered. Below the threshold the sprite shows and the icon is held; above it
            // the sprite is removed and the icon draws.
            // ⚠ The scissor stays ENABLED across RenderSceneObjects (it is disabled AFTER, below) -
            // the Shipyard already renders its 3D under scissor; Fleets was disabling it too early,
            // so the fleet sprites spilled OUTSIDE the frame when the map was dragged (bench 343).
            if (SelectedFleet != null)
            {
                foreach (FleetDataNode node in SelectedFleet.DataNodes)
                {
                    Ship ship = node.Ship;
                    if (ship == null)
                        continue;
                    bool showSprite = CamPos.Z <= 5000f && !ship.Resupplying;
                    if (showSprite)
                    {
                        ship.RelativeFleetOffset = node.RelativeFleetOffset;
                        ship.ShowSceneObjectAt(ship.RelativeFleetOffset, 0);
                    }
                    else
                    {
                        ship.RemoveSceneObject();
                    }
                }
            }
            ScreenManager.RenderSceneObjects();
            Ship_Game.Graphics.RenderStates.DisableScissorTest(batch.GraphicsDevice);
            
            if (!Universe.IsExiting)
            {
                batch.SafeBegin();
                {
                    DrawUI(batch, elapsed);
                    base.Draw(batch, elapsed); // draw automatic elements on top of everything else
                }
                batch.SafeEnd();
            }

            ScreenManager.EndFrameRendering();
        }

        void DrawUI(SpriteBatch batch, DrawTimes elapsed)
        {
            if (Universe.IsExiting)
                return;

            ScreenGroups.DrawDesignTabTip(DesignTabs, Input.CursorPosition);

            EmpireUI.Draw(batch);

            // the fleet node icons and the placement brush live in the scissor-clipped scene
            // block (see Draw), so they stay inside the frame like the grid and the rings.

            DrawSelectedData(batch, elapsed);
        }

        void DrawFleetNode(SpriteBatch batch, FleetDataNode node)
        {
            Ship ship = node.Ship;
            // if ship doesn't exist, grab a template instead
            if (ship == null || CamPos.Z <= 15000f)
                if (!ResourceManager.GetShipTemplate(node.ShipName, out ship))
                    return;

            float radius = node.Ship?.Radius ?? ship.Radius;
            (Vector2 screenPos, float screenR) = GetPosAndRadiusOnScreen(node.RelativeFleetOffset, radius);
            if (screenR < 10f) screenR = 10f;
            RectF r = RectF.FromPointRadius(screenPos, screenR*0.5f);

            Color color = GetTacticalIconColor(node);
            DrawIcon(batch, node, ship, r, color);

            if (node.Ship?.Resupplying == true)
            {
                batch.DrawString(Fonts.Arial8Bold, "Resupplying", screenPos + new Vector2(5f, -5f), Color.White);
            }
            else if (node.Goal != null)
            {
                string buildingAt = "";
                foreach (Goal g in SelectedFleet.Owner.AI.Goals)
                {
                    if (g != node.Goal || g.PlanetBuildingAt == null)
                        continue;

                    buildingAt = g.Type == GoalType.Refit
                        ? $"Refitting at:\n{g.PlanetBuildingAt.Name}"
                        : $"Building at:\n{g.PlanetBuildingAt.Name}";
                }

                if (buildingAt.IsEmpty())
                    buildingAt = "Need spaceport";

                batch.DrawString(Fonts.Arial8Bold, buildingAt, screenPos + new Vector2(5f, -5f), Color.White);
            }
        }

        Color GetTacticalIconColor(FleetDataNode node)
        {
            if (HoveredNodeList.Contains(node) || SelectedNodeList.Contains(node))
                return Color.White;
            if (node.Goal != null) return Color.Yellow;
            if (node.Ship?.Resupplying == true) return Color.Gray;

            return node.Ship != null ? Color.Green : Color.Red;
        }

        // this is the active ship or ship template that we're trying to place
        // into the fleet
        void DrawActiveShipDesign(SpriteBatch batch)
        {
            float radius = (float)ProjectToScreenSize(ActiveShipDesign.Radius);
            RectF screenR = RectF.FromPointRadius(Input.CursorPosition, radius);

            TacticalIcon icon = ActiveShipDesign.TacticalIcon();
            icon.Draw(batch, screenR, Player.EmpireColor);

            float boundingR = Math.Max(radius*1.5f, 16);
            DrawCircle(Input.CursorPosition, boundingR, Player.EmpireColor);
        }

        void DrawIcon(SpriteBatch batch, FleetDataNode node, Ship ship, in RectF r, Color color)
        {
            if (CamPos.Z > 5000f || node.Ship == null || node.Ship?.Resupplying == true)
            {
                TacticalIcon icon = ship.TacticalIcon();
                icon.Draw(batch, r, color);
            }
        }

        void DrawSelectedNodes(SpriteBatch batch)
        {
            foreach (FleetDataNode node in SelectedNodeList)
            {
                (Vector2 screenPos, float screenR) = GetNodeScreenPosAndRadius(node);
                foreach (ClickableSquad squad in ClickableSquads)
                    if (squad.Squad.DataNodes.Contains(node))
                        batch.DrawLine(squad.Rect.Center, screenPos, NeonGreen, 2f);

                DrawCircle(screenPos, screenR, Color.White, 2f);
            }
        }

        void DrawHoveredNodes(SpriteBatch batch)
        {
            foreach (FleetDataNode node in HoveredNodeList)
            {
                (Vector2 screenPos, float screenRadius) = GetNodeScreenPosAndRadius(node);
                foreach (ClickableSquad squad in ClickableSquads)
                    if (squad.Squad.DataNodes.Contains(node))
                        batch.DrawLine(squad.Rect.Center, screenPos, NeonGreen, 2f);
                DrawCircle(screenPos, screenRadius, new(255, 255, 255, 70), 2f);
            }
        }

        void DrawSelectedNodeSensorRange(SpriteBatch batch)
        {
            if (SelectedNodeList.Count == 1)
            {
                SubTexture nodeTexture = ResourceManager.Texture("UI/node");
                FleetDataNode node = SelectedNodeList[0];

                // The Operational Radius slider's relative/absolute semantic
                // is currently inconsistent with how OrdersRadius is stored
                // elsewhere in Fleet.cs (absolute world units). Sidestep that
                // for now and just visualize the ship's actual SensorRange
                // until the slider is reworked.
                //
                // In the Fleet Design screen, node.Ship is usually null —
                // designs are templates, not live instances. Fall back to the
                // ship template (same pattern as DrawFleetNode) so different
                // designs show different sensor halos.
                Ship ship = node.Ship;
                if (ship == null && !ResourceManager.GetShipTemplate(node.ShipName, out ship))
                    return;
                float radius = ship.SensorRange;
                (Vector2 screenPos, float screenRadius) = GetPosAndRadiusOnScreen(node.RelativeFleetOffset, radius);
                RectF nodeRect = new(screenPos, screenRadius * 2, screenRadius * 2);

                // UI/node is stored non-premultiplied (most consumers use
                // additive / SourceAlphaSaturation blends). Default
                // SpriteBatch.AlphaBlend is the premul formula in MonoGame
                // (dst = src.rgb + dst*(1-srcA)) — so an alpha=0 outer ring
                // leaks its RGB into the destination. With the rect sized to
                // ship sensor range projected to screen (often larger than
                // the viewport) that ring floods the whole screen green.
                // Switch this single draw to Additive (src*srcA + dst), which
                // zeroes out alpha=0 pixels naturally and matches how the
                // FOW / shield / fogmap consumers handle the same texture.
                // bench 358 (maintainer): keep the SCISSOR through the blend switch. This runs inside
                // the scene-clipped batch; re-opening without the scissored rasterizer let the halo
                // flood past the frame - and worse, the final plain Begin handed the rest of the
                // scene pass back UNCLIPPED.
                batch.SafeEnd();
                batch.SafeBegin(SpriteBlendMode.Additive, Ship_Game.Graphics.RenderStates.ScissorEnabled);
                batch.Draw(nodeTexture, nodeRect, NeonGreen, 0f, nodeTexture.CenterF);
                batch.SafeEnd();
                batch.SafeBegin(SpriteBlendMode.AlphaBlend, Ship_Game.Graphics.RenderStates.ScissorEnabled);
            }
        }

        void DrawFleetManagementIndicators(SpriteBatch batch)
        {
            Vector2 pPos = ProjectToScreenPos(Vector3.Zero);
            batch.FillRectangle(new(pPos.X - 3, pPos.Y - 3, 6, 6), new(255, 255, 255, 80));

            float textW = Fonts.Arial12Bold.TextWidth("Fleet Center");
            batch.DrawString(Fonts.Arial12Bold, "Fleet Center", new(pPos.X - textW / 2f, pPos.Y + 5f), new Color(255, 255, 255, 70).Premultiplied());

            // draw squad node markers
            float squadTextW = Fonts.Arial10.TextWidth("Squad");
            foreach (ClickableSquad squad in ClickableSquads)
            {
                bool isSelected = SelectedSquad == squad.Squad;
                Color squadNode = isSelected ? Color.Yellow : NeonGreen;

                batch.FillRectangle(RectF.FromCenter(squad.Rect.Center, 4, 4), new(0, 255, 0, 110));
                batch.DrawRectangle(squad.Rect, squadNode);
                batch.DrawString(Fonts.Arial10, "Squad", new(squad.Rect.CenterX - squadTextW / 2f, squad.Rect.Bottom + 5f), NeonGreen);
            }
        }

        void DrawGrid(SpriteBatch batch)
        {
            int size = 20000;
            for (int x = 0; x < 21; x++)
            {
                float wx = x * size / 20 - size / 2;
                Vector2 origin = ProjectToScreenPos(new(wx, -(size / 2), 0));
                Vector2 end = ProjectToScreenPos(new(wx, size - size / 2, 0));
                batch.DrawLine(origin, end, new(211, 211, 211, 70));
            }
            for (int y = 0; y < 21; y++)
            {
                float wy = y * size / 20 - size / 2;
                Vector2 origin = ProjectToScreenPos(new(-(size / 2), wy, 0));
                Vector2 end = ProjectToScreenPos(new(size - size / 2, wy, 0));
                batch.DrawLine(origin, end, new(211, 211, 211, 70));
            }
        }

        void DrawSelectedData(SpriteBatch batch, DrawTimes elapsed)
        {
            RequisitionForces.Visible = false;
            SaveDesign.Visible = false;
            LoadDesign.Visible = false;
            AutoArrange.Visible = false;


            if (SelectedNodeList.Count == 1)
            {
                StuffSelector = new Selector(SelectedStuffRect, new Color(0, 0, 0, 180));
                StuffSelector.Draw(batch, elapsed);
                FleetDataNode node = SelectedNodeList[0];
                Ship ship = node.Ship;
                string text = ship == null ? $"({node.ShipName})"
                            : ship.VanityName.NotEmpty()
                                ? ship.VanityName : $"{ship.Name} ({ship.ShipData.Role})";
                // centred on the cartouche, like the fleet name (maintainer bench 302)
                var cursor = new Vector2(
                    SelectedStuffRect.X + (SelectedStuffRect.W - Fonts.Arial20Bold.TextWidth(text)) * 0.5f,
                    SelectedStuffRect.Y + 10);
                batch.DrawString(Fonts.Arial20Bold, text, cursor, Colors.Cream);

                if (ShowTargetingPanels)
                {
                    cursor.Y = OperationsRect.Y + 10;
                    batch.DrawString(Fonts.Pirulen12, "Movement Orders", cursor, Colors.Cream);

                    OperationsSelector = new Selector(OperationsRect, new Color(0, 0, 0, 180));
                    OperationsSelector.Draw(batch, elapsed);
                    cursor = new Vector2(OperationsRect.X + 20, OperationsRect.Y + 10);
                    batch.DrawString(Fonts.Pirulen12, "Target Selection", cursor, Colors.Cream);
                    SliderArmor.Draw(batch);
                    SliderAssist.Draw(batch);
                    SliderDefend.Draw(batch);
                    SliderDps.Draw(batch);
                    SliderShield.Draw(batch);
                    SliderVulture.Draw(batch);
                    PrioritySelector = new Selector(PrioritiesRect, new Color(0, 0, 0, 180));
                    PrioritySelector.Draw(batch, elapsed);
                    cursor = new Vector2(PrioritiesRect.X + 20, PrioritiesRect.Y + 10);
                    batch.DrawString(Fonts.Pirulen12, "Priorities", cursor, Colors.Cream);
                    OperationalRadius.Draw(batch, elapsed);
                    SliderSize.Draw(ScreenManager);
                }
            }
            else if (SelectedNodeList.Count > 1)
            {
                StuffSelector = new Selector(SelectedStuffRect, new Color(0, 0, 0, 180));
                StuffSelector.Draw(batch, elapsed);
                var cursor = new Vector2(SelectedStuffRect.X + 20, SelectedStuffRect.Y + 10);

                batch.DrawString(Fonts.Arial20Bold, $"Group of {SelectedNodeList.Count} ships selected", cursor, Colors.Cream);

                if (ShowTargetingPanels)
                {
                    cursor.Y = OperationsRect.Y + 10;
                    batch.DrawString(Fonts.Pirulen12, "Group Movement Orders", cursor, Colors.Cream);

                    OperationsSelector = new Selector(OperationsRect, new Color(0, 0, 0, 180));
                    OperationsSelector.Draw(batch, elapsed);
                    cursor = new Vector2(OperationsRect.X + 20, OperationsRect.Y + 10);
                    batch.DrawString(Fonts.Pirulen12, "Group Target Selection", cursor, Colors.Cream);
                    SliderArmor.Draw(batch);
                    SliderAssist.Draw(batch);
                    SliderDefend.Draw(batch);
                    SliderDps.Draw(batch);
                    SliderShield.Draw(batch);
                    SliderVulture.Draw(batch);
                    PrioritySelector = new Selector(PrioritiesRect, new Color(0, 0, 0, 180));
                    PrioritySelector.Draw(batch, elapsed);
                    cursor = new Vector2(PrioritiesRect.X + 20, PrioritiesRect.Y + 10);
                    batch.DrawString(Fonts.Pirulen12, "Group Priorities", cursor, Colors.Cream);
                    OperationalRadius.Draw(batch, elapsed);
                    SliderSize.Draw(ScreenManager);
                }
            }
            else
            {
                StuffSelector = new Selector(SelectedStuffRect, new Color(0, 0, 0, 180));
                StuffSelector.Draw(batch, elapsed);

                Fleet f = SelectedFleet;
                if (f == null)
                    return;

                // name, then the icon directly under it, then the buttons - the label the icon
                // used to carry said nothing the icon does not.
                // centred on the cartouche, in the headline font (maintainer bench 301)
                FleetNameEntry.Text = f.Name;
                Vector2 cursor1 = new Vector2(
                    SelectedStuffRect.X + (SelectedStuffRect.W - FleetNameEntry.Font.TextWidth(f.Name)) * 0.5f,
                    SelectedStuffRect.Y + CartPad);
                FleetNameEntry.SetPos(cursor1);
                FleetNameEntry.Draw(batch, elapsed);

                float iconY = cursor1.Y + Fonts.Arial20Bold.LineSpacing + 8;
                var iconR = new RectF(SelectedStuffRect.X + (SelectedStuffRect.W - CartIcon) * 0.5f,
                                      iconY, CartIcon, CartIcon);
                batch.Draw(f.Icon, iconR, f.Owner.EmpireColor);
                // the Fleet Design Overview cartouche is gone (maintainer bench 300): its
                // text lives in the Codex, end of Warfare

                RequisitionForces.Visible = true;
                SaveDesign.Visible = true;
                LoadDesign.Visible = true;
                AutoArrange.Visible = f.Ships.Count > 0;
            }
        }
    }
}
