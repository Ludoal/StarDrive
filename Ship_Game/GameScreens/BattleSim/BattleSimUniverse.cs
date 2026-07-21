using SDGraphics;
using Color = Microsoft.Xna.Framework.Color;
using SDUtils;
using Ship_Game.AI;
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
        Ship ShipA, ShipB;
        bool Spawned;
        // S3: fight tracking for the battle report (snapshot-based)
        float FightSeconds;
        float OrdStartA, OrdStartB;
        bool FightOver;
        float ReportDelay;   // let the final explosion play before the report
        bool ReportShown;

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
            // no colonies, no research: silence the "No Research!" banner
            us.AutoResearch = true;
            them.AutoResearch = true;
            // 45.30 field result: no module grid button / overlay — the BlackBox
            // espionage mode flips CanBeScannedByPlayer off when relations are
            // created. This is a simulator: full transparency on both sides.
            us.SetCanBeScannedByPlayer(true);
            them.SetCanBeScannedByPlayer(true);

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
                SpawnShips();
            }

            CamDestination = new Vector3d(0, 0, 18000);
            // 45.28 field result: pitch-black ships — ResetLighting early-returns when
            // the HOST's light rig is already active, leaving the arena lit by suns
            // positioned in the host galaxy. Force our own rig (global fills + ambient).
            ResetLighting(forceReset: true);
            // 45.29 field result: STILL black — the ship shader's PointLight slots only
            // bind system-scale "sun" lights (1k <= R < 1M, grouped at one XY); the
            // global fills are excluded by design and no system exists here. Fake a sun
            // at the arena origin, mimicking ResetSolarSystemLights' Key + LocalFill.
            AddLight("Arena Sun Key",  new Vector2(0f, 0f), 2.0f, 215_000f, Color.White, -50000f, fillLight: false, shadowQuality: 0f);
            AddLight("Arena Sun Fill", new Vector2(0f, 0f), 1.1f, 215_000f, Color.White, 0f,      fillLight: false, shadowQuality: 0f);
            UState.Paused = false;
        }

        void SpawnShips()
        {
            // face to face, well inside mutual sensor range (base 20k)
            ShipA = Ship.CreateShipAtPoint(UState, PlayerDesign, Player, new Vector2(-6000f, 0f));
            ShipB = Ship.CreateShipAtPoint(UState, EnemyDesign, Them, new Vector2(6000f, 0f));
            OrdStartA = ShipA?.Ordinance ?? 0f;
            OrdStartB = ShipB?.Ordinance ?? 0f;
            FightSeconds = 0f;
            FightOver = false;
            ReportShown = false;
            PinShips();
        }

        // S3: Rematch from the battle report — same pairing, fresh ships, no reload
        public void Rematch()
        {
            if (ShipA != null && ShipA.Active) ShipA.QueueTotalRemoval();
            if (ShipB != null && ShipB.Active) ShipB.QueueTotalRemoval();
            SpawnShips();
            UState.Paused = false;
        }

        // S3: back to the Shipyard (its LoadContent restores the last worked design)
        public void ExitToShipyard()
        {
            ExitScreen();
            ScreenManager.AddScreen(new ShipDesignScreen(HostGame, HostGame.EmpireUI)
                                    { InitialDesign = PlayerDesign });
        }

        BattleSimResultScreen.ShipReport Report(Ship s, string design, float ordStart)
        {
            return new BattleSimResultScreen.ShipReport
            {
                Design        = design,
                Alive         = s != null && s.Active,
                HullPct       = s != null && s.Active ? s.HealthPercent : 0f,
                ShieldPct     = s == null || s.ShieldMax <= 0f ? -1f
                              : s.Active ? s.ShieldPower / s.ShieldMax : 0f,
                OrdnanceUsed  = s != null && s.Active ? (ordStart - s.Ordinance).LowerBound(0) : ordStart,
                OrdnanceStart = ordStart,
                PowerLeft     = s != null && s.Active ? s.PowerCurrent : 0f,
            };
        }

        void ShowReport(bool aborted)
        {
            ReportShown = true;
            UState.Paused = true;
            ScreenManager.AddScreen(new BattleSimResultScreen(this,
                Report(ShipA, PlayerDesign, OrdStartA),
                Report(ShipB, EnemyDesign, OrdStartB),
                FightSeconds, aborted));
        }

        // 45.22/45.24/45.26 field results: the enemy ship kept FTL-fleeing — the AI
        // empire's managers re-task it no matter the initial order, priority or not.
        // S1 brute force: re-pin the attack order every update tick until it sticks.
        void PinShips()
        {
            if (ShipA == null || ShipB == null || !ShipA.Active || !ShipB.Active)
                return;

            // player ship: re-pin only when idle — manual orders stay untouched
            if (ShipA.AI.State == AIState.AwaitingOrders)
            {
                ShipA.AI.OrderAttackSpecificTarget(ShipB);
                ShipA.AI.SetPriorityOrder(true);
            }
            // AI ship: any deviation from attacking the player gets overridden
            if (ShipB.AI.Target != ShipA)
            {
                ShipB.AI.OrderAttackSpecificTarget(ShipA);
                ShipB.AI.SetPriorityOrder(true);
            }
        }

        public override void Update(float fixedDeltaTime)
        {
            PinShips();

            // S3: fight clock + end detection → battle report after the explosion
            if (!FightOver)
            {
                if (!UState.Paused)
                    FightSeconds += fixedDeltaTime;
                if (Spawned && (ShipA == null || !ShipA.Active || ShipB == null || !ShipB.Active))
                {
                    FightOver = true;
                    ReportDelay = 2.5f;
                }
            }
            else if (!ReportShown)
            {
                ReportDelay -= fixedDeltaTime;
                if (ReportDelay <= 0f)
                    ShowReport(aborted: false);
            }

            base.Update(fixedDeltaTime);
        }

        public override bool HandleInput(InputState input)
        {
            // S3: Escape opens the battle report (aborted if both still stand);
            // leaving the arena goes through the report's buttons from here on.
            if (input.Escaped)
            {
                if (!ReportShown)
                    ShowReport(aborted: !FightOver);
                return true;
            }
            return base.HandleInput(input);
        }

        public override void ExitScreen()
        {
            base.ExitScreen();
            if (HostGame != null)
            {
                // rebuild the host's own light rig (we stole it for the arena),
                // and hand the game back PAUSED — the player just returned.
                HostGame.ResetLighting(forceReset: true);
                HostGame.UState.Paused = true;
            }
        }
    }
}
