using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;   // Alpha() on Color
using SDUtils;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public sealed class Menu2 : UIElementV2
    {
        public Rectangle Menu;

        // the one rule colour, declared with the window fills it goes with
        // only the Hollow path draws a bare rule now; the solid one wears the popup frame
        static Color FrameRule => GameScreens.ReworkScreens.FrameRule;

        public bool Hollow;
        public Color Background;
        public Color Border = Color.Transparent; // mostly for debugging

        // Ludoal fork: the reworked screens' body colour rather than flat near-black, so a Menu2
        // sitting beside one of them reads as the same surface. Callers that pass their own
        // colour keep it.
        public Menu2(in Rectangle theMenu) : this(theMenu, GameScreens.ReworkScreens.WindowBody)
        {
        }
        public Menu2(int x, int y, int width, int height) : this(new Rectangle(x, y, width, height))
        {
        }
        public Menu2(in Rectangle theMenu, Color color) : base(theMenu)
        {
            Menu = theMenu;
            Background = color; // transparent black

        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Hollow)
            {
                // Ludoal fork: the popup window's surface, the one Options and the Codex wear.
                // ⚠ Painted procedurally for a while: it never matched that reference (its corners
                // are hand-drawn bitmaps, its rule an artist's gradient) AND the body colour
                // carries an alpha, so panels built on it went see-through. Opaque again.
                var frame = new PopupFrame(Menu);
                frame.DrawFill(batch, Menu);
                frame.Draw(batch);
            }
            else
            {
                // Hollow: the frame only, whatever is behind it shows through the middle.
                batch.DrawRectangle(Menu, FrameRule);
            }

            if (Border.A > 0)
            {
                batch.DrawRectangle(Rect, Border);
            }
        }

        public override bool HandleInput(InputState input)
        {
            return false; // nothing to handle here
        }
    }
}