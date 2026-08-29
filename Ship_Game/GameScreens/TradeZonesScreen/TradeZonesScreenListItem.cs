using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input; // InputState
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI; // UITable: the shared table charte

namespace Ship_Game
{
    // one trade zone = one row, on the shared table charte: its name, what it serves, what it
    // asks for, and the two action icons the build queues already speak.
    public sealed class TradeZonesScreenListItem : ScrollListItem<TradeZonesScreenListItem>
    {
        public readonly TradeZone Zone;
        UIButton EditColonies;
        UIButton DeleteZone;
        readonly TradeZonesScreen Screen;
        readonly Empire Player;

        public TradeZonesScreenListItem(TradeZonesScreen screen, TradeZone zone, Empire player)
        {
            Screen = screen;
            Zone = zone;
            Player = player;
        }

        public override void PerformLayout()
        {
            int y = (int)Y;
            int h = (int)Height;
            RemoveAll();

            UITable.Column[] cols = Screen.Table.Columns;
            Color color = Color.White;

            Cell(cols[0], Zone.Name, color);
            Cell(cols[1], Zone.NumColonies.ToString(), color);

            // foldable: cut to the column, the tooltip carries the full list
            string joined = Screen.ColonyNames(Zone);
            string shown = UITable.FitText(Fonts.Arial12Bold, joined, cols[2].Rect.Width - 2 * UITable.PadX);
            var served = Cell(cols[2], shown, color);
            if (shown != joined)
                served.Tooltip = joined;

            // a quota of nought is not a quantity: the zone's need is measured instead of ordered
            Cell(cols[3], Zone.Quota <= 0 ? Localizer.Token(GameText.PolFreighterRefitAuto)
                                          : Zone.Quota.ToString(), color);

            EditColonies ??= new UIButton(new UIButton.StyleTextures("NewUI/icon_build_edit_hover1", "NewUI/icon_build_edit_hover2", "NewUI/icon_build_edit_hover2"), Vector2.Zero, "")
            {
                Tooltip = GameText.TzEditColoniesTip,
                OnClick = _ => Screen.EditColonies(Zone),
            };
            DeleteZone ??= new UIButton(new UIButton.StyleTextures("NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2", "NewUI/icon_queue_delete_hover2"), Vector2.Zero, "")
            {
                Tooltip = GameText.TzDeleteZoneTip,
                OnClick = OnDeleteClicked,
                IconTint = Color.Red, // destruction reads red
            };

            SubTexture editTex = ResourceManager.Texture("NewUI/icon_build_edit_hover1");
            SubTexture delTex = ResourceManager.Texture("NewUI/icon_queue_delete_hover1");
            Rectangle actions = cols[cols.Length - 1].Rect;
            int pairW = editTex.Width + 8 + delTex.Width;
            int editX = actions.X + (actions.Width - pairW) / 2;
            int delX = editX + editTex.Width + 8;
            EditColonies.Rect = new Rectangle(editX, y + h / 2 - editTex.Height / 2, editTex.Width, editTex.Height);
            DeleteZone.Rect = new Rectangle(delX, y + h / 2 - delTex.Height / 2, delTex.Width, delTex.Height);

            base.PerformLayout();
        }

        UILabel Cell(UITable.Column c, string text, Color color)
        {
            return Label(UITable.CellPos(Fonts.Arial12Bold, c.Rect, Y, Height, text, c.Align),
                         text, Fonts.Arial12Bold, color);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            EditColonies.Draw(batch, elapsed);
            DeleteZone.Draw(batch, elapsed);
        }

        public override bool HandleInput(InputState input)
        {
            if (EditColonies.HandleInput(input))
                return true;
            if (DeleteZone.HandleInput(input))
                return true;
            return base.HandleInput(input);
        }

        void OnDeleteClicked(UIButton b)
        {
            Screen.ScreenManager.AddScreen(new MessageBoxScreen(Screen, Localizer.Token(GameText.TzDeleteZoneConfirm))
            {
                Accepted = () => Screen.DeleteZone(Zone)
            });
        }
    }
}
