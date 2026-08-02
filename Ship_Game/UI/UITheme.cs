using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using Ship_Game.Data.Serialization;   // [StarDataType] / [StarData]
using Ship_Game.Data.Yaml;
using Color = Microsoft.Xna.Framework.Color;
using Font = Ship_Game.Graphics.Font;
// XNA brings its own Rectangle in on the line above: name the one this file means, the way
// Menu2 does. DrawPlate takes the SDGraphics one, and its callers pass that.
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    /// Ludoal fork: the game's UI style in ONE editable file - Content/UI/Theme.yaml.
    ///
    /// Colours and fonts were spread over 200-odd literals and 800-odd font references, each
    /// naming an appearance ("Arial12Bold", "new Color(118,102,67)") rather than a role. Retouching
    /// the look meant finding every site, and two of them always disagreed afterwards. Here a
    /// screen asks for what a thing IS - a section title, a hostile action, a table's row name -
    /// and the file decides what that looks like.
    ///
    /// It is YAML rather than C# on purpose: editing a colour and relaunching beats editing a
    /// colour and waiting out a build, which is the whole point when a look is still being found.
    [StarDataType]
    public class UIThemeData
    {
        // frames
        [StarData] public Color FrameRule      = new(193, 113, 26);
        [StarData] public Color FrameBody      = new(14, 14, 14, 240);
        [StarData] public Color FrameTitleBar  = new(54, 54, 54, 240);
        [StarData] public int   TitleBarHeight = 46;

        // buttons - hover and press are DERIVED from the tint, never separate colours
        [StarData] public Color PlateNeutral = new(193, 113, 26);
        [StarData] public Color PlateActive  = new(108, 152, 214);
        [StarData] public Color PlateHostile = new(206, 92, 84);
        [StarData] public Color PlateMuted   = new(122, 116, 104);
        [StarData] public float PlateOpacity = 0.92f;
        [StarData] public int   CornerRadius = 8;   // how round a button's corners are
        // how the plate is painted: the face fades from top to foot, the rule sits around it.
        // ⚠ These are alphas over whatever is behind - the tint at full strength reads as neon
        // on a small control, which is why the rule is well under 1.
        [StarData] public float FaceTop      = 0.34f;
        [StarData] public float FaceBottom   = 0.18f;
        [StarData] public float RuleStrength = 0.55f;
        [StarData] public int   RuleWidth    = 2;   // how thick the line around a plate runs
        [StarData] public float HoverLift    = 0.22f;
        [StarData] public float PressDrop    = 0.28f;

        // text
        [StarData] public Color TextPrimary = new(255, 240, 189);
        [StarData] public Color TextDim     = new(190, 180, 150);
        [StarData] public Color TextGood    = new(144, 238, 144);
        [StarData] public Color TextBad     = new(255, 96, 96);
        [StarData] public Color TextLocked  = new(105, 105, 105);

        // tables
        [StarData] public Color  TableHeader       = new(255, 240, 189);
        [StarData] public string TableHeaderFont   = "Arial14Bold";
        [StarData] public Color  TableText         = new(255, 240, 189);
        [StarData] public string TableTextFont     = "Arial12";
        [StarData] public Color  TableTitle        = new(255, 240, 189);
        [StarData] public string TableTitleFont    = "Arial20Bold";
        [StarData] public Color  TableSubtitle     = new(190, 180, 150);
        [StarData] public string TableSubtitleFont = "Arial12";
        [StarData] public Color  TableRowHover     = new(255, 255, 255, 30);
        [StarData] public Color  TableRowSelected  = new(193, 113, 26, 45);
        [StarData] public Color  TableGridLine     = new(118, 102, 67, 128);

        // fonts by role
        [StarData] public string WindowTitle  = "Arial20Bold";
        [StarData] public string SectionTitle = "Arial12Bold";
        [StarData] public string Body         = "Arial12";
        [StarData] public string Value        = "Arial12Bold";
        [StarData] public string Small        = "Arial10";
    }

    public static class UITheme
    {
        static UIThemeData Data;
        static int LoadedForContentId = -1;

        /// The live theme. Reloaded when content is, so a changed file takes effect on a restart
        /// without anything else having to know it changed.
        public static UIThemeData Theme
        {
            get
            {
                if (Data == null || LoadedForContentId != ResourceManager.ContentId)
                    Load();
                return Data;
            }
        }

        static void Load()
        {
            LoadedForContentId = ResourceManager.ContentId;
            try
            {
                // leading slash: the parser's own convention for a Content-relative path,
                // as in DeserializeArray<Good>("/Goods/Goods.yaml")
                Data = YamlParser.DeserializeOne<UIThemeData>("/UI/Theme.yaml");
            }
            catch (System.Exception e)
            {
                // a broken theme file must not take the game down with it - the defaults on
                // UIThemeData are the shipped look, so the UI still draws
                Log.Warning($"UI/Theme.yaml failed to load ({e.Message}); using built-in defaults");
            }
            Data ??= new UIThemeData();
        }

        // ── frames ───────────────────────────────────────────────────────────────────────────
        public static Color FrameRule     => Theme.FrameRule;
        public static Color FrameBody     => Theme.FrameBody;
        public static Color FrameTitleBar => Theme.FrameTitleBar;
        public static int   TitleBarH     => Theme.TitleBarHeight;

        // ── buttons ──────────────────────────────────────────────────────────────────────────
        public static Color PlateNeutral => Theme.PlateNeutral;
        public static Color PlateActive  => Theme.PlateActive;
        public static Color PlateHostile => Theme.PlateHostile;
        public static Color PlateMuted   => Theme.PlateMuted;
        public static float PlateOpacity => Theme.PlateOpacity;

        /// A hovered control is a LIGHTER version of itself, and a held one a darker version -
        /// derived from its own tint so a button never changes colour under the cursor.
        public static Color Hover(Color tint) => tint.LerpTo(Color.White, Theme.HoverLift);
        public static Color Press(Color tint) => tint.LerpTo(Color.Black, Theme.PressDrop);

        /// Ludoal fork: THE painted surface of this interface - rounded, top-lit, ruled - drawn
        /// row by row so it is exact at any size. Buttons and window frames both come through
        /// here; a frame that squared its corners while the buttons inside it rounded theirs was
        /// the whole of what made the two read as different furniture.
        ///
        /// `face` fills it (alpha applied per row for the relief), `rule` draws the edge and the
        /// arc. A button passes ONE tint for both; a window passes its body and its border, which
        /// are two different colours.
        public static void DrawPlate(SpriteBatch batch, in Rectangle r, Color face, Color rule,
                                     int radiusOverride = -1, int ruleWidthOverride = -1)
        {
            if (r.Width <= 0 || r.Height <= 0)
                return;

            int radius = radiusOverride >= 0 ? radiusOverride : Theme.CornerRadius;
            radius = Math.Min(radius, Math.Min(r.Width, r.Height) / 2);
            int rw = ruleWidthOverride >= 0 ? ruleWidthOverride : Theme.RuleWidth;
            rw = Math.Max(1, Math.Min(rw, Math.Min(r.Width, r.Height) / 2));

            for (int y = r.Y; y < r.Bottom; ++y)
            {
                // how far this row is inset by the arc, if it is in a corner band at all
                int dy = y < r.Y + radius       ? radius - (y - r.Y) - 1
                       : y >= r.Bottom - radius ? radius - (r.Bottom - 1 - y) - 1
                       : 0;
                int inset = 0;
                if (dy > 0)
                {
                    // the horizontal half-chord of the circle at this height
                    double dx = Math.Sqrt(Math.Max(0, radius * radius - dy * dy));
                    inset = radius - (int)Math.Round(dx);
                }

                int x = r.X + inset, w = r.Width - 2 * inset;
                if (w <= 0)
                    continue;

                // the FACE: lighter at the top, darker at the foot - the relief, in one place
                float t = (y - r.Y) / (float)Math.Max(1, r.Height - 1);
                float a = Theme.FaceTop + (Theme.FaceBottom - Theme.FaceTop) * t;
                batch.FillRectangle(new Rectangle(x, y, w, 1), face.Alpha(a));

                // the RULE: the first and last rows are a full line, every other row gets its
                // ends - which is what draws the arc, one row at a time
                if (y < r.Y + rw || y >= r.Bottom - rw)
                {
                    batch.FillRectangle(new Rectangle(x, y, w, 1), rule);
                }
                else
                {
                    batch.FillRectangle(new Rectangle(x, y, rw, 1), rule);
                    batch.FillRectangle(new Rectangle(x + w - rw, y, rw, 1), rule);
                }
            }
        }

        // ── text ─────────────────────────────────────────────────────────────────────────────
        public static Color TextPrimary => Theme.TextPrimary;
        public static Color TextDim     => Theme.TextDim;
        public static Color TextGood    => Theme.TextGood;
        public static Color TextBad     => Theme.TextBad;
        public static Color TextLocked  => Theme.TextLocked;

        // ── tables ───────────────────────────────────────────────────────────────────────────
        public static Color TableHeader      => Theme.TableHeader;
        public static Color TableText        => Theme.TableText;
        public static Color TableTitle       => Theme.TableTitle;
        public static Color TableSubtitle    => Theme.TableSubtitle;
        public static Color TableRowHover    => Theme.TableRowHover;
        public static Color TableRowSelected => Theme.TableRowSelected;
        public static Color TableGridLine    => Theme.TableGridLine;

        public static Font TableHeaderFont   => FontOf(Theme.TableHeaderFont, Fonts.Arial14Bold);
        public static Font TableTextFont     => FontOf(Theme.TableTextFont, Fonts.Arial12);
        public static Font TableTitleFont    => FontOf(Theme.TableTitleFont, Fonts.Arial20Bold);
        public static Font TableSubtitleFont => FontOf(Theme.TableSubtitleFont, Fonts.Arial12);

        // ── fonts by role ────────────────────────────────────────────────────────────────────
        public static Font WindowTitle  => FontOf(Theme.WindowTitle, Fonts.Arial20Bold);
        public static Font SectionTitle => FontOf(Theme.SectionTitle, Fonts.Arial12Bold);
        public static Font Body         => FontOf(Theme.Body, Fonts.Arial12);
        public static Font Value        => FontOf(Theme.Value, Fonts.Arial12Bold);
        public static Font Small        => FontOf(Theme.Small, Fonts.Arial10);

        /// ⚠ A name that is not one of the loaded fonts must not throw in the middle of a Draw -
        /// a typo in the theme file would take down whichever screen happened to be open.
        static Font FontOf(string name, Font fallback)
        {
            if (name.IsEmpty())
                return fallback;
            try { return Fonts.GetFont(name); }
            catch { return fallback; }
        }
    }
}
