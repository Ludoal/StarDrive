using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    /// Ludoal fork: the popup window's SURFACE, split out from the class that owned it.
    ///
    /// PopupWindow draws the frame the maintainer picked as this interface's reference look -
    /// hand-drawn corners, a gradient rule under the title, a title band that reaches both
    /// edges. Every attempt to approach it with procedural painting came back short on the
    /// bench, for a reason no amount of tuning fixes: those corners are 28x30 bitmaps and that
    /// rule is an artist's gradient, neither of which an arc computed from a radius reproduces.
    ///
    /// The obstacle was never the drawing, it was PopupWindow itself: it centres its rect on
    /// screen and derives from GameScreen, so a screen that must span the display under the top
    /// bar - or that already derives from something else - cannot simply inherit it. So the
    /// geometry and the draw live here, taking a rect, and PopupWindow becomes one caller among
    /// others rather than the sole owner.
    ///
    /// One arithmetic, several consumers: the alternative was a second copy of these twenty-odd
    /// numbers, which is how two frames that ought to match end up half a pixel apart.
    public struct PopupFrame
    {
        // the corner blocks and their strokes
        Rectangle TL, TR, BL, BR;
        Rectangle TLc, TRc, BLc, BRc;
        // the four sides: a plain band, plus the gradient that rules the top and the foot
        Rectangle TopHoriz, TopSep, BotHoriz, BotSep, LeftVert, RightVert;
        Rectangle BottomFill;

        /// the title band - public because callers place their title text and close cross on it
        public Rectangle TitleRect, TitleLeft, TitleRight;
        Rectangle TitleSep;

        /// the top of the foot band: where a caller's own filler must stop
        public readonly int BottomFillTop => BottomFill.Y;

        /// The title bar's height, as PopupWindow has always built it: the band is 46 tall and
        /// starts 7 below the frame's top edge. Content begins under the sum.
        public const int TitleBarHeight = 46;
        public const int TitleBarTop = 7;

        /// Where a caller's content may start, measured from the frame's own top.
        public static int ContentTop(in Rectangle frame) => frame.Y + TitleBarTop + TitleBarHeight;

        /// What the borders eat on each side. The frame is not a 2px rule: the right band is 11
        /// wide, the foot 30, and content laid out on the raw rect runs under both.
        public const int BorderLeft = 3, BorderRight = 11, BorderBottom = 30;

        /// ⚠ Third measurement, the right one this time: what matters is not where the INK ends
        /// but where the bright RULE row sits. The bottom band is 12 tall and its rule is rows
        /// 0-1 - everything under it is drop shadow - so the visible line runs a full 12 rows
        /// above rect.Bottom. Measuring "last inked row" found the shadow's foot and put the
        /// line 10px high twice over. A caller that wants the LINE on a margin extends its rect
        /// by BottomLine; the shadow rows fall past it, which is what they are for.
        public const int BottomLine = 12;
        public const int TopInk = 7;

        /// The area a caller may actually lay content in - the rect less the title bar and the
        /// borders. ⚠ Use THIS, not the rect, or the last column and the bottom row hide behind
        /// the frame's own edges (maintainer observation on Colony).
        public static Rectangle ContentArea(in Rectangle frame)
            => new(frame.X + BorderLeft, ContentTop(frame),
                   frame.Width - BorderLeft - BorderRight,
                   frame.Bottom - BorderBottom - ContentTop(frame));

        /// Where the close cross goes, matching Options exactly rather than re-deriving it.
        public static Vector2 ClosePos(in Rectangle frame) => new(frame.Right - 44, frame.Y + 19);

        public PopupFrame(in Rectangle rect)
        {
            TL = new Rectangle(rect.X, rect.Y, 28, 30);
            TLc = TL;
            TLc.X -= 2;
            TLc.Y += 3;
            TLc.Width = 30;
            TLc.Height = 27;

            TR = new Rectangle(rect.Right - 28, rect.Y, 28, 30);
            TRc = TR;
            TRc.Y += 3;
            TRc.Width = 28;
            TRc.Height = 27;

            // the gradient rules are fixed-width assets (433 and 424), so they are CENTRED on
            // the span rather than stretched - stretching a gradient banded it visibly.
            // ⚠ The gradient bands are 433 wide, a size picked for Options at 720. On a NARROWER
            // window the leftover goes NEGATIVE and the band starts left of the corner and spills
            // out both sides (maintainer observation at 450 wide). Clamp to what the frame can
            // hold: the texture is uniform along its width, so squeezing it costs nothing.
            int sepW = rect.Width - 60 < 433 ? rect.Width - 60 : 433;
            if (sepW < 1) sepW = 1;
            int distance = rect.Width - 60 - sepW;
            TopSep   = new Rectangle(TL.Right + distance / 2, TL.Y + 3, sepW, 4);
            TopHoriz = new Rectangle(TL.Right - 2, TopSep.Y, rect.Width - 54, 4);

            // ⚠ NO offset on any piece: the vertical rails stop at rect.Height-60 and the corners
            // must meet them - shifting one piece alone opens a visible break at the arc. A
            // caller that wants its rule ON a margin shifts its RECT (see BottomLine), never the
            // frame's parts.
            BL = new Rectangle(rect.X, rect.Bottom - 30, 28, 30);
            BR = new Rectangle(rect.Right - 28, rect.Bottom - 30, 28, 30);
            BotSep   = new Rectangle(BL.Right + distance / 2, BL.Y + 18, sepW, 12);
            BotHoriz = new Rectangle(BL.Right - 2, BotSep.Y, rect.Width - 54, 12);

            // the title band, plus the two stubs that carry it out to the frame's edges - which
            // is why it reads as full width where a band inset by a corner radius does not
            TitleRect  = new Rectangle(rect.X + 28, rect.Y + TitleBarTop, rect.Width - 56, TitleBarHeight);
            // Ludoal fork: the rule UNDER the title. PopupWindow only ever drew popup_separator
            // around a subtitle band, so a window without MiddleText - Colony, and every screen
            // converted in this sweep - had nothing closing its title bar (maintainer observation).
            // ⚠ Stretched, and that is safe: popup_separator fades along its own width (alpha 2 at
            // the ends, 254 at the centre), so scaling keeps the fade instead of banding it.
            TitleSep = new Rectangle(rect.X + 28, TitleRect.Bottom - 1, rect.Width - 56, 2);
            TitleLeft  = new Rectangle(TitleRect.X - 25, TitleRect.Y + 23, 25, TitleRect.Height - 23);
            TitleRight = new Rectangle(TitleRect.Right, TitleRect.Y + 23, 17, TitleRect.Height - 23);

            LeftVert  = new Rectangle(TL.X + 1, TL.Bottom, 2, rect.Height - 60);
            RightVert = new Rectangle(rect.Right - 11, TL.Bottom, 11, rect.Height - 60);
            BLc = new Rectangle(rect.X - 2, BL.Y, 28, 30);   // follow the corners they stroke
            BRc = new Rectangle(BR.X, BL.Y, 28, 30);
            BottomFill = new Rectangle(BL.Right, BL.Y, rect.Width - BL.Width - BR.Width, BL.Height - 12);
        }

        /// The body fill, drawn UNDER the frame. Separate from Draw because a caller may want to
        /// paint its own content between the two - the frame's edges must land on top of it.
        public readonly void DrawFill(SpriteBatch batch, in Rectangle rect)
        {
            batch.Draw(ResourceManager.Texture("Popup/popup_filler_lower"), rect, Color.White);
        }

        public readonly void Draw(SpriteBatch batch)
        {
            batch.Draw(ResourceManager.Texture("Popup/popup_corner_TL"), TL, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_corner_TR"), TR, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_corner_BL"), BL, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_corner_BR"), BR, Color.White);

            batch.Draw(ResourceManager.Texture("Popup/popup_horiz_T"), TopHoriz, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_horiz_T_gradient"), TopSep, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_vert_L"), LeftVert, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_vert_R"), RightVert, Color.White);

            batch.Draw(ResourceManager.Texture("Popup/popup_horiz_B"), BotHoriz, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_horiz_B_gradient"), BotSep, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_filler_lower"), BottomFill, Color.White);

            batch.Draw(ResourceManager.Texture("Popup/popup_filler_title"), TitleRect, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_filler_title"), TitleLeft, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_filler_title"), TitleRight, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_separator"), TitleSep, Color.White);

            batch.Draw(ResourceManager.Texture("Popup/popup_corner_TL_stroke"), TLc, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_corner_TR_stroke"), TRc, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_corner_BL_stroke"), BLc, Color.White);
            batch.Draw(ResourceManager.Texture("Popup/popup_corner_BR_stroke"), BRc, Color.White);
        }
    }
}
