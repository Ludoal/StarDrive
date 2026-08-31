using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (maintainer, 31 Aug '26): the third door onto a freighter's zone, and the one
    // that replaced the two per-ship editors on the ship panel. A freighter belongs to ONE zone,
    // so this list picks rather than ticks: a row is a choice, and choosing closes the window.
    public sealed class ShipTradeZoneScreen : PopupWindow
    {
        readonly Ship Freighter;
        readonly Empire Owner;
        const float SecGap = 8f;
        ScrollList<ZoneRow> ZonesSL;

        public ShipTradeZoneScreen(GameScreen caller, Ship freighter)
            : base(caller, 380, 420)
        {
            Freighter = freighter;
            Owner = freighter.Loyalty;
            TransitionOnTime = 0.25f;
        }

        public override void LoadContent()
        {
            TitleText = Localizer.Token(GameText.TzShipZoneTitle);
            base.LoadContent();

            Rectangle inner = PopupFrame.ContentArea(Rect);
            float x = inner.X + 12, w = inner.Width - 24;
            var box = Add(new Submenu(new RectF(x, inner.Y + SecGap, w, inner.Height - 2 * SecGap),
                                      GameText.TzColonyZones));
            RectF area = box.ClientArea;
            ZonesSL = Add(new ScrollList<ZoneRow>(
                new RectF(area.X + 8, area.Y + 6, area.W - 16, area.H - 12), 28));

            // "no zone" first, because releasing a hull is as ordinary a choice as assigning it
            ZonesSL.AddItem(new ZoneRow(this, null));
            foreach (TradeZone zone in Owner.TradeZones)
                ZonesSL.AddItem(new ZoneRow(this, zone));
        }

        public bool IsCurrent(TradeZone zone)
            => zone == null ? Freighter.TradeZoneId == 0 : Freighter.TradeZoneId == zone.Id;

        public void Choose(TradeZone zone)
        {
            Owner.AssignFreighterToZone(Freighter, zone);
            GameAudio.AcceptClick();
            ExitScreen();
        }

        public sealed class ZoneRow : ScrollListItem<ZoneRow>
        {
            readonly ShipTradeZoneScreen Picker;
            readonly TradeZone Zone;

            public ZoneRow(ShipTradeZoneScreen picker, TradeZone zone)
            {
                Picker = picker;
                Zone = zone;
            }

            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                base.Draw(batch, elapsed);
                string text = Zone == null ? Localizer.Token(GameText.TzNoZone)
                                           : Zone.Exclusive ? $"{Zone.Name}  ({Localizer.Token(GameText.TzExclusive)})"
                                                            : Zone.Name;
                // the standing choice reads gold, the way a current pick reads everywhere else
                Color color = Picker.IsCurrent(Zone) ? Color.Gold : Color.White;
                batch.DrawString(Fonts.Arial12Bold, text, new Vector2(X + 8, Y + 6), color);
            }

            public override bool HandleInput(InputState input)
            {
                if (input.InGameSelect && HitTest(input.CursorPosition))
                {
                    Picker.Choose(Zone);
                    return true;
                }
                return base.HandleInput(input);
            }
        }
    }
}
