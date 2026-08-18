using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Graphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    // One row of the COLONY tab's LIST view: a building INSTANCE standing on a tile.
    // Deletion targets the instance (its tile), never the building type.
    public class BuiltBuildingListItem : ScrollListItem<BuiltBuildingListItem>
    {
        public readonly ColonyScreen Screen;
        public readonly PlanetGridSquare Tile;

        readonly SubTexture FoodIcon = ResourceManager.Texture("NewUI/icon_food");
        readonly SubTexture ProdIcon = ResourceManager.Texture("NewUI/icon_production");
        readonly SubTexture MoneyIcon = ResourceManager.Texture("UI/icon_money_22");
        readonly SubTexture ScienceIcon = ResourceManager.Texture("NewUI/icon_science");
        readonly Font Font12 = Fonts.Arial12Bold;

        // fixed column steps from the right edge; the delete icon owns the last 20px,
        // with a 10px breath before it (bench 422); the step widened leftward
        const float ValueColumnStep = 53f;
        const float FirstValueColumnFromRight = 70f;
        const int IconSize = 24;

        public BuiltBuildingListItem(ColonyScreen screen, string headerText) : base(headerText)
        {
            Screen = screen;
        }

        public BuiltBuildingListItem(ColonyScreen screen, PlanetGridSquare tile)
        {
            Screen = screen;
            Tile = tile;
            if (tile.Building is { Scrappable: true })
                AddCancel(new Vector2(-20, 0), GameText.DoYouWishToScrap, OnDeleteClicked); // the queue's own delete icon (bench 420)
        }

        void OnDeleteClicked()
        {
            Screen.PromptScrapBuilding(Tile);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            Building b = Tile?.Building;
            if (b == null)
                return;

            // everything centres vertically on the selection rectangle (bench 422)
            Color titleColor = Hovered ? Color.White : Colors.Cream;
            batch.Draw(b.IconTex, new Vector2(X, Y + (Height - IconSize) / 2f), new Vector2(IconSize), Color.White);
            batch.DrawString(Font12, b.TranslatedName.Text, X + IconSize + 6, Y + (Height - Font12.LineSpacing) / 2f, titleColor);

            // the building's yields on THIS colony at current population - the shared
            // arithmetic, without the labor sliders' instant weighting (bench 424)
            Screen.BuildingActualYields(b, out float food, out float prod, out float research, laborShare: false);
            float credits = b.Income + b.CreditsPerColonist * Screen.P.PopulationBillion - b.Maintenance;

            DrawValueColumn(batch, FoodIcon, food, 3);
            DrawValueColumn(batch, ProdIcon, prod, 2);
            DrawValueColumn(batch, MoneyIcon, credits, 1);
            DrawValueColumn(batch, ScienceIcon, research, 0);
        }

        // columnFromRight: 0 = rightmost value column
        void DrawValueColumn(SpriteBatch batch, SubTexture icon, float value, int columnFromRight)
        {
            float x = Right - FirstValueColumnFromRight - ValueColumnStep * columnFromRight;
            var iconSize = new Vector2(Font12.LineSpacing + 2);
            batch.Draw(icon, new Vector2(x, Y + (Height - iconSize.Y) / 2f), iconSize);
            Color c = value > 0f ? Colors.Cream : value < 0f ? Color.Salmon : Color.Gray;
            batch.DrawString(Font12, value.String(1), x + iconSize.X + 2, Y + (Height - Font12.LineSpacing) / 2f + 1, c);
        }
    }
}
