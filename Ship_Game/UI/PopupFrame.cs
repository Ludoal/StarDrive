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

        /// Ludoal fork: the frame's texture set, resolved once per skin instead of name by name
        /// inside Draw. The skin is a Content/Textures folder declared in UI/Theme.yaml
        /// (PopupSkin); any piece the folder lacks falls back to the classic Popup set, so a
        /// partial skin still draws whole. ⚠ A replacement set must keep the classic
        /// dimensions - the geometry above (28x30 corners, the 4- and 12-tall bands, the
        /// border eats) is measured off these bitmaps, not derived from them.
        public class StyleTextures
        {
            public SubTexture CornerTL, CornerTR, CornerBL, CornerBR;
            public SubTexture StrokeTL, StrokeTR, StrokeBL, StrokeBR;
            public SubTexture HorizTop, HorizTopGradient;
            public SubTexture HorizBot, HorizBotGradient;
            public SubTexture VertL, VertR;
            public SubTexture FillerLower, FillerTitle, Separator;

            public StyleTextures(string skin)
            {
                SubTexture Tex(string piece)
                    => ResourceManager.TextureOrDefault($"{skin}/{piece}", $"Popup/{piece}");

                CornerTL = Tex("popup_corner_TL");
                CornerTR = Tex("popup_corner_TR");
                CornerBL = Tex("popup_corner_BL");
                CornerBR = Tex("popup_corner_BR");
                StrokeTL = Tex("popup_corner_TL_stroke");
                StrokeTR = Tex("popup_corner_TR_stroke");
                StrokeBL = Tex("popup_corner_BL_stroke");
                StrokeBR = Tex("popup_corner_BR_stroke");
                HorizTop         = Tex("popup_horiz_T");
                HorizTopGradient = Tex("popup_horiz_T_gradient");
                HorizBot         = Tex("popup_horiz_B");
                HorizBotGradient = Tex("popup_horiz_B_gradient");
                VertL = Tex("popup_vert_L");
                VertR = Tex("popup_vert_R");
                FillerLower = Tex("popup_filler_lower");
                FillerTitle = Tex("popup_filler_title");
                Separator   = Tex("popup_separator");
            }
        }

        static StyleTextures Styling;
        static int ContentId = -1;
        static string SkinName;

        /// The live texture set - public because PopupWindow's subtitle band and the Codex's
        /// title separator are cut from the same cloth and must follow the same skin.
        public static StyleTextures Style
        {
            get
            {
                string skin = UITheme.PopupSkin;
                if (Styling == null || ContentId != ResourceManager.ContentId || SkinName != skin)
                {
                    ContentId = ResourceManager.ContentId;
                    SkinName = skin;
                    Styling = new StyleTextures(skin);
                }
                return Styling;
            }
        }

        /// The body fill, drawn UNDER the frame. Separate from Draw because a caller may want to
        /// paint its own content between the two - the frame's edges must land on top of it.
        /// Ludoal fork (maintainer bench 337): the fill is INSET by the border thicknesses so the
        /// grey stops at the visible rule, not at the rect edge. The right/bottom border is a narrow
        /// rule with transparent shadow past it; a fill run to the rect edge shows grey through that
        /// shadow, ~10px past the rule bottom-right. Every caller passes the FULL frame rect and
        /// gets the same clean inset - a centred window (New Game) showed the overrun a screen-edge
        /// popup used to hide off-screen.
        public readonly void DrawFill(SpriteBatch batch, in Rectangle rect)
        {
            var fill = new Rectangle(rect.X + BorderLeft, rect.Y + TopInk,
                                     rect.Width - BorderLeft - BorderRight,
                                     rect.Height - TopInk - BottomLine);
            batch.Draw(Style.FillerLower, fill, Color.White);
        }

        public readonly void Draw(SpriteBatch batch)
        {
            StyleTextures s = Style;

            batch.Draw(s.CornerTL, TL, Color.White);
            batch.Draw(s.CornerTR, TR, Color.White);
            batch.Draw(s.CornerBL, BL, Color.White);
            batch.Draw(s.CornerBR, BR, Color.White);

            batch.Draw(s.HorizTop, TopHoriz, Color.White);
            batch.Draw(s.HorizTopGradient, TopSep, Color.White);
            batch.Draw(s.VertL, LeftVert, Color.White);
            batch.Draw(s.VertR, RightVert, Color.White);

            batch.Draw(s.HorizBot, BotHoriz, Color.White);
            batch.Draw(s.HorizBotGradient, BotSep, Color.White);
            batch.Draw(s.FillerLower, BottomFill, Color.White);

            // no title-band tint (maintainer bench 411): the band wears the BODY fill, so the
            // title reads on the same dark ground as the content, closed by the gradient rule
            batch.Draw(s.FillerLower, TitleRect, Color.White);
            batch.Draw(s.FillerLower, TitleLeft, Color.White);
            batch.Draw(s.FillerLower, TitleRight, Color.White);
            batch.Draw(s.Separator, TitleSep, Color.White);

            batch.Draw(s.StrokeTL, TLc, Color.White);
            batch.Draw(s.StrokeTR, TRc, Color.White);
            batch.Draw(s.StrokeBL, BLc, Color.White);
            batch.Draw(s.StrokeBR, BRc, Color.White);
        }
    }
}
