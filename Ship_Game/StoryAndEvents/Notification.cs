using System.Windows.Forms;
using SDGraphics;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Ship_Game.GameScreens;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game;

public sealed class Notification
{
    public object ReferencedItem1;
    public GameObject ReferencedItem2;

    public Empire RelevantEmpire;
    public Rectangle ClickRect;
    public Rectangle DestinationRect;

    public float transitionElapsedTime;
    public float transDuration = 1f;
        
    public string Message;
    public string Action; // @TODO - this needs an enum!

    public SubTexture Icon;
    public string IconPath;
    public string SymbolPath; // similar ro Relevant Empire, but with custom Texture

    public bool Tech;
    public bool ShowMessage;
    public bool Pause = true;

    // Important notifications are also stored in UniverseState.ImportantEventsList
    // (with Title and StarDate) for later viewing in the ImportantEventsScreen
    public bool Important;
    public string Title;
    public string LogMessage; // optional log override when Message contains UI-only text

    /** @return TRUE if input was captured */
    public bool HandleInput(InputState input, NotificationManager m)
    {
        if (!ClickRect.HitTest(input.CursorPosition))
        {
            ShowMessage = false;
            return false;
        }

        ShowMessage = true;

        if (input.LeftMouseReleased)
        {
            switch (Action)
            {
                case "SnapToPlanet":
                    m.SnapToPlanet(ReferencedItem1 as Planet);
                    break;
                case "SnapToSystem":
                    m.SnapToSystem(ReferencedItem1 as SolarSystem);
                    break;
                case "CombatScreen":
                    m.SnapToCombat(ReferencedItem1 as Planet);
                    break;
                case "LoadEvent":
                    ((ExplorationEvent)ReferencedItem1)?.TriggerExplorationEvent(m.Screen);
                    break;
                case "ResearchScreen":
                    m.ScreenManager.AddScreen(new ResearchPopup(m.Screen, ReferencedItem1 as string));
                    break;
                case "SnapToExpandSystem":
                    m.SnapToExpandedSystem(ReferencedItem2 as Planet, ReferencedItem1 as SolarSystem);
                    break;
                case "ShipDesign":
                    m.ScreenManager.AddScreen(new ShipDesignScreen(m.Screen, m.Screen.EmpireUI));
                    break;
                case "SnapToShip":
                    m.SnapToShip(ReferencedItem1 as Ship);
                    break;
                case "SnapToStation":
                    m.SnapToStation(ReferencedItem1 as Ship);
                    break;
                case "Diplomacy": // Ludoal fork: diplomacy notifications open the diplomacy panel
                    m.ScreenManager.AddScreen(GameScreens.ScreenGroups.Diplomacy(m.Screen));
                    break;
                case "EspionageScreen": // Ludoal fork (wishlist): spy notifications open the espionage panel
                    m.ScreenManager.AddScreen(GameScreens.ScreenGroups.Espionage(m.Screen));
                    break;
                case "Economy": // Ludoal fork (maintainer feedback): the treasury warning opens the economy panel
                    m.ScreenManager.AddScreen(GameScreens.ScreenGroups.Economy(m.Screen));
                    break;
            }
            return true;
        }
        if (input.RightMouseClick && Action != "LoadEvent")
        {
            GameAudio.SubBassWhoosh();
            // ADDED BY SHAHMATT (to unpause game on right clicking notification icon)
            if (GlobalStats.PauseOnNotification && Pause)
                m.Screen.UState.Paused = false;

            return true;
        }
        return false;
    }
}
