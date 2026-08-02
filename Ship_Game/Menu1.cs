using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public sealed class Menu1 : UIElementV2
    {
        // TODO: make private after scroll-list branch is merged
        public Submenu subMenu;
        readonly bool WithSubMenu;


        public Menu1(int x, int y, int width, int height)
            : this(new Rectangle(x, y, width, height))
        {
        }

        public Menu1(int x, int y, int width, int height, bool withSub)
            : this(new Rectangle(x, y, width, height), withSub)
        {
        }

        public Menu1(in Rectangle theMenu) : base(theMenu)
        {
            this.PerformLayout();
        }

        public Menu1(in Rectangle theMenu, bool withSub) : base(theMenu)
        {
            WithSubMenu = withSub;
            this.PerformLayout();
        }

        public override void PerformLayout()
        {
            base.PerformLayout();

            Rectangle r = Rect;
            var subMenuRect = new Rectangle(r.X + 20, r.Y - 5, r.Width - 40, r.Height - 15);

            if (WithSubMenu && subMenu == null)
            {
                subMenu = new Submenu(subMenuRect);
            }
            if (subMenu != null)
            {
                subMenu.Rect = subMenuRect;
                subMenu.PerformLayout();
            }
        }

        public override bool HandleInput(InputState input)
        {
            return false;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // Ludoal fork: painted like the Codex, same as Menu2 - neutral body, a title bar a
            // step lighter, an orange rule - instead of the eight-piece textured surround. Load
            // and Save were the last screens still showing the old frame. Menu1 differs from
            // Menu2 only in carrying a Submenu of its own; they had two texture sets for that.
            UITheme.DrawPlate(batch, Rect, GameScreens.ReworkScreens.WindowBody,
                              GameScreens.ReworkScreens.FrameRule);
            int r = System.Math.Min(UITheme.Theme.CornerRadius, Rect.Height / 2);
            batch.FillRectangle(new Rectangle(Rect.X + r, Rect.Y + UITheme.Theme.RuleWidth,
                                              Rect.Width - 2 * r,
                                              GameScreens.ReworkScreens.WindowTitleBarH),
                                GameScreens.ReworkScreens.WindowTitleBar);
            subMenu?.Draw(batch, elapsed);
        }


    }
}