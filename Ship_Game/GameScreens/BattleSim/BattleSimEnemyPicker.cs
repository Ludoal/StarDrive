using System;
using System.Linq;
using Ship_Game.ExtensionMethods;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (battle simulator S2): choose the enemy design before entering
    // the arena. Doubles as the LOADING VEIL: the arena's LoadContent freezes the
    // render loop for a second or two, so whatever frame was presented last stays
    // on screen — we make sure it is our black "Preparing arena..." frame instead
    // of a flash of the paused game map.
    public sealed class BattleSimEnemyPicker : GameScreen
    {
        readonly UniverseScreen Host;
        readonly string PlayerDesign;
        readonly Menu2 Window;
        readonly ScrollList<PickerItem> DesignSL;
        string ChosenEnemy;
        int LaunchCountdown = -1; // >= 0: veil is up, counting rendered frames before Launch

        public BattleSimEnemyPicker(UniverseScreen host, string playerDesign) : base(host, toPause: host)
        {
            Host = host;
            PlayerDesign = playerDesign;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0f; // the launch path must leave no half-faded frames under the veil

            var rect = new Rectangle(ScreenWidth / 2 - 260, ScreenHeight / 2 - 300, 520, 600);
            Window = Add(new Menu2(rect));
            Add(new CloseButton(rect.Right - 40, rect.Y + 20));

            RectF slRect = new(rect.X + 20, rect.Y + 60, rect.Width - 40, rect.Height - 80);
            DesignSL = Add(new ScrollList<PickerItem>(slRect, 32));
            DesignSL.EnableItemHighlight = true;
            DesignSL.OnDoubleClick = OnPicked;

            PopulateList();
        }

        void PopulateList()
        {
            DesignSL.Reset();
            DesignSL.AddItem(new PickerItem(PlayerDesign, "mirror match", isMirror: true));

            Ship[] ships = ResourceManager.Ships.Ships
                .Filter(s => s.BaseStrength > 0 && s.Name != PlayerDesign)
                .OrderByDescending(s => s.BaseStrength)
                .ThenBy(s => s.Name).ToArr();

            // grouped by race with collapsible headers; the class rides on each line
            var byRace = new Map<string, Array<Ship>>();
            foreach (Ship s in ships)
            {
                string race = s.ShipData.ShipStyle.IsEmpty() ? "Misc" : s.ShipData.ShipStyle;
                if (!byRace.TryGetValue(race, out Array<Ship> group))
                    byRace[race] = group = new Array<Ship>();
                group.Add(s);
            }
            var pairs = byRace.ToArray();
            Array.Sort(keys: byRace.Keys.ToArr(), pairs);
            foreach (var pair in pairs)
            {
                PickerItem header = DesignSL.AddItem(new PickerItem(pair.Key));
                foreach (Ship s in pair.Value) // already strength-sorted from the base list
                    header.AddSubItem(new PickerItem(s.Name,
                        Localizer.GetRole(s.DesignRole, Host.Player) + " \u00b7 str " + s.BaseStrength.String(0), isMirror: false));
            }
        }

        void OnPicked(PickerItem item)
        {
            if (LaunchCountdown >= 0 || item.DesignName == null) // headers don't fight
                return;
            GameAudio.AcceptClick();
            ChosenEnemy = item.DesignName;
            LaunchCountdown = 2; // let the veil reach the screen before the heavy load
        }

        public override void Update(float fixedDeltaTime)
        {
            if (LaunchCountdown > 0)
                LaunchCountdown--;
            else if (LaunchCountdown == 0)
            {
                LaunchCountdown = -2;
                // exit first, launch second — same order as the Shipyard button:
                // our toPause resume fires now, Launch re-pauses the host right after.
                ExitScreen();
                BattleSimUniverse.Launch(Host, PlayerDesign, ChosenEnemy);
                return;
            }
            base.Update(fixedDeltaTime);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (LaunchCountdown >= 0)
            {
                // the veil: this exact frame stays visible while the arena loads
                ScreenManager.FadeBackBufferToBlack(255);
                batch.SafeBegin();
                const string veil = "Preparing arena...";
                batch.DrawString(Fonts.Pirulen16, veil,
                    new Vector2(ScreenCenter.X - Fonts.Pirulen16.MeasureString(veil).X / 2f, ScreenCenter.Y),
                    Color.White);
                batch.SafeEnd();
                return;
            }

            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            base.Draw(batch, elapsed);
            string title = "Choose your opponent";
            batch.DrawString(Fonts.Arial14Bold, title,
                new Vector2(Window.Menu.CenterTextX(title), Window.Menu.Y + 22), Color.Wheat);
            batch.SafeEnd();
        }

        public override bool HandleInput(InputState input)
        {
            if (LaunchCountdown >= 0)
                return true; // veil is up: input is over
            if (input.Escaped || input.RightMouseClick)
            {
                ExitScreen();
                return true;
            }
            return base.HandleInput(input);
        }

        public sealed class PickerItem : ScrollListItem<PickerItem>
        {
            public readonly string DesignName; // null on role headers
            readonly string Detail;
            readonly bool IsMirror;

            public PickerItem(string headerText) : base(headerText) { }

            public PickerItem(string name, string detail, bool isMirror)
            {
                DesignName = name;
                Detail = detail;
                IsMirror = isMirror;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                base.Draw(batch, elapsed);
                if (DesignName == null)
                    return; // role header: base draws it
                var color = IsMirror ? Color.Wheat : Color.White;
                batch.DrawString(Fonts.Arial12Bold, DesignName, new Vector2(X + 8, CenterY - 6), color);
                batch.DrawString(Fonts.Arial12Bold, Detail, new Vector2(X + 280, CenterY - 6), Color.Gray);
            }
        }
    }
}
