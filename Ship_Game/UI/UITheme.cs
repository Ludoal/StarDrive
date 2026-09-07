using System;                          // Math, in DrawPlate's arc arithmetic
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
    /// A screen asks for what a thing IS - a section title, a hostile action, a table's row
    /// name - and the file decides what that looks like, instead of each site naming an
    /// appearance of its own ("Arial12Bold", "new Color(118,102,67)").
    ///
    /// YAML rather than C#: a colour can be changed and the game relaunched without a build.
    [StarDataType]
    public class UIThemeData
    {
        // frames
        [StarData] public Color FrameRule      = new(193, 113, 26);
        [StarData] public Color FrameBody      = new(14, 14, 14, 240);
        [StarData] public int   TitleBarHeight = 46;
        // the Content/Textures folder the popup frame's bitmaps come from - swapping the
        // folder reskins every popup window; PopupFrame falls back piece by piece to the
        // classic Popup set for anything the folder lacks
        [StarData] public string PopupSkin = "Popup";
        // same lever for the submenu chrome (the tab groups' frame and headers): the folder
        // holding the submenu_* pieces. Applies to the default Brown style; the Blue research
        // style is its own deliberate look and stays put.
        [StarData] public string SubmenuSkin = "NewUI";

        // ── tab panels: the two paddings ──────────────────────────────────────────────
        // OUTER is how far a tab panel's content area sits inside the panel's own edges.
        // ⚠ floored per edge at the corner texture's size (9): below that the content runs
        // under the drawn corner. Above it, anything goes.
        [StarData] public int TabPadOuter = 9;
        // INNER is the layout STEP inside a panel: the air between a panel and what sits under
        // it, and the gap a foot notch reckons with. ⚠ It is NOT the text's inset - each kind of
        // element carries its own default (TextPad, ListPadLeft).
        [StarData] public int TabPadInner = 10;
        // The painted plate's height. ⚠ The Wide styles' size reference (UI/dan_button) is
        // 182x25, so 25 is the plate's own height rather than a number picked for a screen
        // (maintainer feedback).
        [StarData] public int ButtonHeight = 25;
        // The lane a scroll list reserves on its right for the scrollbar. ⚠ The drawing is not
        // one width: the BAR is 11 wide, the ARROWS 13. 25 leaves 12 clear to the left of the
        // widest part - room for a selection frame - and the bar closes on the list's own edge,
        // the tab's padding being the margin (maintainer feedback).
        [StarData] public int ScrollbarLane = 25;
        // What a scroll list keeps on its LEFT, inside its own rect. It is the list's own margin,
        // not the panel's: the row's selection frame is drawn in it. A list therefore sits on the
        // CLIENT area and adds this, instead of taking TabPadInner and stacking two margins.
        [StarData] public int ListPadLeft = 5;
        // What TEXT keeps inside a panel's client area, per AXIS. The maintainer's model: every
        // kind of element carries its own default and an instance may overrule it. Sideways, 0 -
        // the frame's own clearance is margin enough. Down, a little air: a caption pinned to the
        // tab strip reads as a mistake, and 7 puts the Galaxy option rows on their 10.
        [StarData] public int TextPadX = 0;
        [StarData] public int TextPadY = 7;

        // buttons - hover and press are DERIVED from the tint, never separate colours
        [StarData] public Color PlateNeutral = new(193, 113, 26);
        [StarData] public Color PlateActive  = new(108, 152, 214);
        [StarData] public Color PlateHostile = new(206, 92, 84);
        [StarData] public Color PlateMuted   = new(122, 116, 104);
        [StarData] public float PlateOpacity = 0.92f;
        [StarData] public int   CornerRadius = 8;   // how round a button's corners are
        // how the plate is painted: the face fades from top to foot, the rule sits around it.
        // ⚠ These are alphas over whatever is behind - the tint at full strength reads as neon
        // on a small control, so the rule stays well under 1.
        // ⚠ these are what a button's opacity ACTUALLY is - PlateOpacity multiplies this ramp
        // rather than replacing it, so the ramp bounds the plate whatever PlateOpacity reads.
        // The gap between top and bottom is what makes the relief.
        [StarData] public float FaceTop      = 0.62f;
        [StarData] public float FaceBottom   = 0.46f;
        [StarData] public float RuleStrength = 0.55f;
        [StarData] public int   RuleWidth    = 2;   // how thick the line around a plate runs
        [StarData] public float HoverLift    = 0.22f;
        [StarData] public float PressDrop    = 0.28f;

        // controls - the small input furniture: slider tracks, checkbox boxes, dropdown
        // panels. One outline for all of them: the slider's track and the checkbox's box
        // disagreed by a shade (72,61,38 vs 96,81,49) for no reason anyone could name.
        [StarData] public Color ControlOutline      = new(96, 81, 49);
        [StarData] public Color ControlOutlineHover = new(164, 154, 133);
        [StarData] public Color ControlFill         = new(22, 22, 23);
        [StarData] public Color ControlHoverFill    = new(128, 87, 43, 50);

        // text
        [StarData] public Color TextPrimary = new(255, 240, 189);
        // fonts by role
        [StarData] public string WindowTitle  = "Arial20Bold";
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
        public static int   TitleBarH     => Theme.TitleBarHeight;
        public static string PopupSkin    => Theme.PopupSkin;
        public static string SubmenuSkin  => Theme.SubmenuSkin;
        public static int   TabPadOuter   => Theme.TabPadOuter;
        public static int   TabPadInner   => Theme.TabPadInner;
        public static int   ButtonHeight  => Theme.ButtonHeight;
        public static int   ScrollbarLane => Theme.ScrollbarLane;
        public static int   ListPadLeft   => Theme.ListPadLeft;
        public static int   TextPadX     => Theme.TextPadX;
        public static int   TextPadY     => Theme.TextPadY;

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
        /// here, so they read as one family of furniture.
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

        // ── controls ─────────────────────────────────────────────────────────────────────────
        /// Ludoal fork: the themed layer for the leaf widgets. A slider, checkbox or dropdown
        /// asks for its outline, panel fill or hover wash here instead of carrying its own colour
        /// literals, so a new widget gets the theme for free by drawing through these.
        public static Color ControlOutline(bool hovered = false)
            => hovered ? Theme.ControlOutlineHover : Theme.ControlOutline;

        public static void DrawControlOutline(SpriteBatch batch, in Rectangle r, bool hovered = false)
            => batch.DrawRectangle(r, ControlOutline(hovered));

        /// the solid fill behind an open panel - a dropdown's list, a combo's tray
        public static void DrawControlFill(SpriteBatch batch, in Rectangle r)
            => batch.FillRectangle(r, Theme.ControlFill);

        /// the translucent wash over a hovered row or surface; premultiplied here so no
        /// caller trips the additive-blend analyzer by filling with a raw alpha colour
        public static void DrawControlHoverFill(SpriteBatch batch, in Rectangle r)
            => batch.FillRectangle(r, Theme.ControlHoverFill.Premultiplied());

        // ── text ─────────────────────────────────────────────────────────────────────────────
        public static Color TextPrimary => Theme.TextPrimary;

        // ── fonts by role ────────────────────────────────────────────────────────────────────
        public static Font WindowTitle  => FontOf(Theme.WindowTitle, Fonts.Arial20Bold);

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
