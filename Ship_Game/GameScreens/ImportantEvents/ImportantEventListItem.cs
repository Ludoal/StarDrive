using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDUtils;
using Ship_Game.UI; // UITable: the shared table charte
using Vector2 = SDGraphics.Vector2;

// ReSharper disable once CheckNamespace
namespace Ship_Game
{
    public sealed class ImportantEventListItem : ScrollListItem<ImportantEventListItem>
    {
        public readonly ImportantNotification Event;
        readonly Graphics.Font NormalFont = Fonts.Arial12Bold;
        readonly UIPanel EventIcon;
        readonly Color RowColor;
        readonly UITable Table;

        public ImportantEventListItem(UITable table, ImportantNotification importantEvent)
        {
            Table    = table;
            Event    = importantEvent;
            RowColor = Event.RelevantEmpire?.EmpireColor ?? Color.LightGray;

            // the faction flag rides LEFT of the title (maintainer, 4 Aug)
            if (Event.RelevantEmpire != null)
            {
                EventIcon = Add(new UIPanel(Pos, ResourceManager.Flag(Event.RelevantEmpire.data.Traits.FlagIndex),
                                            Event.RelevantEmpire.EmpireColor));
            }
            else if (Event.IconPath.NotEmpty() && ResourceManager.TextureLoaded(Event.IconPath))
            {
                EventIcon = Add(new UIPanel(Pos, ResourceManager.Texture(Event.IconPath)));
            }

            if (EventIcon != null)
                EventIcon.Size = new Vector2(40, 40);

            UITable.Column[] cols = Table.Columns;
            AddEventLabel(Event.StarDate.StarDateString(), cols[0], 0, Colors.Cream);
            AddEventLabel(Event.Title, cols[1], 48, RowColor);
            AddEventLabel(Event.Message.Replace('\n', ' '), cols[2], 0, Color.LightGray);
        }

        // one cell: positioned relative to the row from the shared column geometry
        // (the row's own X sits at the table's first column)
        void AddEventLabel(string text, UITable.Column c, int leftInset, Color color)
        {
            float room = c.Rect.Width - 2 * UITable.PadX - leftInset;
            string parsedText = NormalFont.ParseText(text, room);
            UILabel label     = Add(new UILabel(parsedText, NormalFont, color));
            label.Size        = new Vector2(room, 80);
            label.TextAlign   = TextAlign.VerticalCenter;
            label.SetLocalPos(c.Rect.X - Table.TableRect.X + UITable.PadX + leftInset, 0);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            // the row keeps its empire tint - content, not chrome; the column separators
            // belong to the shared charte now
            Color borderColor = DimColor(RowColor, 3);
            batch.FillRectangle(Rect, DimColor(RowColor, 10));
            batch.DrawRectangle(Rect, borderColor);

            if (EventIcon != null)
                EventIcon.Pos = new Vector2(Table.Columns[1].Rect.X + UITable.PadX, Pos.Y + 20);

            base.Draw(batch, elapsed);
        }

        static Color DimColor(Color color, int divider)
        {
            return new Color((byte)(color.R / divider),
                             (byte)(color.G / divider),
                             (byte)(color.B / divider));
        }
    }
}
