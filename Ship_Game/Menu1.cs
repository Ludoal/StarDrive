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
            // Ludoal fork: painted like Menu2 and the confirm dialog - a dark body under a thin
            // brass rule - instead of the eight-piece textured surround. The Load and Save
            // screens were the last places still showing the old frame.
            batch.FillRectangle(Rect, GameScreens.ReworkScreens.GroupFrameFill);
            batch.DrawRectangle(Rect, GameScreens.ReworkScreens.FrameRule);
            subMenu?.Draw(batch, elapsed);
        }


    }
}