using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using System;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    public enum ButtonStyle
    {
        Default,    // empiretopbar_btn_168px
        Small,      // empiretopbar_btn_68px
        Low80,      // empiretopbar_low_btn_80px
        Low100,     // empiretopbar_low_btn_100px
        Medium,     // empiretopbar_btn_132px       -- GoldenBrown button
        MediumMenu, // empiretopbar_btn_132px_menu  -- Grayed out button
        BigDip,     // empiretopbar_btn_168px_dip
        Military,   // empiretopbar_btn_168px_military
        Close,      // NewUI/Close_Normal
        ResearchQueueUp, // "ResearchMenu/button_queue_up"
        ResearchQueueDown, // "ResearchMenu/button_queue_down"
        ResearchQueueCancel, // "ResearchMenu/button_queue_cancel"
        ResearchQueueToTop, // "ResearchMenu/button_queue_to_top"
        // Ludoal fork: a wide button says what it MEANS, not which bitmap it once used. The six
        // dan_button styles (three stock, three "clear") drew six textures for three meanings;
        // they are one mechanism now, so the name carries the meaning and the tint draws it.
        // ⚠ Styling below is indexed by this enum: insert in BOTH, at the same rank.
        Wide,        // neutral
        WideActive,  // an active control, or the order that starts something
        WideHostile, // a hostile action, or the one that cancels an order
        EventConfirm, // UI/btn_event_confirm -- a big wide confirm button for Event Popups
        Text,       // only use TEXT as the button 
    }

    public partial class UIButton
    {
        // Ludoal fork: the one button asset, and how deep its border runs. Both live here so a new
        // PNG (or a thicker frame in a redrawn one) is a single edit for the whole game.
        public const string ButtonTex = "NewUI/button_9s";
        // ⚠ Must be >= the asset's corner RADIUS, or the slice cuts the arc in half and the
        // rounding never shows. The corners are drawn at 6, so this is 6.
        public const int ButtonSlice = 6;

        // The palette a button can mean: nothing in particular, an active control, a hostile
        // action, or one that is currently out of reach. The asset is greyscale, so these ARE
        // the look - change them here and every button of that meaning follows.
        // ⚠ These are the tint at FULL brightness, i.e. what the asset's 255 (the frame) renders
        // as - not the colour of the button's face, which the ramp takes down to about a quarter.
        // A tint multiplies, so it can never lift a pixel: pick them bright, or the frame sinks
        // into the face and the button reads as a bare rectangle.
        public static readonly Color PlateNeutral = new Color(168, 146, 102);
        public static readonly Color PlateActive  = new Color(108, 152, 214);
        public static readonly Color PlateHostile = new Color(206, 92, 84);
        public static readonly Color PlateMuted   = new Color(122, 116, 104);

        public class StyleTextures
        {
            public SubTexture Normal;
            public SubTexture Hover;
            public SubTexture Pressed;

            // Text Colors
            public Color DefaultTextColor = new Color(255, 240, 189);
            public Color HoverTextColor   = new Color(255, 240, 189);
            public Color PressTextColor   = new Color(255, 240, 189);

            // Fallback background colors if texture is null
            public Color DefaultColor = new Color(96, 81, 49);
            public Color HoverColor   = new Color(106, 91, 59);
            public Color PressColor   = new Color(86, 71, 39);

            public bool DrawBackground = true;

            public StyleTextures()
            {
            }

            // Ludoal fork: ONE mechanism for every button. A greyscale bitmap is drawn as a
            // nine-slice - corners keep their pixels, edges stretch on one axis, the middle on
            // both - and the colour comes from the tint rather than from a texture per meaning.
            // Three knobs: which bitmap, which tint per state, how opaque. A button can be pulled
            // to any width and a fair way in height, and a redrawn PNG restyles the whole game.
            public bool NineSlice;
            public int SliceBorder = ButtonSlice;
            public float Opacity = 1f;
            // ⚠ The slice asset is 32x32; a button that never got an explicit rect measures
            // itself off its texture, so it would come out 32x32 instead of the size the stock
            // one gave it. This carries the ORIGINAL size, and is never drawn.
            public SubTexture SizeRef;

            public static StyleTextures Sliced(Color plate, float opacity = 1f, string sizeRef = null,
                                               string tex = ButtonTex, int border = ButtonSlice)
            {
                SubTexture t = ResourceManager.Texture(tex);
                return new StyleTextures
                {
                    Normal = t, Hover = t, Pressed = t,
                    SizeRef     = sizeRef != null ? ResourceManager.Texture(sizeRef) : null,
                    NineSlice   = true,
                    SliceBorder = border,
                    Opacity     = opacity,
                    DefaultColor = plate,
                    HoverColor   = plate.LerpTo(Color.White, 0.22f),
                    PressColor   = plate.LerpTo(Color.Black, 0.28f),
                };
            }

            public StyleTextures(string normal)
            {
                Normal  = ResourceManager.Texture(normal);
                Hover   = ResourceManager.Texture(normal + "_hover");
                Pressed = ResourceManager.Texture(normal + "_pressed");
            }

            public StyleTextures(string normal, string hover)
            {
                Normal  = ResourceManager.Texture(normal);
                Hover   = ResourceManager.Texture(hover);
                Pressed = Hover;
            }

            public StyleTextures(string normal, string hover, string pressed)
            {
                Normal = ResourceManager.Texture(normal);
                Hover = ResourceManager.Texture(hover);
                Pressed = ResourceManager.Texture(pressed);
            }

        }

        static int ContentId;
        static StyleTextures[] Styling;

        static StyleTextures GetDefaultStyle(ButtonStyle style)
        {
            if (Styling != null && ContentId == ResourceManager.ContentId)
                return Styling[(int)style];

            ContentId = ResourceManager.ContentId;
            Styling = new[]
            {
                // Ludoal fork: one bitmap, one mechanism, the meaning carried by the tint. These
                // eight came in five different widths (52 to 168), which no single stretched
                // bitmap could serve; sliced, every one of them is the same control at its own
                // size. Opacity is per style because the bar sits over the map, where a solid
                // plate reads as a hole, while a menu over black wants its full body.
                StyleTextures.Sliced(PlateNeutral, 0.92f, "EmpireTopBar/empiretopbar_btn_168px"),
                StyleTextures.Sliced(PlateNeutral, 0.92f, "EmpireTopBar/empiretopbar_btn_68px"),
                StyleTextures.Sliced(PlateNeutral, 0.85f, "EmpireTopBar/empiretopbar_low_btn_80px"),
                StyleTextures.Sliced(PlateNeutral, 0.85f, "EmpireTopBar/empiretopbar_low_btn_100px"),
                StyleTextures.Sliced(PlateNeutral, 0.92f, "EmpireTopBar/empiretopbar_btn_132px"),
                StyleTextures.Sliced(PlateMuted,   0.75f, "EmpireTopBar/empiretopbar_btn_132px_menu"),
                StyleTextures.Sliced(PlateActive,  0.92f, "EmpireTopBar/empiretopbar_btn_168px_dip"),
                StyleTextures.Sliced(PlateHostile, 0.92f, "EmpireTopBar/empiretopbar_btn_168px_military"),
                new StyleTextures("NewUI/Close_Normal", "NewUI/Close_Hover"),
                new StyleTextures("ResearchMenu/button_queue_up", "ResearchMenu/button_queue_up_hover"),
                new StyleTextures("ResearchMenu/button_queue_down", "ResearchMenu/button_queue_down_hover"),
                new StyleTextures("ResearchMenu/button_queue_cancel", "ResearchMenu/button_queue_cancel_hover"),
                new StyleTextures("ResearchMenu/button_queue_to_top", "ResearchMenu/button_queue_to_top_hover"),
                // the three wide styles. They sit side by side on a Planets row - neutral, active,
                // hostile - which makes that screen the place to read the tints against each other.
                StyleTextures.Sliced(PlateNeutral, 0.92f, "UI/dan_button"),   // Wide
                StyleTextures.Sliced(PlateActive,  0.92f, "UI/dan_button"),   // WideActive
                StyleTextures.Sliced(PlateHostile, 0.92f, "UI/dan_button"),   // WideHostile
                new StyleTextures("UI/btn_event_confirm_big"),
                new StyleTextures() { DrawBackground = false },
            };
            return Styling[(int) style];
        }

        void SetStyle(ButtonStyle style)
        {
            StyleTextures defaultStyle = GetDefaultStyle(style);
            SetStyle(defaultStyle);
        }

        // Ludoal fork: draw the style's bitmap as a nine-slice - see StyleTextures.Sliced
        bool NineSlice;
        int SliceBorder;
        SubTexture SizeRef;          // measured, never drawn - see StyleTextures.SizeRef
        public float Opacity = 1f;   // per-button override, on top of its style

        void SetStyle(StyleTextures style)
        {
            Normal = style.Normal;
            Hover = style.Hover;
            Pressed = style.Pressed;
            NineSlice = style.NineSlice;
            SliceBorder = style.SliceBorder;
            SizeRef = style.SizeRef;
            Opacity = style.Opacity;

            DefaultTextColor = style.DefaultTextColor;
            HoverTextColor = style.HoverTextColor;
            PressTextColor = style.PressTextColor;

            DefaultColor = style.DefaultColor;
            HoverColor = style.HoverColor;
            PressColor = style.PressColor;

            DrawBackground = style.DrawBackground;
        }

        Vector2 GetInitialSize()
        {
            if (SizeRef != null)   // the slice asset is square; the stock texture holds the size
                return SizeRef.SizeF;
            if (Normal != null)
                return Normal.SizeF;
            return new Vector2(2, 2);
        }
        
        SubTexture ButtonTexture()
        {
            switch (State)
            {
                default:                 return Normal;
                case PressState.Hover:   return Hover;
                case PressState.Pressed: return Pressed;
            }
        }

        Color BackgroundColor()
        {
            switch (State)
            {
                default:                 return DefaultColor;
                case PressState.Hover:   return HoverColor;
                case PressState.Pressed: return PressColor;
            }
        }

        Color TextColor()
        {
            switch (State)
            {
                default:                 return DefaultTextColor;
                case PressState.Hover:   return HoverTextColor;
                case PressState.Pressed: return PressTextColor;
            }
        }

    }
}