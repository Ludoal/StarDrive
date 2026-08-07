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

        // maintainer feedback (7 Aug): the race rows read 30px taller than the plain portrait
        // aspect gave - the portrait grows to fill, since Draw derives its width from Height.
        public const int ExtraHeight = 30;

        // the ONE sizing rule for both the Race and the Opponents rows: portrait at 0.8 of the
        // list width, by aspect, plus an optional extra. The Opponents item can't inherit this
        // (ScrollListItem<T> is self-referencing, so the two lists need distinct T), so they share
        // it through this static instead - one arithmetic, no second copy to drift.
        public static int RowHeight(ScrollListBase list, SubTexture portrait, int extra)
        {
            int width = (int)(list.Width * 0.8f);
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
