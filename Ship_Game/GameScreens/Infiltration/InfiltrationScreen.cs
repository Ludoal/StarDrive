using System;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.GameScreens.Espionage;
using Ship_Game.GameScreens.EspionageNew;
using Color = Microsoft.Xna.Framework.Color;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game.GameScreens
{
    // Ludoal fork (player design, same principle as the Diplomacy dashboard): one
    // COLUMN per major empire. Header = the EmpireButton (portrait + per-empire
    // infiltration weight slider; the player's own column carries the defense and
    // budget sliders), then the five infiltration levels stacked as sections with
    // variable geometry: reached levels list their unlocks, the level being
    // infiltrated shows progress and status, levels beyond show a dim band only.
    public sealed class InfiltrationScreen : GameScreen
    {
        public readonly UniverseScreen Universe;
        public Empire SelectedEmpire;
        readonly Empire Player;
        public static readonly Color PanelBackground = new Color(23, 20, 14);

        Menu2 TitleBar;
        Vector2 TitlePos;
        Menu2 DMenu;
        Rectangle LeftRect;

        Array<EmpireColumn> Columns = new();

        Font Font12 = Fonts.Arial12;
        Font Font12Bold = Fonts.Arial12Bold;

        const int HeaderH = 280;   // EmpireButton (148) + its sliders below

        class EmpireColumn
        {
            public Empire E;
            public Rectangle Rect;
        }

        public InfiltrationScreen(UniverseScreen parent) : base(parent, toPause: parent)
        {
            Universe = parent;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            Player = Universe.Player;
            SelectedEmpire = Player;
        }

        public override void LoadContent()
        {
            // Empire-style title bar
            string espTitle = Localizer.Token(GameText.EspionageOverview);
            var titleRect = new Rectangle(2, 44, ScreenWidth * 2 / 3, 80);
            TitleBar = new Menu2(titleRect);
            TitlePos = new Vector2(titleRect.X + titleRect.Width / 2 - Fonts.Laserian14.MeasureString(espTitle).X / 2f,
                                   titleRect.Y + titleRect.Height / 2 - Fonts.Laserian14.LineSpacing / 2);

            LeftRect = new Rectangle(2, titleRect.Bottom + 5, ScreenWidth - 10, ScreenHeight - titleRect.Bottom - 7);
            DMenu = new Menu2(LeftRect);
            CloseButton(LeftRect.Right - 40, LeftRect.Y + 20);

            Empire[] majors = Universe.UState.ActiveMajorEmpires;
            int n = majors.Length.LowerBound(1);
            int colW = ((LeftRect.Width - 40) / n).UpperBound(230);
            int totalW = colW * n;
            int x0 = LeftRect.X + (LeftRect.Width - totalW) / 2;

            for (int i = 0; i < majors.Length; ++i)
            {
                var col = new Rectangle(x0 + i * colW, LeftRect.Y + 16, colW - 8, LeftRect.Height - 32);
                Columns.Add(new EmpireColumn { E = majors[i], Rect = col });
                // the button carries the portrait and the weight/budget sliders
                var btnRect = new Rectangle(col.X + (col.Width - 134) / 2, col.Y + 6, 134, 148);
                Add(new EmpireButton(this, majors[i], btnRect, OnEmpireSelected));
            }

            GameAudio.MuteRacialMusic();
        }

        void OnEmpireSelected(EmpireButton button)
        {
            if (Universe.Player == button.Empire || Universe.Player.IsKnown(button.Empire))
                RefreshSelectedEmpire(button.Empire);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            TitleBar.Draw(batch, elapsed);
            batch.DrawString(Fonts.Laserian14, Localizer.Token(GameText.EspionageOverview), TitlePos, Colors.Cream);
            DMenu.Draw(batch, elapsed);

            foreach (EmpireColumn col in Columns)
                DrawColumnBody(batch, col);

            base.Draw(batch, elapsed); // empire buttons, sliders, close button
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        void DrawColumnBody(SpriteBatch batch, EmpireColumn c)
        {
            Rectangle col = c.Rect;
            batch.FillRectangle(col, PanelBackground);
            batch.DrawRectangle(col, new Color(60, 54, 40));

            Empire e = c.E;
            bool known = e == Player || Player.IsKnown(e);
            float y = col.Y + HeaderH;

            if (!known)
            {
                batch.DrawString(Font12, "No contact", new Vector2(col.X + 8, y), Color.Gray);
                return;
            }

            if (e == Player)
            {
                SectionBand(batch, col, ref y, "COUNTER-ESPIONAGE");
                batch.DrawString(Font12, Font12.ParseText("Your defense weight and espionage budget are set above.", col.Width - 16), new Vector2(col.X + 8, y), Color.Wheat);
                return;
            }

            if (e.IsDefeated)
            {
                batch.DrawString(Font12, "Defeated", new Vector2(col.X + 8, y), Color.Gray);
                return;
            }

            Ship_Game.Espionage esp = Player.GetEspionage(e);
            for (byte level = 1; level <= Ship_Game.Espionage.MaxLevel; ++level)
                DrawLevelSection(batch, esp, level, col, ref y);
        }

        void SectionBand(SpriteBatch batch, Rectangle col, ref float y, string title, Color? textColor = null)
        {
            var band = new Rectangle(col.X + 1, (int)y, col.Width - 2, 18);
            batch.FillRectangle(band, new Color(54, 46, 24));
            batch.DrawString(Font12Bold, title, new Vector2(band.X + (band.Width - Font12Bold.TextWidth(title)) / 2f, band.Y + 2), textColor ?? Colors.Cream);
            y += 24;
        }

        void Line(SpriteBatch batch, Rectangle col, ref float y, string text, Color color)
        {
            batch.DrawString(Font12, text, new Vector2(col.X + 8, y), color);
            y += Font12.LineSpacing + 3;
        }

        // one unlock headline per level for the compact view; details keep living
        // in the game's tooltips/design — first draft, bench will iterate
        static string LevelHeadline(byte level) => level switch
        {
            1 => "Scan their ships",
            2 => "Projectors alert, fleet intel",
            3 => "Homeworld mole, deeper intel",
            4 => "Leech tech",
            _ => "Leech income",
        };

        void DrawLevelSection(SpriteBatch batch, Ship_Game.Espionage esp, byte level, Rectangle col, ref float y)
        {
            bool reached = esp.Level >= level;
            bool isNext = esp.Level == level - 1;

            if (reached)
            {
                bool active = esp.LimitLevel >= level;
                SectionBand(batch, col, ref y, $"LEVEL {level}", active ? Color.LightGreen : Color.Gray);
                Line(batch, col, ref y, Truncate(LevelHeadline(level), col.Width - 16), active ? Color.LightGreen : Color.Gray);
                if (!active)
                    Line(batch, col, ref y, "Paused (limit)", Color.Gray);
                y += 3;
                return;
            }

            if (isNext)
            {
                SectionBand(batch, col, ref y, $"LEVEL {level}");
                Line(batch, col, ref y, Truncate(LevelHeadline(level), col.Width - 16), Color.Wheat);

                // manual progress bar: fill + numbers
                float max = esp.LevelCost(level);
                float cur = esp.LevelProgress.UpperBound(max);
                var barRect = new Rectangle(col.X + 8, (int)y, col.Width - 16, 12);
                batch.FillRectangle(barRect, new Color(10, 10, 10));
                if (max > 0f && cur > 0f)
                    batch.FillRectangle(new Rectangle(barRect.X + 1, barRect.Y + 1, (int)((barRect.Width - 2) * (cur / max)), 10), new Color(30, 120, 30));
                batch.DrawRectangle(barRect, new Color(60, 54, 40));
                y += 16;

                float pointPerTurn = esp.GetProgressToIncrease(Player.EspionagePointsPerTurn, Player.CalcTotalEspionageWeight());
                string status; Color sc;
                if (esp.LevelProgress == 0 && pointPerTurn == 0) { status = "Not started"; sc = Color.Gray; }
                else if (pointPerTurn > 0) { status = "In progress"; sc = Color.Yellow; }
                else { status = "Halted"; sc = Color.Red; }
                batch.DrawString(Font12, status, new Vector2(col.X + 8, y), sc);
                string nums = $"{(int)cur}/{(int)max}";
                batch.DrawString(Font12, nums, new Vector2(col.Right - 8 - Font12.TextWidth(nums), y), Color.Wheat);
                y += Font12.LineSpacing + 6;
                return;
            }

            // far levels: a dim band only
            SectionBand(batch, col, ref y, $"LEVEL {level}", new Color(110, 100, 80));
        }

        string Truncate(string text, int width)
        {
            if (Font12.TextWidth(text) <= width)
                return text;
            while (text.Length > 3 && Font12.TextWidth(text + "..") > width)
                text = text.Substring(0, text.Length - 1);
            return text + "..";
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (input.KeyPressed(Keys.E) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (Player.Universe.Debug && !SelectedEmpire.isPlayer && HandleDebugInput(input))
                return true;

            return base.HandleInput(input);
        }

        bool HandleDebugInput(InputState input)
        {
            Keys[] keys = [Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5];
            for (byte i = 0; i < keys.Length; i++)
            {
                if (input.KeyPressed(keys[i]))
                {
                    Player.GetEspionage(SelectedEmpire).SetInfiltrationLevelTo(i);
                    return true;
                }
            }

            return false;
        }

        // kept for EmpireButton callbacks — the dashboard redraws everything live,
        // so these are just bookkeeping now
        public void RefreshSelectedEmpire(Empire selectedEmpire)
        {
            SelectedEmpire = selectedEmpire;
        }

        public void RefreshInfiltrationLevelStatus(Ship_Game.Espionage espionage)
        {
        }
    }
}
