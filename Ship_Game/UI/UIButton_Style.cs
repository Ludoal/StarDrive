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
        DanButton,     // UI/dan_button  -- wide brown button
        DanButtonBlue, // UI/dan_button_blue -- blue version of dan_button
        DanButtonRed, // UI/dan_button_red -- red version of dan_button
        // Ludoal fork: the NewUI/dan_button family, dormant in Content until now. Added rather than
        // repointed - the three styles above are used by the stock screens, and the whole game would
        // change look. ⚠ Styling below is indexed by this enum: insert in BOTH, at the same rank.
        DanButtonClear, // NewUI/dan_button_clear -- the new look, hover goes blue
        DanButtonClearRed, // NewUI/dan_button_red_clear -- the new look, for a hostile action
        DanButtonClearBlue, // NewUI/dan_button_blue_clear -- the new look, for an active control
        EventConfirm, // UI/btn_event_confirm -- a big wide confirm button for Event Popups
        Text,       // only use TEXT as the button 
    }

    public partial class UIButton
    {
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

            // Ludoal fork: a PAINTED style keeps a texture for its SIZE but never draws it, so a
            // button that never got an explicit rect still measures itself the way it always did
            // (GetInitialSize reads Normal). Painting rather than stretching one bitmap is what
            // lets a 52px and a 182px button share a look: the stock button textures come in five
            // widths and the new-look bitmap only in one, so any swap would deform the rule on
            // its edges.
            public SubTexture SizeRef;

            public StyleTextures()
            {
            }

            // Ludoal fork: the painted new look - dark translucent plate, thin gold rule, in the
            // grammar the reworked screens and the top bar already use.
            public static StyleTextures Painted(string sizeRef) => new StyleTextures
            {
                SizeRef        = ResourceManager.Texture(sizeRef),
                DrawBackground = true,
                DefaultColor   = new Color(14, 12, 9),
                HoverColor     = new Color(38, 56, 84),
                PressColor     = new Color(8, 7, 5),
            };

            // A button that MEANS something (an active control, a hostile action) keeps its colour
            // in the plate rather than in a texture of its own.
            public static StyleTextures PaintedTinted(string sizeRef, Color plate) => new StyleTextures
            {
                SizeRef        = ResourceManager.Texture(sizeRef),
                DrawBackground = true,
                DefaultColor   = plate,
                HoverColor     = plate.LerpTo(Color.White, 0.18f),
                PressColor     = plate.LerpTo(Color.Black, 0.25f),
            };

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

            public StyleTextures(string normal, ButtonStyle style)
            {
                Normal = Hover = Pressed = ResourceManager.Texture(normal);
                switch (style)
                {
                    default:
                    case ButtonStyle.DanButton:
                        HoverTextColor = new Color(255, 255, 255, 150).Premultiplied();
                        PressTextColor = new Color(255, 255, 255, 150).Premultiplied();
                        break;
                    case ButtonStyle.DanButtonBlue:
                        DefaultTextColor = new Color(205, 229, 255);
                        HoverTextColor   = new Color(174, 202, 255);
                        PressTextColor   = new Color(174, 202, 255);
                        break;
                    case ButtonStyle.DanButtonRed:
                        DefaultTextColor = Color.Red;
                        HoverTextColor   = Color.White;
                        PressTextColor   = Color.Green;
                        break;
                }
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
                // Ludoal fork: painted, not textured. These eight came in five different widths
                // (52 to 168), so no single bitmap could serve them without stretching its rule;
                // a plate takes any width. Each keeps its old texture as the SIZE reference, so
                // buttons that never got an explicit rect measure exactly as before.
                StyleTextures.Painted("EmpireTopBar/empiretopbar_btn_168px"),
                StyleTextures.Painted("EmpireTopBar/empiretopbar_btn_68px"),
                StyleTextures.Painted("EmpireTopBar/empiretopbar_low_btn_80px"),
                StyleTextures.Painted("EmpireTopBar/empiretopbar_low_btn_100px"),
                StyleTextures.Painted("EmpireTopBar/empiretopbar_btn_132px"),
                StyleTextures.Painted("EmpireTopBar/empiretopbar_btn_132px_menu"),
                StyleTextures.Painted("EmpireTopBar/empiretopbar_btn_168px_dip"),
                StyleTextures.Painted("EmpireTopBar/empiretopbar_btn_168px_military"),
                new StyleTextures("NewUI/Close_Normal", "NewUI/Close_Hover"),
                new StyleTextures("ResearchMenu/button_queue_up", "ResearchMenu/button_queue_up_hover"),
                new StyleTextures("ResearchMenu/button_queue_down", "ResearchMenu/button_queue_down_hover"),
                new StyleTextures("ResearchMenu/button_queue_cancel", "ResearchMenu/button_queue_cancel_hover"),
                new StyleTextures("ResearchMenu/button_queue_to_top", "ResearchMenu/button_queue_to_top_hover"),
                // Ludoal fork: painted too, so every button in the game shares ONE grammar rather
                // than a bitmap here and a plate there. Blue and red keep their meaning through
                // the plate colour instead of a separate texture.
                StyleTextures.Painted("UI/dan_button"),
                StyleTextures.PaintedTinted("UI/dan_button_blue", new Color(38, 56, 84)),
                StyleTextures.PaintedTinted("UI/dan_button_red",  new Color(96, 34, 34)),
                // Ludoal fork: same rank as DanButtonClear in the enum above
                new StyleTextures("NewUI/dan_button_clear", "NewUI/dan_button_blue_clear",
                                  "NewUI/dan_button_blue_clear"),
                new StyleTextures("NewUI/dan_button_red_clear", "NewUI/dan_button_blue_clear",
                                  "NewUI/dan_button_blue_clear"),
                new StyleTextures("NewUI/dan_button_blue_clear", "NewUI/dan_button_clear",
                                  "NewUI/dan_button_clear"),
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

        SubTexture SizeRef;   // Ludoal fork: measured, never drawn - see StyleTextures.Painted

        void SetStyle(StyleTextures style)
        {
            Normal = style.Normal;
            Hover = style.Hover;
            Pressed = style.Pressed;
            SizeRef = style.SizeRef;

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
            if (Normal != null)
                return Normal.SizeF;
            if (SizeRef != null)   // Ludoal fork: a painted style measures off its reference
                return SizeRef.SizeF;
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