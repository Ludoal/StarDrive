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

        // maintainer feedback (7 Aug): the opponent rows size the SAME way as the race rows - the
        // shared RaceArchetypeListItem.RowHeight rule - just a touch smaller (no ExtraHeight). One
        // arithmetic for both lists, so their sizing can never drift.
        public override int ItemHeight => RaceArchetypeListItem.RowHeight(List, Portrait, 0);

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