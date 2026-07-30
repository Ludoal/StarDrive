using SDGraphics;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game.GameScreens.MainMenu
{
    /// <summary>
    /// Ludoal fork: opt in to the screens this fork has rebuilt.
    ///
    /// They are BETA and off by default. Each one replaces a stock BlackBox screen entirely,
    /// so the choice is made here rather than inside the screen: the stock version and ours
    /// never have to coexist on screen, and an experienced player meets the interface they
    /// already know unless they ask for the new one.
    ///
    /// Applied the next time one of those screens is opened. In practice that is immediate:
    /// this popup is reached through the options screen, so any screen it would affect has
    /// been closed on the way here.
    /// </summary>
    public sealed class ReworkOptionsScreen : PopupWindow
    {
        public ReworkOptionsScreen(GameScreen parent) : base(parent, 560, 430)
        {
            TitleText = "Rework Options";
            MiddleText = "Nothing to choose here for now: the rebuilt screens are the only ones, "
                       + "since the top bar is being reworked as a whole.";
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        // Ludoal fork: the panel is EMPTY on purpose, and kept on purpose. The rebuilt screens are
        // no longer optional - the stock ones stay in the tree as an upstream reference, not as a
        // playable alternative - but the whole opt-in mechanism is left standing so a screen can be
        // made optional again by adding one line here. Nothing is wired to a setting any more.
        public override void LoadContent()
        {
            base.LoadContent();
        }
    }
}
