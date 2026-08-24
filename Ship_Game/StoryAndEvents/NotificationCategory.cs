namespace Ship_Game;

// Ludoal fork (wishlist): every player notification is born with one of these categories, so the
// per-category auto-clear can key off it. There is deliberately NO default / "General" / zero
// value: AddNotification takes the category as a required argument, so the compiler refuses any
// notification that is not classified. A new notification added later cannot slip into a silent
// catch-all - it will not compile until it names its category. (Lek's rule: an Unknown=0 is a
// programmed leak.) The player-facing grouping is by how the player TRIAGES an alert, not by the
// code that raises it.
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
