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
    /// Applied the next time a screen is opened — an open screen is not rebuilt underneath
    /// the player.
    /// </summary>
    public sealed class ReworkOptionsScreen : PopupWindow
    {
        public ReworkOptionsScreen(GameScreen parent) : base(parent, 560, 430)
        {
            TitleText = "Rework Options";
            MiddleText = "Rebuilt screens, off by default while they are still beta. "
                       + "Leave one off to play the original BlackBox version.";
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
        }

        public override void LoadContent()
        {
            base.LoadContent();

            UIList list = AddList(new Vector2(X + 40, Y + 150), new Vector2(Width - 80, 210));
            list.Padding = new Vector2(2f, 8f);

            list.AddCheckbox(() => GlobalStats.ShipyardComparison, title: "Shipyard comparison",
                tooltip: "Shift-click a module or design to pin it, and every row shows how the "
                       + "one on the workbench compares. Includes the hover cartouche. Off: the "
                       + "panels show plain values, with no delta column and no hover frame.");

            list.AddCheckbox(() => GlobalStats.ReworkEconomy, title: "Economy",
                tooltip: "Full-screen economic overview with per-planet budget columns. "
                       + "Off: the original budget window.");

            list.AddCheckbox(() => GlobalStats.ReworkDiplomacy, title: "Diplomacy",
                tooltip: "Rebuilt relations screen: treaty lanes, filter checkboxes and the "
                       + "merged treaties matrix. Off: the original diplomacy screen.");

            list.AddCheckbox(() => GlobalStats.ReworkEspionage, title: "Espionage",
                tooltip: "Rebuilt infiltration screen. Off: the original espionage screen.");

            list.AddLabel("A screen already open keeps its layout until it is reopened.")
                .Color = Colors.Cream;
        }
    }
}
