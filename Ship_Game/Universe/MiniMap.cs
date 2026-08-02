using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Audio;
using Ship_Game.Empires.Components;
using Ship_Game.Gameplay;
using Ship_Game.Ships;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe.SolarBodies;

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    public sealed class MiniMap : UIElementContainer
    {
        readonly ToggleButton ExoticBonuses;
        readonly ToggleButton FreighterUtil;

        readonly UniverseScreen Universe;
        readonly Rectangle Housing;
        Rectangle ActualMap;
        // Ludoal fork: the two zoom buttons are gone - Page Up / Page Down and the wheel already
        // do it, and zoom-to-ship belongs with the ship. Important Events has its own Galaxy tab.
        readonly ToggleButton InfluenceZones;   // Ludoal fork (F4)
        readonly ToggleButton GravityWellsOnly; // Ludoal fork (F5)
        readonly ToggleButton GravityWells;
        readonly ToggleButton AIScreen;
        readonly ToggleButton DeepSpaceBuild;
        readonly ToggleButton RangeOverley;
        readonly ToggleButton VisionOverlayBtn; // Ludoal fork: F3 vision overlay

        readonly SubTexture Node;
        readonly SubTexture Node1;
        readonly float Scale;
        readonly Vector2 MiniMapZero;
        Empire Player => Universe.Player;
        float pulseTime => Universe.NormalFlashTimer;
        float quickPulseTime => Universe.FastFlashTimer;

        public MiniMap(UniverseScreen universe, in Rectangle housing) : base(housing)
        {
            Universe       = universe;
            Housing        = housing;
            Node           = ResourceManager.Texture("UI/node");
            Node1          = ResourceManager.Texture("UI/node1");
            // Ludoal fork: the map fills the housing except for the two button bands - one ABOVE
            // it, one to its LEFT - plus the frame's own margin. Constants, not fractions of the
            // housing: a button is 25x22 whatever the box does, so the bands cannot be a ratio.
            const int BtnW = 25, BtnH = 22, BandGap = 4, Edge = 6;
            ActualMap = new Rectangle(housing.X + BtnW + BandGap + Edge,
                                      housing.Y + BtnH + BandGap + Edge,
                                      housing.Width  - (BtnW + BandGap + Edge) - Edge,
                                      housing.Height - (BtnH + BandGap + Edge) - Edge);

            // ── the two bands (Ludoal fork, maintainer layout) ──────────────────────────────
            // Two families, and the gap between them is what says so: an OVERLAY toggles a
            // rendering on the map and stays lit; a TAB pops a panel at a screen edge. They used
            // to wear three texture styles between them and sit in two arbitrary columns.
            //
            // TOP band, horizontal:  [Influence Vision Subspace]  gap  [AI DSB]
            // LEFT band, vertical:   [Gravity Range TradeRoutes]  gap  [Freighters Exotic]
            //
            // The tabs are placed by where their panel COMES OUT: AI and DSB are temporary and
            // open at the right edge, so they sit top-right; Freighters and Exotic are left open
            // for monitoring and come out at the bottom, so they sit bottom-left.
            const int Gap = 14; // the family separator - wider than the 2px a list would use

            UIList topOverlays = AddList(new Vector2(ActualMap.X, Housing.Y + Edge));
            topOverlays.Name = "MiniMapOverlaysTop";
            topOverlays.LayoutStyle = ListLayoutStyle.ResizeList;
            topOverlays.Direction = new Vector2(1, 0); // horizontal
            InfluenceZones = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/flagicon", InfluenceZones_OnClick)); // F2
            VisionOverlayBtn = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_spy_small", VisionOverlay_OnClick)); // F3
            GravityWells = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_ftloverlay", GravityWells_OnClick)); // subspace projectors (F4)

            UIList topTabs = AddList(new Vector2(ActualMap.X + 3 * BtnW + Gap, Housing.Y + Edge));
            topTabs.Name = "MiniMapTabsTop";
            topTabs.LayoutStyle = ListLayoutStyle.ResizeList;
            topTabs.Direction = new Vector2(1, 0);
            AIScreen = topTabs.Add(new ToggleButton(ToggleButtonStyle.Button, "AI", AIScreen_OnClick));
            DeepSpaceBuild = topTabs.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_dsbw", DeepSpaceBuild_OnClick));

            UIList leftOverlays = AddList(new Vector2(Housing.X + Edge, ActualMap.Y));
            leftOverlays.Name = "MiniMapOverlaysLeft";
            leftOverlays.LayoutStyle = ListLayoutStyle.ResizeList;
            GravityWellsOnly = leftOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/node_inhibit", GravityWellsOnly_OnClick)); // F5
            RangeOverley = leftOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_rangeoverlay", RangeOverly_OnClick)); // F6
            // (a Trade Routes overlay belongs here, under Gravity Wells and near Freighters)

            UIList leftTabs = AddList(new Vector2(Housing.X + Edge, ActualMap.Y + 3 * BtnH + Gap));
            leftTabs.Name = "MiniMapTabsLeft";
            leftTabs.LayoutStyle = ListLayoutStyle.ResizeList;
            FreighterUtil = leftTabs.Add(new ToggleButton(ToggleButtonStyle.Button, "NewUI/icon_freighter_util", FreighterUtilizationScreen_OnClick));
            ExoticBonuses = leftTabs.Add(new ToggleButton(ToggleButtonStyle.Button, "NewUI/icon_exotic_Bonuses_big", ExoticBonusScreen_OnClick));
            // ⚠ the SMALLER side, not the width: the galaxy is square, so scaling on the long
            // edge would push it past the short one. It happened to work while the map was
            // taller than it was wide; the reworked frame makes it wider than tall.
            int shortSide = ActualMap.Width < ActualMap.Height ? ActualMap.Width : ActualMap.Height;
            Scale = shortSide / (Universe.UState.Size * 2.1f); // negative map values are fine
            // ⚠ the map's CENTRE, derived - the old +100/+100 was half of a 200x210 map and would
            // put the origin off-centre now that the frame gives the map its full width.
            MiniMapZero = new Vector2(ActualMap.X + ActualMap.Width * 0.5f,
                                      ActualMap.Y + ActualMap.Height * 0.5f);
        }

        Vector2 WorldToMiniPos(Vector2 pos)
            => new Vector2(MiniMapZero.X + pos.X * Scale, MiniMapZero.Y + pos.Y * Scale);

        float WorldToMiniRadius(float radius)
        {
            float miniRadius = radius * Scale;
            float rscale = miniRadius * 0.004f;
            rscale = Math.Max(0.006f, rscale);
            return rscale;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Visible)
                return;

            // Ludoal fork: a plain frame instead of the brass radar housing (maintainer decision).
            // That texture spent 81px on the left and 33 on top being decorative, which is why
            // the map itself was a 200x210 island inside a 276x256 box. The frame is a rule and
            // a fill now, so the map gets the room back.
            Rectangle inflateMap = ActualMap;
            inflateMap.Inflate(4, 4);
            batch.FillRectangle(inflateMap, new Color(8, 8, 8).Alpha(0.85f));
            batch.DrawRectangle(inflateMap, GameScreens.ReworkScreens.FrameRule);
            
            foreach (SolarSystem system in Universe.UState.Systems)
            {
                Vector2 miniSystemPos = WorldToMiniPos(system.Position);
                var star = new Rectangle((int)miniSystemPos.X, (int)miniSystemPos.Y, 2, 2);
                batch.FillRectangle(star, Color.Gray);

                // Ludoal fork: colonized systems wear a ring in their owner's color
                // (grey when contested by several empires; white is the Ralyeh color). Fog respected.
                if (system.OwnerList.Count > 0 && system.IsExploredBy(Player))
                {
                    Color ring = Color.Gray; // Ludoal fork: contested = grey, white belongs to the Ralyeh
                    if (system.OwnerList.Count == 1)
                    {
                        foreach (Empire owner in system.OwnerList)
                            ring = owner.EmpireColor;
                    }
                    batch.DrawCircle(new Vector2(miniSystemPos.X + 1f, miniSystemPos.Y + 1f), 4f, ring);
                }
            }

            try
            {
                // UI/node1 is stored non-premultiplied in the §4.6 #7
                // lossless-alpha cache. The colored Node1 draws here leak
                // through alpha=0 outer pixels under MonoGame's default
                // premul AlphaBlend, which makes the nodes appear as
                // hard-edged blobs instead of soft gradients. NonPremultiplied
                // is the XNA-classic AlphaBlend formula (src*srcA + dst*(1-srcA))
                // — alpha=0 contributes 0 (gradient preserved) and overlapping
                // nodes blend smoothly without the Additive over-brightness.
                batch.SafeEnd();
                batch.SafeBegin(SpriteBlendMode.NonPremultiplied);

                // Ludoal fork: minimap influence follows the main map — with the
                // influence zones follow the influence overlay (F2)
                if (Universe.ShowingInfluenceOverlay)
                    DrawMinimapInfluenceNodes(batch);
                DrawSelected(batch, Player);
                DrawWarnings(batch);

                batch.SafeEnd();
                batch.SafeBegin();
            }
            catch (Exception e)
            {
                Log.Error(e, $"MiniMap Draw crashed {e.InnerException}");
            }

            Vector2 upperLeftView = Universe.UnprojectToWorldPosition(new Vector2(0f, 0f));
            upperLeftView = new Vector2(HelperFunctions.RoundTo(upperLeftView.X, 1), HelperFunctions.RoundTo(upperLeftView.Y, 1));
            
            var right = Universe.UnprojectToWorldPosition(new Vector2(Universe.ScreenWidth, 0f));

            right = new Vector2(HelperFunctions.RoundTo(right.X, 1), 0f);
            
            float xdist = (right.X - upperLeftView.X) * Scale;
            xdist = HelperFunctions.RoundTo(xdist, 1);

            float ydist = xdist * Universe.ScreenHeight / Universe.ScreenWidth;
            ydist = HelperFunctions.RoundTo(ydist, 1);
            // draw and clamp minimap viewing area rectangle.
            var lookingAt = new Rectangle((int)MiniMapZero.X + (int)(upperLeftView.X * Scale), 
                                          (int)MiniMapZero.Y + (int)(upperLeftView.Y * Scale),
                                          (int)xdist, (int)ydist);
            if (lookingAt.Width < 2)
            {
                lookingAt.Width  = 2;
                lookingAt.Height = 2;
            }
            float lookRightEdge = lookingAt.X;
            float lookBottomEdge = lookingAt.Y;

            lookingAt.X = (int)lookRightEdge.UpperBound(ActualMap.X + ActualMap.Width - lookingAt.Width);
            lookingAt.Y = (int)lookBottomEdge.UpperBound(ActualMap.Height + ActualMap.Y - lookingAt.Height);
            lookingAt.X = (int)lookingAt.X.LowerBound(ActualMap.X);
            lookingAt.Y = (int)lookingAt.Y.LowerBound(ActualMap.Y);

            batch.FillRectangle(lookingAt, new Color(255, 255, 255, 30).Premultiplied());
            batch.DrawRectangle(lookingAt, Color.White);
            var topMiddleView   = new Vector2(lookingAt.X +  lookingAt.Width / 2, lookingAt.Y);
            var botMiddleView   = new Vector2(topMiddleView.X - 1f, lookingAt.Y + lookingAt.Height);
            var leftMiddleView  = new Vector2(lookingAt.X, lookingAt.Y + lookingAt.Height / 2);
            var rightMiddleView = new Vector2(lookingAt.X + lookingAt.Width, leftMiddleView.Y + 1f);
            batch.DrawLine(new Vector2(topMiddleView.X, MiniMapZero.Y - 100), topMiddleView, Color.White);
            batch.DrawLine(new Vector2(botMiddleView.X, ActualMap.Y + ActualMap.Height), botMiddleView, Color.White);
            batch.DrawLine(new Vector2(ActualMap.X, leftMiddleView.Y), leftMiddleView, Color.White);
            batch.DrawLine(new Vector2(ActualMap.X + ActualMap.Width, rightMiddleView.Y), rightMiddleView, Color.White);

            GravityWells.IsToggled     = Universe.ShowingFTLOverlay;
            DeepSpaceBuild.IsToggled = Universe.DeepSpaceBuildWindow.Visible;
            AIScreen.IsToggled       = Universe.aw.IsOpen;
            ExoticBonuses.IsToggled  = Universe.ExoticBonusesWindow.IsOpen;
            FreighterUtil.IsToggled =  Universe.FreighterUtilizationWindow.IsOpen;

            RangeOverley.IsToggled         = Universe.ShowingRangeOverlay;
            InfluenceZones.IsToggled       = Universe.ShowingInfluenceOverlay;   // Ludoal fork (F4)
            GravityWellsOnly.IsToggled     = Universe.ShowingGravityWellOverlay; // Ludoal fork (F5)
            // Ludoal fork (bench): Vision was missing from this list, so its button only lit up
            // when clicked — pressing F3 turned the overlay on with the button still dark (maintainer feedback).
            VisionOverlayBtn.IsToggled     = Universe.ShowingVisionOverlay;      // Ludoal fork (F3)
            
            base.Draw(batch, elapsed);
        }

        void DrawWarnings(SpriteBatch batch)
        {
            float radius = 0.02f;
            float ringRad = 0.023f * pulseTime;
            foreach (IncomingThreat threat in Player.SystemsWithThreat)
            {
                DrawThreats(Color.Red, threat);
            }

            foreach (IncomingThreat threat in Player.AlliedSystemsWithThreat())
            {
                DrawThreats(Color.Orange, threat);
            }

            foreach (var system in Player.AI.ThreatMatrix.GetAllSystemsWithFactions())
            {
                if (system.OwnerList.Count > 0) continue;
                var point = WorldToMiniPos(system.Position);
                radius = 0.025f * Universe.SlowFlashTimer;
                batch.Draw(Node1, point, Color.Black, 0f, Node.CenterF, radius, SpriteEffects.None, 1f);
                batch.Draw(Node1, point, Color.Yellow, 0f, Node.CenterF, radius - 0.0055f, SpriteEffects.None, 1f);
                batch.Draw(Node1, point, Color.Black, 0f, Node.CenterF, radius - 0.0055f * 2, SpriteEffects.None, 1f);
            }

            foreach (ThreatCluster c in Player.AI.ThreatMatrix.GetAllFactionBases())
            {
                var point = WorldToMiniPos(c.Position);
                radius = 0.025f * Universe.SlowFlashTimer;
                var warningColor = new Color(Color.Yellow, 200).Premultiplied();
                batch.Draw(Node1, point, warningColor, 0f, Node.CenterF, radius, SpriteEffects.None, 1f);
                batch.Draw(Node1, point, Color.Black, 0f, Node.CenterF, radius - 0.005f, SpriteEffects.None, 1f);
                batch.Draw(Node1, point, c.Loyalty.EmpireColor, 0f, Node.CenterF, 0.012f, SpriteEffects.None, 1f);
            }

            void DrawThreats(Color color, IncomingThreat threat)
            {
                var system = threat.TargetSystem;
                Vector2 miniSystemPos = WorldToMiniPos(system.Position);
                float pulseRad = radius + ringRad;
                batch.Draw(Node1, miniSystemPos, color, 0f, Node.CenterF, pulseRad + 0.009f, SpriteEffects.None, 0f);
                batch.Draw(Node1, miniSystemPos, Color.Black, 0f, Node.CenterF, pulseRad + 0.002f, SpriteEffects.None, 0f);
                batch.Draw(Node1, miniSystemPos, color, 0f, Node.CenterF, radius, SpriteEffects.None, 0f);
            }
        }

        void DrawSelected(SpriteBatch batch, Empire empire)
        {
            Ship ship     = Universe.SelectedShip;
            Planet planet = Universe.SelectedPlanet;
            var system    = Universe.SelectedSystem;
            var fleet     = Universe.SelectedFleet;
            Ship[] selectedShips = Universe.SelectedShips.ToArr();

            Array<Vector2> centers = new();

            if (ship != null) centers.Add(ship.Position);
            else if (planet != null) centers.Add(planet.Position);
            else if (system != null) centers.Add(system.Position);
            else if (fleet != null) centers = new(fleet.Ships.Select(s=> s.Position));
            else if (selectedShips.Length > 0)  centers = new(selectedShips.Select(s => s.Position));

            float radius = 0.023f;
            foreach (var center in centers)
            {
                var nodePos = WorldToMiniPos(center);

                batch.Draw(Node1, nodePos, new Color(Color.Black, (byte)(255 * quickPulseTime)), 0f, Node.CenterF, radius, SpriteEffects.None, 1f);
                batch.Draw(Node1, nodePos, new Color(Color.LightGray, (byte)(255 * quickPulseTime)), 0f, Node.CenterF, radius * quickPulseTime, SpriteEffects.None, 1f);
            }
        }

        void DrawMinimapNodes(SpriteBatch batch, Empire empire,
                              Empire.InfluenceNode[] nodes, bool excludeProjectors)
        {
            Vector2 nodeOrigin = Node.CenterF;
            // Halo alpha kept low (~30%) so overlapping nodes stay readable under
            // NonPremultiplied blend — at higher alpha the territory layer would
            // saturate the sensor halos underneath into a single uniform blob.
            // The empire-color halo and black darkening overlay scale with the
            // border-strength slider, but floored at 0.5 so the minimap never
            // loses empire-territory readability even when the universe screen
            // borders are dimmed all the way down.
            byte haloAlpha = (byte)(80 * GlobalStats.InfluenceNodeAlpha.LowerBound(0.5f));
            var transparentBlack = new Color(Color.Black, haloAlpha);

            for (int i = 0; i < nodes.Length; i++)
            {
                ref Empire.InfluenceNode node = ref nodes[i];
                if (!node.KnownToPlayer)
                    continue;

                bool combat = false;
                float intensity = 0.005f;
                var ec = new Color(empire.EmpireColor, haloAlpha);
                if (empire.isPlayer)
                {
                    if (node.Source is Ship ship)
                    {
                        if (ship.Loyalty != empire) // ignore allied nodes, they are drawn in their own loop
                            continue;
                        if (excludeProjectors && ship.IsSubspaceProjector)
                            continue;
                        if (empire.isPlayer && ship.OnHighAlert)
                            combat = true;
                    }
                    else if (node.Source is Planet planet)
                    {
                        if (planet.Owner != empire) // ignore allied nodes, they are drawn in their own loop
                            continue;
                        if (planet.RecentCombat)
                        {
                            combat = true;
                            intensity += 0.001f;
                        }
                        else if (planet.SpaceCombatNearPlanet)
                        {
                            combat = true;
                        }
                    }
                }

                float nodeRad = WorldToMiniRadius(node.Radius);
                Vector2 nodePos = WorldToMiniPos(node.Position);
                
                if (combat)
                {
                    float radius = Math.Max(0.02f, nodeRad) * pulseTime;
                    batch.Draw(Node1, nodePos, Color.Black, 0f, nodeOrigin, radius - intensity, SpriteEffects.None, 0f);
                    batch.Draw(Node1, nodePos, Color.Red,   0f, nodeOrigin, radius, SpriteEffects.None, 0f);
                    batch.Draw(Node1, nodePos, Color.Black, 0f, nodeOrigin, radius - intensity * 2, SpriteEffects.None, 0f);
                }
                
                {
                    float radius = Math.Min(0.09f, nodeRad);
                    // Empire-colored gradient halo + black darkening overlay; both
                    // alphas scaled by the border-color-strength slider above.
                    batch.Draw(Node1, nodePos, ec, 0f, nodeOrigin, radius, SpriteEffects.None, 1f);
                    batch.Draw(Node1, nodePos, transparentBlack, 0f, nodeOrigin, nodeRad, SpriteEffects.None, 1f);
                }
            }
        }

        void DrawMinimapInfluenceNodes(SpriteBatch batch)
        {
            UniverseState uState = Universe.UState;
            for (int i = 0; i < uState.Empires.Count; i++)
            {
                Empire e = uState.Empires[i];
                // Draw player nodes last so it will be over allied races
                if (e.isPlayer)
                    continue;

                Relationship rel = uState.Player.GetRelations(e);
                if (rel.Known || Universe.Debug)
                {
                    DrawMinimapEmpireNodes(batch, e);
                }
            }

            DrawMinimapEmpireNodes(batch, uState.Player);
        }
        
        void DrawMinimapEmpireNodes(SpriteBatch batch, Empire e)
        {
            DrawMinimapNodes(batch, e, e.SensorNodes, excludeProjectors: true);
            DrawMinimapNodes(batch, e, e.BorderNodes, excludeProjectors:false);
        }

        public void DeepSpaceBuild_OnClick(ToggleButton toggleButton)
        {
            Universe.InputOpenDeepSpaceBuildWindow();
        }

        public void GravityWells_OnClick(ToggleButton toggleButton)
        {
            GameAudio.AcceptClick();
            Universe.ShowingFTLOverlay = !Universe.ShowingFTLOverlay;
        }

        public void VisionOverlay_OnClick(ToggleButton toggleButton) // Ludoal fork (F3)
        {
            GameAudio.AcceptClick();
            Universe.ShowingVisionOverlay = !Universe.ShowingVisionOverlay;
        }

        public void RangeOverly_OnClick(ToggleButton toggleButton)
        {
            GameAudio.AcceptClick();
            Universe.ShowingRangeOverlay = !Universe.ShowingRangeOverlay;            
        }

        public void InfluenceZones_OnClick(ToggleButton toggleButton) // Ludoal fork (F4)
        {
            GameAudio.AcceptClick();
            Universe.ShowingInfluenceOverlay = !Universe.ShowingInfluenceOverlay;
        }

        public void GravityWellsOnly_OnClick(ToggleButton toggleButton) // Ludoal fork (F5)
        {
            GameAudio.AcceptClick();
            Universe.ShowingGravityWellOverlay = !Universe.ShowingGravityWellOverlay;
        }

        public void AIScreen_OnClick(ToggleButton toggleButton)
        {
            if (!Universe.aw.IsOpen)
                Universe.DeepSpaceBuildWindow.Hide();   // they share the right screen edge
            Universe.aw.ToggleVisibility();
        }

        public void ExoticBonusScreen_OnClick(ToggleButton toggleButton)
        {
            if (Player.Universe.P.DisableMiningOps)
            {
                GameAudio.NegativeClick();
            }
            else
            {
                GameAudio.AcceptClick();
                Universe.ExoticBonusesWindow.ToggleVisibility();
                FreighterUtil.IsToggled = Universe.FreighterUtilizationWindow.IsOpen;
            }
        }

        public void FreighterUtilizationScreen_OnClick(ToggleButton toggleButton)
        {
                GameAudio.AcceptClick();
                Universe.FreighterUtilizationWindow.ToggleVisibility();
                ExoticBonuses.IsToggled = Universe.ExoticBonusesWindow.IsOpen;
        }

        public override bool HandleInput(InputState input)
        {
            if (!Housing.HitTest(input.CursorPosition))
                return false;

            // (the two zoom buttons are gone - Page Up and Page Down still do the job, and the
            // wheel does it better; zoom-to-ship belongs with the ship, not with the map)
            if (DeepSpaceBuild.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.OpensTheDeepSpaceBuilding, "B");

            if (GravityWells.Rect.HitTest(input.CursorPosition))
                // TODO: phase 5 — wire up a dedicated FTL-overlay codex entry, then re-add codexUid here.
                ToolTip.CreateTooltip(GameText.FtlOverlayVisualisesSubspaceProjection, "F4");

            // Ludoal fork (bench): F6, not F3 — the label was left behind when Range moved off
            // F3 to make room for the Vision overlay, so the tooltip promised the wrong key.
            if (RangeOverley.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.WeaponsRangeOverlayVisualisesShips, "F6");

            // Ludoal fork (bench): the Vision button had no tooltip at all — it was added to the
            // row without a matching entry here, so it was the one button on the minimap that
            // said nothing (maintainer feedback).
            if (VisionOverlayBtn.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip("Vision overlay: everything your sensors actually see — "
                                    + "ships, planets and the coverage your spies bring in", "F3");

            if (InfluenceZones.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.InfluenceOverlayVisualises, "F2");

            if (GravityWellsOnly.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.GravityWellOverlayVisualises, "F5");
            if (AIScreen.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.OpensTheAutomationPanelWhich, "H");

            // (Important Events has its own tab in the Galaxy group now)
            if (ExoticBonuses.Rect.HitTest(input.CursorPosition))
            {
                ToolTip.CreateTooltip(Player.Universe.P.DisableMiningOps ? GameText.OpensEmpireExoticBonusesDisabled
                                                                         : GameText.OpensEmpireExoticBonuses, "M");
            }

            if (FreighterUtil.Rect.HitTest(input.CursorPosition))
            {
                ToolTip.CreateTooltip(GameText.OpenFreighterUtilWindow, "N");
            }
            return base.HandleInput(input);
        }
    }
}