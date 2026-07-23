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
        readonly string[] EnemyDesigns; // S5: 1..N opponents
        Empire Them;
        Ship ShipA;
        // S5: one entry per opponent
        class Foe { public string Design; public Ship Ship; public float OrdStart; }
        readonly Array<Foe> Foes = new();
        bool Spawned;
        // S3: fight tracking for the battle report (snapshot-based)
        float FightSeconds;
        float OrdStartA;
        bool FightOver;
        float ReportDelay;   // let the final explosion play before the report
        bool ReportShown;

        BattleSimUniverse(UniverseParams p, float radius, UniverseScreen hostGame,
                          string playerDesign, string[] enemyDesigns) : base(p, radius)
        {
            HostGame = hostGame;
            PlayerDesign = playerDesign;
            EnemyDesigns = enemyDesigns;
            UState.NoEliminationVictory = true;
            UState.CanShowDiplomacyScreen = false;
        }

        public static void Launch(UniverseScreen hostGame, string playerDesign, string enemyDesign)
            => Launch(hostGame, playerDesign, new[] { enemyDesign });

        // S5: group fights — same arena, N opponents
        public static void Launch(UniverseScreen hostGame, string playerDesign, string[] enemyDesigns)
        {
            var p = new UniverseParams();
            p.DebugDisableShipLaunch = true; // no launch sequence: combat-capable immediately

            var sim = new BattleSimUniverse(p, 500_000f, hostGame, playerDesign, enemyDesigns);

            // any two distinct major races will do — the arena is about the ships;
            // Hard difficulty only affects ship AI modifiers, there is no economy here
            var majors = ResourceManager.MajorRaces;
            Empire us   = sim.UState.CreateEmpire(majors[0], isPlayer: true, GameDifficulty.Hard);
            Empire them = sim.UState.CreateEmpire(majors[1], isPlayer: false, GameDifficulty.Hard);
            sim.Them = them;
            Empire.SetRelationsAsKnown(us, them);

            // Player feedback: fresh sim empires had zero unlocked techs, so anything
            // tech-dependent lied — dynamic hangars fell back to the base craft.
            // Full unlock (without empire bonuses: raw designs stay comparable)
            // keeps the arena faithful. Same loop as debug ctrl-F1, minus the audio.
            foreach (Empire e in new[] { us, them })
            {
                foreach (TechEntry t in e.TechEntries)
                    if (!t.Unlocked)
                        t.DebugUnlockFromTechScreen(e, e, bonusUnlock: false);
                // same closing sweep as the debug unlock: without it the buildable-ship
                // list stays stale and dynamic hangars still fall back to the base craft
                e.UpdateShipsWeCanBuild();
                e.UpdateForNewTech();
            }
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
            // S5: opponents form a column on the player's axis; 1600 spacing keeps
            // even capitals clear of each other (cap of 10 -> +/-7200 spread)
            Foes.Clear();
            for (int i = 0; i < EnemyDesigns.Length; ++i)
            {
                float y = (i - (EnemyDesigns.Length - 1) * 0.5f) * 1600f;
                Ship s = Ship.CreateShipAtPoint(UState, EnemyDesigns[i], Them, new Vector2(6000f, y));
                Foes.Add(new Foe { Design = EnemyDesigns[i], Ship = s, OrdStart = s?.Ordinance ?? 0f });
            }
            OrdStartA = ShipA?.Ordinance ?? 0f;
            FightSeconds = 0f;
            FightOver = false;
            ReportShown = false;
            PinShips();
        }

        bool AllFoesDown()
        {
            foreach (Foe f in Foes)
                if (f.Ship != null && f.Ship.Active)
                    return false;
            return true;
        }

        // S3: Rematch from the battle report — same pairing, fresh ships, no reload
        public void Rematch()
        {
            if (ShipA != null && ShipA.Active) ShipA.QueueTotalRemoval();
            foreach (Foe f in Foes)
                if (f.Ship != null && f.Ship.Active) f.Ship.QueueTotalRemoval();
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
                FoesReport(),
                FightSeconds, aborted));
        }

        // S5: one opponent keeps the classic report; a group gets an aggregate line
        // (per-foe rows are the next step)
        BattleSimResultScreen.ShipReport FoesReport()
        {
            if (Foes.Count == 1)
                return Report(Foes[0].Ship, Foes[0].Design, Foes[0].OrdStart);

            int alive = 0, shielded = 0;
            float hull = 0f, shieldSum = 0f, ordStart = 0f, ordLeft = 0f, power = 0f;
            foreach (Foe f in Foes)
            {
                Ship s = f.Ship;
                ordStart += f.OrdStart;
                if (s != null && s.Active)
                {
                    ++alive;
                    hull += s.HealthPercent;
                    if (s.ShieldMax > 0f) { ++shielded; shieldSum += s.ShieldPower / s.ShieldMax; }
                    ordLeft += s.Ordinance;
                    power += s.PowerCurrent;
                }
            }
            return new BattleSimResultScreen.ShipReport
            {
                Design        = Foes.Count + " ships (" + alive + " left)",
                Alive         = alive > 0,
                HullPct       = hull / Foes.Count,
                ShieldPct     = shielded == 0 ? -1f : shieldSum / shielded,
                OrdnanceUsed  = (ordStart - ordLeft).LowerBound(0),
                OrdnanceStart = ordStart,
                PowerLeft     = power,
            };
        }

        // 45.22/45.24/45.26 field results: the enemy ship kept FTL-fleeing — the AI
        // empire's managers re-task it no matter the initial order, priority or not.
        // S1 brute force: re-pin the attack order every update tick until it sticks.
        void PinShips()
        {
            if (ShipA == null || !ShipA.Active)
                return;

            // S5: the player's pin target is the nearest living opponent
            Ship nearest = null;
            float bestD = float.MaxValue;
            foreach (Foe f in Foes)
            {
                Ship s = f.Ship;
                if (s == null || !s.Active) continue;
                float d = s.Position.SqDist(ShipA.Position);
                if (d < bestD) { bestD = d; nearest = s; }
            }
            if (nearest == null)
                return;

            // player ship: re-pin only when idle — manual orders stay untouched
            if (ShipA.AI.State == AIState.AwaitingOrders)
            {
                ShipA.AI.OrderAttackSpecificTarget(nearest);
                ShipA.AI.SetPriorityOrder(true);
            }
            // AI ships: any deviation from attacking the player gets overridden
            foreach (Foe f in Foes)
            {
                Ship s = f.Ship;
                if (s == null || !s.Active) continue;
                if (s.AI.Target != ShipA)
                {
                    s.AI.OrderAttackSpecificTarget(ShipA);
                    s.AI.SetPriorityOrder(true);
                }
                // Priority holds only for the approach: once in weapons contact it is
                // released so the combat loop honors the ship's stance (artillery keeps
                // its range, hold holds...) — ships stop stacking on each other.
                if (s.InCombat && s.AI.HasPriorityOrder)
                    s.AI.SetPriorityOrder(false);
            }
            if (ShipA.InCombat && ShipA.AI.HasPriorityOrder)
                ShipA.AI.SetPriorityOrder(false);
        }

        public override void Update(float fixedDeltaTime)
        {
            PinShips();

            // S3: fight clock + end detection → battle report after the explosion
            if (!FightOver)
            {
                if (!UState.Paused)
                    FightSeconds += fixedDeltaTime;
                if (Spawned && (ShipA == null || !ShipA.Active || AllFoesDown()))
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
