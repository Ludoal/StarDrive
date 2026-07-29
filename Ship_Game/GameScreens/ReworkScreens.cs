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

        public static bool IsDiplomacy(GameScreen s) => s is MainDiplomacyScreen or MainDiplomacyScreenRework;

        public static bool IsEspionage(GameScreen s) => s is InfiltrationScreen or InfiltrationScreenRework;
    }
}
