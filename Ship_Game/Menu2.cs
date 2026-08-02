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
        static Color FrameRule => GameScreens.ReworkScreens.FrameRule;
        public static int TitleBarH => GameScreens.ReworkScreens.WindowTitleBarH;

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
                // Ludoal fork: painted like the Codex - neutral body, a title bar a step lighter
                // across the top, an orange rule around the lot - instead of the sculpted corner
                // set. Those corners were asymmetric (69x38 at the top, 48x28 at the bottom) with
                // their own extenders and repeats, so the frame could never match anything else
                // on screen. Nothing reads a client area off this class, so no content moves.
                // Ludoal fork: THE same painted plate the buttons use - rounded, top-lit, ruled.
                // A frame that squared its corners while the buttons inside it rounded theirs was
                // the whole of what made a window and its contents read as different furniture.
                UITheme.DrawPlate(batch, Menu, Background, FrameRule);
                // the title bar over it, inset so it does not square off the arc at the top
                int r = System.Math.Min(UITheme.Theme.CornerRadius, Menu.Height / 2);
                batch.FillRectangle(new Rectangle(Menu.X + r, Menu.Y + UITheme.Theme.RuleWidth,
                                                  Menu.Width - 2 * r, TitleBarH),
                                    GameScreens.ReworkScreens.WindowTitleBar);
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