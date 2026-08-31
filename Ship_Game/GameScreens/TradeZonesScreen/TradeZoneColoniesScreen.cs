using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork: a zone is a LIST OF COLONIES, so it is composed by ticking colonies - never by
    // drawing on the map. Opened to create one (zone: null) or to edit an existing one.
    public sealed class TradeZoneColoniesScreen : PopupWindow
    {
        readonly TradeZonesScreen Screen;
        readonly TradeZone Zone; // null while creating
        readonly Array<Planet> Chosen = new();
        int Quota; // held locally: while creating, there is no zone yet to write it on
        bool Exclusive;
        CargoPriority Priority;
        UITextEntry NameEntry;
        UILabel NameTakenLabel;
        // the dialog's three sections own their heights; the colony list takes what is left,
        // which is the ONE thing that should stretch when the window does
        // the settings box grew by two rows - the regime and its one lever - so its height says
        // so here rather than being discovered by the list that closes on it
        const float SecGap = 8f, NameSecH = 62f, SettingsSecH = 142f, ApplyLineH = 40f;
        ScrollList<ColonyPickItem> ColoniesSL;
        DropOptions<CargoPriority> PriorityList;
        UILabel PriorityLabel;
        UIButton ApplyButton;

        public TradeZoneColoniesScreen(TradeZonesScreen screen, TradeZone zone)
            : base(screen, 520, 640)
        {
            Screen = screen;
            Zone = zone;
            Quota = zone?.Quota ?? 0;
            Exclusive = zone?.Exclusive ?? false;
            Priority = zone?.Priority ?? CargoPriority.Auto;
            TransitionOnTime = 0.25f;
            if (zone != null)
            {
                foreach (int id in zone.Colonies)
                {
                    Planet p = screen.UState.GetPlanet(id);
                    if (p != null)
                        Chosen.Add(p);
                }
            }
        }

        public override void LoadContent()
        {
            TitleText = Localizer.Token(Zone == null ? GameText.TzNewZone : GameText.TzEditZone);
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            // three sections, each a one-tab frame - the same furniture the Policies page uses.
            // Their heights are constants and the list is sized FROM them, never the reverse.
            float x = inner.X + 12, w = inner.Width - 24;
            float listH = inner.Height - NameSecH - SettingsSecH - ApplyLineH - 4 * SecGap;
            float nameY = inner.Y + SecGap;
            float listY = nameY + NameSecH + SecGap;
            float setY  = listY + listH + SecGap;

            var nameBox = Add(new Submenu(new RectF(x, nameY, w, NameSecH), GameText.TzName));
            RectF nameArea = nameBox.ClientArea;
            NameEntry = Add(new UITextEntry(nameArea.X + 10, nameArea.Y + 6, nameArea.W - 20,
                                            Fonts.Arial12Bold, Zone?.Name ?? ""));
            // no auto-capture: this dialog has a list and a rail besides the name, so a field
            // that grabs the keyboard on hover or on the first keypress steals every other
            // control's input. It takes focus on a CLICK, like every other field in the game.
            NameEntry.MaxCharacters = 40;
            NameEntry.OnTextChanged = OnNameChanged;
            NameTakenLabel = Add(new UILabel(new Vector2(nameArea.X + 10, nameArea.Y + 28),
                                             GameText.TzNameTaken, Fonts.Arial12, Color.Red));
            NameTakenLabel.Visible = false;

            var colBox = Add(new Submenu(new RectF(x, listY, w, listH), GameText.TzNumColonies));
            RectF colArea = colBox.ClientArea;
            ColoniesSL = Add(new ScrollList<ColonyPickItem>(
                new RectF(colArea.X + 8, colArea.Y + 6, colArea.W - 16, colArea.H - 12), 28));

            foreach (Planet p in Screen.Player.GetPlanets().Sorted(true, p => p.Name))
                ColoniesSL.AddItem(new ColonyPickItem(this, p));

            // Ludoal fork (maintainer feedback, Roland Johansen): the bodies our stations stand on
            // are offered after the colonies. They are not colonies and never will be, so they sit
            // in their own run rather than pretending to a sort they do not share.
            foreach (Planet body in Screen.Player.StationBodies().Sorted(true, b => b.Name))
            {
                // named for what STANDS there (maintainer bench 556): a body carrying a rig reads
                // exactly like a colony in a list of colonies, and it is not one
                string kind = Screen.Player.StationKindOn(body);
                ColoniesSL.AddItem(new ColonyPickItem(this, body,
                    kind.NotEmpty() ? $"{body.Name}  ({kind})" : body.Name));
            }

            // the zone's own lever. Nought is not a quantity here: it hands the number back to
            // the measure, so the rail reads Auto at its left stop.
            var setBox = Add(new Submenu(new RectF(x, setY, w, SettingsSecH), GameText.TzSettings));
            RectF setArea = setBox.ClientArea;
            Add(new UILabel(new Vector2(setArea.X + 10, setArea.Y + 6), GameText.TzAssignedFreighters,
                            Fonts.Arial12Bold, Colors.Cream)).Tooltip = GameText.TzAssignedTip;
            var rail = Add(new FloatSlider(SliderStyle.Decimal, new Vector2(setArea.W - 40, 28),
                                           "", 0, 20, Quota)
            {
                Step = 1,
                Tip = GameText.TzAssignedTip,
                TrackYOffset = -5,
                ZeroString = GameText.PolFreighterRefitAuto,
            });
            rail.Pos = new Vector2(setArea.X + 10, setArea.Y + 28);
            rail.OnChange = s => Quota = (int)s.AbsoluteValue;

            // the REGIME, then its one lever. Fixed rows off the box's own top, the same grammar
            // the rail above them uses.
            Add(new UICheckBox(setArea.X + 10, setArea.Y + 58, () => Exclusive, v => Exclusive = v,
                               Fonts.Arial12Bold, GameText.TzExclusive, GameText.TzExclusiveTip));
            // ⚠ the lever belongs to the EXCLUSIVE regime alone: a soft zone borrows from the
            // common pool and its goods follow the empire's own order, so the picker would be a
            // control with nothing at the end of it (bench 561). It appears with the regime.
            //
            // Hidden rather than greyed, and the reason is honest: DropOptions has no greyed
            // state, and giving it one touches every screen in the game that uses a picker.
            PriorityLabel = Add(new UILabel(new Vector2(setArea.X + 10, setArea.Y + 90), GameText.FreighterPriority,
                            Fonts.Arial12Bold, Colors.Cream, GameText.FreighterPriorityTip));
            PriorityList = new DropOptions<CargoPriority>(
                new Vector2(setArea.X + setArea.W - 170, setArea.Y + 88), 160, 18);
            PriorityList.AddOption(GameText.FreighterPriorityAuto, CargoPriority.Auto);
            PriorityList.AddOption(GameText.FreighterPriorityProductionFirst, CargoPriority.ProductionFirst);
            PriorityList.AddOption(GameText.FreighterPriorityColonistsFirst, CargoPriority.ColonistsFirst);
            // ⚠ Trade First is absent BY DESIGN: it lifts the foreign pass at the scale of the
            // empire, and a zone serves no foreign planet. The zone object refuses it too, so the
            // omission here is a reminder rather than the guard itself.
            PriorityList.ActiveValue = Priority;
            PriorityList.OnValueChange = v => Priority = v;

            UpdatePriorityVisible();
            ApplyButton = ButtonMedium(x, inner.Bottom - ApplyLineH, GameText.TzApply, OnApplyClicked);
            ApplyButton.Text = Localizer.Token(GameText.TzApply);
            // added LAST so an open list draws over what sits under it, Apply included
            Add(PriorityList);
        }

        // the lever shows with the regime it belongs to, and follows the tick live
        void UpdatePriorityVisible()
        {
            if (PriorityLabel != null) PriorityLabel.Visible = Exclusive;
            if (PriorityList != null)  PriorityList.Visible  = Exclusive;
        }

        public override void Update(float fixedDeltaTime)
        {
            UpdatePriorityVisible();
            base.Update(fixedDeltaTime);
        }

        public bool IsChosen(Planet p) => Chosen.Contains(p);

        public void SetChosen(Planet p, bool on)
        {
            if (on) Chosen.AddUnique(p);
            else    Chosen.Remove(p);
        }

        // a name already worn by another zone is refused while it is typed, not silently
        // corrected at Apply: the player keeps the last word on their own text.
        void OnNameChanged(string newName)
        {
            bool taken = false;
            foreach (TradeZone z in Screen.Player.TradeZones)
                if (z != Zone && z.Name == newName)
                    taken = true;

            NameTakenLabel.Visible = taken;
            if (ApplyButton != null) // the handler is wired before the button exists
                ApplyButton.Enabled = !taken;
        }

        void OnApplyClicked(UIButton b)
        {
            // an empty pick on an existing zone dissolves it: a zone with no colony would read as
            // "everywhere" downstream, so it is never a state we keep
            Screen.ApplyColonies(Zone, Chosen, Quota, NameEntry.Text, Exclusive, Priority);
            GameAudio.AcceptClick();
            ExitScreen();
        }

        // one colony = one ticked line
        public sealed class ColonyPickItem : ScrollListItem<ColonyPickItem>
        {
            readonly TradeZoneColoniesScreen Picker;
            readonly Planet Colony;
            UICheckBox Box;

            // ⚠ NOT "Label": UIElementContainer up the chain has a Label() of its own, and
            // CS0108 is an error in this repo (bench 556, the sweep I owed my own rule)
            readonly string Caption;

            public ColonyPickItem(TradeZoneColoniesScreen picker, Planet colony, string caption = null)
            {
                Picker = picker;
                Colony = colony;
                Caption = caption ?? colony.Name;
            }

            public override void PerformLayout()
            {
                RemoveAll();
                Box = Add(new UICheckBox(X + 4, Y + 2,
                                         () => Picker.IsChosen(Colony),
                                         on => Picker.SetChosen(Colony, on),
                                         Fonts.Arial12Bold, Caption, GameText.TzColonyPickTip));
                base.PerformLayout();
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                base.Draw(batch, elapsed);
                Box?.Draw(batch, elapsed);
            }

            public override bool HandleInput(InputState input)
            {
                if (Box != null && Box.HandleInput(input))
                    return true;
                return base.HandleInput(input);
            }
        }
    }
}
