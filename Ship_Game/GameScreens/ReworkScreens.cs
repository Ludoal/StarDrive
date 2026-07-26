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
    /// Being byte-identical to upstream is the whole point, so restore a stock file with
    /// `git checkout &lt;base&gt; -- &lt;path&gt;`, never by copying it in: a plain copy gets the line
    /// endings wrong and every one of its lines then shows as changed.
    ///
    /// The Shipyard's floating hover cartouche (ShipInfoOverlayComponent) is deliberately NOT
    /// part of this: the colony and fleet screens use it directly, in both regimes.
    ///
    /// Shipyard, Diplomacy and Espionage are still to come — Economy first, to find out what
    /// the pattern really costs before repeating it three times.
    /// </summary>
    public static class ReworkScreens
    {
        public static GameScreen Economy(UniverseScreen u)
            => GlobalStats.ReworkEconomy ? new BudgetScreenRework(u) : new BudgetScreen(u);
    }
}
