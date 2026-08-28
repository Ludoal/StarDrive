using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Audio;
using Ship_Game.Graphics;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    /// Ludoal fork (Blueprints chantier): one entry of the plan, as a QUEUE row rather than a
    /// tile on a grid. The plan is read as a chronology - built and replaced from the top,
    /// rebuilt from the bottom - so its order has to be visible and movable, which a grid of
    /// tiles could never show.
    ///
    /// ⚠ The row is a VIEW over the tile that holds the building: moving an entry swaps what two
    /// tiles carry, it does not move any data of its own. The tiles remain the plan's storage,
    /// so saving, loading and the colony simulation are untouched by this screen change.
    public class BlueprintsPlanListItem : ScrollListItem<BlueprintsPlanListItem>
    {
        public readonly BlueprintsScreen Screen;
        public readonly BlueprintsTile Tile;
        public Building Building => Tile.Building;
        public bool DescriptionPinned; // bench 535: set by the screen, worn as a gold liseré

        readonly Font Font12 = Fonts.Arial12Bold;
        readonly Font Font8 = Fonts.Arial8Bold;
        const float IconSize = 28f;

        // the outpost is the plan's foundation: it is neither moved nor removed
        bool Fixed => Building == null || Building.IsCapitalOrOutpost;

        public BlueprintsPlanListItem(BlueprintsScreen screen, BlueprintsTile tile)
        {
            Screen = screen;
            Tile = tile;
            if (Building != null && !Building.IsCapitalOrOutpost)
            {
                // ⚠ the Colony construction queue's OWN controls, not glyphs of my own
                // (maintainer, bench 535): the plan is a queue, so it wears the queue's icons -
                // a player who has moved a build order already knows this row by heart.
                AddUp(new Vector2(-90, 6), GameText.BpMoveUp, OnUpClicked);
                AddDown(new Vector2(-60, 6), GameText.BpMoveDown, OnDownClicked);
                AddCancel(new Vector2(-30, 6), GameText.RightClickToRemove, OnRemoveClicked);
            }
        }

        void OnUpClicked() => Screen.MovePlanEntry(this, -1);
        void OnDownClicked() => Screen.MovePlanEntry(this, +1);
        void OnRemoveClicked() => Screen.RemovePlanEntry(this);

        public override int ItemHeight => 32;

        public override bool HandleInput(InputState input)
        {
            // the gesture the grid had, kept - and CONSUMED either way, because an unconsumed
            // right-click falls through to the popup's generic close (bench 347)
            if (Hovered && input.RightMouseClick && HitTest(input.CursorPosition))
            {
                if (Fixed) GameAudio.NegativeClick();
                else       Screen.RemovePlanEntry(this);
                return true;
            }
            return base.HandleInput(input);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            Building b = Building;
            if (b == null)
                return;

            // rank first: the number IS the meaning of this screen now
            batch.DrawString(Font8, (ItemIndex + 1).ToString(), X + 2, Y + 10, Color.Gray);

            Color tint = Tile.Unlocked ? Color.White : Color.Gray;
            batch.Draw(b.IconTex, new Vector2(X + 18, Y + 2), new Vector2(IconSize), tint);
            batch.DrawString(Font12, b.TranslatedName.Text, X + 18 + IconSize + 6, Y + 8,
                             Tile.Unlocked ? Color.White : Color.DarkGray);

            if (DescriptionPinned)
                batch.DrawRectangle(Rect, Color.Gold);
        }
    }
}
