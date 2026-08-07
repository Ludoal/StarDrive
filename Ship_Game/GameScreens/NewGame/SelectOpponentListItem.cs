using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Universe;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public class SelectOpponentListItem : ScrollListItem<SelectOpponentListItem>
    {
        // Ludoal fork (maintainer feedback, 7 Aug): the item holds the params it reads, not the
        // whole screen - the opponent list moved from its own popup into a New Game tab, so it can
        // no longer assume a SelectOpponentsScreen owner.
        public UniverseParams Params;
        public IEmpireData EmpireData;
        public SubTexture Portrait;

        public SelectOpponentListItem(UniverseParams settings, IEmpireData empireData)
        {
            Params = settings;
            EmpireData = empireData;
            Portrait = ResourceManager.Texture("Races/" + empireData.VideoPath);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);

            int height = (int)Height;
            int width = (int)Portrait.GetWidthFromHeightAspect(height);
            var portrait = new Rectangle((int)X +10, (int)Y, width, height);
            bool selected = Params.SelectedOpponents.Contains(EmpireData);
            float alpha = selected ? 1f : 0.3f;
            batch.Draw(Portrait, portrait, Color.White.Alpha(alpha));
            if (selected)
                batch.DrawRectangle(portrait, EmpireData.Traits.Color, thickness: 2);
        }
    }
}