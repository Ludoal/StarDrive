using System;
using System.Linq;
using System.Text;
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
        // S5: click stages opponents into the group roster (S5.1: the only gesture)
        readonly Array<string> Roster = new();
        string[] ChosenGroup;
        readonly UIButton FightBtn, ClearBtn;
        readonly ScrollList<RosterItem> RosterSL;
        const int RosterCap = 10; // readability cap (field preference, 45.65 bench)
        int LaunchCountdown = -1; // >= 0: veil is up, counting rendered frames before Launch

        public BattleSimEnemyPicker(UniverseScreen host, string playerDesign) : base(host, toPause: host)
        {
            Host = host;
            PlayerDesign = playerDesign;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0f; // the launch path must leave no half-faded frames under the veil

            var rect = new Rectangle(ScreenWidth / 2 - 260, ScreenHeight / 2 - 330, 520, 660); // S5: +60 for the roster floor
            Window = Add(new Menu2(rect));
            Add(new CloseButton(rect.Right - 40, rect.Y + 20));

            RectF slRect = new(rect.X + 20, rect.Y + 60, rect.Width - 40, rect.Height - 270);
            DesignSL = Add(new ScrollList<PickerItem>(slRect, 32));
            DesignSL.EnableItemHighlight = true;
            DesignSL.OnClick = OnPicked; // S5.1: click = stage/unstage; the button launches

            // S5: the group roster — grouped "design xN" rows, click removes one
            RectF rosterRect = new(rect.X + 20, rect.Y + rect.Height - 192, rect.Width - 40, 120);
            RosterSL = Add(new ScrollList<RosterItem>(rosterRect, 24));
            RosterSL.EnableItemHighlight = true;
            RosterSL.OnClick = OnRosterClicked;

            // S5: group controls — hidden until the roster has a first opponent
            FightBtn = Add(new UIButton(ButtonStyle.Default, new Vector2(rect.X + 20, rect.Bottom - 48), "Fight group"));
            FightBtn.OnClick = b => LaunchGroup();
            ClearBtn = Add(new UIButton(ButtonStyle.Default, new Vector2(rect.Right - 220, rect.Bottom - 48), "Clear"));
            ClearBtn.OnClick = b => { GameAudio.AcceptClick(); Roster.Clear(); RefreshRoster(); };

            PopulateList();
        }

        void PopulateList()
        {
            DesignSL.Reset();
            DesignSL.AddItem(new PickerItem(PlayerDesign, "mirror match", isMirror: true, picker: this));

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
                foreach (Ship s in pair.Value.OrderByDescending(x => x.DesignRole)
                                             .ThenByDescending(x => x.BaseStrength)) // class blocks, heaviest first
                    header.AddSubItem(new PickerItem(s.Name,
                        Localizer.GetRole(s.DesignRole, Host.Player) + " \u00b7 str " + s.BaseStrength.String(0), isMirror: false, picker: this));
            }
        }

        void StageIntoGroup(PickerItem item)
        {
            if (Roster.Count >= RosterCap)
            {
                GameAudio.NegativeClick();
                return;
            }
            GameAudio.AcceptClick();
            Roster.Add(item.DesignName);
            RefreshRoster();
        }

        public bool IsStaged(string design) => Roster.Contains(design);

        // rebuild the grouped view (insertion order, "xN" per design)
        void RefreshRoster()
        {
            RosterSL.Reset();
            var counts = new Map<string, int>();
            var order = new Array<string>();
            foreach (string d in Roster)
            {
                if (counts.TryGetValue(d, out int c)) counts[d] = c + 1;
                else { counts[d] = 1; order.Add(d); }
            }
            foreach (string d in order)
                RosterSL.AddItem(new RosterItem(d, counts[d]));
        }

        // click a roster row: remove ONE instance of that design
        void OnRosterClicked(RosterItem item)
        {
            if (LaunchCountdown >= 0 || item == null)
                return;
            GameAudio.AcceptClick();
            Roster.Remove(item.Design);
            RefreshRoster();
        }

        // S5.1 (field feedback): ONE gesture for everything — click stages a design,
        // clicking it again unstages it; Shift+click stacks another copy of a design
        // already in the group. Launching is the button's job, single opponent included.
        void OnPicked(PickerItem item)
        {
            if (LaunchCountdown >= 0 || item.DesignName == null) // headers don't fight
                return;
            if (!Input.IsShiftKeyDown && Roster.Contains(item.DesignName))
            {
                GameAudio.AcceptClick();
                Roster.Remove(item.DesignName);
                RefreshRoster();
                return;
            }
            StageIntoGroup(item);
        }

        void LaunchGroup()
        {
            if (LaunchCountdown >= 0 || Roster.IsEmpty)
                return;
            GameAudio.AcceptClick();
            ChosenGroup = Roster.ToArray();
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
                BattleSimUniverse.Launch(Host, PlayerDesign, ChosenGroup);
                return;
            }
            FightBtn.Visible = ClearBtn.Visible = RosterSL.Visible = Roster.NotEmpty; // S5
            if (Roster.NotEmpty)
                FightBtn.Text = Roster.Count == 1 ? "Fight" : "Fight group (" + Roster.Count + ")";
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
            string title = "Pick your opponents - click stages, click again unstages";
            batch.DrawString(Fonts.Arial14Bold, title,
                new Vector2(Window.Menu.CenterTextX(title, Fonts.Arial14Bold), Window.Menu.Y + 22), Color.Wheat);

            if (Roster.NotEmpty)
            {
                string grp = "Group roster - click a line to remove one";
                batch.DrawString(Fonts.Arial12Bold, grp,
                    new Vector2(Window.Menu.X + 20, Window.Menu.Bottom - 206), Color.Wheat);
            }
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

        // S5: one grouped roster line - "design xN"
        public sealed class RosterItem : ScrollListItem<RosterItem>
        {
            public readonly string Design;
            readonly int Count;

            public RosterItem(string design, int count)
            {
                Design = design;
                Count = count;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                base.Draw(batch, elapsed);
                batch.DrawString(Fonts.Arial12Bold, Design, new Vector2(X + 8, CenterY - 6), Color.White);
                if (Count > 1)
                    batch.DrawString(Fonts.Arial12Bold, "x" + Count, new Vector2(X + 300, CenterY - 6), Color.Wheat);
            }
        }

        public sealed class PickerItem : ScrollListItem<PickerItem>
        {
            public readonly string DesignName; // null on role headers
            readonly string Detail;
            readonly bool IsMirror;
            readonly BattleSimEnemyPicker Picker; // S5.1: staged designs light up

            public PickerItem(string headerText) : base(headerText) { }

            public PickerItem(string name, string detail, bool isMirror, BattleSimEnemyPicker picker)
            {
                DesignName = name;
                Detail = detail;
                IsMirror = isMirror;
                Picker = picker;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                base.Draw(batch, elapsed);
                if (DesignName == null)
                    return; // role header: base draws it
                bool staged = Picker != null && Picker.IsStaged(DesignName);
                var color = staged ? Color.LightGreen : IsMirror ? Color.Wheat : Color.White;
                batch.DrawString(Fonts.Arial12Bold, DesignName, new Vector2(X + 8, CenterY - 6), color);
                batch.DrawString(Fonts.Arial12Bold, Detail, new Vector2(X + 280, CenterY - 6), Color.Gray);
            }
        }
    }
}
