using System;
using System.Linq.Expressions;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    /// Ludoal fork: a checkbox riding a design dropdown - "Autocolonize" plus which colony ship
    /// to build, "Autoexplore" plus which scout. Lifted out of AutomationWindow so the
    /// Automation tab of the Empire group can wear the same control; the window dies with the
    /// map overlay it belonged to.
    public class CheckedDropdown : UIElementV2
    {
        UICheckBox Check;
        UICheckBox AutoPickBox;     // Ludoal fork (design Ludo): auto-pick lives ON its row
        Func<bool> IsAutoPicked;
        DropOptions<int> Options;

        public DropOptions<int> Create(Expression<Func<bool>> binding, LocalizedText title, LocalizedText tooltip)
        {
            Check = new UICheckBox(-200f, -200f, binding, Fonts.Arial12Bold, title, tooltip);
            Options = new DropOptions<int>(new Vector2(-200f, -200f), 190, 18);
            return Options;
        }

        // overload with an Auto Pick checkbox left of the dropdown - checked, the manual
        // selection hides and an "Auto Pick" label takes its place
        public DropOptions<int> Create(Expression<Func<bool>> binding, LocalizedText title, LocalizedText tooltip,
                                       Expression<Func<bool>> autoPick)
        {
            Check = new UICheckBox(-200f, -200f, binding, Fonts.Arial12Bold, title, tooltip);
            AutoPickBox = new UICheckBox(-200f, -200f, autoPick, Fonts.Arial12Bold, "",
                                         "Auto Pick: always use the best design available");
            IsAutoPicked = autoPick.Compile();
            Options = new DropOptions<int>(new Vector2(-200f, -200f), 168, 18);
            return Options;
        }

        public override void PerformLayout()
        {
            Check.Pos = Pos;
            Check.PerformLayout();
            float optionsX = Pos.X;
            if (AutoPickBox != null)
            {
                AutoPickBox.Pos = new Vector2(Pos.X, Pos.Y + 17f);
                AutoPickBox.PerformLayout();
                optionsX += 22f;
            }
            Options.Pos = new Vector2(optionsX, Pos.Y + 16f);
            Options.PerformLayout();
            Height = Options.Bottom - Pos.Y;
        }

        public override bool HandleInput(InputState input)
        {
            return Check.HandleInput(input)
                || (AutoPickBox?.HandleInput(input) ?? false)
                || Options.HandleInput(input);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            Check.Draw(batch, elapsed);
            if (AutoPickBox != null)
            {
                AutoPickBox.Draw(batch, elapsed);
                if (IsAutoPicked())
                {
                    batch.DrawString(Fonts.Arial12Bold, "Auto Pick",
                                     new Vector2(Options.X + 4, Options.Y + 3), Color.LightGreen);
                    return;
                }
            }
            Options.Draw(batch, elapsed);
        }
    }
}
