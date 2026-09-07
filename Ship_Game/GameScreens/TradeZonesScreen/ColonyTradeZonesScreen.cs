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
    // Ludoal fork (maintainer feedback): the MIRROR of TradeZoneColoniesScreen. That one asks a
    // zone which colonies it serves; this one asks a colony which zones serve it - the same
    // membership, read from the other end. A newly founded world is reached from its own screen
    // instead of a trip to the Trade page, which is the whole point of the shortcut.
    public sealed class ColonyTradeZonesScreen : PopupWindow
    {
        readonly Planet Colony;
        readonly Empire Owner;
        readonly Array<TradeZone> Chosen = new();
        // the list takes what the Apply line leaves - the one thing that should stretch
        const float SecGap = 8f, ApplyLineH = 40f;
        ScrollList<ZonePickItem> ZonesSL;

        public ColonyTradeZonesScreen(GameScreen caller, Planet colony)
            : base(caller, 420, 480)
        {
            Colony = colony;
            Owner = colony.Owner;
            TransitionOnTime = 0.25f;
            foreach (TradeZone zone in Owner.TradeZones)
                if (zone.Serves(colony))
                    Chosen.Add(zone);
        }

        public override void LoadContent()
        {
            TitleText = Localizer.Token(GameText.TzColonyZonesTitle);
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            float x = inner.X + 12, w = inner.Width - 24;
            float listH = inner.Height - ApplyLineH - 3 * SecGap;
            float listY = inner.Y + SecGap;

            var zoneBox = Add(new Submenu(new RectF(x, listY, w, listH), GameText.TzColonyZones));
            RectF zoneArea = zoneBox.ClientArea;
            ZonesSL = Add(new ScrollList<ZonePickItem>(
                new RectF(zoneArea.X + 8, zoneArea.Y + 6, zoneArea.W - 16, zoneArea.H - 12), 28));

            // the list order IS the priority, so the zones are offered in that order rather than
            // sorted by name: what the player sees here is what the dispatch will read
            foreach (TradeZone zone in Owner.TradeZones)
                ZonesSL.AddItem(new ZonePickItem(this, zone));

            // an empire with no zone yet gets a sentence instead of an empty frame: the control
            // exists, and a blank list would read as a fault rather than as a starting point
            if (Owner.TradeZones.IsEmpty)
            {
                // WRAPPED to the frame it sits in (maintainer bench 554): a sentence handed to a
                // label is drawn on one line and runs straight out of the box - the font folds it,
                // and it folds against the frame's own width rather than a number typed here.
                string hint = Fonts.Arial12.ParseText(Localizer.Token(GameText.TzNoZonesHint), zoneArea.W - 24);
                Add(new UILabel(new Vector2(zoneArea.X + 12, zoneArea.Y + 12), hint, Fonts.Arial12, Color.Gray));
            }

            ButtonMedium(x, inner.Bottom - ApplyLineH, GameText.TzApply, OnApplyClicked);
            // ★ the second door onto the creation form, opened from the world it will serve: the
            // colony arrives ticked and locked, and the name is proposed after it. A player who
            // has just founded a world should not need a trip to the Trade page to give it a zone.
            ButtonMedium(x + w - 150, inner.Bottom - ApplyLineH, GameText.TzNewZone, OnNewZoneClicked);
        }

        void OnNewZoneClicked(UIButton b)
        {
            // no Trade page behind this one, so the form writes through the empire and this
            // window closes: reopening it is how the player sees the new zone in the list.
            ScreenManager.AddScreen(new TradeZoneColoniesScreen(this, null, Owner, null, Colony));
            ExitScreen();
        }

        public bool IsChosen(TradeZone zone) => Chosen.Contains(zone);

        public void SetChosen(TradeZone zone, bool on)
        {
            if (on) Chosen.AddUnique(zone);
            else    Chosen.Remove(zone);
        }

        void OnApplyClicked(UIButton b)
        {
            // through the one writer of a zone's edit, so an emptied zone is dissolved here and
            // not a turn later; the zone's other members - station bodies included - are kept
            foreach (TradeZone zone in Owner.TradeZones.ToArr())
            {
                if (IsChosen(zone) == zone.Serves(Colony))
                    continue;
                var chosen = new Array<Planet>();
                foreach (int id in zone.Colonies)
                {
                    Planet p = Owner.Universe.GetPlanet(id);
                    if (p != null && p != Colony)
                        chosen.Add(p);
                }
                if (IsChosen(zone))
                    chosen.Add(Colony);
                Owner.ApplyZoneEdit(zone, chosen, zone.Quota, zone.Name, zone.Exclusive, zone.Priority);
            }

            GameAudio.EchoAffirmative();
            ExitScreen();
        }

        // one zone = one ticked line, with the size of the zone as its second word
        public sealed class ZonePickItem : ScrollListItem<ZonePickItem>
        {
            readonly ColonyTradeZonesScreen Picker;
            readonly TradeZone Zone;
            UICheckBox Box;

            public ZonePickItem(ColonyTradeZonesScreen picker, TradeZone zone)
            {
                Picker = picker;
                Zone = zone;
            }

            public override void PerformLayout()
            {
                RemoveAll();
                Box = Add(new UICheckBox(X + 4, Y + 2,
                                         () => Picker.IsChosen(Zone),
                                         on => Picker.SetChosen(Zone, on),
                                         Fonts.Arial12Bold, Zone.Name, GameText.TzColonyPickTip));
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
