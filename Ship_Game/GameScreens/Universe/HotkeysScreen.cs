using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    // Ludoal fork (maintainer feedback): a placeholder for customisable hotkeys, opened from the
    // in-game menu. A framed popup with the class's close cross and title; the body is filled later.
    public sealed class HotkeysScreen : PopupWindow
    {
        public HotkeysScreen(GameScreen parent) : base(parent, 640, 480)
        {
            TransitionOnTime = 0.25f;
        }

        public override void LoadContent()
        {
            // the window names itself in its own title bar; frame and close cross are PopupWindow's
            TitleText = "Hotkeys";
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            string note = "Customisable hotkeys are coming in a future update.";
            var pos = new Vector2(inner.X + (inner.Width - Fonts.Arial14Bold.TextWidth(note)) / 2f,
                                  inner.Y + 20);
            Add(new UILabel(pos, note, Fonts.Arial14Bold, Color.Gray));
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            base.Draw(batch, elapsed);
        }
    }
}
