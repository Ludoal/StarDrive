using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    [DebuggerTypeProxy(typeof(DropOptionsDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
    public class DropOptions<T> : UIElementV2
    {
        readonly RecTexPair[] Border = new RecTexPair[16];
        int BorderCount;

        Rectangle OpenRect;
        Rectangle ClickAbleOpenRect;
        readonly Array<Entry> Options = new Array<Entry>();
        public bool Open;
        // Ludoal fork (dropdown unification): a READ-ONLY dropdown shows its value greyed
        // and answers nothing - the instrument-panel convention (a command in read-only
        // mode shows its state, it never hides it)
        public bool ReadOnly;

        public int ActiveIndex;
        public int Count         => Options.Count;
        public bool NotEmpty     => Options.NotEmpty;
        public Entry Active      => Options[ActiveIndex];
        public string ActiveName => Options[ActiveIndex].Name.Text;
        
        public T ActiveValue
        {
            get => Options[ActiveIndex].Value;
            set
            {
                int index = IndexOfValue(value);
                if (index != -1)
                    ActiveIndex = index;
                else 
                    Log.Error($"{GetType().GetTypeName()}.set_ActiveValue failed! No value {value} in Options list");
            }
        }

        public Action<T> OnValueChange;

        Ref<T> PropertyRef;
        public Expression<Func<T>> PropertyBinding
        {
            set => PropertyRef = new Ref<T>(value);
        }

        public class Entry
        {
            public LocalizedText Name;
            public bool Hover;
            public Rectangle Rect;
            public T Value;

            // maintainer feedback: an entry may carry a glyph to its left - the lock the game
            // already draws elsewhere for an exclusive blueprint. Optional: an entry without
            // one lays out exactly as before, so every other caller is untouched.
            public SubTexture Icon;

            public Entry(in LocalizedText name, T value)
            {
                Name  = name;
                Value = value;
            }
            public void UpdateRect(UIElementV2 parent, int index)
            {
                Rect = new Rectangle((int)parent.X, (int)parent.Y + (int)parent.Height * index + 3, (int)parent.Width, 18);
            }
            public override string ToString() => $"{Name}: {Value}";
        }


        public DropOptions(in Rectangle rect) : base(rect)
        {
            Reset();
        }
        public DropOptions(Vector2 pos, int width, int height)
            : base(pos, new Vector2(width, height))
        {
            Reset();
        }
        public DropOptions(float x, float y, float width, float height)
            : base(new Vector2(x, y), new Vector2(width, height))
        {
            Reset();
        }
        public DropOptions(int width, int height)
        {
            Size = new Vector2(width, height);
            Reset();
        }

        public void Clear()
        {
            ActiveIndex = 0;
            Options.Clear();
        }

        public void CopyTo(Entry[] items) => Options.CopyTo(items);

        public int IndexOfEntry(string name)
        {
            for (int i = 0; i < Options.Count; ++i)
                if (Options[i].Name.Text == name)
                    return i;
            return -1;
        }

        public bool SetActiveEntry(string name)
        {
            int i = IndexOfEntry(name);
            if (i == -1)
                return false;
            ActiveIndex = i;
            return true;
        }

        int IndexOfValue(T value)
        {
            for (int i = 0; i < Options.Count; ++i)
                if (Options[i].Value.Equals(value))
                    return i;
            return -1;
        }

        public bool SetActiveValue(T value)
        {
            int i = IndexOfValue(value);
            if (i == -1)
                return false;
            ActiveIndex = i;
            return true;
        }

        public void AddOption(in LocalizedText option, T value)
        {
            var e = new Entry(option, value);
            e.UpdateRect(this, Options.Count);
            Options.Add(e);
        }

        // same, with a glyph drawn ahead of the text - both on the closed title and in the
        // open list, so the mark does not vanish once the choice is made.
        public void AddOption(in LocalizedText option, T value, SubTexture icon)
        {
            var e = new Entry(option, value) { Icon = icon };
            e.UpdateRect(this, Options.Count);
            Options.Add(e);
        }

        public bool Contains(Func<T, bool> selector)
        {
            for (int i = 0; i < Options.Count; ++i)
                if (selector(Options[i].Value))
                    return true;
            return false;
        }

        static bool IsMouseHoveringOver(in Rectangle rect)
        {
            return rect.HitTest(GameBase.ScreenManager.input.CursorPosition);
        }

        string WrappedCacheKey; string WrappedCacheValue; float WrappedCacheWidth;
        string WrappedString(string text, int iconRoom = 0)
        {
            float maxWidth = Width - 22 - iconRoom;
            // bench 455: this ran the MeasureString truncation loop EVERY FRAME for every
            // truncated cell - the Colonies page slowdown. Cache per (text, width), and
            // guard the loop: an over-narrow box must never Remove() past empty.
            if (text == WrappedCacheKey && maxWidth == WrappedCacheWidth)
                return WrappedCacheValue;
            string result = text;
            if (Fonts.Arial12Bold.MeasureString(text).X > maxWidth)
            {
                var sb = new StringBuilder(text, text.Length + 2);
                while (sb.Length > 1 && Fonts.Arial12Bold.MeasureString(sb).X > maxWidth)
                    sb.Remove(sb.Length-1, 1);
                sb.Append("...");
                result = sb.ToString();
            }
            WrappedCacheKey = text; WrappedCacheWidth = maxWidth; WrappedCacheValue = result;
            return result;
        }

        static Vector2 TextPosition(Rectangle rect)
        {
            return new Vector2(rect.X + 10, rect.Y + rect.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2);
        }

        // the room an entry's glyph takes ahead of its text, gap included - 0 without one.
        // Read by the DRAWING and by the TRUNCATION, which is the whole point: bench 570 had
        // labels cut too late because the width the lock ate was known to one and not the other.
        static int IconRoom(Entry e)
            => e?.Icon == null ? 0
             : e.Icon.Width * (Fonts.Arial12Bold.LineSpacing - 3) / e.Icon.Height + 4;

        // draws an entry's glyph when it has one, and answers where its text starts. Sized to
        // the line so it rides the text rather than the row, and squared off its own ratio.
        static Vector2 DrawIcon(SpriteBatch batch, Entry e, Rectangle rect, Color color)
        {
            Vector2 pos = TextPosition(rect);
            if (e?.Icon == null)
                return pos;
            int h = Fonts.Arial12Bold.LineSpacing - 3;
            var r = new Rectangle((int)pos.X, rect.Y + rect.Height / 2 - h / 2,
                                  e.Icon.Width * h / e.Icon.Height, h);
            batch.Draw(e.Icon, r, color);
            return new Vector2(pos.X + IconRoom(e), pos.Y);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Visible)
                return;

            bool hover = !ReadOnly && IsMouseHoveringOver(Rect);
            if (hover) // draw border if mouse is hovering
                UITheme.DrawControlHoverFill(batch, Rect);

            for (int i = 0; i < BorderCount; ++i) // draw borders
                Border[i].Draw(batch, ReadOnly ? Color.DarkGray : Color.White);

            if (Count > 0) // draw active item
            {
                Color color = ReadOnly ? Color.Gray : hover ? Color.White : Colors.Cream;
                batch.DrawString(Fonts.Arial12Bold, WrappedString(ActiveName, IconRoom(Options[ActiveIndex])),
                                 DrawIcon(batch, Options[ActiveIndex], Rect, color), color);
            }

            if (Open) // draw drop options
            {
                DrawOpenOptions(batch);
            }
        }

        void DrawOpenOptions(SpriteBatch batch)
        {
            UITheme.DrawControlFill(batch, OpenRect);

            int drawOffset = 1;
            for (int i = 0; i < Options.Count; ++i)
            {
                if (i == ActiveIndex)
                    continue;

                Entry e = Options[i];
                e.UpdateRect(this, drawOffset);
                if (IsMouseHoveringOver(e.Rect))
                {
                    var hoverLeft   = new Rectangle(e.Rect.X + 5,  e.Rect.Y + 1, 6, 15);
                    var hoverMiddle = new Rectangle(e.Rect.X + 11, e.Rect.Y + 1, e.Rect.Width - 22, 15);
                    var hoverRight  = new Rectangle(hoverMiddle.X + hoverMiddle.Width, hoverMiddle.Y, 6, 15);
                    batch.Draw(ResourceManager.Texture("NewUI/dropdown_menuitem_hover_left"), hoverLeft, Color.White);
                    batch.Draw(ResourceManager.Texture("NewUI/dropdown_menuitem_hover_middle"), hoverMiddle, Color.White);
                    batch.Draw(ResourceManager.Texture("NewUI/dropdown_menuitem_hover_right"), hoverRight, Color.White);
                }
                batch.DrawString(Fonts.Arial12Bold, WrappedString(e.Name.Text, IconRoom(e)),
                                 DrawIcon(batch, e, e.Rect, Color.White), Color.White);
                ++drawOffset;
            }
        }

        public override void PerformLayout()
        {
            if (!Visible)
                return;

            base.PerformLayout();
            if (PropertyRef != null) // ensure our drop-down list is in sync with the property binding!
            {
                T bindingValue = PropertyRef.Value;
                if (!bindingValue.Equals(ActiveValue))
                    SetActiveValue(bindingValue);
            }

            Reset();
        }

        // an open list covers whatever is under it: drawn last, asked first
        public override bool DrawsAboveSiblings => Open;

        public override bool AboveHitTest(Vector2 pos) => Open && ClickAbleOpenRect.HitTest(pos);

        public override bool HandleInput(InputState input)
        {
            // An empty dropdown is inert — don't capture input or toggle Open. Reading
            // ActiveName / Active later would index Options[0] and throw IOOB. The
            // existing Count==1 auto-close at the title-toggle below doesn't catch
            // Count==0 because we never reach it.
            if (Options.Count == 0 || ReadOnly)
                return false;

            bool overTitle = HitTest(input.CursorPosition);
            bool overExpanded = Open && ClickAbleOpenRect.HitTest(input.CursorPosition);

            // maintainer: a click anywhere else closes the list without changing the
            // selection, and is CONSUMED - closing is the whole gesture. Letting it through
            // meant a list dismissed by accident also fired whatever sat behind it.
            if (Open && input.LeftMouseClick && !overTitle && !overExpanded)
            {
                Open = false;
                Reset();
                return true;
            }

            if (overTitle && input.InGameSelect)
            {
                Open = !Open;
                if (Open && Options.Count == 1)
                    Open = false;

                if (Open) GameAudio.AcceptClick();
                Reset();
                return true; // click: input was definitely captured
            }
            if (overExpanded && input.InGameSelect)
            {
                for (int i = 0; i < Options.Count; ++i)
                {
                    Entry e = Options[i];
                    if (!e.Rect.HitTest(input.CursorPosition))
                        continue;

                    Active.Rect = e.Rect;
                    e.Rect = new Rectangle();
                    ActiveIndex = i;
                    OnValueChange?.Invoke(ActiveValue);

                    if (PropertyRef != null)
                        PropertyRef.Value = ActiveValue;

                    GameAudio.AcceptClick();
                    Open = false;
                    Reset();
                    return true; // click: input was definitely captured
                }
                Open = false;
                Reset();
            }
            return overTitle || overExpanded; // input was captured?
        }

        // Policies phase 0: the hovered entry (open list) or the active value under the
        // cursor (closed title) - lets a host surface per-entry documentation elsewhere
        public bool TryGetHoveredEntry(out T value)
        {
            Vector2 cursor = GameBase.ScreenManager.input.CursorPosition;
            if (Open)
            {
                for (int i = 0; i < Options.Count; ++i)
                {
                    if (i == ActiveIndex)
                        continue;
                    Entry e = Options[i];
                    if (e.Rect.HitTest(cursor))
                    {
                        value = e.Value;
                        return true;
                    }
                }
            }
            if (NotEmpty && !ReadOnly && HitTest(cursor))
            {
                value = ActiveValue;
                return true;
            }
            value = default;
            return false;
        }

        public void Reset()
        {
            Array.Clear(Border, 0, Border.Length);

            var ttl = ResourceManager.Texture("NewUI/dropdown_menu_corner_TL");
            var ttr = ResourceManager.Texture("NewUI/dropdown_menu_corner_TR");
            var tbl = ResourceManager.Texture("NewUI/dropdown_menu_corner_BL");
            var tbr = ResourceManager.Texture("NewUI/dropdown_menu_corner_BR");
            var left  = ResourceManager.Texture("NewUI/dropdown_menu_sides_left");
            var right = ResourceManager.Texture("NewUI/dropdown_menu_sides_right");
            var top = ResourceManager.Texture("NewUI/dropdown_menu_sides_top");
            var bot = ResourceManager.Texture("NewUI/dropdown_menu_sides_bottom");

            int x = Rect.X, y = Rect.Y, w = Rect.Width, h = Rect.Height;
            var tl = Border[0] = new RecTexPair(x, y, ttl);
            var tr = Border[1] = new RecTexPair(x+w-ttr.Width, y, ttr);
            var bl = Border[2] = new RecTexPair(x, y+h-tbl.Height, tbl);
            var br = Border[3] = new RecTexPair(x+w-tbl.Width, y+h-tbr.Height, tbr);
            Border[4] = new RecTexPair(x, y+6, h-12, left);
            Border[5] = new RecTexPair(x+w-6, y+6, h-12, right);
            Border[6] = new RecTexPair(x+tl.W, y, top, w-tl.W-tr.W);
            Border[7] = new RecTexPair(x+tl.W, y+h-6, bot, w-bl.W-br.W);
            BorderCount = 8;
            if (Open)
            {
                int height = (Options.Count - 1) * 18;
                OpenRect = new Rectangle(x + 6, y + h + 3 + 6, w - 12, height - 12);
                ClickAbleOpenRect = new Rectangle(x + 6, y + h + 3, w - 12, height - 6);

                tl = Border[8]  = new RecTexPair(x, y+h+3, ttl);
                tr = Border[9]  = new RecTexPair(x+w-ttr.Width, tl.Y, ttr);
                bl = Border[10] = new RecTexPair(x, tl.Y+height-tbl.Height, tbl);
                br = Border[11] = new RecTexPair(x+w-tbl.Width, tl.Y+height-tbr.Height, tbr);
                Border[12] = new RecTexPair(x, tl.Y+6, height-12, left);
                Border[13] = new RecTexPair(x+w-6, tl.Y+6, height-12, right);
                Border[14] = new RecTexPair(x+tl.W, tl.Y, top, w-tl.W-tr.W);
                Border[15] = new RecTexPair(x+tl.W, tl.Y+height-6, bot, w-bl.W-br.W);
                BorderCount = 16;
            }
        }

        struct RecTexPair
        {
            readonly Rectangle Rect;
            readonly SubTexture Tex;
            public int Y => Rect.Y;
            public int W => Rect.Width;

            public RecTexPair(int x, int y, SubTexture t)
            {
                Rect = new Rectangle(x, y, t.Width, t.Height);
                Tex = t;
            }
            public RecTexPair(int x, int y, int h, SubTexture t)
            {
                Rect = new Rectangle(x, y, t.Width, h);
                Tex = t;
            }
            public RecTexPair(int x, int y, SubTexture t, int w)
            {
                Rect = new Rectangle(x, y, w, t.Height);
                Tex = t;
            }
            public void Draw(SpriteBatch spriteBatch, Color color)
            {
                spriteBatch.Draw(Tex, Rect, color);
            }
        }
    }

    internal sealed class DropOptionsDebugView<T>
    {
        readonly DropOptions<T> Collection;

        public DropOptionsDebugView(DropOptions<T> collection)
        {
            Collection = collection;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public DropOptions<T>.Entry[] Items
        {
            get
            {
                var items = new DropOptions<T>.Entry[Collection.Count];
                Collection.CopyTo(items);
                return items;
            }
        }
    }
}