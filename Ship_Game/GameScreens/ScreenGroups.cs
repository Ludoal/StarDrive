using System;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDGraphics.Input;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens
{
    /// <summary>
    /// Ludoal fork: the screen groups of the unified top bar - the one door to every group
    /// screen, and the geometry they all lay themselves out on.
    ///
    /// Why a factory rather than a check at each call site: these screens are opened from fifteen
    /// places between the top bar, the notifications, the colony screen's Edit button and each
    /// other. A test spread over fifteen sites is a test that will be forgotten at the sixteenth.
    /// One place decides; every caller goes through here.
    ///
    /// This branch is its own game: the stock screens it replaced are gone from the tree.
    /// Upstream updates are small and are studied change by change, carried over by hand
    /// where they earn it - there is no merge target to keep byte-identical any more.
    ///
    /// The Shipyard's floating hover cartouche (ShipInfoOverlayComponent) was never doubled: the
    /// colony and fleet screens use it directly.
    /// </summary>
    public static class ScreenGroups
    {
        // Ludoal fork: the group's shared geometry, in one place - three screens build the same
        // frame and tab row, and a value copied three times is a value that will drift.
        //
        // TabRowY is the top of the Submenu RECT, whose first row is the tab strip - the frame
        // itself opens one tab row lower. Derived from the top bar rather than graven: the frame
        // is wanted 10px under the bar, and the rect starts one tab row above that. When the bar
        // changes height, every group's frame follows it.
        // ⚠ TabRowY IS the top of the tab strip, so the 10px clearance is added and nothing is
        // taken off for the strip's own height - subtracting it lifted the tabs a full row into
        // the bar, over the treasury and research readouts.
        const int TabStripH = 25; // Submenu.TabHeight, which is private
        public const int TabRowY = EmpireUIOverlay.BarTop + EmpireUIOverlay.BarH + 10;
        // the top of a group's visible FRAME - one tab strip below the tab row. Bar overlays that
        // want to line up with the group frames (not the tab strip) anchor here.
        // bench 353 (Lek's diagnosis): the strip's USEFUL height is TabHeight-2, not TabHeight - tabs
        // overlap by 2px (Submenu.cs:181/155, Rect.CutTop(TabHeight-2)). The real visible frame top an
        // etalon group screen (Research) opens at is TabRowY + TabHeight - 2 = 77, so GroupFrameTop was
        // 2px too low. Fixed at the source so every client (Colony) inherits the truth, not the slip.
        public const int GroupFrameTop = TabRowY + TabStripH - 2;
        // the same margin the top bar keeps: the frame's sides line up with the bar above it,
        // and one of the two moving is a thing you would only notice once it looked wrong
        public const int FrameMargin = EmpireUIOverlay.BarTop;
        public const int ColumnGutter = 5;   // inside the frame, left and right of the columns
        public const int ColumnGap = 5;      // between two columns
        public const int ColumnPadV = 5;     // above and below the columns, inside the frame
        public const int ClosePadding = 5;   // the close cross, off the client area's top-right
        public const int GroupColumns = 8;   // the row is always eight wide, known or not

        public static readonly LocalizedText[] GroupTabTitles =
        {
            "Intelligence", "Bonuses", "Trends", "Relationships", "Espionage"
        };

        // Ludoal fork: what each tab holds, for the hover tip. Submenu.Tab has no tooltip field and
        // Submenu is shared, so the screens raise these themselves against the tab rects it does
        // expose. A key is shown only where the tab HAS one of its own: Bonuses and Relationships
        // are reached through the group, so advertising the group's key on them read as a promise
        // the tab does not keep.
        public static readonly string[] GroupTabTips =
        {
            Localizer.Token(GameText.DvGroupTabTipDiplomacy),
            Localizer.Token(GameText.DvGroupTabTipTraits),
            Localizer.Token(GameText.DvGroupTabTipTrends),
            Localizer.Token(GameText.DvGroupTabTipTreaties),
            Localizer.Token(GameText.DvGroupTabTipInfiltration),
        };

        public static readonly string[] GroupTabKeys = { "I", "", "", "", "E" };

        // ── Galaxy group ──────────────────────────────────────────────────────────────────────
        // Ludoal fork: the second group of the unified top bar. Same frame, same tab row as the
        // Diplomacy group - one geometry for the whole bar.
        public static readonly LocalizedText[] GalaxyTabTitles =
        {
            "Planets", "Exotic Systems", "Patrols", "Events"
        };

        public static readonly string[] GalaxyTabTips =
        {
            Localizer.Token(GameText.DvGalaxyTabTipPlanets),
            Localizer.Token(GameText.DvGalaxyTabTipExoticSystems),
            Localizer.Token(GameText.DvGalaxyTabTipPatrols),
            Localizer.Token(GameText.DvGalaxyTabTipEvents),
        };

        // the keys those screens already close on, in tab order
        public static readonly string[] GalaxyTabKeys = { "L", "G", "P", "F7" };

        // Ludoal fork: ONE place that knows which screen a Galaxy tab opens. Each screen used to
        // carry its own switch over the other three, so a fourth tab meant editing all of them -
        // and the copy that got missed would simply open nothing.
        public static GameScreen GalaxyTab(int index, UniverseScreen u) => index switch
        {
            0 => new PlanetListScreen(u, u.EmpireUI),
            1 => new ExoticSystemsListScreen(u, u.EmpireUI),
            2 => new EmpirePatrolsScreen(u, u.Player),
            _ => new ImportantEventsScreen(u),
        };

        // Ludoal fork: the switch every Galaxy screen runs when another of its tabs is clicked.
        // `self` is the tab the caller sits on, so it can leave itself alone.
        public static void SwitchGalaxyTab(int index, int self, UniverseScreen u, GameScreen caller)
        {
            if (index == self)
                return;
            caller.ExitScreen();
            Audio.GameAudio.AcceptClick();
            if (IsHostedTab(Group.Galaxy, index, u))
                u.OpenHostedTabPanel?.Invoke();
            else
                u.ScreenManager.AddScreen(GalaxyTab(index, u));
        }

        // ── the hosted tab (spec: colony-as-tab; universal by maintainer decision) ──────────
        // Ludoal fork: when a subject rides a group's row (u.HostedTab* armed for that group),
        // the row shows one extra tab at the end wearing the subject's name. The subject's
        // panel is not a stacked screen (the colony is the universe's workersPanel), so the
        // switches route its index to the armed opener instead of a factory.
        public static bool IsHostedTab(Group g, int index, UniverseScreen u)
            => u.HostedTabTitle != null && u.HostedTabGroup == g
            && index == StockTitles(g).Length;

        static LocalizedText[] StockTitles(Group g)
            => g == Group.Galaxy    ? GalaxyTabTitles
             : g == Group.Diplomacy ? GroupTabTitles
             : EmpireTabTitles;

        /// the live tab row of a group: the stock titles, plus the hosted tab when armed.
        /// The EMPIRE group's colony tab is PERMANENT (maintainer): with no seat armed it
        /// wears the remembered colony (the capital by default).
        public static LocalizedText[] LiveTitles(Group g, UniverseScreen u)
        {
            LocalizedText[] stock = StockTitles(g);
            string colonyTitle = u.HostedTabTitle != null && u.HostedTabGroup == g
                               ? u.HostedTabTitle
                               : g == Group.Empire ? u.EmpireColonyDefault?.Name : null;
            if (colonyTitle == null)
                return stock;
            var live = new LocalizedText[stock.Length + 1];
            Array.Copy(stock, live, stock.Length);
            live[stock.Length] = new LocalizedText(colonyTitle, LocalizationMethod.RawText);
            return live;
        }

        // ── Empire group ──────────────────────────────────────────────────────────────────────
        // Ludoal fork: the third group of the unified top bar. Same frame and tab row again.
        public static readonly LocalizedText[] EmpireTabTitles =
        {
            "Colonies", "Ships", "Troops", "Economy", "Research", "Automation", "Policies"
        };

        public static readonly string[] EmpireTabTips =
        {
            Localizer.Token(GameText.DvEmpireTabTipColonies),
            Localizer.Token(GameText.DvEmpireTabTipShips),
            Localizer.Token(GameText.DvEmpireTabTipTroops),
            Localizer.Token(GameText.DvEmpireTabTipEconomy),
            Localizer.Token(GameText.DvEmpireTabTipResearch),
            Localizer.Token(GameText.DvEmpireTabTipAutomation),
            Localizer.Token(GameText.DvEmpireTabTipPolicies),
        };

        // read off the top bar's own tooltips and each screen's closing key, not guessed
        // Policies has no letter of its own: every letter is already bound elsewhere, so its
        // shortcut is a decision rather than a free pick. An empty entry is the established
        // way to say "no key" here (the group row does it too).
        public static readonly string[] EmpireTabKeys = { "U", "K", "C", "T", "R", "H", "" };

        // Ludoal fork: ONE factory and ONE switch for the Empire group - each of its screens
        // used to carry its own copy of this switch with a default case, and two of those
        // defaults disagreed (Budget's fell to Research, Research's to Economy), so a sixth tab
        // would have opened a different screen depending on where you clicked it. Same cure the
        // Galaxy group got.
        public static GameScreen EmpireTab(int index, UniverseScreen u) => index switch
        {
            0 => new EmpireManagementScreen(u, u.EmpireUI),
            1 => new ShipListScreen(u, u.EmpireUI),
            2 => new TroopListScreen(u, u.EmpireUI),
            3 => Economy(u),
            4 => new ResearchScreenNew(u, u, u.EmpireUI),
            // Ludoal fork: Automation takes its OWN case now. It used to ride the default, and a
            // default that swallows every unknown index is how a seventh tab silently opens the
            // sixth screen - no error, no clue, a long hunt.
            5 => new AutomationScreen(u),
            _ => new PoliciesScreen(u),
        };

        public static void SwitchEmpireTab(int index, int self, UniverseScreen u, GameScreen caller)
        {
            if (index == self)
                return;
            caller.ExitScreen();
            Audio.GameAudio.AcceptClick();
            if (index == EmpireTabTitles.Length) // the PERMANENT colony tab (maintainer)
                u.OpenEmpireColonyTab();
            else
                u.ScreenManager.AddScreen(EmpireTab(index, u));
        }

        // ── Design group ──────────────────────────────────────────────────────────────────────
        // Ludoal fork: the fourth group - the three screens where a design is built rather than
        // read. The Shipyard is a workshop rather than a table, so it carries the tab row over
        // its own layout instead of filling the frame with a list.
        public static readonly LocalizedText[] DesignTabTitles =
        {
            "Fleets", "Shipyard", "Blueprints"
        };

        public static readonly string[] DesignTabTips =
        {
            Localizer.Token(GameText.DvDesignTabTipFleets),
            Localizer.Token(GameText.DvDesignTabTipShipyard),
            Localizer.Token(GameText.DvDesignTabTipBlueprints),
        };

        // read off the top bar's own tooltips, not guessed
        public static readonly string[] DesignTabKeys = { "J", "Y", "F" };

        // Ludoal fork: the reserved first line some tabs carry for their filters and counts - one
        // row of controls, tight. It held 30 for a 20px row.
        public const int GalaxyHeaderH = 26;

        // Ludoal fork: a table fills its frame, 5px clear all round - the 20px inset these screens
        // used to carry belonged to the brass surround they no longer have. `headerH` is the band
        // above the table for column titles, plus any reserved first line.
        public static RectF GalaxyTable(in RectF client, float reservedLine = 0)
        {
            // ⚠ THREE things inset this table, and I only knew about one for three builds:
            //   1. NineSliceSprite cuts the CORNER textures off the frame to get ClientArea, and
            //      submenu_corner_TL is 9x9 - so it already sits 9px in, 18 off the height;
            //   2. ScrollList then insets ItemsHousing by PaddingLeft 8 / PaddingTop 15 /
            //      PaddingBot 15 - which is what is actually DRAWN, not the rect we hand it;
            //   3. whatever we add here.
            // Hence the 15px at the foot: that is PaddingBot, nothing of ours. We hand the list a
            // rect that pulls its own padding back out, so the visible margin is the 5px asked for.
            // ⚠ and the four paddings are NOT equal: PaddingRight is 24 against PaddingLeft's 8,
            // because it reserves the scrollbar lane. Pulling back symmetrically would leave 21px
            // on the right - each edge is compensated by its own padding.
            // ⚠ HORIZONTALLY only. PaddingTop/PaddingBot inset the LIST, but the column titles are
            // drawn ABOVE the list rect and never saw that padding - pulling back vertically as
            // well lifted them 19px into the tab row. The top keeps the client area as it is.
            const float corner = 9, want = 5;
            const float padL = 8, padR = 24, padB = 15;
            float backL = corner + padL - want;
            float backR = corner + padR - want;
            float backB = corner + padB - want;
            float top = client.Y + reservedLine;
            // one line of Arial20Bold plus its breathing room; 40 left an obvious gap
            const float columnTitles = 28;
            return new(client.X - backL, top + columnTitles, client.W + backL + backR,
                       client.Bottom + backB - (top + columnTitles));
        }

        // The tab the cursor is over, or -1. Ludoal fork: Tab.Hover is only set while the Submenu
        // handles input, so the rect is hit-tested directly - a draw pass can run between two
        // input passes.
        public static int GroupTabUnderCursor(Submenu tabs, Vector2 cursor)
        {
            for (int i = 0; i < tabs.Tabs.Count; ++i)
                if (tabs.Tabs[i].Rect.HitTest(cursor))
                    return i;
            return -1;
        }

        // Raise the hovered tab's tip, under its own tab rather than at the cursor.
        public static void DrawTabTip(Submenu tabs, Vector2 cursor, string[] tips, string[] keys)
        {
            int i = GroupTabUnderCursor(tabs, cursor);
            if (i < 0 || i >= tips.Length)
                return;
            RectF r = tabs.Tabs[i].Rect;
            ToolTip.CreateTooltip(tips[i], i < keys.Length ? keys[i] : "", new Vector2(r.X, r.Bottom + 4));
        }

        public static void DrawGroupTabTip(Submenu tabs, Vector2 cursor)
            => DrawTabTip(tabs, cursor, GroupTabTips, GroupTabKeys);

        public static void DrawGalaxyTabTip(Submenu tabs, Vector2 cursor)
            => DrawTabTip(tabs, cursor, GalaxyTabTips, GalaxyTabKeys);

        public static void DrawEmpireTabTip(Submenu tabs, Vector2 cursor)
            => DrawTabTip(tabs, cursor, EmpireTabTips, EmpireTabKeys);

        public static void DrawDesignTabTip(Submenu tabs, Vector2 cursor)
            => DrawTabTip(tabs, cursor, DesignTabTips, DesignTabKeys);

        // Ludoal fork: build a group's tab row on a screen, in one call - all four steps in the
        // order they have to happen. PerformLayout is what makes ClientArea known, and it has to
        // run before anything is measured against it.
        public static Submenu AddGroupTabs(GameScreen screen, LocalizedText[] titles, int selected,
                                           Action<int> onChange, out Rectangle frame, bool fullScreen = false,
                                           bool withClose = true)
        {
            frame = GroupFrame(screen.ScreenWidth, screen.ScreenHeight, fullScreen);
            var tabs = screen.Add(new Submenu(new RectF(frame.X, frame.Y, frame.Width, frame.Height), titles));
            tabs.OnTabChange = onChange;
            tabs.PerformLayout();
            tabs.SelectedIndex = selected;
            if (withClose) // the hosted colony keeps its own popup close cross instead
            {
                Vector2 closePos = GroupClosePos(tabs.ClientArea);
                screen.Add(new CloseButton(closePos.X, closePos.Y));
            }
            return tabs;
        }

        // Ludoal fork (bench 379): the Diplomacy group's own factory, born for the hosted
        // seat's Esc-return. Relationships needs a caller-built intel array, so its index
        // routes through the main screen, which opens the diagram on arrival by itself.
        public static GameScreen DiploTab(int index, UniverseScreen u)
            => (MainDiplomacyScreen.Tab)index == MainDiplomacyScreen.Tab.Espionage
                ? new InfiltrationScreen(u)
             : (MainDiplomacyScreen.Tab)index == MainDiplomacyScreen.Tab.Trends
                ? new TrendsScreen(u)
                : new MainDiplomacyScreen(u, (MainDiplomacyScreen.Tab)index);

        /// the factory of a HOSTING group
        public static GameScreen TabOf(Group g, int index, UniverseScreen u)
            => g == Group.Galaxy  ? GalaxyTab(index, u)
             : g == Group.Empire  ? EmpireTab(index, u)
             : DiploTab(index, u);

        // Ludoal fork (maintainer feedback): the target frame width. Group screens never grow past
        // this even at 1920 fullscreen, so a screen looks identical windowed and fullscreen - the
        // whole point of the resolution charter. One named constant, the single point of truth for
        // the width cap (height stays at the 1080p footprint).
        // bench 345: 1680 -> 1600, so at 1920 the right margin is wide enough to show the whole
        // minimap beside the capped frame.
        public const int MaxFrameWidth = 1600;
        // and the height cap: no group screen grows past the 1080p footprint (the resolution charter)
        public const int MaxFrameHeight = 1080;

        // Ludoal fork (bench 389, maintainer): ONE floor for every table - the info cartouche
        // zone plus the ship cartouche's two possible rows of order buttons (52 each + 4 gap,
        // ShipInfoUIElement), 10 px of air. Replaces the 1080p cap (bench 343/361 announced the
        // freed bottom-left as the cartouche's home) and the short-lived per-table split: a ship
        // can be selected from the band under ANY panel, and the stretched star cartouche fits
        // too. The housing anchors at screenH-257 (UniverseScreen.LoadContent), visible frame
        // FrameShave=61 lower. The reservation is permanent - it belongs to the zone, not to
        // whether a cartouche is showing at this instant.
        // bench 428: the specific orders live on ONE row now (the generics moved to the
        // right column), so the clearance carries a single row (52 + 4 gap) - the pages
        // reclaim the dead second row and run to 10px of the button strip
        public const int CartoucheClearance = 257 - 61 + 10 + 56;

        // bench 409 (maintainer decision): below 1200 of display height every frame runs to
        // the display foot - at 1080 the tables still read short, so the cartouche
        // reservation only holds at 1200 and above. One change here feeds GroupFrame and
        // every content-sized table alike.
        public const int FullHeightBelow = 1200;
        public static float FullTableHeight(int screenH)
            => screenH < FullHeightBelow ? screenH - TabRowY - FrameMargin
                                         : screenH - CartoucheClearance - TabRowY;

        // the height cap: the full-frame group screens (Research, Fleets, Shipyard windowed) stop
        // at inf(1080p footprint, the tables' own floor) - bench 390 (maintainer): they must not
        // dive past where a table stops, so the info cartouche keeps its reserved bottom-left at
        // every resolution. FullTableHeight already carries the cartouche+order-rows clearance;
        // Min with the 1080 cap keeps the resolution charter. Tables that DEVELOP in height go
        // through the content-sized variant, never this frame.
        public static Rectangle GroupFrame(int screenW, int screenH)
            => new(FrameMargin, TabRowY, Math.Min(screenW, MaxFrameWidth) - 2 * FrameMargin,
                   Math.Min(MaxFrameHeight - TabRowY - FrameMargin, (int)FullTableHeight(screenH)));

        // the group's CLIENT area - the frame less the tab row and the chrome, computed by
        // the formula's owner (Submenu.CalcGroupClientArea) so a panel hosted in a group's
        // frame without being a Submenu sits pixel-identical to a real tab's content
        public static RectF GroupClientArea(int screenW, int screenH)
            => Submenu.CalcGroupClientArea(new RectF(GroupFrame(screenW, screenH)), SubmenuStyle.Brown);

        // Ludoal fork (bench 355): a Shipyard-only full-screen frame. Same left/top anchor on the rail
        // (FrameMargin, TabRowY) so it still reads as the Design tab, but it drops the MaxFrame caps and
        // spans the whole display less the margins. Used only when the Shipyard's Full Screen toggle is
        // on; every table screen keeps the capped GroupFrame (the resolution charter). The camera offset
        // below reads this SAME function, so the 3D workbench recentres with the wider frame instead of
        // drifting.
        public static Rectangle GroupFrame(int screenW, int screenH, bool fullScreen)
            => fullScreen
                ? new(FrameMargin, TabRowY, screenW - 2 * FrameMargin, screenH - TabRowY - FrameMargin)
                : GroupFrame(screenW, screenH);

        public static Vector2 GroupFrameCameraOffset(int screenW, int screenH, bool fullScreen)
        {
            Rectangle frame = GroupFrame(screenW, screenH, fullScreen);
            return new Vector2((frame.CenterX() - screenW * 0.5f) / screenW,
                               (frame.CenterY() - screenH * 0.5f) / screenH);
        }

        // Ludoal fork: the group frame is anchored to the left margin and the bar, so once it caps
        // (1680 wide) its centre sits left of and above the screen centre. A 3D screen that fills
        // this frame (Shipyard, Fleets) must shift its optical centre by the same amount, or the
        // model drifts down-right at hi-res. Returned as a fraction of the screen, feeding
        // SetPerspectiveProjection's offCentre - the one place this offset is computed.
        public static Vector2 GroupFrameCameraOffset(int screenW, int screenH)
        {
            Rectangle frame = GroupFrame(screenW, screenH);
            return new Vector2((frame.CenterX() - screenW * 0.5f) / screenW,
                               (frame.CenterY() - screenH * 0.5f) / screenH);
        }

        // ── Race columns (Diplomacy group) ────────────────────────────────────────────────────
        // A race column is a FIXED width; the frame HUGS its visible columns rather than spanning
        // the screen, and the window grows with the faction count, bounded only by the physical
        // screen - the horizontal scroller pages whatever does not fit. The height is the 900p
        // frame's, always.
        const int RaceRefH = 900;
        const int NineSliceCorners = 18; // what Submenu cuts off a frame to get its ClientArea
        // Ludoal fork: race columns are a FIXED width, not a share of the frame. The window grows
        // with the faction count, capped only by the physical screen (the horizontal scroller pages
        // the rest). 228 is the measured column width (name + flag), without the inter-column gap;
        // the pitch adds ColumnGap on top - it fits one more column at 1440/1680, 8 still fit at 1920.
        public const int RaceColumnWidth = 228;

        // the column run inside a frame that wide: client area less a gutter each side, plus one
        // gap because the pitch below carries a trailing gap the last column does not draw
        static int RaceColumnRun(int frameW)
            => frameW - NineSliceCorners - 2 * ColumnGutter + ColumnGap;

        // fixed pitch: the column width plus one inter-column gap. No longer varies with count.
        public static int RaceColumnPitch(int screenW, int count) => RaceColumnWidth + ColumnGap;

        // how many columns the screen can show at that pitch - the frame never grows past the
        // screen's own footprint, the rest scrolls
        public static int RaceVisibleColumns(int screenW, int count)
        {
            count = Math.Max(count, 1);
            int pitch = RaceColumnPitch(screenW, count);
            // Ludoal fork: how many fixed-width columns fit is judged against the physical screen,
            // no 1920 ceiling; the rest scrolls.
            int avail = RaceColumnRun(screenW - 2 * FrameMargin);
            // the fit test forgives what the round-UP pitch added (at most GroupColumns-1
            // px over the whole row), or a row can fall one column short of what actually fits
            return Math.Min(count, Math.Max(1, (avail + GroupColumns - 1) / pitch));
        }

        // the frame that hugs the VISIBLE columns at that pitch - floored on what the group's
        // own tab strip needs, so the tabs never fold into a second line (the bench-290 lesson)
        public static Rectangle RaceColumnsFrame(int screenW, int screenH, int count)
        {
            count = Math.Max(count, 1);
            int pitch = RaceColumnPitch(screenW, count);
            int vis = RaceVisibleColumns(screenW, count);
            int frameW = pitch * vis - ColumnGap + 2 * ColumnGutter + NineSliceCorners;
            frameW = Math.Max(frameW, (int)MinTabStripWidth(GroupTabTitles) + 1);
            return new(FrameMargin, TabRowY, frameW,
                       Math.Min(screenH, RaceRefH) - TabRowY - FrameMargin);
        }

        // the left edge of a centred row of race columns - centred against the client
        // rather than pinned to the gutter, for the case where the tab-strip floor won.
        // `count` is the VISIBLE count; an overflowing row pins to the gutter.
        public static int RaceColumnsLeft(in RectF client, int pitch, int count)
        {
            int drawn = pitch * Math.Max(count, 1) - ColumnGap;
            return Math.Max((int)client.X + ColumnGutter,
                            (int)client.X + ((int)client.W - drawn) / 2);
        }

        // ── the race-row scroller (maintainer bench 299) ─────────────────────────────────────
        // Scrolls BY WHOLE COLUMNS: the row always lands on the column grid, so no partial
        // column ever bleeds past the frame border and no scissor clipping is needed. The
        // fork's own control: FloatSlider is a value slider and ScrollList only goes vertical.
        public class RaceRowScroller
        {
            public Rectangle Track;     // where the bar lives, set by the screen at layout
            public Rectangle WheelArea; // the wheel scrolls when the cursor is anywhere in here
            public int Count, VisibleCols, Pitch;
            public int First;           // index of the leftmost visible column
            bool Dragging; float GrabDX;

            public bool Overflowing => Count > VisibleCols;
            public int Max => Math.Max(0, Count - VisibleCols);
            public int OffsetX => First * Pitch;
            public bool Shows(int i) => i >= First && i < First + VisibleCols;

            Rectangle Thumb
            {
                get
                {
                    int w = Math.Max(30, Track.Width * VisibleCols / Math.Max(Count, 1));
                    int x = Track.X + (Max == 0 ? 0 : (Track.Width - w) * First / Max);
                    return new Rectangle(x, Track.Y, w, Track.Height);
                }
            }

            public void Draw(SpriteBatch batch)
            {
                if (!Overflowing)
                    return;
                batch.FillRectangle(Track, new Color(10, 10, 10));
                batch.DrawRectangle(Track, new Color(60, 54, 40));
                batch.FillRectangle(Thumb.Bevel(-1), new Color(118, 102, 67));
            }

            // returns true when the row moved or the gesture was consumed
            // the GRAB zone is taller than the drawn rail (maintainer bench 300: hard to
            // catch) - 5px of tolerance above and below
            Rectangle GrabZone(in Rectangle r) => new(r.X, r.Y - 5, r.Width, r.Height + 10);

            public bool HandleInput(InputState input)
            {
                if (!Overflowing)
                    return false;
                int first = First;
                if (Dragging)
                {
                    if (input.LeftMouseDown)
                    {
                        Rectangle th = Thumb;
                        float t = (input.CursorPosition.X - GrabDX - Track.X) / Math.Max(1, Track.Width - th.Width);
                        First = ((int)Math.Round(t * Max)).Clamped(0, Max);
                        return true;
                    }
                    Dragging = false;
                }
                if (WheelArea.HitTest(input.CursorPosition))
                {
                    if (input.ScrollIn)  { First = (First - 1).Clamped(0, Max); return First != first; }
                    if (input.ScrollOut) { First = (First + 1).Clamped(0, Max); return First != first; }
                }
                if (input.LeftMouseClick && GrabZone(Track).HitTest(input.CursorPosition))
                {
                    Rectangle th = Thumb;
                    if (GrabZone(th).HitTest(input.CursorPosition))
                    {
                        Dragging = true;
                        GrabDX = input.CursorPosition.X - th.X;
                    }
                    else // a page toward the click
                        First = (First + (input.CursorPosition.X > th.Center.X ? VisibleCols : -VisibleCols)).Clamped(0, Max);
                    return true;
                }
                return false;
            }
        }

        // The 900p footprint, whatever the resolution: Relationships/Blueprints keep this frame -
        // their diagram was laid out for it and does not rearrange, so a bigger screen just leaves
        // space at the frame's right. Fixed 1440x900, independent of the race columns (which grow
        // unbounded on their own pitch).
        const int Fixed900Width = 1440;
        public static Rectangle GroupFrame900(int screenW, int screenH)
            => new(FrameMargin, TabRowY, Math.Min(screenW, Fixed900Width) - 2 * FrameMargin,
                   Math.Min(screenH, RaceRefH) - TabRowY - FrameMargin);

        // Ludoal fork: a content-sized frame may hug a table narrower than its own tab strip,
        // which folds the tabs into a second line - the frame width floors on what the strip
        // needs, the content inside doesn't move
        public static float MinTabStripWidth(LocalizedText[] titles)
        {
            float w = 22; // the nine-slice corners either side of the menu bar
            SubTexture right = ResourceManager.Texture("NewUI/submenu_header_right");
            foreach (LocalizedText t in titles)
                w += Fonts.Pirulen12.TextWidth(t.Text) + 2 + right.Width;
            return w;
        }

        // Ludoal fork: the content-sized variant - the frame hugs what it holds, anchored on
        // the bar and the left margin instead of spanning the screen. First taker: the
        // Automation tab, whose content is two columns of category boxes.
        public static Submenu AddGroupTabs(GameScreen screen, LocalizedText[] titles, int selected,
                                           Action<int> onChange, float contentW, float contentH)
        {
            contentW = Math.Max(contentW, MinTabStripWidth(titles));
            var tabs = screen.Add(new Submenu(new RectF(FrameMargin, TabRowY, contentW, contentH), titles));
            tabs.OnTabChange = onChange;
            tabs.PerformLayout();
            tabs.SelectedIndex = selected;
            Vector2 closePos = GroupClosePos(tabs.ClientArea);
            screen.Add(new CloseButton(closePos.X, closePos.Y));
            return tabs;
        }

        // One column width for the whole group: the client area less a gutter each side, split
        // eight ways. Ludoal fork: the columns fill the frame rather than sitting inside a
        // narrower band of it.
        // Ludoal fork: the pitch of one column slot. Always the eight-empire pitch, so a galaxy
        // with fewer majors gets the same columns rather than wider ones.
        public static int GroupColumnWidth(in RectF client)
        {
            // rounded UP, so the eight slots span the full run and the division's remainder does
            // not pile up as extra margin on the right
            int run = (int)client.W - 2 * ColumnGutter + ColumnGap;
            return (run + GroupColumns - 1) / GroupColumns;
        }

        // The left edge of a row of `count` columns. At eight it lands exactly on the gutter; below
        // eight the row is centred. ⚠ The pitch above includes one gap, so the drawn run is
        // pitch*count - gap: counting the trailing gap made the edges 8-9px while the columns
        // themselves were 5 apart, which reads as a doubled margin.
        public static int GroupColumnsLeft(in RectF client, int count)
        {
            if (count >= GroupColumns)
                return (int)client.X + ColumnGutter;
            int drawn = GroupColumnWidth(client) * count - ColumnGap;
            return (int)client.X + ((int)client.W - drawn) / 2;
        }

        // Ludoal fork: the close cross in the top-right corner INSIDE the frame, 5px padding both
        // ways - measured off the client area, not off the frame rect, whose top edge is the tab
        // row itself (measuring from there put the cross above the frame, level with the tabs).
        // Close_Normal is 20x20.
        // ⚠ Close_Normal is 20x20 but its cross does not fill the bitmap: at an equal 5px offset on
        // both axes it reads as 5 from the top and 10 from the right, so the horizontal offset is
        // 5px tighter to make the two visual margins match.
        const int CloseSize = 20;
        const int CloseRightTrim = 5;
        public static Vector2 GroupClosePos(in RectF client)
            => new(client.Right - CloseSize - ClosePadding + CloseRightTrim, client.Y + ClosePadding);

        // Ludoal fork: what a 3D scene inside the frame is clipped to. ClientArea is already 9px
        // in on each side - NineSliceSprite cuts the corner textures off to get it - which shows
        // as a margin of bare screen around a starfield clipped to it. The scene takes the frame's
        // full width and floor instead, keeping only the client TOP so it stays under the tabs.
        // ⚠ frame is a Rectangle (UIElementV2.Rect) while client is a RectF (Submenu.ClientArea):
        // the two are genuinely different types here, not an oversight.
        public static RectF GroupSceneArea(in Rectangle frame, in RectF client)
            => new(frame.X, client.Y, frame.Width, frame.Bottom - client.Y);

        // Ludoal fork: where side content may start - clear of the close cross, which is the
        // topmost thing the frame owns. Screens that put columns against the frame's top edge
        // read this rather than each deriving the cross's height again.
        public static float GroupContentTop(in RectF client)
            => client.Y + ClosePadding + CloseSize + 10;

        // Ludoal fork: the band above a group's frame - the top bar and the tab row. The
        // table screens close on any right-click, but the Shipyard and Fleets spend that
        // gesture on their own work (deleting a module, dropping a design), so they close
        // only when the click lands UP HERE, where nothing else wants it.
        public static bool InTopBand(Vector2 cursor)
            => cursor.Y < TabRowY + TabStripH;

        // Ludoal fork (maintainer bench 336): the margin OUTSIDE the group frame - the live universe
        // map showing around the window. A right-click there closes the Shipyard / Fleets; inside the
        // frame the click keeps its design gesture. bench 355: takes the fullScreen flag so it tests
        // against the SAME frame the window is drawn from - in Full Screen the frame fills the display,
        // so the close margin shrinks to nothing instead of sitting under the expanded workbench.
        public static bool OutsideGroupFrame(Vector2 cursor, int screenW, int screenH, bool fullScreen = false)
            => !GroupFrame(screenW, screenH, fullScreen).HitTest(cursor);

        // The vertical span of a column inside the frame. ⚠ ClientArea.H already stops short of the
        // frame's bottom border, so only the TOP pad is added - taking one off the bottom as well
        // left roughly twice the gap there.
        public static int GroupColumnTop(in RectF client) => (int)client.Y + ColumnPadV;
        public static int GroupColumnHeight(in RectF client) => (int)client.H - ColumnPadV;

        // Ludoal fork: the group's frames are built transparent, so the galaxy map showed straight
        // through them - plainly on Relationships, which has no columns of its own to cover it.
        // Dark and mostly opaque: enough that the panel reads as a panel, little enough that the
        // map is still felt behind it.
        // ⚠ Alpha() and not a fourth constructor argument: the repo's own SD0001 analyzer rejects
        // that - an alpha under 255 renders additive-bright under premultiplied AlphaBlend.
        public static readonly Color GroupFrameFill = new Color(14, 12, 9).Alpha(0.92f);

        // Ludoal fork: the rect that fill belongs on. ⚠ NOT ClientArea, which is the frame's
        // INNER area: NineSliceSprite cuts it back by the corner textures' own size (9px a side),
        // so a fill painted there stops 9px short of the border on all four sides - the gap that
        // showed on every table screen (maintainer feedback).
        // Submenu's own SetBackground has always used the FULL rect minus the tab strip, which is
        // exactly why Fleets and the Shipyard - the two screens that call it - looked right. This
        // is that same arithmetic, borrowed rather than re-derived, for the screens that cannot
        // use SetBackground: it parents a child, and a child is drawn by base.Draw, i.e. AFTER
        // everything a screen paints by hand. On screens that draw their tables manually it would
        // bury their own content.
        // The 23 is Submenu's TabHeight - 2, read from the source and not measured on a capture.
        // ⚠ RectF and not Rect: UIElementV2 carries BOTH, Rect being the integer one, and taking
        // that path would quietly round a geometry the rest of the frame keeps in floats.
        // ⚠ The strip's height is ASKED of the Submenu, never assumed: it is TabRows*TabHeight, so
        // a screen whose tabs wrap to a second row reports a taller strip. The constant read off
        // one-row screens spilled the fill up onto the tabs (maintainer observation).
        public static RectF GroupFrameFillRect(Submenu tabs)
            => tabs.NumTabs == 0 ? tabs.RectF : tabs.RectF.CutTop((int)tabs.TabStripHeight);

        // Ludoal fork: the line every frame, panel and button draws around itself. One source:
        // the same numbers were written out in five places, which is how two of them end up
        // disagreeing after somebody retouches one.
        // ⚠ Read off the Codex rather than eyeballed: its border is Popup/popup_vert_L, which is
        // (193,113,26) - a frank orange. The (118,102,67) brass this used to be is the OLD trim's
        // colour, so every frame painted with it read as the thing we were replacing.
        public static Color FrameRule => UITheme.FrameRule;

        // The Codex's own body colour: Popup/popup_filler_lower. Neutral grey, not the warm
        // near-black the group frames use.
        public static Color WindowBody => UITheme.FrameBody;

        public static GameScreen Economy(UniverseScreen u) => new BudgetScreen(u);

        // Ludoal fork: both top-bar buttons lead into the same four-tab group, each landing on its
        // own tab. Espionage tab: its content is its own screen, which carries the same tab row.
        public static GameScreen Diplomacy(UniverseScreen u)
            => new MainDiplomacyScreen(u, MainDiplomacyScreen.Tab.Intelligence);

        public static GameScreen Espionage(UniverseScreen u) => new InfiltrationScreen(u);

        // Ludoal fork (bench 46.173): asking "is the caller already this screen?" has to know
        // about BOTH classes, or the answer is wrong for whichever regime is not the stock one.
        // The top bar tests this to close a screen when its own key is pressed again, and with
        // only the stock type named, a reworked Economy, Diplomacy or Espionage never recognised
        // itself and simply stacked a second copy (maintainer feedback). Same reason the openers live here: one
        // place knows the pairing, and no call site has to remember there are two of each.
        public static bool IsEconomy(GameScreen s) => s is BudgetScreen;

        // ── Which group a screen belongs to ───────────────────────────────────────────────────
        // Ludoal fork: the top bar tints the button of the group you are inside. One place knows
        // the membership - a test spread over the bar's draw would drift the moment a tab moves.
        public enum Group { None, Galaxy, Empire, Diplomacy, Design }

        public static Group GroupOf(GameScreen s) => s switch
        {
            null => Group.None,

            // Ludoal fork (migration, bench 386): a stacked colony belongs to whichever
            // group its hosted seat names - the one membership that is dynamic
            ColonyScreen c => c.P.Universe.Screen.HostedTabGroup,

            PlanetListScreen or ExoticSystemsListScreen or EmpirePatrolsScreen
                or ImportantEventsScreen
                => Group.Galaxy,

            EmpireManagementScreen or ShipListScreen or TroopListScreen
                or ResearchScreenNew or BudgetScreen
                => Group.Empire,

            InfiltrationScreen
                => Group.Diplomacy,

            FleetDesignScreen or ShipDesignScreen or BlueprintsScreen
                => Group.Design,

            _ => IsDiplomacyGroup(s) ? Group.Diplomacy : Group.None,
        };

        // Ludoal fork: "is the caller already THIS destination?", asked before opening one. Each
        // answers for its own screens only: the two share a tab row, but they are two destinations,
        // and a test that covered the whole group answered yes to both - so from Diplomacy the
        // Espionage key closed the group without opening Espionage, and the other way round.
        // Both regimes are still named, which is what the 46.173 bug was about: with only the
        // stock type listed, a reworked screen never recognised itself and stacked a second copy.
        public static bool IsDiplomacy(GameScreen s)
            => s is MainDiplomacyScreen
                 or DiplomacyScreen.RelationshipsDiagramScreen;

        public static bool IsEspionage(GameScreen s)
            => s is InfiltrationScreen;

        static bool IsDiplomacyGroup(GameScreen s)
            => s is MainDiplomacyScreen
                 or InfiltrationScreen
                 or TrendsScreen
                 or DiplomacyScreen.RelationshipsDiagramScreen;
    }
}
