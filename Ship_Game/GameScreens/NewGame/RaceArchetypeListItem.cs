using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public class RaceArchetypeListItem : ScrollListItem<RaceArchetypeListItem>
    {
        public RaceDesignScreen Screen;
        public IEmpireData EmpireData;
        public SubTexture Portrait;

        public RaceArchetypeListItem(RaceDesignScreen screen, IEmpireData empireData)
        {
            Screen = screen;
            EmpireData = empireData;
            Portrait = ResourceManager.Texture("Races/" + empireData.VideoPath);
        }

        // maintainer feedback (7 Aug, bench 343): the rows read 20px taller than the plain portrait
        // aspect gave (30 no longer fit the frame). BOTH the race and the opponent lists use this one
        // constant, so they stay identical in height and can't drift apart.
        public const int ExtraHeight = 20;

        // the ONE sizing rule for both the Race and the Opponents rows: portrait at 0.8 of the
        // list width, by aspect, plus an optional extra. The Opponents item can't inherit this
        // (ScrollListItem<T> is self-referencing, so the two lists need distinct T), so they share
        // it through this static instead - one arithmetic, no second copy to drift.
        public static int RowHeight(ScrollListBase list, SubTexture portrait, int extra)
        {
            // bench 345: size off the ITEMS area (ItemsHousing), which already excludes the scrollbar
            // lane and side padding - list.Width is the whole list, so 0.8 of it ran the portrait
            // under the slider. 0.9 of the (narrower) items area keeps the same visual size.
            int width = (int)(list.ItemsHousing.W * 0.9f); // RectF uses .W, not .Width (Rect/RectF law)
            return (int)portrait.GetHeightFromWidthAspect(width) + extra;
        }

        public override int ItemHeight => RowHeight(List, Portrait, ExtraHeight);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);

            int height = (int)Height;
            int width = (int)Portrait.GetWidthFromHeightAspect(height);
            var portrait = new Rectangle((int)CenterX - width/2, (int)Y, width, height);
            batch.Draw(Portrait, portrait, Color.White);

            if (Screen.SelectedData == EmpireData)
            {
                batch.DrawRectangle(portrait, Color.BurlyWood);
            }
        }
    }
}
