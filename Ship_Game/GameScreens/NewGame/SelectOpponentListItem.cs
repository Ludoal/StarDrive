using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Universe;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public class SelectOpponentListItem : ScrollListItem<SelectOpponentListItem>
    {
        // Ludoal fork: the item holds the params it reads, not the whole screen - it cannot
        // assume a SelectOpponentsScreen owner.
        public UniverseParams Params;
        public IEmpireData EmpireData;
        public SubTexture Portrait;

        public SelectOpponentListItem(UniverseParams settings, IEmpireData empireData)
        {
            Params = settings;
            EmpireData = empireData;
            Portrait = ResourceManager.Texture("Races/" + empireData.VideoPath);
        }

        // The opponent rows size exactly like the race rows - same RowHeight rule and the same
        // ExtraHeight constant, so the two lists stay identical in height.
        public override int ItemHeight => RaceArchetypeListItem.RowHeight(List, Portrait, RaceArchetypeListItem.ExtraHeight);

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);

            // centred, like the race rows - the portrait used to sit hard left and overflow
            int height = (int)Height;
            int width = (int)Portrait.GetWidthFromHeightAspect(height);
            var portrait = new Rectangle((int)CenterX - width/2, (int)Y, width, height);
            bool selected = Params.SelectedOpponents.Contains(EmpireData);
            float alpha = selected ? 1f : 0.3f;
            batch.Draw(Portrait, portrait, Color.White.Alpha(alpha));
            if (selected)
                batch.DrawRectangle(portrait, EmpireData.Traits.Color, thickness: 2);
        }
    }
}