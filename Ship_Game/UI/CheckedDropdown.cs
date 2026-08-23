using System;
using System.Linq.Expressions;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    /// Ludoal fork: a checkbox riding a design dropdown - "Autocolonize" plus which colony ship
    /// to build, "Autoexplore" plus which scout. Shared so the
    /// Automation tab of the Empire group can wear the same control; the window dies with the
    /// map overlay it belonged to.
    public class CheckedDropdown : UIElementV2
    {
        UICheckBox Check;           // null when the row leads with a fixed title instead of a toggle
        UILabel TitleOnly;          // the fixed-title lead, used in place of Check
        UICheckBox AutoPickBox;     // Ludoal fork (design Ludo): auto-pick lives ON its row
        Func<bool> IsAutoPicked;
        DropOptions<int> Options;

        public DropOptions<int> Create(Expression<Func<bool>> binding, LocalizedText title, LocalizedText tooltip)
        {
            Check = new UICheckBox(-200f, -200f, binding, Fonts.Arial12Bold, title, tooltip);
            Options = new DropOptions<int>(new Vector2(-200f, -200f), 190, 18);
            return Options;
        }

        // Fixed-title variant: no lead toggle, just a label + an Auto Pick box + the dropdown.
        // The model is a choice, not an on/off - so the row names it ("Freighter Model") and the
        // Auto Pick box switches between best-model and the manual list.
        public DropOptions<int> CreateTitled(LocalizedText title, LocalizedText tooltip, Expression<Func<bool>> autoPick)
        {
            TitleOnly = new UILabel(new Vector2(-200f, -200f), title, Fonts.Arial12Bold, Color.White) { Tooltip = tooltip };
            AutoPickBox = new UICheckBox(-200f, -200f, autoPick, Fonts.Arial12Bold, "", GameText.AutoPickTooltip);
            IsAutoPicked = autoPick.Compile();
            Options = new DropOptions<int>(new Vector2(-200f, -200f), 168, 18);
            return Options;
        }

        // overload with an Auto Pick checkbox left of the dropdown - checked, the manual
        // selection hides and an "Auto Pick" label takes its place
        public DropOptions<int> Create(Expression<Func<bool>> binding, LocalizedText title, LocalizedText tooltip,
                                       Expression<Func<bool>> autoPick)
        {
            Check = new UICheckBox(-200f, -200f, binding, Fonts.Arial12Bold, title, tooltip);
            AutoPickBox = new UICheckBox(-200f, -200f, autoPick, Fonts.Arial12Bold, "",
                                         GameText.AutoPickTooltip);
            IsAutoPicked = autoPick.Compile();
            Options = new DropOptions<int>(new Vector2(-200f, -200f), 168, 18);
            return Options;
        }

        // the checkbox label's reserved width, so every picker of a column starts on the same
        // vertical line whatever its toggle says
        const float LabelRoom = 215f; // fits "Auto Build Research Stations" clear of the Auto Pick box (bench)

        public override void PerformLayout()
        {
            // The picker rides the lead's own row, to its right - the row stays as tall as a
            // plain checkbox and the boxes widen instead. The lead is the toggle, or a fixed title.
            UIElementV2 lead = (UIElementV2)Check ?? TitleOnly;
            lead.Pos = new Vector2(Pos.X, Pos.Y + (Check == null ? 2f : 0f));
            lead.PerformLayout();
            float optionsX = Pos.X + LabelRoom;
            if (AutoPickBox != null)
            {
                AutoPickBox.Pos = new Vector2(optionsX, Pos.Y);
                AutoPickBox.PerformLayout();
                optionsX += 22f;
            }
            Options.Pos = new Vector2(optionsX, Pos.Y - 1f);
            Options.PerformLayout();
            Height = Math.Max(lead.Height, Options.Bottom - Pos.Y);
        }

        public override bool HandleInput(InputState input)
        {
            return (Check?.HandleInput(input) ?? false)
                || (AutoPickBox?.HandleInput(input) ?? false)
                || Options.HandleInput(input);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (Check != null) Check.Draw(batch, elapsed);
            else               TitleOnly.Draw(batch, elapsed);
            if (AutoPickBox != null)
            {
                AutoPickBox.Draw(batch, elapsed);
                if (IsAutoPicked())
                {
                    batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.AutoPick),
                                     new Vector2(Options.X + 4, Options.Y + 3), Color.White); // white (bench 305)
                    return;
                }
            }
            Options.Draw(batch, elapsed);
        }
    }

    /// A plain label riding a multi-option dropdown to its right, so a picker with no
    /// on/off toggle lines up the same way as the CheckedDropdown rows beside it.
    public class LabeledDropdown<T> : UIElementV2
    {
        UILabel Label;
        public DropOptions<T> Options { get; private set; }

        public DropOptions<T> Create(LocalizedText title, LocalizedText tooltip)
        {
            Label = new UILabel(new Vector2(-200f, -200f), title, Fonts.Arial12Bold, Color.White) { Tooltip = tooltip };
            Options = new DropOptions<T>(new Vector2(-200f, -200f), 190, 18);
            return Options;
        }

        const float LabelRoom = 215f; // same column start as CheckedDropdown so the pickers align

        public override void PerformLayout()
        {
            Label.Pos = new Vector2(Pos.X, Pos.Y + 2f);
            Label.PerformLayout();
            Options.Pos = new Vector2(Pos.X + LabelRoom, Pos.Y - 1f);
            Options.PerformLayout();
            Height = Math.Max(Label.Height, Options.Bottom - Pos.Y);
        }

        public override bool HandleInput(InputState input) => Options.HandleInput(input);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            Label.Draw(batch, elapsed);
            Options.Draw(batch, elapsed);
        }
    }
}
