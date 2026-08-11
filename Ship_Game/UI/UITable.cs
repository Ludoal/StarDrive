using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDUtils;
using Ship_Game.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Rectangle = SDGraphics.Rectangle;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game.UI
{
    public enum TableAlign
    {
        Left,    // unique text
        Center,  // a category / tag
        Number,  // right-aligned on the decimal (callers format with fixed decimals)
    }

    // the colour charte for value columns: colour carries the RESULT, never the nature
    public enum TableColor
    {
        Plain,   // white - a bare figure (population, speed)
        Neutral, // vanilla, gray at zero - a value whose nature never changes (a cost is a cost)
        Signed,  // green/red/gray - a RESULT (net, remainder)
    }

    // The table spec in ONE place so every table screen shares a single geometry instead of
    // divergent copies:
    //   20px side margins off the frame BORDER; headers centred and bold, vanilla -
    //   orange on the sorted column; one horizontal rule under the headers; verticals
    //   BETWEEN columns only, never at the extremities; numbers right on the decimal,
    //   unique text left, categories centred, one character (8px) of padding per side;
    //   the scrollbar's lane is RESERVED after the last column with room for the row
    //   selector, which spans the phantom extremity lines (4px outside the edge columns).
    // The table owns geometry and chrome; the screen owns its data, rows and sorting.
    public sealed class UITable
    {
        public sealed class Column
        {
            public string Title = "";
            public SubTexture Icon;   // header icon instead of text (money, troops, strength)
            public Color? Badge;      // small corner dot: two columns may share an icon and
                                      // still tell apart
            public int Width;         // fixed px - the column doctrine
            public int MinWidth;      // AutoSize floor: zero means the data alone decides
            public TableAlign Align = TableAlign.Left;
            public LocalizedText Tip;
            public bool Sortable;
            public bool Sorted;       // wears the orange
            public bool Ascending;    // the sort direction this column remembers
            public bool Hover;
            public TableColor Coloring = TableColor.Plain; // how this column's values wear colour
            public bool Bold;         // the column's cells draw in the bold body font
            // When the table's natural width exceeds what the resolution allows, foldable
            // columns give the difference back - their text is cut with an ellipsis and the
            // full value moves to a tooltip
            public bool Foldable;
            public bool Folded;       // set by FitToWidth when this column actually gave width
            // the separator drawn at this column's LEFT edge, when it should differ from
            // the charte's warm line (e.g. a muted gray to sub-group columns)
            public Color? SepColor;
            public Rectangle Rect;    // absolute header band, set by Layout

            public Font CellFont => Bold ? Fonts.Arial12Bold : Fonts.Arial12;
        }

        // the colour a value wears in a column of the given charte; the near-zero snap
        // kills the "-0.00" class of display. Every numeric zero reads gray: what produces
        // or consumes nothing recedes, whatever the column.
        public static Color ValueColor(TableColor kind, float v)
        {
            if (v > -0.005f && v < 0.005f) v = 0f;
            if (v == 0f)
                return Color.Gray;
            switch (kind)
            {
                case TableColor.Signed:  return v > 0f ? Color.ForestGreen : Color.Red;
                case TableColor.Neutral: return Vanilla;
                default:                 return Color.White;
            }
        }

        public const int SideMargin = 15; // off the frame border; the selection box needs the room on the left
        public const int PadX = 8;        // one character of cell padding
        public const int SliderLane = 26; // reserved after the last column
        public const int HeaderH = 16;
        public static readonly Color Vanilla = Colors.Cream;
        public static readonly Color RuleColor = new Color(118, 102, 67, 255);

        public readonly Column[] Columns;
        public Rectangle TableRect; // first column X .. last column Right, headers .. bottom
        public RectF ListRect;      // hand this to the ScrollList - it carries the slider lane
        public int HeaderY;
        public int RuleY;

        public UITable(Column[] columns) { Columns = columns; }

        // Column width from the data: the widest value the column will actually show, plus
        // the padding pair - floored by the title's own width. Call before Layout, with the
        // values the screen is about to display.
        public static void AutoSize(Column c, Font font, IEnumerable<string> values)
        {
            float w = font.TextWidth(c.Title);
            if (c.Icon != null)
                w = Math.Max(w, c.Icon.Width);
            foreach (string v in values)
                if (v.NotEmpty())
                    w = Math.Max(w, font.TextWidth(v));
            // MinWidth floors the result when set - the data alone decides otherwise
            c.Width = Math.Max((int)w + 2 * PadX, c.MinWidth);
        }

        // A capped table never cuts a row in half: the height a screen asks for snaps down
        // to whole rows when the resolution cannot hold them all. `overhead` is everything
        // that is not rows - header band, margins, any reserved footer - and `pitch` the row
        // height plus the list's item padding.
        public static float ContentHeightFor(float overhead, int rows, int pitch, float available)
        {
            float wanted = overhead + rows * pitch;
            if (wanted <= available)
                return wanted;
            return overhead + (int)((available - overhead) / pitch) * pitch;
        }

        // if the natural widths exceed what the resolution allows, the FOLDABLE columns
        // give the difference back, split between them - call after the AutoSize passes
        public void FitToWidth(int maxTableWidth)
        {
            int over = TableWidth - maxTableWidth;
            if (over <= 0)
                return;
            var folds = new Array<Column>();
            foreach (Column c in Columns)
                if (c.Foldable)
                    folds.Add(c);
            if (folds.IsEmpty)
                return;
            int share = over / folds.Count + 1;
            foreach (Column f in folds)
            {
                int cut = Math.Min(share, over);
                f.Width = Math.Max(80, f.Width - cut);
                f.Folded = true;
                over -= cut;
            }
        }

        // the fold's visible half: cut text to the room with an ellipsis; the caller
        // shows the full value in a tooltip when the returned string differs
        public static string FitText(Font font, string text, int room)
        {
            if (text.IsEmpty() || font.TextWidth(text) <= room)
                return text;
            int len = Math.Max(1, (int)(text.Length * (room / font.TextWidth(text))));
            string t = text.Substring(0, len).TrimEnd() + "...";
            while (len > 1 && font.TextWidth(t) > room)
                t = text.Substring(0, --len).TrimEnd() + "...";
            // never slice a word (maintainer bench 305): back off to the last whole one -
            // unless the FIRST word alone overflows, where a char cut beats an empty cell
            int space = len <= text.Length ? text.LastIndexOf(' ', Math.Min(len, text.Length) - 1) : -1;
            if (space > 0)
                t = text.Substring(0, space).TrimEnd() + "...";
            return t;
        }

        public int TableWidth
        {
            get { int w = 0; foreach (Column c in Columns) w += c.Width; return w; }
        }

        // content width for a content-sized group frame that hugs this table:
        // margins + columns + slider lane, mapped back through the frame corners
        public float ContentWidth => TableWidth + 2 * (SideMargin - 9) + SliderLane + 18;

        // client: the group frame's ClientArea. headerTop/bottom: the vertical span the
        // table may use, absolute.
        // when set, Layout snaps the list's foot so only WHOLE rows show - the value is
        // the row height PLUS the list's 4px item padding (maintainer bench 307: the
        // overhead constants the screens fed ContentHeightFor were near-misses, and a
        // few px of the next row still peeked)
        public int RowPitch;

        public void Layout(in RectF client, float headerTop, float bottom)
        {
            int x0 = (int)client.X + (SideMargin - 9);
            int x = x0;
            HeaderY = (int)headerTop;
            foreach (Column c in Columns)
            {
                c.Rect = new Rectangle(x, HeaderY, c.Width, HeaderH);
                x += c.Width;
            }
            // a breath above and below the rule (maintainer bench 288): the headers - tall
            // header icons included - don't sit on the line, and the first row keeps its
            // distance too
            RuleY = HeaderY + HeaderH + 6;
            TableRect = new Rectangle(x0, HeaderY, x - x0, (int)bottom - HeaderY);
            // ScrollList insets its ItemsHousing by PaddingLeft 8 / PaddingTop 15 /
            // PaddingRight 24: this rect makes the item lane start at the first column,
            // pulls the TOP padding back so the first row sits 6px under the rule (a
            // padding, not an empty line - maintainer bench 289), and leaves the slider
            // its reserved lane right of the last column
            ListRect = new RectF(x0 - 8, RuleY - 9, (x - x0) + 8 + SliderLane, bottom - (RuleY - 9));
            if (RowPitch > 0)
            {
                // ScrollList pads 15 top and bottom; what remains is the row lane
                float usable = ListRect.H - 30;
                float snapped = (int)(usable / RowPitch) * RowPitch;
                ListRect.H = snapped + 30;
                TableRect.Height = (int)(ListRect.Bottom - TableRect.Y); // the chrome follows
            }
        }

        // the hover selector spans the PHANTOM extremity lines: the item lane starts at
        // the first column (the selector's own 4px bevel gives the left line) and the
        // right side trims back from the slider lane to 4px past the last column
        public void ApplyHighlightTo(ScrollListBase list)
        {
            list.HighlightLeftExtend = 0;
            list.HighlightRightExtend = (TableRect.Right + 4) - ((int)ListRect.Right - 24 + 4);
        }

        public void DrawChrome(SpriteBatch batch)
        {
            Font font = Fonts.Arial12Bold;
            foreach (Column c in Columns)
            {
                if (c.Icon != null)
                {
                    var iconR = new Rectangle(c.Rect.X + c.Rect.Width / 2 - c.Icon.Width / 2,
                                              HeaderY - 2, c.Icon.Width, c.Icon.Height);
                    batch.Draw(c.Icon, iconR, Color.White);
                    if (c.Badge != null)
                        batch.FillRectangle(new RectF(iconR.Right - 3, iconR.Bottom - 3, 5, 5), c.Badge.Value);
                }
                else if (c.Title.NotEmpty())
                {
                    Color hc = c.Sorted ? Color.Orange
                             : c.Hover && c.Sortable ? Color.White : Vanilla;
                    var pos = new Vector2(c.Rect.X + c.Rect.Width / 2 - font.TextWidth(c.Title) / 2f, HeaderY);
                    batch.DrawString(font, c.Title, pos.Rounded(), hc);
                }
            }

            batch.FillRectangle(new Rectangle(TableRect.X, RuleY, TableRect.Width, 1), RuleColor);
            for (int i = 1; i < Columns.Length; ++i)
                batch.FillRectangle(new Rectangle(Columns[i].Rect.X, RuleY, 1, TableRect.Bottom - RuleY),
                                    Columns[i].SepColor ?? RuleColor);
        }

        // header hover (tooltips, white sortables) and clicks. Returns the clicked
        // sortable column's index, or -1 - the screen owns what sorting means.
        public int HandleInput(InputState input)
        {
            for (int i = 0; i < Columns.Length; ++i)
            {
                Column c = Columns[i];
                var band = new Rectangle(c.Rect.X, c.Rect.Y - 2, c.Rect.Width, RuleY - c.Rect.Y + 4);
                c.Hover = band.HitTest(input.CursorPosition);
                if (!c.Hover)
                    continue;
                if (c.Tip.IsValid)
                    ToolTip.CreateTooltip(c.Tip);
                if (c.Sortable && input.LeftMouseClick)
                    return i;
            }
            return -1;
        }

        // marks `col` as the sorted column (the orange one) and flips its direction;
        // returns true if the new direction is ascending
        public bool SetSorted(int col)
        {
            foreach (Column c in Columns)
                c.Sorted = false;
            Column sc = Columns[col];
            sc.Sorted = true;
            sc.Ascending = !sc.Ascending;
            return sc.Ascending;
        }

        // one cell, aligned per the charte. rowY/rowH: the row band the text centres in.
        public static void DrawCell(SpriteBatch batch, Font font, in Rectangle col,
                                    float rowY, float rowH, string text, Color color, TableAlign align)
        {
            float y = rowY + rowH / 2f - font.LineSpacing / 2f;
            float x = align == TableAlign.Left ? col.X + PadX
                    : align == TableAlign.Center ? col.X + col.Width / 2f - font.TextWidth(text) / 2f
                    : col.Right - PadX - font.TextWidth(text);
            batch.DrawString(font, text, new Vector2(x, y).Rounded(), color);
        }

        // the cell position for label-based rows (ScrollListItem children)
        public static Vector2 CellPos(Font font, in Rectangle col, float rowY, float rowH, string text, TableAlign align)
        {
            float y = rowY + rowH / 2f - font.LineSpacing / 2f;
            float x = align == TableAlign.Left ? col.X + PadX
                    : align == TableAlign.Center ? col.X + col.Width / 2f - font.TextWidth(text) / 2f
                    : col.Right - PadX - font.TextWidth(text);
            return new Vector2(x, y).Rounded();
        }
    }
}
