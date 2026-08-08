using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.ExtensionMethods;   // CenterTextX
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public class PopupWindow : GameScreen
    {
        // Ludoal fork: the frame's own geometry and draw now live in PopupFrame, so a screen that
        // cannot inherit this class - one that must span the display, or that already derives
        // from something else - still gets THIS surface rather than an approximation of it.
        PopupFrame Frame;
        protected Rectangle BottomBigFill;
        public Rectangle TitleRect;
        public Rectangle TitleLeft;
        public Rectangle TitleRight;
        public Rectangle EmpireFlagRect;
        public Rectangle MidContainer;

        /// Ludoal fork: where a subclass's own content may start - under the title bar, and under
        /// the subtitle band when there is one. ⚠ Not PopupFrame.ContentTop: that one knows the
        /// frame but not whether this window set MiddleText, which costs another 88px.
        protected int BodyTop => MidContainer.Height > 0
                               ? MidContainer.Bottom
                               : PopupFrame.ContentTop(Rect);
        protected Rectangle MidSepTop;
        protected Rectangle MidSepBot;
        public string TitleText;
        public string MiddleText;

        public UILabel TitleLabel;
        public UILabel MiddleLabel;
        public CloseButton Close;

        public Vector2 BodyTextStart;

        private static Rectangle CenterScreen(int width, int height)
        {
            return new Rectangle(GameBase.ScreenWidth  / 2 - width  / 2, 
                                 GameBase.ScreenHeight / 2 - height / 2, width, height);
        }

        protected PopupWindow(GameScreen parent, int width, int height)
            : base(parent, CenterScreen(width, height),
                   toPause: parent as UniverseScreen/*only pause if popup on top of universe*/)
        {
            IsPopup = true;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();

            // this window's own extras, drawn UNDER the frame so its edges land on top: the big
            // lower fill, and the subtitle band with its separators when a caller sets MiddleText
            batch.Draw(ResourceManager.Texture("Popup/popup_filler_lower"), BottomBigFill, Color.White);

            if (MidContainer.Height != 0)
                batch.Draw(ResourceManager.Texture("Popup/popup_filler_lower"), MidContainer, Color.White);
            if (MidSepTop.Height != 0)
                batch.Draw(ResourceManager.Texture("Popup/popup_separator"), MidSepTop, Color.White);
            if (MidSepBot.Height != 0)
                batch.Draw(ResourceManager.Texture("Popup/popup_separator"), MidSepBot, Color.White);

            Frame.Draw(batch);

            base.Draw(batch, elapsed);

            batch.SafeEnd();
        }

        // Ludoal fork (bench 362): a popup summoned by a frame-bound screen (the Shipyard) centres
        // on that frame instead of the display - set before AddScreen, applied at LoadContent.
        public Vector2? CenterOn;

        public override void LoadContent()
        {
            RemoveAll();

            Rect = CenterScreen(Rect.Width, Rect.Height);
            if (CenterOn != null)
                Rect = new Rectangle((int)(CenterOn.Value.X - Rect.Width / 2f),
                                     (int)(CenterOn.Value.Y - Rect.Height / 2f), Rect.Width, Rect.Height);
            Frame = new PopupFrame(Rect);
            // the title band is the frame's, but this class places text and a flag on it
            TitleRect  = Frame.TitleRect;
            TitleLeft  = Frame.TitleLeft;
            TitleRight = Frame.TitleRight;

            EmpireFlagRect = new Rectangle(TitleRight.X-75, TitleRight.Y-22, 45, 45);

            Vector2 closePos = PopupFrame.ClosePos(Rect);
            Close = CloseButton(closePos.X, closePos.Y);

            if (TitleText != null)
            {
                // Ludoal fork: centred on the WINDOW, not left-aligned - every panel in the game
                // names itself the same way now. ⚠ Centred on Rect and not on TitleRect: that one
                // is inset 28 on the left and 56 in width, so centring in it lands off to a side.
                var pos = new Vector2(Rect.CenterTextX(TitleText, UITheme.WindowTitle),
                                      TitleRect.CenterY() - UITheme.WindowTitle.LineSpacing / 2);
                TitleLabel = Label(pos.Rounded(), TitleText, UITheme.WindowTitle);
            }

            if (MiddleText != null)
            {
                MidContainer = new Rectangle(TitleLeft.X, TitleRect.Bottom, TitleRect.Width + TitleLeft.Width + TitleRight.Width, 88);
                MiddleText = Fonts.Arial12Bold.ParseText(MiddleText, MidContainer.Width - 50);
                var textSize = Fonts.Arial12Bold.MeasureString(MiddleText);
                var pos = new Vector2(MidContainer.CenterX() - textSize.X / 2f, 
                                      MidContainer.CenterY() - textSize.Y / 2f);
                MiddleLabel = Label(pos.Rounded(), MiddleText, Fonts.Arial12Bold);
            }
            else
            {
                MidContainer = new Rectangle(TitleLeft.X, TitleRect.Bottom, TitleRect.Width + TitleLeft.Width + TitleRight.Width, 0);
            }

            MidSepTop = new Rectangle(MidContainer.X, MidContainer.Y, MidContainer.Width, 2);
            MidSepBot = new Rectangle(MidContainer.X, MidContainer.Bottom - 2, MidContainer.Width, 2);
            BottomBigFill = new Rectangle(MidContainer.X, MidContainer.Bottom, MidContainer.Width, Frame.BottomFillTop - MidContainer.Bottom);

            BodyTextStart = new Vector2(BottomBigFill.Left + 12, BottomBigFill.Top + 12);

            base.LoadContent();
        }
    }
}