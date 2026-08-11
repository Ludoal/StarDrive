using System;
using Ship_Game.Universe;

namespace Ship_Game;

public abstract class PlanetScreen : GameScreen
{
    public readonly Planet P;
    public readonly UniverseState Universe;
    public readonly Empire Player;

    // Ludoal fork (spec: living universe): a planet screen may claim the auto-pause like
    // any page - the colony does (uniform with the groups, subject to the page-pause
    // option), the ground combat view never does (a live battle keeps the resume).
    protected PlanetScreen(GameScreen parent, Planet p, UniverseScreen toPause = null) : base(parent, toPause)
    {
        P = p ?? throw new ArgumentNullException(nameof(p));
        Universe = p.Universe;
        Player = Universe.Player;
        IsPopup = true; // auto-dismiss with right-click
    }
}
