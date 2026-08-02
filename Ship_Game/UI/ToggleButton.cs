using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.AI;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public class ToggleButtonStyle
    {
        public int Width  { get; private set; }
        public int Height { get; private set; }
        public int ContentId { get; private set; }
        string Folder;

        /// Ludoal fork: TRUE for the styles whose art is a PLATE (the Minimap family), FALSE for
        /// the bare glyphs (the SelectionBox arrows). A plate gets an opaque ground painted under
        /// it so it reads over the starfield; an arrow must not.
        public bool Plated;
        public SubTexture Active   { get; private set; }
        public SubTexture Inactive { get; private set; }
        public SubTexture Hover    { get; private set; }
        public SubTexture Press    { get; private set; }

        public void Reload()
        {
            ContentId = ResourceManager.ContentId;
            Active   = ResourceManager.Texture(Folder + Active.Name);
            Inactive = ResourceManager.Texture(Folder + Inactive.Name);
            Hover    = ResourceManager.Texture(Folder + Hover.Name);
            Press    = ResourceManager.Texture(Folder + Press.Name);
        }

        public static readonly ToggleButtonStyle Formation = new ToggleButtonStyle
        {
            Width  = 24,
            Height = 24,
            ContentId = ResourceManager.ContentId,
            Folder   = "SelectionBox/",
            Active   = ResourceManager.Texture("SelectionBox/button_formation_active"),
            Inactive = ResourceManager.Texture("SelectionBox/button_formation_inactive"),
            Hover    = ResourceManager.Texture("SelectionBox/button_formation_hover"),
            Press    = ResourceManager.Texture("SelectionBox/button_formation_pressed")
        };

        public static readonly ToggleButtonStyle LockedDesigns = new ToggleButtonStyle
        {
            Width  = 20,
            Height = 20,
            ContentId = ResourceManager.ContentId,
            Folder   = "SelectionBox/",
            Active   = ResourceManager.Texture("SelectionBox/button_LockedDesigns_active"),
            Inactive = ResourceManager.Texture("SelectionBox/button_formation_inactive"),
            Hover    = ResourceManager.Texture("SelectionBox/button_formation_hover"),
            Press    = ResourceManager.Texture("SelectionBox/button_formation_pressed")
        };

        public static readonly ToggleButtonStyle Grid = new ToggleButtonStyle
        {
            Width  = 34,
            Height = 24,
            ContentId = ResourceManager.ContentId,
            Folder   = "SelectionBox/",
            Active   = ResourceManager.Texture("SelectionBox/button_grid_active"),
            Inactive = ResourceManager.Texture("SelectionBox/button_grid_inactive"),
            Hover    = ResourceManager.Texture("SelectionBox/button_grid_hover"),
            Press    = ResourceManager.Texture("SelectionBox/button_grid_pressed")
        };

        public static readonly ToggleButtonStyle PlayerDesigns = new ToggleButtonStyle
        {
            Width  = 29,
            Height = 20,
            ContentId = ResourceManager.ContentId,
            Folder   = "SelectionBox/",
            Active   = ResourceManager.Texture("SelectionBox/button_PlayerDesigns_active"),
            Inactive = ResourceManager.Texture("SelectionBox/button_grid_inactive"),
            Hover    = ResourceManager.Texture("SelectionBox/button_grid_hover"),
            Press    = ResourceManager.Texture("SelectionBox/button_grid_pressed")
        };

        public static readonly ToggleButtonStyle ArrowLeft = new ToggleButtonStyle
        {
            Width  = 14,
            Height = 35,
            ContentId = ResourceManager.ContentId,
            Folder   = "SelectionBox/",
            Active   = ResourceManager.Texture("SelectionBox/button_arrow_left"),
            Inactive = ResourceManager.Texture("SelectionBox/button_arrow_left"),
            Hover    = ResourceManager.Texture("SelectionBox/button_arrow_left_hover"),
            Press    = ResourceManager.Texture("SelectionBox/button_arrow_left_hover")
        };

        public static readonly ToggleButtonStyle ArrowRight = new ToggleButtonStyle
        {
            Width  = 14,
            Height = 35,
            ContentId = ResourceManager.ContentId,
            Folder   = "SelectionBox/",
            Active   = ResourceManager.Texture("SelectionBox/button_arrow_right"),
            Inactive = ResourceManager.Texture("SelectionBox/button_arrow_right"),
            Hover    = ResourceManager.Texture("SelectionBox/button_arrow_right_hover"),
            Press    = ResourceManager.Texture("SelectionBox/button_arrow_right_hover")
        };

        public static readonly ToggleButtonStyle ButtonB = new ToggleButtonStyle
        {
            Width  = 25,
            Height = 22,
            ContentId = ResourceManager.ContentId,
            Folder   = "Minimap/",
            Plated   = true,
            Active   = ResourceManager.Texture("Minimap/button_B_active"),
            Inactive = ResourceManager.Texture("Minimap/button_B_normal"),
            Hover    = ResourceManager.Texture("Minimap/button_B_hover"),
            Press    = ResourceManager.Texture("Minimap/button_B_normal")
        };

        public static readonly ToggleButtonStyle ButtonC = new ToggleButtonStyle
        {
            Width  = 25,
            Height = 22,
            ContentId = ResourceManager.ContentId,
            Folder   = "Minimap/",
            Plated   = true,
            Active   = ResourceManager.Texture("Minimap/button_C_normal"),
            Inactive = ResourceManager.Texture("Minimap/button_C_normal"),
            Hover    = ResourceManager.Texture("Minimap/button_C_hover"),
            Press    = ResourceManager.Texture("Minimap/button_C_normal")
        };

        public static readonly ToggleButtonStyle Button = new ToggleButtonStyle
        {
            Width  = 25,
            Height = 22,
            ContentId = ResourceManager.ContentId,
            Folder   = "Minimap/",
            Plated   = true,
            Active   = ResourceManager.Texture("Minimap/button_active"),
            Inactive = ResourceManager.Texture("Minimap/button_normal"),
            Hover    = ResourceManager.Texture("Minimap/button_hover"),
            Press    = ResourceManager.Texture("Minimap/button_normal")
        };

        public static readonly ToggleButtonStyle ButtonDown = new ToggleButtonStyle
        {
            Width  = 25,
            Height = 26,
            ContentId = ResourceManager.ContentId,
            Folder   = "Minimap/",
            Plated   = true,
            Active   = ResourceManager.Texture("Minimap/button_active"),
            Inactive = ResourceManager.Texture("Minimap/button_down_inactive"),
            Hover    = ResourceManager.Texture("Minimap/button_down_hover"),
            Press    = ResourceManager.Texture("Minimap/button_down_inactive")
        };
    }

    // TODO: Replace with UIButton
    public class ToggleButton : UIElementV2
    {
        // If TRUE, this ToggleButton is Toggled Active [x], if false, it is inactive [ ]
        public bool IsToggled;

        public bool Hover;
        bool WasClicked; // purely visual

        public LocalizedText Tooltip;

        readonly ToggleButtonStyle Style;
        SubTexture IconTexture, IconActive;

        Vector2 WordPos;
        protected string IconPath;
        Rectangle IconRect;

        public Action<ToggleButton> OnClick;
        public Action<ToggleButton> OnHover;

        public override string ToString() => $"{TypeName} [{(IsToggled?"x":" ")}] {ElementDescr} Icon:{IconPath}";

        public ToggleButton(Vector2 pos, ToggleButtonStyle style, string iconPath = "")
        {
            Pos = pos;
            Size = new Vector2(style.Width, style.Height);
            Style = style;
            IconPath = iconPath;
            UpdateStyle();
            this.PerformLayout();
        }

        public ToggleButton(float x, float y, ToggleButtonStyle style, string iconPath = "")
            : this(new Vector2(x, y), style, iconPath)
        {
        }

        public ToggleButton(ToggleButtonStyle style, string iconPath, Action<ToggleButton> onClick)
        {
            Size = new Vector2(style.Width, style.Height);
            Style = style;
            IconPath = iconPath;
            OnClick = onClick;
            UpdateStyle();
            this.PerformLayout();
        }

        public override void PerformLayout()
        {
            if (IconTexture == null)
            {
                WordPos = new Vector2(X + 12 - Fonts.Arial12Bold.MeasureString(IconPath).X / 2f,
                                      Y + 12 - Fonts.Arial12Bold.LineSpacing / 2f);             
            }
            else
            {
                // Ludoal fork: an icon bigger than the button frame overflowed it
                // (32px FollowIcon in a 24px frame) — clamp to the frame with a margin
                int iconW = IconTexture.Width, iconH = IconTexture.Height;
                if (iconW > Rect.Width - 4 || iconH > Rect.Height - 4)
                {
                    float fit = Math.Min((Rect.Width - 4f) / iconW, (Rect.Height - 4f) / iconH);
                    iconW = (int)(iconW * fit);
                    iconH = (int)(iconH * fit);
                }
                IconRect = new Rectangle((int)CenterX - iconW / 2,
                                         (int)CenterY - iconH / 2,
                                         iconW, iconH);
            }
        }

        void UpdateStyle()
        {
            if (Style.ContentId != ResourceManager.ContentId)
            {
                Style.Reload();
            }
            if (IconPath.NotEmpty())
            {
                IconTexture = ResourceManager.TextureOrNull(IconPath);
                IconActive  = ResourceManager.TextureOrNull(IconPath+"_active");
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Visible)
                return;

            UpdateStyle();

            if (WasClicked)
            {
                WasClicked = false;
                batch.Draw(Style.Press, Rect, Color.White);
            }
            // Ludoal fork: a solid ground UNDER the texture (maintainer: "les boutons sont trop
            // transparents"). The Minimap art averages alpha 182, so over a starfield the button
            // reads as a ghost - and the alpha lives in the PNGs, where raising it would mean
            // redrawing shared assets. An opaque plate behind them costs nothing and lifts every
            // state at once; the toggled one wears the theme's active tint so ON still reads ON.
            // ⚠ only the Minimap family: those are plates, and a solid ground behind them is what
            // makes them read. The arrow styles are bare glyphs from SelectionBox/ - a dark
            // square behind an arrow would be worse than the transparency it fixes.
            if (Style.Plated)
                batch.FillRectangle(Rect, IsToggled ? UITheme.PlateActive.Alpha(0.55f)
                                                    : new Color(12, 12, 12).Alpha(0.72f));

            if (IsToggled)
            {
                batch.Draw(Style.Active, Rect, Color.White);
            }
            else if (Hover)
            {
                batch.Draw(Style.Hover, Rect, Color.White);
            }
            else
            {
                batch.Draw(Style.Inactive, Rect, Color.White);
            }
            
            if (IconTexture == null)
            {
                batch.DrawString(Fonts.Arial12Bold, IconPath, WordPos, IsToggled ? Color.White : Color.Gray);
            }
            else
            {
                Rectangle iconRect = IconActive == null ? IconRect : Rect;
                batch.Draw(IsToggled &&IconActive != null ? IconActive : IconTexture, iconRect, Color.White);            
            }
        }

        public override bool HandleInput(InputState input)
        {
            if (!Visible || !Enabled)
                return false;

            bool wasHovered = Hover;
            Hover = base.HitTest(input.CursorPosition);
            if (Hover)
            {
                if (!wasHovered)
                    GameAudio.ButtonMouseOver();

                if (Tooltip.IsValid)
                    ToolTip.CreateTooltip(Tooltip);

                OnHover?.Invoke(this);

                if (input.LeftMouseClick)
                {
                    GameAudio.AcceptClick();
                    IsToggled = !IsToggled;
                    WasClicked = true;
                    OnClick?.Invoke(this);
                    return true;
                }

                // edge case: capture mouse release events
                // NOTE: this is legacy behaviour and hard to fix
                return input.LeftMouseReleased;
            }
            return false;
        }
    }
}