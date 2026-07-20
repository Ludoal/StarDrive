using SDGraphics;
using Ship_Game.Ships;
using Ship_Game.Universe;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game
{
    // Ludoal fork (battle simulator S1): a disposable 1v1 arena, StarSector-style,
    // launched from the Shipyard. Minimal universe: two empires at war, no systems,
    // no planets, no strategic AI food. The game universe below stays alive, paused.
    //
    // S1 is the platform validation prototype: it must prove that
    //   (a) a planet-less universe survives in-game (strategic AI without a HomeSystem),
    //   (b) two stacked UniverseScreens coexist (own SimThread each, host paused),
    //   (c) combat auto-engages and the exit path restores the hosting game cleanly.
    // The enemy design selection UI comes in S2; S1 mirrors the player's design.
    public sealed class BattleSimUniverse : UniverseScreen
    {
        readonly UniverseScreen HostGame; // the real game we cover; paused while we run
        readonly string PlayerDesign;
        readonly string EnemyDesign;
        Empire Them;
        bool Spawned;

        BattleSimUniverse(UniverseParams p, float radius, UniverseScreen hostGame,
                          string playerDesign, string enemyDesign) : base(p, radius)
        {
            HostGame = hostGame;
            PlayerDesign = playerDesign;
            EnemyDesign = enemyDesign;
            UState.NoEliminationVictory = true;
            UState.CanShowDiplomacyScreen = false;
        }

        public static void Launch(UniverseScreen hostGame, string playerDesign, string enemyDesign)
        {
            var p = new UniverseParams();
            p.DebugDisableShipLaunch = true; // no launch sequence: combat-capable immediately

            var sim = new BattleSimUniverse(p, 500_000f, hostGame, playerDesign, enemyDesign);

            // any two distinct major races will do — the arena is about the ships;
            // Hard difficulty only affects ship AI modifiers, there is no economy here
            var majors = ResourceManager.MajorRaces;
            Empire us   = sim.UState.CreateEmpire(majors[0], isPlayer: true, GameDifficulty.Hard);
            Empire them = sim.UState.CreateEmpire(majors[1], isPlayer: false, GameDifficulty.Hard);
            sim.Them = them;
            Empire.SetRelationsAsKnown(us, them);
            us.AI.DeclareWarOn(them, WarType.BorderConflict);

            if (hostGame != null)
                hostGame.UState.Paused = true;

            // AddScreen, NOT GoToScreen: the hosting game must survive below.
            // (DeveloperUniverse's ClearScene would wipe the game's 3D scene — avoided.)
            ScreenManager.Instance.AddScreen(sim);
        }

        public override void LoadContent()
        {
            base.LoadContent();

            if (!Spawned)
            {
                Spawned = true;
                // face to face, well inside mutual sensor range (base 20k)
                Ship a = Ship.CreateShipAtPoint(UState, PlayerDesign, Player, new Vector2(-6000f, 0f));
                Ship b = Ship.CreateShipAtPoint(UState, EnemyDesign, Them, new Vector2(6000f, 0f));

                // 45.22 field result: with no explicit orders both ships FTL-jumped away
                // (default AI goals in an empty universe). Lock them on each other.
                if (a != null && b != null)
                {
                    a.AI.OrderAttackSpecificTarget(b);
                    b.AI.OrderAttackSpecificTarget(a);
                }
            }

            CamDestination = new Vector3d(0, 0, 18000);
            UState.Paused = false;
        }

        public override bool HandleInput(InputState input)
        {
            // the arena is disposable: Escape leaves immediately, back to the game
            if (input.Escaped)
            {
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        public override void ExitScreen()
        {
            base.ExitScreen();
            if (HostGame != null)
                HostGame.UState.Paused = false;
        }
    }
}
