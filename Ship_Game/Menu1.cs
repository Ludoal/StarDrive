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
            // Ludoal fork: the popup window's surface - the same one Options and the Codex wear.
            // ⚠ It was painted procedurally for a while and that was a mistake twice over: it
            // never matched the reference look (those corners are hand-drawn bitmaps), and the
            // body colour carries an alpha, so every panel built on it went see-through - on Race
            // Design the menu behind it read straight through the traits (maintainer observation).
            // The filler texture is opaque, which is what a panel holding text needs to be.
            var frame = new PopupFrame(Rect);
            frame.DrawFill(batch, Rect);
            frame.Draw(batch);
            subMenu?.Draw(batch, elapsed);
        }


    }
}