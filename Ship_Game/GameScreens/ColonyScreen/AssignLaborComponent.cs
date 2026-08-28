using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDGraphics;
using Rectangle = SDGraphics.Rectangle;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    public class AssignLaborComponent : UIElementContainer
    {
        Planet Planet;
        ColonySliderGroup Sliders;
        Submenu Title;
        bool UseTitle;
        bool ShowMaxValue; // show the 100%-labor potential beside each current value
        float MaxSliderRatio; // slider width when ShowMaxValue is on (Colony and Colonies differ)

        // Ludoal fork (maintainer bench 299): the title row can carry EXTRA TABS - the
        // Terraforming tab migrated here from the facilities block, whose row was folding
        // to a second line. The host wires OnTabChange and hides the sliders itself.
        public Submenu TitleMenu => Title;
        public bool SlidersVisible { get => Sliders.Visible; set => Sliders.Visible = value; }

        // Ludoal fork (maintainer bench 524): Labor's own lever, above the three padlocks.
        // ⚠ bench 526: it needs no reserve carved off the block. The first slider already sits a
        // quarter of the way down, and the toggle fits in that gap - reserving a row on top of it
        // took 22px from the sliders twice over, which showed up as three rows squeezed into the
        // bottom of the frame here and, worse, on the Colonies list where the block is shorter.
        const int AutoRowH = 22; // the toggle's own height, used to seat it - NOT taken off the block
        UICheckBox AutoToggle;

        public AssignLaborComponent(Planet p, RectF rect, bool useTitleFrame,
                                    LocalizedText[] titleTabs = null, bool showMaxValue = false,
                                    float maxSliderRatio = 0.50f) : base(rect)
        {
            Planet = p;
            UseTitle = useTitleFrame;
            ShowMaxValue = showMaxValue;
            MaxSliderRatio = maxSliderRatio;

            Sliders = Add(new ColonySliderGroup(p, SlidersHousing, drawIcons: useTitleFrame, showMaxValue: showMaxValue)
            {
                OnSlidersChanged = OnSlidersChanged
            });

            // ⚠ the getter/setter form, NOT the expression-tree one: AutoLabor resolves a
            // three-state field, and Ref<T>(Expression<Func<T>>) only ever accepts a direct
            // member access - a computed property there throws when the screen opens.
            AutoToggle = Add(new UICheckBox(0f, 0f, () => Planet.AutoLabor, v => Planet.AutoLabor = v,
                                           Fonts.Arial12Bold, GameText.LaborAuto, GameText.LaborAutoTip));

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
                // one value column needs ~45% width; showing the max adds a second decimal
                // column, so the slider yields more room when ShowMaxValue is on. Colony and
                // Colonies pass different ratios - Colonies is tighter so its Supply column fits.
                int sliderW = (Width * (ShowMaxValue ? MaxSliderRatio : 0.55f)).RoundTo10();
                int sliderH = (int)Height - 25;
                return new Rectangle(sliderX, sliderY, sliderW, sliderH);
            }
        }

        public override void PerformLayout()
        {
            if (Title != null) Title.Rect = Rect;
            Sliders.Rect = SlidersHousing;
            base.PerformLayout();
            // ⚠ seated AFTER the sliders have laid themselves out: the toggle sits on the PADLOCK
            // column, centred on it, one row above the first slider - and both of those numbers
            // belong to the group, which is why they are asked of it rather than rebuilt here
            // (bench 525: built from the block's own corner it landed high and far to the left).
            AutoToggle.PerformLayout();
            // clamped: on the Colonies list the block is short enough that a quarter of it can be
            // less than the toggle's own height, and the toggle would climb out of the top
            AutoToggle.Pos = new Vector2(Sliders.LockColumnCenterX - AutoToggle.Width / 2,
                                         Math.Max(Sliders.FirstRowTop - AutoRowH, Y + 2));
            AutoToggle.PerformLayout();
        }
    }
}
