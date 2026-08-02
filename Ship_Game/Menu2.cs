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

        // the one brass line, declared with the frame fill it goes with
        static Color FrameRule => GameScreens.ReworkScreens.FrameRule;

        public bool Hollow;
        public Color Background;
        public Color Border = Color.Transparent; // mostly for debugging

        // Ludoal fork: the reworked screens' body colour rather than flat near-black, so a Menu2
        // sitting beside one of them reads as the same surface. Callers that pass their own
        // colour keep it.
        public Menu2(in Rectangle theMenu) : this(theMenu, new Color(14, 12, 9).Alpha(0.92f))
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
                // Ludoal fork: painted in the grammar the Codex and the reworked screens use -
                // a dark body under a thin brass rule - instead of the sculpted corner set.
                // The corners were asymmetric (69x38 at the top, 48x28 at the bottom) with their
                // own extenders and repeats, so the frame could never match anything else on
                // screen. Nothing reads a client area off this class, so no content moves.
                batch.FillRectangle(Menu, Background);
                batch.DrawRectangle(Menu, FrameRule);
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