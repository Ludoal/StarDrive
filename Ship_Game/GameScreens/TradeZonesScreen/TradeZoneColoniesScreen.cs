using Microsoft.Xna.Framework.Graphics;
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
        ScrollList<ColonyPickItem> ColoniesSL;
        UIButton ApplyButton;

        public TradeZoneColoniesScreen(TradeZonesScreen screen, TradeZone zone)
            : base(screen, 520, 560)
        {
            Screen = screen;
            Zone = zone;
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
            TitleText = Localizer.Token(Zone == null ? GameText.TzNewZone : GameText.TzEditColonies);
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            ColoniesSL = Add(new ScrollList<ColonyPickItem>(
                new RectF(inner.X + 20, inner.Y + 20, inner.Width - 40, inner.Height - 90), 28));

            foreach (Planet p in Screen.Player.GetPlanets().Sorted(true, p => p.Name))
                ColoniesSL.AddItem(new ColonyPickItem(this, p));

            ApplyButton = ButtonMedium(inner.X + 20, inner.Bottom - 40, GameText.TzApply, OnApplyClicked);
            ApplyButton.Text = Localizer.Token(GameText.TzApply);
        }

        public bool IsChosen(Planet p) => Chosen.Contains(p);

        public void SetChosen(Planet p, bool on)
        {
            if (on) Chosen.AddUnique(p);
            else    Chosen.Remove(p);
        }

        void OnApplyClicked(UIButton b)
        {
            // an empty pick on an existing zone dissolves it: a zone with no colony would read as
            // "everywhere" downstream, so it is never a state we keep
            Screen.ApplyColonies(Zone, Chosen);
            GameAudio.AcceptClick();
            ExitScreen();
        }

        // one colony = one ticked line
        public sealed class ColonyPickItem : ScrollListItem<ColonyPickItem>
        {
            readonly TradeZoneColoniesScreen Picker;
            readonly Planet Colony;
            UICheckBox Box;

            public ColonyPickItem(TradeZoneColoniesScreen picker, Planet colony)
            {
                Picker = picker;
                Colony = colony;
            }

            public override void PerformLayout()
            {
                RemoveAll();
                Box = Add(new UICheckBox(X + 4, Y + 2,
                                         () => Picker.IsChosen(Colony),
                                         on => Picker.SetChosen(Colony, on),
                                         Fonts.Arial12Bold, Colony.Name, GameText.TzColonyPickTip));
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
