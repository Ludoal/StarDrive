using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;

namespace Ship_Game
{
    // Ludoal fork: "Stats+" add-on tab in the colony facilities panel.
    // Everything about the tab lives in this file; the only hooks in existing
    // code are one AddTab() call and one dispatch line in DrawDetailInfo().
    // Skeleton only for now — content blocks ("one block = one unit") land next.
    public partial class ColonyScreen
    {
        public const string StatsPlusTabTitle = "Stats+"; // working title, trivial to rename

        bool IsStatsPlusTabSelected => PFacilities.IsTabSelected(StatsPlusTabTitle);

        void DrawStatsPlusTab(SpriteBatch batch, Vector2 bCursor)
        {
            batch.DrawString(TextFont, "Colony statistics, reimagined - coming soon.",
                             bCursor, Color.Gray);
        }
    }
}
