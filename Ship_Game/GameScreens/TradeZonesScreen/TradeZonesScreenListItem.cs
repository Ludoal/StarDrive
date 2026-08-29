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

            // one label PER colony rather than one joined string: each name is a target the
            // player can click to centre the map on it, the way every other name in the game
            // behaves. What does not fit is elided, and the ellipsis carries the whole list.
            LayOutColonyNames(cols[2].Rect, color);

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

        // the clickable name lanes, rebuilt with the row
        readonly Array<(Rectangle Rect, Planet Colony)> NameHits = new();

        void LayOutColonyNames(Rectangle cell, Color color)
        {
            NameHits.Clear();
            Graphics.Font font = Fonts.Arial12Bold;
            int x = cell.X + UITable.PadX;
            int right = cell.X + cell.Width - UITable.PadX;
            int y = (int)(Y + Height / 2 - font.LineSpacing / 2f);
            string all = Screen.ColonyNames(Zone);

            for (int i = 0; i < Zone.Colonies.Count; ++i)
            {
                Planet colony = Screen.UState.GetPlanet(Zone.Colonies[i]);
                if (colony == null)
                    continue;

                string text = i == 0 ? colony.Name : ", " + colony.Name;
                int w = (int)font.TextWidth(text);
                if (x + w > right) // no room left: say so once and stop
                {
                    Label(new Vector2(x, y), "...", font, color).Tooltip = all;
                    return;
                }

                UILabel label = Label(new Vector2(x, y), text, font, color);
                label.Tooltip = GameText.TzPanToColonyTip;
                NameHits.Add((new Rectangle(x, y, w, font.LineSpacing), colony));
                x += w;
            }
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
            if (input.LeftMouseClick)
            {
                foreach ((Rectangle rect, Planet colony) in NameHits)
                {
                    if (rect.HitTest(input.CursorPosition))
                    {
                        Screen.PanTo(colony);
                        return true;
                    }
                }
            }
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
