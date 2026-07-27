using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;


namespace Ship_Game
{
    public class ModuleSelectListItem : ScrollListItem<ModuleSelectListItem>
    {
        public ShipModule Module;
        Empire Player;
        public ModuleSelectListItem(Empire player, string headerText) : base(headerText)
        {
            Player = player;
        }

        public ModuleSelectListItem(Empire player, ShipModule module)
        {
            Player = player;
            Module = module;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);

            if (Module != null)
            {
                DrawModule(batch);

                // Ludoal fork (bench 46.152): the gesture hint belongs to the ROW, not to the
                // list. Hung off the list's rect it appeared wherever the cursor first entered
                // and stayed there, and it fired over category headers too - those have no
                // Module, so they no longer qualify. The comparison line only shows when the
                // feature is on: a hint promising a gesture that does nothing is worse than none.
                if (HitTest(GameBase.ScreenManager.input.CursorPosition))
                    ToolTip.CreateTooltip(GlobalStats.ShipyardComparison
                        ? "Click to pick this module\nShift-click to pin it for comparison"
                        : "Click to pick this module");
            }
        }

        void DrawModule(SpriteBatch batch)
        {
            ShipModule m = Module;
            bool isObsolete = m.IsObsolete(Player);

            var bCursor = new Vector2(List.X + 15, Y);
            SubTexture tex = m.ModuleTexture;
            var rect = new Rectangle((int)bCursor.X, (int)bCursor.Y, tex.Width, tex.Height);
            float aspectRatio = (float)tex.Width / tex.Height;
            float w = rect.Width;
            float h = rect.Height;
            for (; w > 30f || h > 30f; h = h - 1.6f)
            {
                w -= aspectRatio * 1.6f;
            }
            rect.Width  = (int)w;
            rect.Height = (int)h;
            batch.Draw(tex, rect, Color.White);

            var tCursor       = new Vector2(bCursor.X + 35f, bCursor.Y + 3f);
            Color nameColor   = isObsolete ? Color.Red : Color.White; 
            string moduleName = Localizer.Token(m.NameIndex);
            if (Fonts.Arial12Bold.MeasureString(moduleName).X + 90 < List.Width)
            {
                batch.DrawString(Fonts.Arial12Bold, moduleName, tCursor, nameColor);
                tCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
            }
            else
            {
                batch.DrawString(Fonts.Arial11Bold, moduleName, tCursor, nameColor);
                tCursor.Y += Fonts.Arial11Bold.LineSpacing + 3;
            }

            string restriction = m.Restrictions.ToString();
            batch.DrawString(Fonts.Arial8Bold, restriction, tCursor, Color.Orange);
            tCursor.X += Fonts.Arial8Bold.MeasureString(restriction).X;
            string size = $" ({m.XSize}x{m.YSize})";
            batch.DrawString(Fonts.Arial8Bold, size, tCursor, Color.Gray);
            tCursor.X += Fonts.Arial8Bold.MeasureString(size).X;

            if (m.InstalledWeapon?.IsTurret == true && !m.DisableRotation)
            {
                var rotateRect = new Rectangle((int)bCursor.X + 228, (int)bCursor.Y + 3, 15, 16);
                var turretRect = new Rectangle((int)bCursor.X + 226, (int)bCursor.Y + 20, 18, 20);
                batch.Draw(ResourceManager.Texture("UI/icon_can_rotate"), rotateRect, Color.White);
                batch.Draw(ResourceManager.Texture("NewUI/icon_turret"), turretRect, Color.White);
                if (rotateRect.HitTest(GameBase.ScreenManager.input.CursorPosition) || turretRect.HitTest(GameBase.ScreenManager.input.CursorPosition))
                    ToolTip.CreateTooltip(GameText.ThisModuleCanBeRotated);
            }
            else if (!m.DisableRotation)
            {
                var rotateRect = new Rectangle((int)bCursor.X + 228, (int)bCursor.Y + 3, 20, 22);
                batch.Draw(ResourceManager.Texture("UI/icon_can_rotate"), rotateRect, Color.White);
                if (rotateRect.HitTest(GameBase.ScreenManager.input.CursorPosition))
                    ToolTip.CreateTooltip(GameText.IndicatesThatThisModuleCan);
            }
            else if (m.InstalledWeapon?.IsTurret == true)
            {
                var turretRect = new Rectangle((int)bCursor.X + 223, (int)bCursor.Y + 3, 25, 23);
                batch.Draw(ResourceManager.Texture("NewUI/icon_turret"), turretRect, Color.White);
                if (turretRect.HitTest(GameBase.ScreenManager.input.CursorPosition))
                    ToolTip.CreateTooltip(GameText.IndicatesThisModuleHasA);
            }

            if (isObsolete)
            {
                var obsoleteRect = new Rectangle((int)bCursor.X + 220, (int)bCursor.Y + 22, 17, 17);
                batch.Draw(ResourceManager.Texture("NewUI/icon_queue_delete"), obsoleteRect, Color.Red);
                if (obsoleteRect.HitTest(GameBase.ScreenManager.input.CursorPosition))
                    ToolTip.CreateTooltip(GameText.ThisModuleWasMarkedAs);
            }
        }
    }
}
