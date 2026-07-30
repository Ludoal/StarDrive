using System;
using SDGraphics;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens
{
    /// <summary>
    /// Ludoal fork: the one door to the screens this fork rebuilt, and the geometry the reworked
    /// top bar lays them out on.
    ///
    /// Why a factory rather than a check at each call site: these screens are opened from fifteen
    /// places between the top bar, the notifications, the colony screen's Edit button and each
    /// other. A test spread over fifteen sites is a test that will be forgotten at the sixteenth.
    /// One place decides; every caller goes through here.
    ///
    /// ⚠ THE STOCK SCREENS ARE STILL HERE, AND THEY ARE NOT DEAD CODE. They are no longer reachable
    /// in game - there is nothing left to choose - but they are kept BYTE-IDENTICAL TO UPSTREAM on
    /// purpose: when upstream changes something of substance in one of them (a budget formula, an
    /// espionage rule), the merge lands cleanly on the stock file and its diff tells us exactly what
    /// to carry into our version. Delete them and that reference goes with them.
    ///
    /// So: never edit a stock screen. Restore one with `git checkout &lt;base&gt; -- &lt;path&gt;`, never by
    /// copying it in - a plain copy gets the line endings wrong and every line then shows as
    /// changed. And OUR versions carry the Rework suffix: they are the addition.
    ///
    /// The Shipyard's floating hover cartouche (ShipInfoOverlayComponent) was never doubled: the
    /// colony and fleet screens use it directly.
    /// </summary>
    public static class ReworkScreens
    {
        // Ludoal fork: the group's shared geometry, in one place - three screens build the same
        // frame and tab row, and a value copied three times is a value that will drift.
        //
        // TabRowY is the top of the Submenu RECT, whose first row is the tab strip - the frame
        // itself opens one tab row lower. The top bar draws Help and the speed buttons at Y=64 on a
        // 24px texture, so their bottom edge is 88, and the FRAME is wanted 10px under that: the
        // rect starts a tab row above, at 98 - 25. They move into the unified bar later, at which
        // point this can rise.
        const int TabStripH = 25; // Submenu.TabHeight, which is private
        public const int TabRowY = 64 + 24 + 10 - TabStripH;
        public const int FrameMargin = 10;   // clear of every screen edge
        public const int ColumnGutter = 5;   // inside the frame, left and right of the columns
        public const int ColumnGap = 5;      // between two columns
        public const int ColumnPadV = 5;     // above and below the columns, inside the frame
        public const int ClosePadding = 5;   // the close cross, off the client area's top-right
        public const int GroupColumns = 8;   // the row is always eight wide, known or not

        public static readonly LocalizedText[] GroupTabTitles =
        {
            "Intelligence", "Bonuses", "Relationships", "Espionage"
        };

        // Ludoal fork: what each tab holds, for the hover tip. Submenu.Tab has no tooltip field and
        // Submenu is shared, so the screens raise these themselves against the tab rects it does
        // expose. The keys are the ones the screens themselves close on: I for the diplomacy side,
        // E for espionage.
        public static readonly string[] GroupTabTips =
        {
            "Race, rank and empire data, artifacts and treaties.",
            "Racial traits and the bonuses they grant.",
            "Treaty diagram between every empire you know of.",
            "Infiltration: budget, defense and operations by level.",
        };

        public static readonly string[] GroupTabKeys = { "I", "I", "I", "E" };

        // ── Galaxy group ──────────────────────────────────────────────────────────────────────
        // Ludoal fork: the second group of the unified top bar. Same frame, same tab row as the
        // Diplomacy group - one geometry for the whole bar.
        public static readonly LocalizedText[] GalaxyTabTitles =
        {
            "Planets", "Exotic Systems", "Patrols"
        };

        public static readonly string[] GalaxyTabTips =
        {
            "Every planet you know of, sortable, with the troops you can land.",
            "Systems holding exotic resources, and what they grant.",
            "Standing patrol routes and the fleets flying them.",
        };

        // the keys those screens already close on
        public static readonly string[] GalaxyTabKeys = { "P", "G", "" };

        // ── Empire group ──────────────────────────────────────────────────────────────────────
        // Ludoal fork: the third group of the unified top bar. Same frame and tab row again.
        public static readonly LocalizedText[] EmpireTabTitles =
        {
            "Colonies", "Ships", "Troops", "Economy", "Research"
        };

        public static readonly string[] EmpireTabTips =
        {
            "Every colony you hold, its labor, storage and construction.",
            "Every ship you own, sortable, with its orders and upkeep.",
            "Your troops: where they are, their strength and status.",
            "Treasury and taxes, with the budget of each colony.",
            "The technology tree and the research queue.",
        };

        // read off the top bar's own tooltips and each screen's closing key, not guessed
        public static readonly string[] EmpireTabKeys = { "U", "K", "C", "T", "R" };

        // The first line of the Planets tab is reserved for its filters and troop count, so its
        // table starts lower than the other tabs'. Ludoal fork.
        public const int GalaxyHeaderH = 30;

        // Ludoal fork: a table fills its frame, 5px clear all round - the 20px inset these screens
        // used to carry belonged to the brass surround they no longer have. `headerH` is the band
        // above the table for column titles, plus any reserved first line.
        public static RectF GalaxyTable(in RectF client, float reservedLine = 0)
        {
            // ⚠ NO padding of our own: the client area already insets on all four sides, and its
            // height already stops short of the bottom border - adding 5px gave 5 too many left,
            // right and top, and 10 too many at the foot. The frame IS the margin.
            float top = client.Y + reservedLine;
            const float columnTitles = 40;
            return new(client.X, top + columnTitles, client.W,
                       client.Bottom - (top + columnTitles));
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

        // Ludoal fork: build a group's tab row on a screen, in one call - all four steps in the
        // order they have to happen. PerformLayout is what makes ClientArea known, and it has to
        // run before anything is measured against it.
        public static Submenu AddGroupTabs(GameScreen screen, LocalizedText[] titles, int selected,
                                           Action<int> onChange, out Rectangle frame)
        {
            frame = GroupFrame(screen.ScreenWidth, screen.ScreenHeight);
            var tabs = screen.Add(new Submenu(new RectF(frame.X, frame.Y, frame.Width, frame.Height), titles));
            tabs.OnTabChange = onChange;
            tabs.PerformLayout();
            tabs.SelectedIndex = selected;
            Vector2 closePos = GroupClosePos(tabs.ClientArea);
            screen.Add(new CloseButton(closePos.X, closePos.Y));
            return tabs;
        }

        public static Rectangle GroupFrame(int screenW, int screenH)
            => new(FrameMargin, TabRowY, screenW - 2 * FrameMargin, screenH - TabRowY - FrameMargin);

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

        public static GameScreen Economy(UniverseScreen u) => new BudgetScreenRework(u);

        // Ludoal fork: both top-bar buttons lead into the same four-tab group, each landing on its
        // own tab. Espionage tab: its content is its own screen, which carries the same tab row.
        public static GameScreen Diplomacy(UniverseScreen u)
            => new MainDiplomacyScreenRework(u, MainDiplomacyScreenRework.Tab.Intelligence);

        public static GameScreen Espionage(UniverseScreen u) => new InfiltrationScreenRework(u);

        // Ludoal fork (bench 46.173): asking "is the caller already this screen?" has to know
        // about BOTH classes, or the answer is wrong for whichever regime is not the stock one.
        // The top bar tests this to close a screen when its own key is pressed again, and with
        // only the stock type named, a reworked Economy, Diplomacy or Espionage never recognised
        // itself and simply stacked a second copy (maintainer feedback). Same reason the openers live here: one
        // place knows the pairing, and no call site has to remember there are two of each.
        public static bool IsEconomy(GameScreen s) => s is BudgetScreen or BudgetScreenRework;

        // Ludoal fork: the reworked group is FOUR screens sharing one tab row, so both top-bar
        // buttons have to recognise all of them - otherwise pressing a key while inside the group
        // stacks a second copy instead of closing it (the 46.173 bug, one test per class short).
        public static bool IsDiplomacy(GameScreen s)
            => s is MainDiplomacyScreen || IsDiplomacyGroup(s);

        public static bool IsEspionage(GameScreen s)
            => s is InfiltrationScreen || IsDiplomacyGroup(s);

        static bool IsDiplomacyGroup(GameScreen s)
            => s is MainDiplomacyScreenRework
                 or InfiltrationScreenRework
                 or DiplomacyScreen.RelationshipsDiagramScreen;
    }
}
