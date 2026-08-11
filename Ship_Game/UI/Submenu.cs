using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI;
using System.Collections.Generic;

namespace Ship_Game;

public enum SubmenuStyle
{
    Brown,
    Blue,
}

public class Submenu : UIPanel
{
    public class Tab
    {
        public int Index;
        public string Title;
        public RectF Rect;
        public bool Selected;
        public bool Hover;

        internal bool RowStart; // this tab is the first in this row
        internal bool RowEnd; // this tab is the last in this row

        public override string ToString() => $"Tab {Index} {Title} Sel={Selected} Hov={Hover}";
    }

    // Rect area for the top menu, if we have added Tabs
    RectF MenuBar;

    // Ludoal fork: how tall the tab strip actually runs - TabRows*TabHeight, so it answers for a
    // second row too. Callers that paint a background under the tabs need this and cannot guess
    // it: a hardcoded one-row height spills onto the tabs the moment a screen wraps to two.
    public float TabStripHeight => MenuBar.H;
    // Client area where child objects should be inserted
    public override RectF ClientArea { get; set; }

    // nine-slice helper for the submenu border
    NineSliceSprite N;

    readonly Graphics.Font Font = Fonts.Pirulen12;
    readonly SubmenuStyle Style;

    // If set, draws a background element before the Submenu itself is drawn
    UIElementV2 Background;
    bool AutoSizeBackground;
        
    // EVT: Triggered when a tab is changed
    public Action<int> OnTabChange;
    int CurSelectedIndex = -1;

    public Submenu(in RectF rect, LocalizedText title, SubmenuStyle style = SubmenuStyle.Brown)
        : base(rect, Color.Transparent)
    {
        Style = style;
        AddTab(title);
    }

    public Submenu(LocalPos pos, Vector2 size, LocalizedText title, SubmenuStyle style = SubmenuStyle.Brown)
        : base(pos, size, Color.Transparent)
    {
        Style = style;
        AddTab(title);
    }

    public Submenu(in RectF rect, IEnumerable<LocalizedText> tabs, SubmenuStyle style = SubmenuStyle.Brown)
        : base(rect, Color.Transparent)
    {
        Style = style;
        foreach (LocalizedText tab in tabs)
            AddTab(tab);
    }

    public Submenu(LocalPos pos, Vector2 size, IEnumerable<LocalizedText> tabs, SubmenuStyle style = SubmenuStyle.Brown)
        : base(pos, size, Color.Transparent)
    {
        Style = style;
        foreach (LocalizedText tab in tabs)
            AddTab(tab);
    }

    public Submenu(in RectF theMenu, SubmenuStyle style = SubmenuStyle.Brown)
        : base(theMenu, Color.Transparent)
    {
        Style = style;
        this.PerformLayout();
    }

    public Submenu(in Rectangle theMenu, SubmenuStyle style = SubmenuStyle.Brown)
        : base(theMenu, Color.Transparent)
    {
        Style = style;
        this.PerformLayout();
    }

    public Submenu(int x, int y, int width, int height, SubmenuStyle style = SubmenuStyle.Brown)
        : this(new(x, y, width, height), style)
    {
    }

    public Submenu(float x, float y, float width, float height, SubmenuStyle style = SubmenuStyle.Brown)
        : this(new((int)x, (int)y, (int)width, (int)height), style)
    {
    }

    public Submenu(LocalPos pos, Vector2 size, SubmenuStyle style = SubmenuStyle.Brown)
        : base(pos, size, Color.Transparent)
    {
        Style = style;
    }

    public override void PerformLayout()
    {
        base.PerformLayout();

        if (Background != null)
        {
            if (AutoSizeBackground)
            {
                var (localPos, size) = GetBackgroundRect();
                Background.SetLocalPos(localPos);
                Background.SetAbsSize(size);
            }
            Background.PerformLayout();
        }

        InitializeRects();
        // this will update MenuBar and ClientArea
        RecalculateTabRects();
    }

    void InitializeRects()
    {
        StyleTextures s = GetStyle();
        N ??= new();
        N.Update(new(Rect), s.CornerTL, s.CornerTR, s.CornerBL, s.CornerBR, 
                            s.HorizVert, s.HorizVert, borderWidth:2);

        // set the defaults for MenuBar position and width
        MenuBar = new(N.Top.X, N.Top.Y, N.Top.W, 0);
        ClientArea = N.ClientArea; // use the default ClientArea
    }

    (LocalPos, Vector2) GetBackgroundRect()
    {
        if (Tabs.IsEmpty) return (LocalPos.Zero, Size);
        return (new LocalPos(0, TabHeight - 2), Rect.CutTop(TabHeight - 2).Size());
    }

    // Adds a colored background Selector to this Submenu
    public void SetBackground(Color color)
    {
        Background?.RemoveFromParent();
        AutoSizeBackground = true;
        var (localPos, size) = GetBackgroundRect();
        Background = new Selector(this, localPos, size, color);
        SendToBackZOrder(Background);
    }

    // Adds a background to this submenu
    public void SetBackground(UIElementV2 background)
    {
        Background?.RemoveFromParent();
        AutoSizeBackground = false;
        Background = Add(background);
        SendToBackZOrder(Background);
    }

    public Array<Tab> Tabs = new();
    public int NumTabs => Tabs.Count;
    int TabRows;
    Vector2 NextTabPos; // relative position for next tab
    public const int TabHeight = 25; // tabs advance by TabHeight-2 - the strip's true bottom
        
    public void AddTab(LocalizedText title)
    {
        if (N == null)
            InitializeRects();

        Tab tab = new() { Index = Tabs.Count, Title = title.Text };
        UpdateTabRect(tab);
        Tabs.Add(tab);
    }

    // Remove all Tabs
    public void ClearTabs()
    {
        Tabs.Clear();
        CurSelectedIndex = -1; // don't trigger event during Clear()
        RecalculateTabRects();
    }

    // Ludoal fork: the two primitives a DYNAMIC tab needs - a hosted panel's tab (the colony)
    // is added, renamed and removed at runtime, which the append-only tab set never allowed.
    // Removing the selected tab routes its collapse through SelectedIndex, so OnTabChange
    // fires exactly once: closing a hosted tab IS a tab change, and the host's handler is
    // what restores the panel the tab was opened from.
    public void RemoveTab(int index)
    {
        if ((uint)index >= (uint)Tabs.Count)
            return;
        bool wasSelected = CurSelectedIndex == index;
        Tabs.RemoveAt(index);
        for (int i = 0; i < Tabs.Count; ++i)
            Tabs[i].Index = i;
        RecalculateTabRects();
        if (wasSelected)
        {
            CurSelectedIndex = -1; // points at a gone tab; the setter below fires the change
            SelectedIndex = Math.Min(index, Tabs.Count - 1);
        }
        else if (CurSelectedIndex > index)
        {
            --CurSelectedIndex; // same tab, new seat - no event
        }
    }

    /// the "another colony replaces the tab" path: rename + panel swap, no create/destroy
    public void RenameTab(int index, LocalizedText title)
    {
        if ((uint)index >= (uint)Tabs.Count)
            return;
        Tabs[index].Title = title.Text;
        RecalculateTabRects(); // the tab's width follows its text
    }

    int IndexOf(string title) => Tabs.FirstIndexOf(tab => tab.Title == title);
    public bool IsSelected(string title) => SelectedIndex != -1 && IndexOf(title) == SelectedIndex;
    public bool ContainsTab(string title) => IndexOf(title) != -1;

    void RecalculateTabRects()
    {
        NextTabPos = Vector2.Zero;
        TabRows = 0;
        foreach (Tab tab in Tabs)
            UpdateTabRect(tab);
    }
        
    void UpdateTabRect(Tab tab)
    {
        float w = Font.TextWidth(tab.Title) + 2 + GetStyle().HeaderRight.Width;

        // new rect will go over upper right edge? line change
        if (MenuBar.Right < (MenuBar.X + NextTabPos.X + w))
        {
            NextTabPos.X = 0f;
            NextTabPos.Y += TabHeight - 2;
            ++TabRows;
        }
        else
        {
            // if line didn't change, always clear the last tabs end of row flag
            if (tab.Index > 0) Tabs[tab.Index - 1].RowEnd = false;
            if (TabRows == 0) ++TabRows;
        }

        tab.RowEnd = true; // always mark the last element as end of row
        tab.RowStart = NextTabPos.X == 0f;
        Vector2 newPos = new(MenuBar.X + NextTabPos.X, MenuBar.Y + NextTabPos.Y);
        
        NextTabPos.X += w;
        MenuBar = new(MenuBar.X, MenuBar.Y, MenuBar.W, TabRows*TabHeight);
        // same terms as before, through the owner formula (MenuBar.X/W derive from N.Top,
        // which derives from the same corner textures CalcGroupClientArea reads).
        // ⚠ new RectF(Rect), not RectF: N was laid out on the INTEGER rect, and the formula
        // must read the same one to stay bit-identical.
        ClientArea = CalcGroupClientArea(new RectF(Rect), Style, TabRows);

        tab.Rect = new(newPos, new(w, TabHeight));
    }

    protected virtual void OnTabChangedEvt(int newIndex)
    {
        if (CurSelectedIndex != newIndex)
        {
            CurSelectedIndex = newIndex;
            OnTabChange?.Invoke(newIndex);
        }
    }

    public int SelectedIndex
    {
        get => CurSelectedIndex;
        set
        {
            int newIndex = -1;
            for (int i = 0; i < Tabs.Count; ++i)
            {
                Tab tab = Tabs[i];
                tab.Selected = (i == value);
                if (tab.Selected) newIndex = i;
            }
            OnTabChangedEvt(newIndex);
        }
    }

    public bool IsTabSelected(string tabName)
    {
        return CurSelectedIndex >= 0 ? Tabs[CurSelectedIndex].Title == tabName : false;
    }

    public override bool HandleInput(InputState input)
    {
        if (!Visible || !Enabled)
            return false;

        Vector2 mousePos = input.CursorPosition;
        for (int i = 0; i < Tabs.Count; i++)
        {
            Tab tab = Tabs[i];
            tab.Hover = tab.Rect.HitTest(mousePos);
            if (tab.Hover && input.LeftMouseClick)
            {
                GameAudio.AcceptClick();
                SelectedIndex = i;
                return true;
            }
        }

        return base.HandleInput(input);
    }

    public override void Update(float fixedDeltaTime)
    {
        if (SelectedIndex == -1 && Tabs.NotEmpty)
            SelectedIndex = 0;

        Background?.Update(fixedDeltaTime);
        base.Update(fixedDeltaTime);
    }

    public override void Draw(SpriteBatch batch, DrawTimes elapsed)
    {
        if (!Visible)
            return;
            
        Background?.Draw(batch, elapsed);
        base.Draw(batch, elapsed);

        StyleTextures s = GetStyle();
            
        if (Tabs.NotEmpty)
        {
            for (int i = 0; i < Tabs.Count; i++)
            {
                Tab t = Tabs[i];
                bool selected = Tabs.Count == 1 || t.Selected;
                if (t.RowStart) DrawMenuBarTopLeft(t, selected);

                RectF r = t.Rect;
                // middle part of a tab
                var middle = new RectF(r.X, r.Y, r.W - s.HeaderRight.Width, TabHeight);
                // right side of a tab
                var right = new RectF(r.X + r.W - s.HeaderRight.Width, r.Y, s.HeaderRight.Width, TabHeight);

                if     (selected) DrawSelectedTab(t, middle, right);
                else if (t.Hover) DrawHoveredTab(t, middle, right);
                else              DrawUnselectedTab(t, middle, right);

                DrawTabText(t, r);
                //if (DebugDraw) // debugging a Tab:
                //{
                //    batch.DrawRectangle(right, t.RowEnd ? Color.Yellow : Color.LightBlue);
                //}
            }

            // if we have tabs, draw horizontal bars for every row
            for (int i = 1; i < (TabRows+1); ++i)
            {
                N.DrawTopBar(batch, MenuBar.Y + i*(TabHeight-2));
            }
            N.DrawVerticalBars(batch, MenuBar.Y + (TabHeight-2));
            N.DrawBottomBar(batch);
        }
        else // only draw the border if there are no tabs
        {
            N.DrawBorders(batch);
        }

        if (DebugDraw) // debugging the submenu
        {
            N.DrawDebug(batch);
            batch.DrawRectangle(ClientArea, Color.Green);
        }

        
        void DrawMenuBarTopLeft(Tab t, bool selected)
        {
            SubTexture header = s.HeaderLeftUnsel;
            if     (selected) header = s.HeaderLeft;
            else if (t.Hover) header = s.HoverLeftEdge;

            batch.Draw(header, new RectF(N.TL.X, t.Rect.Y, header.SizeF), Color.White);
        }

        void DrawSelectedTab(Tab t, in RectF middle, in RectF right)
        {
            batch.Draw(s.HeaderMiddle, middle, Color.White);
            batch.Draw(s.HeaderRight, right, Color.White);

            if (NotRowEnd(t, out Tab nextTab))
            {
                SubTexture tab = nextTab.Hover ? s.HoverLeft : s.HeaderRightExtUnsel;
                batch.Draw(tab, right, Color.White);
            }
        }

        void DrawHoveredTab(Tab t, in RectF middle, in RectF right)
        {
            batch.Draw(s.HoverMid, middle, Color.White);
            batch.Draw(s.HoverRight, right, Color.White);
            if (NotRowEnd(t, out Tab nextTab)) // we have nextTab, draw |_\
            {
                SubTexture tex = nextTab.Selected ? s.HeaderRightExt : s.HeaderRightExtUnsel;
                batch.Draw(tex, right, Color.White);
            }
        }

        void DrawUnselectedTab(Tab t, in RectF middle, in RectF right)
        {
            batch.Draw(s.HeaderMiddleUnsel, middle, Color.White);
            batch.Draw(s.HeaderRightUnsel, right, Color.White);
            if (NotRowEnd(t, out Tab nextTab))
            {
                SubTexture tex = nextTab.Selected ? s.HeaderRightExt :
                    nextTab.Hover    ? s.HoverLeft :
                    s.HeaderRightExtUnsel;
                batch.Draw(tex, right, Color.White);
            }
        }

        void DrawTabText(Tab t, in RectF r)
        {
            var textPos = new Vector2(r.X, (r.Y + r.H / 2 - Font.LineSpacing / 2));
            batch.DrawString(Font, t.Title, textPos, Colors.Cream);
        }
    }

    bool NotRowEnd(Tab t, out Tab nextTab)
    {
        if (!t.RowEnd && t.Index < (Tabs.Count - 1))
        {
            nextTab = Tabs[t.Index + 1];
            return true;
        }
        nextTab = null;
        return false;
    }

    public class StyleTextures
    {
        public SubTexture HorizVert;
        public SubTexture CornerTL, CornerTR, CornerBR, CornerBL;
        public SubTexture HoverLeftEdge, HoverLeft, HoverMid, HoverRight;

        // tab menu header design elements
        public SubTexture HeaderLeft,     HeaderLeftUnsel;
        public SubTexture HeaderMiddle,   HeaderMiddleUnsel;
        public SubTexture HeaderRight,    HeaderRightUnsel;
        public SubTexture HeaderRightExt, HeaderRightExtUnsel;

        static SubTexture Tex(string tex) => ResourceManager.Texture(tex);

        public StyleTextures(SubmenuStyle style)
        {
            switch (style)
            {
                case SubmenuStyle.Brown:
                    // Ludoal fork: the Brown set follows UI/Theme.yaml's SubmenuSkin folder,
                    // falling back piece by piece to the classic NewUI set - same lever as
                    // PopupFrame's PopupSkin
                    string skin = UITheme.SubmenuSkin;
                    SubTexture Skinned(string piece)
                        => ResourceManager.TextureOrDefault($"{skin}/{piece}", $"NewUI/{piece}");

                    HorizVert       = Skinned("submenu_horiz_vert");
                    CornerTL        = Skinned("submenu_corner_TL");
                    CornerTR        = Skinned("submenu_corner_TR");
                    CornerBR        = Skinned("submenu_corner_BR");
                    CornerBL        = Skinned("submenu_corner_BL");

                    HoverLeftEdge = Skinned("submenu_header_hover_leftedge");
                    HoverLeft     = Skinned("submenu_header_hover_left");
                    HoverMid      = Skinned("submenu_header_hover_mid");
                    HoverRight    = Skinned("submenu_header_hover_right");

                    HeaderLeft          = Skinned("submenu_header_left");
                    HeaderLeftUnsel     = Skinned("submenu_header_left_unsel");
                    HeaderMiddle        = Skinned("submenu_header_middle");
                    HeaderMiddleUnsel   = Skinned("submenu_header_middle_unsel");
                    HeaderRight         = Skinned("submenu_header_right");
                    HeaderRightUnsel    = Skinned("submenu_header_right_unsel");
                    HeaderRightExt      = Skinned("submenu_header_rightextend");
                    HeaderRightExtUnsel = Skinned("submenu_header_rightextend_unsel");
                    break;
                    
                case SubmenuStyle.Blue:
                    HorizVert     = Tex("ResearchMenu/submenu_horiz_vert");
                    CornerTL      = Tex("ResearchMenu/submenu_corner_TL");
                    CornerTR      = Tex("ResearchMenu/submenu_corner_TR");
                    CornerBR      = Tex("ResearchMenu/submenu_corner_BR");
                    CornerBL      = Tex("ResearchMenu/submenu_corner_BL");

                    // research menu doesn't have any hovers, so just reuse left/middle/right
                    HoverLeftEdge = Tex("ResearchMenu/submenu_header_left");
                    HoverLeft     = Tex("ResearchMenu/submenu_header_left");
                    HoverMid      = Tex("ResearchMenu/submenu_header_middle");
                    HoverRight    = Tex("ResearchMenu/submenu_header_right");

                    HeaderLeft          = Tex("ResearchMenu/submenu_header_left");
                    HeaderLeftUnsel     = Tex("ResearchMenu/submenu_header_left");
                    HeaderMiddle        = Tex("ResearchMenu/submenu_header_middle");
                    HeaderMiddleUnsel   = Tex("ResearchMenu/submenu_header_middle");
                    HeaderRight         = Tex("ResearchMenu/submenu_header_right");
                    HeaderRightUnsel    = Tex("ResearchMenu/submenu_header_right");
                    HeaderRightExt      = Tex("ResearchMenu/submenu_transition_right");
                    HeaderRightExtUnsel = Tex("ResearchMenu/submenu_transition_right");
                    break;
            }
        }
    }

    static int ContentId;
    static StyleTextures[] Styling;
    static string StylingSkin;

    StyleTextures GetStyle() => GetStyle(Style);

    static StyleTextures GetStyle(SubmenuStyle style)
    {
        if (Styling == null || ContentId != ResourceManager.ContentId || StylingSkin != UITheme.SubmenuSkin)
        {
            ContentId = ResourceManager.ContentId;
            StylingSkin = UITheme.SubmenuSkin;
            Styling = new[]
            {
                new StyleTextures(SubmenuStyle.Brown),
                new StyleTextures(SubmenuStyle.Blue),
            };
        }
        return Styling[(int)style];
    }

    // Ludoal fork: the tab group's chrome WITHOUT the tabs, for elements that wear the same
    // furniture without being Submenus - the cartouches, the minimap. One scratch sprite,
    // refreshed on every call: Draw runs on the UI thread only, and the alternative is an
    // allocation per frame.
    static readonly NineSliceSprite FrameSprite = new();

    public static void DrawFrameOnly(SpriteBatch batch, in RectF r, SubmenuStyle style = SubmenuStyle.Brown)
    {
        StyleTextures s = GetStyle(style);
        FrameSprite.Update(r, s.CornerTL, s.CornerTR, s.CornerBL, s.CornerBR,
                              s.HorizVert, s.HorizVert, borderWidth: 2);
        FrameSprite.DrawBorders(batch);
    }

    // Ludoal fork: the cartouche recipe, written once - the near-opaque ground 2px inside
    // the chrome, then the frame on top. The star/planet/ship/fleet cartouches and the
    // minimap each carried this pair as their own copy; one arithmetic, several consumers.
    public static void DrawFrameWithGround(SpriteBatch batch, in RectF r, SubmenuStyle style = SubmenuStyle.Brown)
    {
        RectF ground = new(r.X + 2, r.Y + 2, r.W - 4, r.H - 4);
        batch.FillRectangle(ground, new Color(8, 10, 14).Alpha(0.94f));
        DrawFrameOnly(batch, r, style);
    }

    // Ludoal fork: the group client-area formula, extracted to its owner. The tab-layout
    // path below calls this (the formula exists ONCE), and a screen hosted in a group's
    // frame without being a Submenu reads the same rect - pixel-identical to a real tab's
    // content by construction, not by calibration. Terms: the menu bar starts at the TL
    // corner texture's width and is amputated of both corners; the client area starts under
    // tabRows of tabs and stops above the BL corner's height.
    public static RectF CalcGroupClientArea(in RectF rect, SubmenuStyle style, int tabRows = 1)
    {
        StyleTextures s = GetStyle(style);
        float menuW = rect.W - (s.CornerTL.Width + s.CornerTR.Width);
        float menuH = tabRows * TabHeight;
        return new(rect.X + s.CornerTL.Width, rect.Y + menuH, menuW, rect.H - (menuH + s.CornerBL.Height));
    }

}