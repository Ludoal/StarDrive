namespace Ship_Game;

// Ludoal fork: every player notification is born with one of these categories, so the per-category
// auto-clear can key off it. ⚠ There is deliberately NO default / "General" / zero value -
// AddNotification takes the category as a required argument, so a new notification does not
// compile until it names one. The grouping follows how the player TRIAGES an alert.
public enum NotificationCategory
{
    Exploration = 1, // anomalies, researchable/mineable planets & stars, system explored, scout lost
    Colony,          // colonized, capital transfer, colony died, colony hazards (volcano/meteor/lava), starvation
    Construction,    // buildings built/destroyed, research/mining stations, orbital limits, empty queue
    Combat,          // invasion, enemy troops, conquest, rebellion, crash sites
    Diplomacy,       // treaties, war, peace, empire merged/surrendered
    Espionage,       // spy ops, agents, moles
    Economy,         // treasury low, resources
    Events,          // random / story / tech event popups (the ones that pause the game)
    Threats,         // Remnants and Pirates - hostile non-diplomatic menaces
}
