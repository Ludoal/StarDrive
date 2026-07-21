using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.ExtensionMethods;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (battle simulator S3): the battle report. Snapshot-based stats
    // (the engine keeps no cumulative damage counters — a per-damage-type
    // breakdown needs combat hooks and is deferred). Escape or the button
    // returns to the Shipyard; Rematch respawns the same pairing in place.
    public sealed class BattleSimResultScreen : GameScreen
    {
        public struct ShipReport
        {
            public string Design;
            public bool Alive;
            public float HullPct;      // 0..1
            public float ShieldPct;    // 0..1, or -1 when the design has no shields
            public float OrdnanceUsed;
            public float OrdnanceStart;
            public float PowerLeft;
        }

        readonly BattleSimUniverse Sim;
        readonly ShipReport Us, Them;
        readonly string Verdict;
        readonly Color VerdictColor;
        readonly string Duration;
        readonly Menu2 Window;

        public BattleSimResultScreen(BattleSimUniverse sim, in ShipReport us, in ShipReport them,
                                     float fightSeconds, bool aborted) : base(sim, toPause: null)
        {
            Sim = sim;
            Us = us;
            Them = them;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0f;

            if      (aborted)              { Verdict = "Aborted";            VerdictColor = Color.Gray;  }
            else if (us.Alive && !them.Alive) { Verdict = "Victory";         VerdictColor = Color.LightGreen; }
            else if (!us.Alive && them.Alive) { Verdict = "Defeat";          VerdictColor = Color.Red;   }
            else if (!us.Alive)            { Verdict = "Mutual destruction"; VerdictColor = Color.Orange; }
            else                           { Verdict = "Time will tell";     VerdictColor = Color.Gray;  }

            int secs = (int)fightSeconds;
            Duration = (secs / 60) + ":" + (secs % 60).ToString("00");

            var rect = new Rectangle(ScreenWidth / 2 - 320, ScreenHeight / 2 - 210, 640, 420);
            Window = Add(new Menu2(rect));

            ButtonMedium(rect.X + 30, rect.Bottom - 55, "Rematch", b =>
            {
                ExitScreen();
                Sim.Rematch();
            });
            ButtonMedium(rect.Right - 210, rect.Bottom - 55, "Back to Shipyard", b => ToShipyard());
        }

        void ToShipyard()
        {
            ExitScreen();
            Sim.ExitToShipyard();
        }

        void Row(SpriteBatch batch, ref Vector2 c, string label, string us, string them,
                 Color usColor, Color themColor)
        {
            batch.DrawString(Fonts.Arial12Bold, label, new Vector2(c.X, c.Y), Color.LightGray);
            batch.DrawString(Fonts.Arial12Bold, us,    new Vector2(c.X + 220, c.Y), usColor);
            batch.DrawString(Fonts.Arial12Bold, them,  new Vector2(c.X + 420, c.Y), themColor);
            c.Y += Fonts.Arial12Bold.LineSpacing + 6;
        }

        static string Pct(float v) => (v * 100f).String(0) + " %";
        static Color PctColor(float v) => v > 0.66f ? Color.LightGreen : v > 0.33f ? Color.Yellow : Color.Red;

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            base.Draw(batch, elapsed);

            string title = "BATTLE REPORT";
            batch.DrawString(Fonts.Laserian14, title,
                new Vector2(Window.Menu.CenterTextX(title, Fonts.Laserian14), Window.Menu.Y + 24), Color.Wheat);

            string verdictLine = Verdict + "  -  " + Duration;
            batch.DrawString(Fonts.Arial14Bold, verdictLine,
                new Vector2(Window.Menu.CenterTextX(verdictLine, Fonts.Arial14Bold), Window.Menu.Y + 58), VerdictColor);

            var c = new Vector2(Window.Menu.X + 40, Window.Menu.Y + 100);
            Row(batch, ref c, "", "YOU", "ENEMY", Color.Wheat, Color.Wheat);
            Row(batch, ref c, "Design", Us.Design, Them.Design, Color.White, Color.White);
            Row(batch, ref c, "Status", Us.Alive ? "intact" : "destroyed", Them.Alive ? "intact" : "destroyed",
                Us.Alive ? Color.LightGreen : Color.Red, Them.Alive ? Color.LightGreen : Color.Red);
            Row(batch, ref c, "Hull", Pct(Us.HullPct), Pct(Them.HullPct), PctColor(Us.HullPct), PctColor(Them.HullPct));
            Row(batch, ref c, "Shields",
                Us.ShieldPct   < 0f ? "none" : Pct(Us.ShieldPct),
                Them.ShieldPct < 0f ? "none" : Pct(Them.ShieldPct),
                Us.ShieldPct   < 0f ? Color.Gray : PctColor(Us.ShieldPct),
                Them.ShieldPct < 0f ? Color.Gray : PctColor(Them.ShieldPct));
            Row(batch, ref c, "Ordnance used",
                Us.OrdnanceUsed.String(0)   + " / " + Us.OrdnanceStart.String(0),
                Them.OrdnanceUsed.String(0) + " / " + Them.OrdnanceStart.String(0),
                Color.White, Color.White);
            Row(batch, ref c, "Power left", Us.PowerLeft.String(0), Them.PowerLeft.String(0),
                Color.White, Color.White);

            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            if (input.Escaped || input.RightMouseClick)
            {
                ToShipyard();
                return true;
            }
            return base.HandleInput(input);
        }
    }
}
