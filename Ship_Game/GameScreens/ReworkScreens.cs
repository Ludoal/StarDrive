using SDGraphics;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens
{
    /// <summary>
    /// Ludoal fork: the screens this fork rebuilt from scratch can each be swapped back to the
    /// stock BlackBox version, from Options -> Rework Options.
    ///
    /// Why a factory rather than a check at each call site: these screens are opened from
    /// fifteen places between the top bar, the notifications, the colony screen's Edit button
    /// and each other. A test spread over fifteen sites is a test that will be forgotten at the
    /// sixteenth. One place decides; every caller goes through here.
    ///
    /// The naming rule matters for maintenance: the STOCK classes keep their original names and
    /// their original files, byte for byte, so upstream fixes land on them with no conflict and
    /// the classic versions stay current for free. It is OUR versions that carry the Rework
    /// suffix — they are the addition, so they are the ones that should be marked as such.
    ///
    /// Staying close to upstream is the point, so restore a stock file with
    /// `git checkout &lt;base&gt; -- &lt;path&gt;`, never by copying it in: a plain copy gets the line
    /// endings wrong and every one of its lines then shows as changed.
    ///
    /// ⚠ One deliberate exception (maintainer feedback): the three stock screens carry the fork's live top bar.
    /// It is a feature of the fork rather than of the rework, so turning a rebuilt screen off
    /// should not cost the player the navigation that comes with every other panel. They are
    /// therefore NOT byte-identical, and a future upstream merge will conflict on those few
    /// lines - which is the accepted price.
    ///
    /// The Shipyard's floating hover cartouche (ShipInfoOverlayComponent) is deliberately NOT
    /// part of this: the colony and fleet screens use it directly, in both regimes.
    ///
    /// Diplomacy and Espionage share ONE setting: the rework merges both into a single four-tab
    /// group (Intelligence, Bonuses, Relationships, Espionage), so there is nothing left to
    /// enable separately. The Shipyard is still to come.
    /// </summary>
    public static class ReworkScreens
    {
        // Ludoal fork: the group's shared geometry, in one place - three screens build the same
        // frame and tab row, and a value copied three times is a value that will drift.
        //
        // TabRowY: the top bar draws Help and the speed buttons at Y=64 on a 24px texture. The row
        // rides 2px under them, which is as high as it goes while they are still there; they move
        // into the unified bar later.
        public const int TabRowY = 64 + 24 - 10;
        public const int FrameMargin = 10;   // clear of every screen edge
        public const int ColumnGutter = 5;   // inside the frame, left and right of the columns
        public const int ColumnGap = 5;      // between two columns
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
        public static void DrawGroupTabTip(Submenu tabs, Vector2 cursor)
        {
            int i = GroupTabUnderCursor(tabs, cursor);
            if (i < 0 || i >= GroupTabTips.Length)
                return;
            RectF r = tabs.Tabs[i].Rect;
            ToolTip.CreateTooltip(GroupTabTips[i], GroupTabKeys[i], new Vector2(r.X, r.Bottom + 4));
        }

        public static Rectangle GroupFrame(int screenW, int screenH)
            => new(FrameMargin, TabRowY, screenW - 2 * FrameMargin, screenH - TabRowY - FrameMargin);

        // One column width for the whole group: the client area less a gutter each side, split
        // eight ways. Ludoal fork: the columns fill the frame rather than sitting inside a
        // narrower band of it.
        public static int GroupColumnWidth(in RectF client)
            => ((int)client.W - 2 * ColumnGutter) / GroupColumns;

        // Ludoal fork: the column width is always the eight-empire one, so a galaxy with fewer
        // majors gets the same columns rather than wider ones - and the shorter row is centred
        // instead of hugging the left edge.
        public static int GroupColumnsLeft(in RectF client, int count)
        {
            int colW = GroupColumnWidth(client);
            int drawn = colW * count - ColumnGap; // the last column has no gap after it
            return (int)client.X + ((int)client.W - drawn) / 2;
        }

        // Ludoal fork: the close cross in the frame's top-right corner, 5px padding both ways.
        // Close_Normal is 20x20.
        const int CloseSize = 20;
        public static Vector2 GroupClosePos(in Rectangle frame)
            => new(frame.Right - CloseSize - 5, frame.Y + 5);

        // Ludoal fork: the group's frames are built transparent, so the galaxy map showed straight
        // through them - plainly on Relationships, which has no columns of its own to cover it.
        // Dark and mostly opaque: enough that the panel reads as a panel, little enough that the
        // map is still felt behind it.
        public static readonly Color GroupFrameFill = new Color(14, 12, 9, 235);

        public static GameScreen Economy(UniverseScreen u)
            => GlobalStats.ReworkEconomy ? new BudgetScreenRework(u) : new BudgetScreen(u);

        // Ludoal fork: both top-bar buttons lead into the same four-tab group, each landing on its
        // own tab. Espionage tab: its content is its own screen, which carries the same tab row.
        public static GameScreen Diplomacy(UniverseScreen u)
            => GlobalStats.ReworkDiplomacyGroup
             ? new MainDiplomacyScreenRework(u, MainDiplomacyScreenRework.Tab.Intelligence)
             : new MainDiplomacyScreen(u);

        public static GameScreen Espionage(UniverseScreen u)
            => GlobalStats.ReworkDiplomacyGroup ? new InfiltrationScreenRework(u) : new InfiltrationScreen(u);

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
                 or DiplomacyScreen.RelationshipsDiagramRework;
    }
}
