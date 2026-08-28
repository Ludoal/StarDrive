using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Audio;
using Ship_Game.Graphics;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

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

        readonly Font Font12 = Fonts.Arial12Bold;
        readonly Font Font8 = Fonts.Arial8Bold;

        // the three gestures of a queue, in the order a hand reaches for them
        const float ButtonW = 22f;
        const float IconSize = 28f;

        public BlueprintsPlanListItem(BlueprintsScreen screen, BlueprintsTile tile)
        {
            Screen = screen;
            Tile = tile;
        }

        public override int ItemHeight => 32;

        // the outpost is the plan's foundation: it is neither moved nor removed
        bool Fixed => Building == null || Building.IsCapitalOrOutpost;

        Rectangle RemoveRect => new((int)(Right - ButtonW - 4), (int)Y + 6, (int)ButtonW, 20);
        Rectangle DownRect   => new((int)(Right - 2*ButtonW - 6), (int)Y + 6, (int)ButtonW, 20);
        Rectangle UpRect     => new((int)(Right - 3*ButtonW - 8), (int)Y + 6, (int)ButtonW, 20);

        public override bool HandleInput(InputState input)
        {
            if (!Fixed && Hovered)
            {
                if (UpRect.HitTest(input.CursorPosition))
                {
                    ToolTip.CreateTooltip(GameText.BpMoveUp);
                    if (input.LeftMouseClick) { Screen.MovePlanEntry(this, -1); return true; }
                }
                else if (DownRect.HitTest(input.CursorPosition))
                {
                    ToolTip.CreateTooltip(GameText.BpMoveDown);
                    if (input.LeftMouseClick) { Screen.MovePlanEntry(this, +1); return true; }
                }
                else if (RemoveRect.HitTest(input.CursorPosition))
                {
                    ToolTip.CreateTooltip(GameText.RightClickToRemove);
                    if (input.LeftMouseClick) { Screen.RemovePlanEntry(this); return true; }
                }
                else if (input.RightMouseClick && HitTest(input.CursorPosition))
                {
                    Screen.RemovePlanEntry(this); // the gesture the grid had, kept
                    return true;
                }
            }
            else if (Fixed && Hovered && input.RightMouseClick && HitTest(input.CursorPosition))
            {
                // the outpost refuses, out loud - and the click is still CONSUMED, or the popup
                // reads it as "close me" (bench 347)
                GameAudio.NegativeClick();
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

            if (Fixed)
                return;

            // the controls only appear under the hand: a queue of thirty rows wearing three
            // glyphs each reads as noise until you are actually pointing at one
            if (!Hovered)
                return;

            DrawGlyph(batch, UpRect, "^");
            DrawGlyph(batch, DownRect, "v");
            DrawGlyph(batch, RemoveRect, "x");
        }

        void DrawGlyph(SpriteBatch batch, in Rectangle r, string glyph)
        {
            bool over = r.HitTest(Screen.Input.CursorPosition);
            batch.DrawString(Font12, glyph, r.X + 8, r.Y + 3, over ? Color.Gold : Color.Gray);
        }
    }
}
