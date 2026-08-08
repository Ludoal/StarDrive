using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDGraphics;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public class AssignLaborComponent : UIElementContainer
    {
        Planet Planet;
        ColonySliderGroup Sliders;
        Submenu Title;
        bool UseTitle;

        // Ludoal fork (maintainer bench 299): the title row can carry EXTRA TABS - the
        // Terraforming tab migrated here from the facilities block, whose row was folding
        // to a second line. The host wires OnTabChange and hides the sliders itself.
        public Submenu TitleMenu => Title;
        public bool SlidersVisible { get => Sliders.Visible; set => Sliders.Visible = value; }

        public AssignLaborComponent(Planet p, RectF rect, bool useTitleFrame,
                                    LocalizedText[] titleTabs = null) : base(rect)
        {
            Planet = p;
            UseTitle = useTitleFrame;

            Sliders = Add(new ColonySliderGroup(p, SlidersHousing, drawIcons: useTitleFrame)
            {
                OnSlidersChanged = OnSlidersChanged
            });

            if (useTitleFrame)
            {
                Title = Add(new Submenu(rect, titleTabs ?? new LocalizedText[] { GameText.AssignLabor }));
            }

            RequiresLayout = true;
        }

        void OnSlidersChanged()
        {
            Planet.UpdateIncomes();
        }

        Rectangle SlidersHousing
        {
            get
            {
                int sliderX = (int)X + (UseTitle ? 60 : 10);
                int sliderY = (int)Y + 25;
                int sliderW = (Width * 0.55f).RoundTo10(); // bench 345: -5% off the slider so a 3-digit value (100.2) fits to its right
                int sliderH = (int)Height - 25;
                return new Rectangle(sliderX, sliderY, sliderW, sliderH);
            }
        }

        public override void PerformLayout()
        {
            if (Title != null) Title.Rect = Rect;
            Sliders.Rect = SlidersHousing;
            base.PerformLayout();
        }
    }
}
