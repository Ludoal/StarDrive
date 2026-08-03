using System;
using SDGraphics;
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
            "Intelligence", "Bonuses", "Relationships", "Espionage"
        };

        // Ludoal fork: what each tab holds, for the hover tip. Submenu.Tab has no tooltip field and
        // Submenu is shared, so the screens raise these themselves against the tab rects it does
        // expose. A key is shown only where the tab HAS one of its own: Bonuses and Relationships
        // are reached through the group, so advertising the group's key on them read as a promise
        // the tab does not keep.
        public static readonly string[] GroupTabTips =
        {
            "Race, rank and empire data, artifacts and treaties.",
            "Racial traits and the bonuses they grant.",
            "Treaty diagram between every empire you know of.",
            "Infiltration: budget, defense and operations by level.",
        };

        public static readonly string[] GroupTabKeys = { "I", "", "", "E" };

        // ── Galaxy group ──────────────────────────────────────────────────────────────────────
        // Ludoal fork: the second group of the unified top bar. Same frame, same tab row as the
        // Diplomacy group - one geometry for the whole bar.
        public static readonly LocalizedText[] GalaxyTabTitles =
        {
            "Planets", "Exotic Systems", "Patrols", "Events"
        };

        public static readonly string[] GalaxyTabTips =
        {
            "Every planet you know of, sortable, with the troops you can land.",
            "Systems holding exotic resources, and what they grant.",
            "Standing patrol routes and the fleets flying them.",
            "The log of what happened to your empire, newest first.",
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
            u.ScreenManager.AddScreen(GalaxyTab(index, u));
        }

        // ── Empire group ──────────────────────────────────────────────────────────────────────
        // Ludoal fork: the third group of the unified top bar. Same frame and tab row again.
        public static readonly LocalizedText[] EmpireTabTitles =
        {
            "Colonies", "Ships", "Troops", "Economy", "Research", "Automation"
        };

        public static readonly string[] EmpireTabTips =
        {
            "Every colony you hold, its labor, storage and construction.",
            "Every ship you own, sortable, with its orders and upkeep.",
            "Your troops: where they are, their strength and status.",
            "Treasury and taxes, with the budget of each colony.",
            "The technology tree and the research queue.",
            "What runs itself: taxes, exploration, construction, trade - and which alerts stay quiet.",
        };

        // read off the top bar's own tooltips and each screen's closing key, not guessed
        public static readonly string[] EmpireTabKeys = { "U", "K", "C", "T", "R", "H" };

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
            _ => new AutomationScreen(u),
        };

        public static void SwitchEmpireTab(int index, int self, UniverseScreen u, GameScreen caller)
        {
            if (index == self)
                return;
            caller.ExitScreen();
            Audio.GameAudio.AcceptClick();
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
            "Arrange your fleets and save their formations.",
            "Design a ship: place modules, set its stance and role.",
            "Colony blueprints: the build order a governor follows.",
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
        public static bool IsEconomy(GameScreen s) => s is BudgetScreenRework;

        // ── Which group a screen belongs to ───────────────────────────────────────────────────
        // Ludoal fork: the top bar tints the button of the group you are inside. One place knows
        // the membership - a test spread over the bar's draw would drift the moment a tab moves.
        public enum Group { None, Galaxy, Empire, Diplomacy, Design }

        public static Group GroupOf(GameScreen s) => s switch
        {
            null => Group.None,

            PlanetListScreen or ExoticSystemsListScreen or EmpirePatrolsScreen
                or ImportantEventsScreen
                => Group.Galaxy,

            EmpireManagementScreen or ShipListScreen or TroopListScreen
                or ResearchScreenNew or BudgetScreenRework
                => Group.Empire,

            InfiltrationScreenRework
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
            => s is MainDiplomacyScreenRework
                 or DiplomacyScreen.RelationshipsDiagramScreen;

        public static bool IsEspionage(GameScreen s)
            => s is InfiltrationScreenRework;

        static bool IsDiplomacyGroup(GameScreen s)
            => s is MainDiplomacyScreenRework
                 or InfiltrationScreenRework
                 or DiplomacyScreen.RelationshipsDiagramScreen;
    }
}
