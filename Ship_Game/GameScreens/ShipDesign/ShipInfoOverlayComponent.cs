using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Ships;
using Ship_Game.Gameplay; // HullSlot
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Universe;
using Ship_Game.UI;   // NineSliceSprite - the submenu frame, worn tabless
#pragma warning disable CA1001

namespace Ship_Game.GameScreens.ShipDesign
{
    public sealed class ShipInfoOverlayComponent : UIElementV2
    {
        readonly GameScreen Screen;
        readonly UniverseState Universe;
        Empire Player => Universe.Player;

        IShipDesign SelectedDesign;
        readonly NineSliceSprite Frame = new();
        DesignShip TempShip;
        ShipDesignStats Ds => TempShip.DesignStats;
        Graphics.Font TitleFont;
        Graphics.Font Font;
        int TextWidth;
        // every label the verbose list can wear - the value column is measured over them
        static readonly string[] VerboseLabels = { "WIP", "Offense", "Weapons", "Hangars",
                                                   "Bomb Bays", "Max Range", "Repair", "EMP Prot",
                                                   "FTL Speed", "Sub Speed", "Turn", "Troops", "Cargo" };

        public ShipInfoOverlayComponent(GameScreen screen, UniverseState us)
        {
            Visible = false;
            Screen = screen;
            Universe = us;
        }

        float GetSize()
        {
            float minimumSize = 340;
            return Math.Max(minimumSize, (Screen.Width * 0.16f).RoundTo10());
        }

        public void ShowToLeftOf(Vector2 leftOf, IShipDesign design)
        {
            Visible = design != null;
            if (Visible)
            {
                float size = GetSize();
                ShowShip(design, new(leftOf.X - size*1.6f, leftOf.Y - size/4), size);
            }
        }

        // bench 447: seat the overlay INSIDE a host rect (the colony Description pane) -
        // the box footprint is (size * 1.5, size), fitted to the host
        public void ShowInRect(in Rectangle host, IShipDesign design)
        {
            Visible = design != null;
            if (Visible)
            {
                float size = Math.Min(host.Height - 10, (host.Width - 20) / 1.5f);
                ShowShip(design, new(host.X + 10, host.Y + 5), size);
            }
        }

        public void ShowToTopOf(Vector2 topOf, IShipDesign design)
        {
            Visible = design != null;
            if (Visible)
            {
                float size = GetSize();
                ShowShip(design, new(topOf.X, topOf.Y - size - 20), size);
            }
        }

        void ShowShip(IShipDesign design, Vector2 screenPos, float shipRectSize)
        {
            screenPos = screenPos.RoundTo10();
            screenPos.X = Math.Max(100f, screenPos.X);

            if (SelectedDesign != design)
            {
                try // we got some errors here, so try to handle it gracefully and just report error
                {
                    TempShip = new(Universe, design as Ships.ShipDesign);
                    TempShip.RecalculatePower();
                    TempShip.ShipStatusChange();
                    SelectedDesign = design;
                }
                catch (Exception e)
                {
                    Log.Error(e, $"ShowShip failed: {design.Name}"); // automatic error report
                    SelectedDesign = null;
                    Visible = false;
                    return;
                }
            }

            TextWidth = (shipRectSize/2).RoundTo10();
            Size = new(shipRectSize + TextWidth, shipRectSize);
            Pos = screenPos;
            if (Pos.X < 0) Pos.X = 0;
            if (Pos.Y < 0) Pos.Y = 0;
            if (Bottom > Screen.Height) Pos.Y -= (Bottom - Screen.Height);

            TitleFont = Fonts.Arial14Bold;
            Font      = Fonts.Arial11Bold;
        }

        public override bool HandleInput(InputState input)
        {
            return Rect.HitTest(input.CursorPosition);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            Ship s = TempShip;
            if (!Visible || s == null)
                return;

            // The housing is sized on the HULL's grid, not a blind square: a wide hull
            // letterboxed in a square leaves a void under and beside it. The module size obeys
            // RenderOverlay's own arithmetic - width / (maxSpan+1), capped 24 - so handing it a
            // rect of ms*(maxSpan+1) reproduces the ms measured; the grid centres in that rect,
            // so the rect is placed off the SHIP's edges: right edge 10px off the frame,
            // vertically centred on the panel.
            int gw = Math.Max(1, s.Grid.Width), gh = Math.Max(1, s.Grid.Height);
            int maxSpan = Math.Max(gw, gh);
            float availW = Width - TextWidth - 20;
            float availH = Height - 20;
            float ms = Math.Min(Math.Min(availW, availH) / (maxSpan + 1f), 24f);
            int size = (int)(ms * (maxSpan + 1));
            // ⚠ the GRID can carry empty rows and columns around the hull, and by how much
            // varies per model - so the anchor is the HULL's own bounds, read from the slots
            // exactly as RenderOverlay draws them, never the grid's
            int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
            var hullSlots = s.ShipData?.BaseHull?.HullSlots;
            if (hullSlots != null && hullSlots.Length > 0)
            {
                foreach (HullSlot hs in hullSlots)
                {
                    if (hs.Pos.X < minX) minX = hs.Pos.X;
                    if (hs.Pos.Y < minY) minY = hs.Pos.Y;
                    if (hs.Pos.X > maxX) maxX = hs.Pos.X;
                    if (hs.Pos.Y > maxY) maxY = hs.Pos.Y;
                }
            }
            else
            {
                minX = 0; minY = 0; maxX = gw - 1; maxY = gh - 1;
            }
            // RenderOverlay centres the GRID in the rect it is handed; the rect is placed so
            // the HULL sits CENTRED in the image band, both axes - right-edge anchoring fills
            // the window with wide hulls but glues narrow ones to the right.
            float hullCx    = (minX + (maxX - minX + 1) / 2f - gw / 2f) * ms;
            float hullCy    = (minY + (maxY - minY + 1) / 2f - gh / 2f) * ms;
            float bandCentre = X + TextWidth + 10 + availW / 2f;
            var shipOverlay = new Rectangle((int)(bandCentre - size / 2f - hullCx),
                                            (int)(CenterY - size / 2f - hullCy), size, size);
            // Ludoal fork: the submenu frame without its tab, instead of a per-frame Menu2, which
            // draws the full popup window - far too much furniture for a hover overlay. Same
            // nine-slice the sliders on the Fleets page wear.
            // Ludoal fork: a touch more opaque so the map does not read through
            batch.FillRectangle(Rect, new Color(8, 10, 14).Alpha(0.98f));
            Frame.Update(new RectF(Rect),
                         ResourceManager.Texture("NewUI/submenu_corner_TL"),
                         ResourceManager.Texture("NewUI/submenu_corner_TR"),
                         ResourceManager.Texture("NewUI/submenu_corner_BL"),
                         ResourceManager.Texture("NewUI/submenu_corner_BR"),
                         ResourceManager.Texture("NewUI/submenu_horiz_vert"),
                         ResourceManager.Texture("NewUI/submenu_horiz_vert"),
                         borderWidth: 2);
            Frame.DrawBorders(batch);

            s.RenderOverlay(batch, shipOverlay, showModules:true, drawHullBackground:true, moduleHealthColor:false, markLockedModules: true);
            float mass          = s.Stats.GetMass(Player);
            float warpSpeed     = s.Stats.GetFTLSpeed(mass, Player);
            float subLightSpeed = s.Stats.GetSTLSpeed(mass, Player);
            float turnRateDeg   = s.Stats.GetTurnRadsPerSec(s.Level).ToDegrees();

            // the value column starts past the LONGEST label, measured - a share of text width
            // sized for a short label runs the value straight into it. Declared before the
            // first DrawText call: the local functions capture it (CS0165 otherwise).
            float labelRoom = 0f;
            foreach (string l in VerboseLabels)
                labelRoom = Math.Max(labelRoom, Font.TextWidth(l));
            labelRoom += 24f; // the column rides right of the longest label by a clear lane

            var p = new Vector2(X + 25, Y + 22);
            DrawText(TitleFont, s.Name, "", Color.White);
            DrawText(Font, $"{s.ShipData.ShipCategory}, {s.ShipData.DefaultCombatState}", "", Color.Gray);

            Vector2 start = p;
            // --- Core values with icons --- //
            float charWidth = 10;

            // left side
            CoreValue(charWidth*3.5f, "UI/icon_offense", "DPS", Str(s.TotalDps), Color.OrangeRed);
            if (Ds.HasEnergyWeapons)
            {
                float duration = Ds.HasBeams() ? Ds.BurstEnergyDuration : Ds.EnergyDuration;
                bool isInf = Ds.HasBeams() ? Ds.HasBeamDurationPositive() : Ds.HasEnergyWepsPositive();
                string energyTime = isInf ? "INF" : $"{duration}s";
                CoreValue(charWidth*3.5f, "UI/lightningBolt", "ETM", energyTime, Color.LightGoldenrodYellow);
            }
            if (Ds.HasOrdnance())
            {
                string ammoTime = Ds.HasOrdInfinite() ? "INF" : $"{(int)Ds.AmmoTime}s";
                CoreValue(charWidth*3.5f, "Modules/Ordnance", "OTM", ammoTime, Color.Khaki);
            }

            // right side
            p = new(start.X + charWidth * 10, start.Y);
            CoreValue(charWidth*2, "UI/icon_shield", "HP", Str(s.HealthMax), Color.CadetBlue);
            if (s.ShieldMax > 0)
            {
                CoreValue(charWidth*2, "Modules/Shield_1KW", "SP", Str(s.ShieldMax), Color.AliceBlue);
            }

            ////////////////////////////////////

            // verbose stats - the compact panel's block order: combat, defence, mobility,
            // payload, with air between the categories. The air is paid only when a later line
            // draws, so an empty block folds with it. Air under the iconed core block, then the
            // verbose list.
            p = new(start.X, start.Y + 60 + Font.LineSpacing * 0.5f);
            bool lineDrawn = false, airPending = false;
            void Air() => airPending = true;
            void PayAir()
            {
                if (airPending && lineDrawn)
                    p.Y += Font.LineSpacing * 0.5f;
                airPending = false;
                lineDrawn = true;
            }

            if (Ds.CompletionPercent != 100)
            {
                PayAir();
                DrawText(Font, "WIP", $"{Ds.CompletionPercent}%", Color.Yellow, titleColor: Color.Gray);
            }

            // the compact's own labels, abbreviated for the micro: grey labels, no colon,
            // values in the charte's neutral white
            DrawValue("Offense", Ds.Strength);
            DrawValue("Weapons", s.Weapons.Count);
            DrawValue("Hangars", s.Carrier.AllFighterHangars.Length);
            DrawValue("Bomb Bays", s.BombBays.Count);
            DrawValue("Max Range", s.WeaponsMaxRange);
            Air();
            DrawValue("Repair", s.RepairRate);
            DrawValue("EMP Prot", s.EmpTolerance);
            Air();
            DrawValue("FTL Speed", warpSpeed);
            DrawValue("Sub Speed", subLightSpeed);
            DrawValue("Turn", turnRateDeg);
            Air();
            DrawValue("Troops", s.TroopCapacity);
            DrawValue("Cargo", s.CargoSpaceMax);

            void CoreValue(float ident, string icon, string title, string value, Color color)
            {
                batch.Draw(ResourceManager.Texture(icon), new RectF(p.X, p.Y, 20, 20), Color.White);
                batch.DrawString(Font, title, new Vector2(p.X+22, p.Y+1).Rounded(), color);
                batch.DrawString(Font, value, new Vector2(p.X+22+ident, p.Y+1).Rounded(), color);
                p.Y += 20;
            }

            void DrawText(Graphics.Font font, string title, string text, Color color, Color? titleColor = null)
            {
                var ident = new Vector2(p.X + labelRoom, p.Y);
                batch.DrawString(font, title, p, titleColor ?? color);
                batch.DrawString(font, text, ident, color);
                p.Y += font.LineSpacing + 2;
            }

            void DrawValue(string title, float value)
            {
                if (value <= 0f)
                    return;
                PayAir(); // a pending block break is paid by the first line that draws
                var ident = new Vector2(p.X + labelRoom, p.Y);
                batch.DrawString(Font, title, p, Color.Gray);
                batch.DrawString(Font, Str(value), ident, Color.White);
                p.Y += Font.LineSpacing + 2;
            }
        }

        static string Str(float value) => value.GetNumberString();
    }
}
