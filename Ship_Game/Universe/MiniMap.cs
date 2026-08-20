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
        // bench 449: one-shot calibration of the frustum geometry against the real
        // projection - linear in camera height, so three constants say it all
        bool FrustumCalibrated;
        double FrustumWidthPerHeight, FrustumOffsetXPerHeight, FrustumOffsetYPerHeight;
        /// Ludoal fork: the drawn map's real rect, and the projection that plots on it. The click
        /// handler reads all three so the INVERSE of WorldToMiniPos is guaranteed to match it.
        public Rectangle MapRect => ActualMap;
        public Vector2 MapCentre => MiniMapZero;
        public float MapScale => Scale;
        // Ludoal fork: the two zoom buttons are gone - Page Up / Page Down and the wheel already
        // do it, and zoom-to-ship belongs with the ship. Important Events has its own Galaxy tab.
        readonly ToggleButton InfluenceZones;   // Ludoal fork (F4)
        readonly ToggleButton GravityWellsOnly; // Ludoal fork (F5)
        readonly ToggleButton FoodRoutes;          // Ludoal fork (bench 428): one toggle per goods
        readonly ToggleButton ProdRoutes;
        readonly ToggleButton PopRoutes;
        readonly ToggleButton ColonizationRoutes;  // Ludoal fork (wishlist)
        readonly ToggleButton GravityWells;
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
            // Edge is the gap the whole widget keeps from the screen corner - the same 10 the
            // overlay tabs use. ⚠ Edge is INSIDE the housing, which is already 10px off the
            // screen edge - going higher pushes the widget further from the corner. Edge must
            // also cover the frame's INFLATE (6) or the painted rule spills past the housing.
            // BandGap (maintainer feedback) is how far the icons stand off that rule.
            const int BtnW = 25, BtnH = 22, BandGap = 13, Edge = 6;
            ActualMap = new Rectangle(housing.X + BtnW + BandGap + Edge,
                                      housing.Y + BtnH + BandGap + Edge,
                                      housing.Width  - (BtnW + BandGap + Edge) - Edge,
                                      housing.Height - (BtnH + BandGap + Edge) - Edge);

            // ── the two bands (Ludoal fork, maintainer layout) ──────────────────────────────
            // Two families, and the gap between them is what says so: an OVERLAY toggles a
            // rendering on the map and stays lit; a TAB pops a panel at a screen edge.
            //
            // TOP band:   [Influence Vision Subspace Gravity Range] ..... [DSB]
            // LEFT band:  [ reserved for route filters ] ..... [Freighters Exotic]
            //
            // Three groups, each on its own axis. The top row is the map OVERLAYS - what the map
            // draws over itself - and they read as one family. The head of the LEFT band is kept
            // free for ROUTE FILTERS (Trade Routes, Colonization Routes, ...). The tabs sit at
            // the far end of their band, placed by where their panel comes out: DSB opens at the
            // right edge and is temporary, Freighters and Exotic open at the bottom and stay open.

            UIList topOverlays = AddList(new Vector2(ActualMap.X, Housing.Y + Edge));
            topOverlays.Name = "MiniMapOverlaysTop";
            topOverlays.LayoutStyle = ListLayoutStyle.ResizeList;
            topOverlays.Direction = new Vector2(1, 0); // horizontal
            // ⚠ ALL FIVE map overlays in one row (maintainer): they are one family - each toggles
            // a rendering on the map and stays lit - so splitting them across two bands said
            // something the code did not mean.
            InfluenceZones = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/flagicon", InfluenceZones_OnClick)); // F2
            VisionOverlayBtn = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_spy_small", VisionOverlay_OnClick)); // F3
            GravityWells = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_ftloverlay", GravityWells_OnClick)); // subspace projectors (F4)
            GravityWellsOnly = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/node_inhibit", GravityWellsOnly_OnClick)); // F5
            RangeOverley = topOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_rangeoverlay", RangeOverly_OnClick)); // F6

            // Ludoal fork (wishlist): the ROUTE FILTERS take the seat the left band's head
            // has kept reserved for them (see the band map above). No hotkeys yet - they
            // will get theirs with the key-customization workstream.
            UIList leftOverlays = AddList(new Vector2(Housing.X + Edge, ActualMap.Y));
            leftOverlays.Name = "MiniMapRouteFilters";
            leftOverlays.LayoutStyle = ListLayoutStyle.ResizeList;
            // bench 428: one filter per goods, wearing the resource's own icon
            FoodRoutes = leftOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "NewUI/icon_food", FoodRoutes_OnClick));
            ProdRoutes = leftOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "NewUI/icon_production", ProdRoutes_OnClick));
            PopRoutes = leftOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_pop_22", PopRoutes_OnClick));
            ColonizationRoutes = leftOverlays.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/ColonizeIcon", ColonizationRoutes_OnClick));

            // ⚠ the tabs go to the OPPOSITE end of their band, not beside the overlays
            // (maintainer): top band pushes them RIGHT, left band pushes them DOWN. The empty
            // middle is what separates the two families, rather than a 14px gap.
            UIList topTabs = AddList(new Vector2(ActualMap.Right - BtnW, Housing.Y + Edge));
            topTabs.Name = "MiniMapTabsTop";
            topTabs.LayoutStyle = ListLayoutStyle.ResizeList;
            topTabs.Direction = new Vector2(1, 0);
            // Ludoal fork: the AI tab is gone - Automation is a tab of the Empire group now (H)
            DeepSpaceBuild = topTabs.Add(new ToggleButton(ToggleButtonStyle.Button, "UI/icon_dsbw", DeepSpaceBuild_OnClick));

            UIList leftTabs = AddList(new Vector2(Housing.X + Edge, ActualMap.Bottom - 2 * BtnH));
            leftTabs.Name = "MiniMapTabsLeft";
            leftTabs.LayoutStyle = ListLayoutStyle.ResizeList;
            FreighterUtil = leftTabs.Add(new ToggleButton(ToggleButtonStyle.Button, "NewUI/icon_freighter_util", FreighterUtilizationScreen_OnClick));
            ExoticBonuses = leftTabs.Add(new ToggleButton(ToggleButtonStyle.Button, "NewUI/icon_exotic_Bonuses_big", ExoticBonusScreen_OnClick));
            // ⚠ the SMALLER side, not the width: the galaxy is square, so scaling on the long
            // edge would push it past the short one.
            int shortSide = ActualMap.Width < ActualMap.Height ? ActualMap.Width : ActualMap.Height;
            Scale = shortSide / (Universe.UState.Size * 2.1f); // negative map values are fine
            // ⚠ the map's CENTRE, derived - a fixed constant would put the origin off-centre
            // whenever the frame's width changes.
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

            // Ludoal fork (maintainer decision): a plain frame instead of the brass radar
            // housing. The minimap wears the same furniture as every Submenu, without being
            // one. The ground is the map's OWN, drawn here rather than by the top bar - a fill
            // painted from the bar would land on top of other screens' content.
            Rectangle inflateMap = ActualMap;
            inflateMap.Inflate(6, 6);
            Submenu.DrawFrameWithGround(batch, new RectF(inflateMap));
            
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

            // bench 449 (the FORMULA, not another filter - maintainer call): the noise was
            // in the SOURCE. Unprojecting through the float view matrix every frame loses
            // precision as |world coords| grow - no display-side filter can fix a noisy
            // input. The rect now derives from CAMERA STATE (CamPos, a smooth double):
            // frustum geometry is linear in camera height, so ONE calibration against the
            // real projection gives width-per-height and centre-offset-per-height, and
            // every later frame is pure arithmetic on clean numbers.
            double camH = Universe.CamPos.Z;
            var frustum = Universe.VisibleWorldRect;
            if (!FrustumCalibrated && camH > 0 && frustum.Width > 0)
            {
                FrustumWidthPerHeight   = frustum.Width / camH;
                FrustumOffsetXPerHeight = (frustum.X1 + frustum.Width  / 2 - Universe.CamPos.X) / camH;
                FrustumOffsetYPerHeight = (frustum.Y1 + frustum.Height / 2 - Universe.CamPos.Y) / camH;
                FrustumCalibrated = true;
            }
            double vw = FrustumWidthPerHeight * camH;
            double vh = vw * Universe.ScreenHeight / (double)Universe.ScreenWidth;
            double cx = Universe.CamPos.X + FrustumOffsetXPerHeight * camH;
            double cy = Universe.CamPos.Y + FrustumOffsetYPerHeight * camH;
            double lookX = MiniMapZero.X + (cx - vw / 2) * Scale;
            double lookY = MiniMapZero.Y + (cy - vh / 2) * Scale;
            double lookW = vw * Scale;
            double lookH = vh * Scale;
            var lookingAt = new Rectangle((int)Math.Round(lookX), (int)Math.Round(lookY),
                                          (int)Math.Round(lookW), (int)Math.Round(lookH));
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
            // the map's top edge, like the three other guide lines - a fixed 100px radius
            // only reached it at the default minimap size (maintainer feedback)
            batch.DrawLine(new Vector2(topMiddleView.X, ActualMap.Y), topMiddleView, Color.White);
            batch.DrawLine(new Vector2(botMiddleView.X, ActualMap.Y + ActualMap.Height), botMiddleView, Color.White);
            batch.DrawLine(new Vector2(ActualMap.X, leftMiddleView.Y), leftMiddleView, Color.White);
            batch.DrawLine(new Vector2(ActualMap.X + ActualMap.Width, rightMiddleView.Y), rightMiddleView, Color.White);

            GravityWells.IsToggled     = Universe.ShowingFTLOverlay;
            DeepSpaceBuild.IsToggled = Universe.DeepSpaceBuildWindow.Visible;
            ExoticBonuses.IsToggled  = Universe.ExoticBonusesWindow.IsOpen;
            FreighterUtil.IsToggled =  Universe.FreighterUtilizationWindow.IsOpen;

            RangeOverley.IsToggled         = Universe.ShowingRangeOverlay;
            InfluenceZones.IsToggled       = Universe.ShowingInfluenceOverlay;   // Ludoal fork (F4)
            GravityWellsOnly.IsToggled     = Universe.ShowingGravityWellOverlay; // Ludoal fork (F5)
            FoodRoutes.IsToggled           = Universe.ShowingFoodRoutesOverlay;         // Ludoal fork (bench 428)
            ProdRoutes.IsToggled           = Universe.ShowingProdRoutesOverlay;
            PopRoutes.IsToggled            = Universe.ShowingPopRoutesOverlay;
            ColonizationRoutes.IsToggled   = Universe.ShowingColonizationRoutesOverlay; // Ludoal fork (wishlist)
            // Ludoal fork (maintainer feedback): without this sync, pressing F3 turns the
            // overlay on but leaves the button dark until clicked.
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

        public void FoodRoutes_OnClick(ToggleButton toggleButton) // Ludoal fork (bench 428)
        {
            GameAudio.AcceptClick();
            Universe.ShowingFoodRoutesOverlay = !Universe.ShowingFoodRoutesOverlay;
        }

        public void ProdRoutes_OnClick(ToggleButton toggleButton)
        {
            GameAudio.AcceptClick();
            Universe.ShowingProdRoutesOverlay = !Universe.ShowingProdRoutesOverlay;
        }

        public void PopRoutes_OnClick(ToggleButton toggleButton)
        {
            GameAudio.AcceptClick();
            Universe.ShowingPopRoutesOverlay = !Universe.ShowingPopRoutesOverlay;
        }

        public void ColonizationRoutes_OnClick(ToggleButton toggleButton) // Ludoal fork (wishlist)
        {
            GameAudio.AcceptClick();
            Universe.ShowingColonizationRoutesOverlay = !Universe.ShowingColonizationRoutesOverlay;
        }

        public void GravityWellsOnly_OnClick(ToggleButton toggleButton) // Ludoal fork (F5)
        {
            GameAudio.AcceptClick();
            Universe.ShowingGravityWellOverlay = !Universe.ShowingGravityWellOverlay;
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
                ToolTip.CreateTooltip(GameText.OpensTheDeepSpaceBuilding, KeyBindings.Name(KeyBindings.DeepSpaceBuildWindow));

            if (GravityWells.Rect.HitTest(input.CursorPosition))
                // TODO: phase 5 — wire up a dedicated FTL-overlay codex entry, then re-add codexUid here.
                ToolTip.CreateTooltip(GameText.FtlOverlayVisualisesSubspaceProjection, KeyBindings.Name(KeyBindings.FTLOverlay));

            if (RangeOverley.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.WeaponsRangeOverlayVisualisesShips, KeyBindings.Name(KeyBindings.RangeOverlay));

            if (VisionOverlayBtn.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.UhVisionOverlayTooltip, KeyBindings.Name(KeyBindings.VisionOverlay));

            if (FoodRoutes.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.UhFoodRoutesTooltip);

            if (ProdRoutes.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.UhProdRoutesTooltip);

            if (PopRoutes.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.UhColonistRoutesTooltip);

            if (ColonizationRoutes.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.UhColonizationRoutesTooltip);

            if (InfluenceZones.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.InfluenceOverlayVisualises, KeyBindings.Name(KeyBindings.InfluenceOverlay));

            if (GravityWellsOnly.Rect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.GravityWellOverlayVisualises, KeyBindings.Name(KeyBindings.GravityWellOverlay));

            // (Important Events has its own tab in the Galaxy group now)
            if (ExoticBonuses.Rect.HitTest(input.CursorPosition))
            {
                ToolTip.CreateTooltip(Player.Universe.P.DisableMiningOps ? GameText.OpensEmpireExoticBonusesDisabled
                                                                         : GameText.OpensEmpireExoticBonuses, "M");
            }

            if (FreighterUtil.Rect.HitTest(input.CursorPosition))
            {
                ToolTip.CreateTooltip(GameText.OpenFreighterUtilWindow, KeyBindings.Name(KeyBindings.FreighterUtilWindow));
            }
            return base.HandleInput(input);
        }
    }
}