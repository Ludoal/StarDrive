using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.ExtensionMethods;   // CenterTextX
using Rectangle = SDGraphics.Rectangle;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    public class UIColorPicker : UIElementV2
    {
        public Color CurrentColor = Color.White;
        public string Title = "Color";

        // the band under the title bar where the colour grid starts, and the close cross -
        // both derived from the live Rect so they follow the window wherever it sits
        int GridTop => PopupFrame.ContentTop(Rect) + 8;
        Rectangle CloseRect
        {
            get
            {
                Vector2 p = PopupFrame.ClosePos(Rect);
                return new Rectangle((int)p.X, (int)p.Y, 20, 20);
            }
        }

        public UIColorPicker(in Rectangle rect) : base(rect)
        {
        }

        public override bool HandleInput(InputState input)
        {
            if (!Visible)
                return false;

            if (input.RightMouseClick)
            {
                Visible = false;
                return true;
            }

            if (!HitTest(input.CursorPosition))
            {
                if (input.LeftMouseClick)
                {
                    Visible = false;
                    return true;
                }
                return false;
            }
            
            if (input.LeftMouseClick && CloseRect.HitTest(input.CursorPosition))
            {
                Visible = false;
                return true;
            }

            if (input.LeftMouseDown)
            {
                // ⚠ the hit map mirrors the DRAW map exactly - same origins, or the aim
                // lands beside the swatch under the cursor
                int yPosition = GridTop;
                int xPositionStart = (int)X + 20;
                for (int i = 0; i <= 255; i++)
                {
                    for (int j = 0; j <= 255; j++)
                    {
                        var thisColor = new Color((byte)i, (byte)j, CurrentColor.B);
                        var colorRect = new Rectangle(2 * j + xPositionStart - 4, yPosition - 4, 8, 8);
                        if (colorRect.HitTest(input.CursorPosition))
                        {
                            CurrentColor = thisColor;
                        }
                    }
                    yPosition += 2;
                }

                yPosition = GridTop;
                for (int i = 0; i <= 255; i++)
                {
                    var thisColor = new Color(CurrentColor.R, CurrentColor.G, Convert.ToByte(i));
                    var colorRect = new Rectangle((int)X + 10 + 575, yPosition, 20, 2);
                    if (colorRect.HitTest(input.CursorPosition))
                    {
                        CurrentColor = thisColor;
                    }
                    yPosition += 2;
                }
            }

            // always capture hovered input to avoid propagating input to behind us
            return true;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Visible)
                return;

            // the popup window's surface, with title and close cross placed the way
            // PopupWindow places its own
            var frame = new PopupFrame(Rect);
            frame.DrawFill(batch, Rect);
            frame.Draw(batch);
            var titlePos = new Vector2(Rect.CenterTextX(Title, UITheme.WindowTitle),
                                       Rect.Y + PopupFrame.TitleBarTop
                                       + (PopupFrame.TitleBarHeight - UITheme.WindowTitle.LineSpacing) / 2);
            batch.DrawString(UITheme.WindowTitle, Title, titlePos, UITheme.TextPrimary);
            batch.Draw(ResourceManager.Texture("NewUI/Close_Normal"), CloseRect, Color.White);

            SubTexture spark = ResourceManager.Texture("Particles/spark");

            int yPosition = GridTop;
            int xPositionStart = (int)X + 20;
            for (int i = 0; i <= 255; i++)
            {
                for (int j = 0; j <= 255; j++)
                {
                    var r = new Rectangle(2 * j + xPositionStart, yPosition, 2, 2);
                    var thisColor = new Color(Convert.ToByte(i), Convert.ToByte(j), CurrentColor.B);
                    batch.Draw(spark, r, thisColor);
                    if (thisColor.R == CurrentColor.R && thisColor.G == CurrentColor.G)
                    {
                        batch.Draw(spark, r, Color.Red);
                    }
                }
                yPosition += 2;
            }

            yPosition = GridTop;
            for (int i = 0; i <= 255; i++)
            {
                var r = new Rectangle((int)X + 10 + 575, yPosition, 20, 2);
                var thisColor = new Color(CurrentColor.R, CurrentColor.G, Convert.ToByte(i));
                batch.Draw(spark, r, thisColor);
                if (thisColor.B == CurrentColor.B)
                {
                    batch.Draw(spark, r, Color.Red);
                }
                yPosition += 2;
            }
        }
    }
}