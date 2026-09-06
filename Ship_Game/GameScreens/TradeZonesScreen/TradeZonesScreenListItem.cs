using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input; // InputState
using SDUtils;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.UI; // UITable: the shared table charte

namespace Ship_Game
{
    // one trade zone = one row, on the shared table charte: its name, what it serves, what it
    // asks for, and the two action icons the build queues already speak.
    public sealed class TradeZonesScreenListItem : ScrollListItem<TradeZonesScreenListItem>
    {
        public readonly TradeZone Zone;
        UIButton EditColonies;
        UIButton DeleteZone;
        readonly TradeZonesScreen Screen;
        readonly Empire Player;

        public TradeZonesScreenListItem(TradeZonesScreen screen, TradeZone zone, Empire player)
        {
            Screen = screen;
            Zone = zone;
            Player = player;
        }

        public override void PerformLayout()
        {
            int y = (int)Y;
            int h = (int)Height;
            RemoveAll();

            UITable.Column[] cols = Screen.Table.Columns;
            Color color = Color.White;

            // an EXCLUSIVE zone wears the padlock, exactly as an exclusive blueprint does on the
            // governor row - one mark for one idea across the two systems (bench 559). The state
            // is shown here; it is still CHANGED in the zone's own dialog, one door per gesture.
            if (Zone.Exclusive)
            {
                int lockX = cols[0].Rect.X + UITable.PadX;
                Add(new UIPanel(new Rectangle(lockX, (int)(Y + Height / 2 - LockSize / 2), LockSize, LockSize),
                                ResourceManager.Texture("NewUI/icon_lock")))
                    .Tooltip = GameText.TzExclusiveTip;
                Label(new Vector2(lockX + LockLane,
                                  Y + Height / 2 - Fonts.Arial12Bold.LineSpacing / 2f),
                      Zone.Name, Fonts.Arial12Bold, color);
            }
            else
            {
                Cell(cols[0], Zone.Name, color);
            }
            Cell(cols[1], Zone.NumColonies.ToString(), color);

            // one label PER colony rather than one joined string: each name is a target the
            // player can click to centre the map on it, the way every other name in the game
            // behaves. What does not fit is elided, and the ellipsis carries the whole list.
            LayOutColonyNames(cols[2].Rect, color);

            Cell(cols[3], Zone.MeasuredNeed.ToString(), color).Tooltip = GameText.TzRequiredTip;
            Cell(cols[4], Zone.ActiveFreighters(Player).ToString(), color).Tooltip = GameText.TzActiveTip;
            // an exclusive zone holding no hull of its own is served by NOBODY - the common pass
            // has let its worlds go, and it has nothing to send in their place. The figure reads
            // red rather than as a quiet nought: the state drives the zone's conduct, so the
            // player has to be able to see it (maintainer feedback).
            bool unserved = Zone.Exclusive && Zone.MemberFreighters(Player).Count == 0;
            Cell(cols[5], TargetAndOwned(Zone, Player), unserved ? Color.Red : color)
                .Tooltip = GameText.TzOwnedTargetTip;
            // greyed on a soft zone: the priority belongs to the exclusive regime, and a soft
            // zone's hulls follow the empire's own order - the word is shown rather than hidden so
            // the column keeps its shape, but it is not a setting there (maintainer feedback)
            Cell(cols[6], PriorityText(Zone), Zone.Exclusive ? color : Color.Gray)
                .Tooltip = GameText.TzPriorityTip;

            EditColonies ??= new UIButton(new UIButton.StyleTextures("NewUI/icon_build_edit_hover1", "NewUI/icon_build_edit_hover2", "NewUI/icon_build_edit_hover2"), Vector2.Zero, "")
            {
                Tooltip = GameText.TzEditColoniesTip,
                OnClick = _ => Screen.EditColonies(Zone),
            };
            DeleteZone ??= new UIButton(new UIButton.StyleTextures("NewUI/icon_queue_delete_hover1", "NewUI/icon_queue_delete_hover2", "NewUI/icon_queue_delete_hover2"), Vector2.Zero, "")
            {
                Tooltip = GameText.TzDeleteZoneTip,
                OnClick = OnDeleteClicked,
                IconTint = Color.Red, // destruction reads red
            };

            SubTexture editTex = ResourceManager.Texture("NewUI/icon_build_edit_hover1");
            SubTexture delTex = ResourceManager.Texture("NewUI/icon_queue_delete_hover1");
            // four slots of equal width, centred in the Actions lane: two arrows that order the
            // zones, then the pencil and the bin. The slot is a constant; the icons centre in it.
            Rectangle actions = cols[cols.Length - 1].Rect;
            const int Slot = 24, Slots = 4;
            int lane = actions.X + (actions.Width - Slot * Slots) / 2;
            int centreY = y + h / 2;
            // ⚠ the arrows are drawn at the row's icon size, not at their texture's own: left to
            // themselves they come out taller than the pencil and the bin beside them.
            // ⚠⚠ and the position they are given is NOT the icon's top: the placer adds a hard
            // fifteen - a centring from the days of 30px rows - and then centres the icon on that
            // by itself. So what it wants here is the row's centre minus those fifteen, and any
            // half-icon of our own would be counted twice (bench 578). The pencil and the bin next
            // to them take no such detour: they are buttons placed on the real centre.
            const int ArrowIcon = 17, PlacerCentre = 15;
            AddUp(new Vector2(lane + 4 - X, centreY - PlacerCentre - Y), GameText.TzMoveUpTip,
                  () => Screen.MoveZone(Zone, up: true), ArrowIcon);
            AddDown(new Vector2(lane + Slot + 4 - X, centreY - PlacerCentre - Y), GameText.TzMoveDownTip,
                    () => Screen.MoveZone(Zone, up: false), ArrowIcon);
            int editX = lane + 2 * Slot + (Slot - editTex.Width) / 2;
            int delX = lane + 3 * Slot + (Slot - delTex.Width) / 2;
            EditColonies.Rect = new Rectangle(editX, centreY - editTex.Height / 2, editTex.Width, editTex.Height);
            DeleteZone.Rect = new Rectangle(delX, centreY - delTex.Height / 2, delTex.Width, delTex.Height);

            base.PerformLayout();
        }

        // ⚠ ONE source for the padlock's footprint: the row draws from it and the page WIDENS the
        // name column by it. Measured on the names alone, the column was too narrow by exactly
        // this much and long names ran under their neighbour (bench 561).
        public const int LockSize = 16, LockGap = 6, LockLane = LockSize + LockGap;

        // Owned over target, in one cell. A soft zone owns nothing by construction, so it shows
        // NOTHING rather than a "0 / 5" that would read as a failure to reach a target it never
        // had. ⚠ public and static: the column measurer has to size on the very string the row
        // draws, not on a bare count.
        public static string TargetAndOwned(TradeZone zone, Empire player)
        {
            string target = zone.Quota > 0 ? zone.Quota.ToString()
                                           : Localizer.Token(GameText.PolFreighterRefitAuto);
            // a soft zone owns nothing, so it shows the setting alone rather than a bracket
            // holding a nought that would read as a target it failed to reach
            return zone.Exclusive ? $"{target} ({zone.MemberFreighters(player).Count})" : target;
        }

        public static string PriorityText(TradeZone zone) => zone.Priority switch
        {
            CargoPriority.ProductionFirst => Localizer.Token(GameText.FreighterPriorityProductionFirst),
            CargoPriority.ColonistsFirst  => Localizer.Token(GameText.FreighterPriorityColonistsFirst),
            _                             => Localizer.Token(GameText.FreighterPriorityAuto),
        };

        // the clickable name lanes, rebuilt with the row
        readonly Array<(Rectangle Rect, Planet Colony)> NameHits = new();

        void LayOutColonyNames(Rectangle cell, Color color)
        {
            NameHits.Clear();
            Graphics.Font font = Fonts.Arial12Bold;
            int x = cell.X + UITable.PadX;
            int right = cell.X + cell.Width - UITable.PadX;
            int y = (int)(Y + Height / 2 - font.LineSpacing / 2f);
            string all = Screen.ColonyNames(Zone);

            for (int i = 0; i < Zone.Colonies.Count; ++i)
            {
                Planet colony = Screen.UState.GetPlanet(Zone.Colonies[i]);
                if (colony == null)
                    continue;

                string text = i == 0 ? colony.Name : ", " + colony.Name;
                int w = (int)font.TextWidth(text);
                if (x + w > right) // no room left: say so once and stop
                {
                    Label(new Vector2(x, y), "...", font, color).Tooltip = all;
                    return;
                }

                UILabel label = Label(new Vector2(x, y), text, font, color);
                label.Tooltip = GameText.TzPanToColonyTip;
                NameHits.Add((new Rectangle(x, y, w, font.LineSpacing), colony));
                x += w;
            }
        }

        UILabel Cell(UITable.Column c, string text, Color color)
        {
            return Label(UITable.CellPos(Fonts.Arial12Bold, c.Rect, Y, Height, text, c.Align),
                         text, Fonts.Arial12Bold, color);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            EditColonies.Draw(batch, elapsed);
            DeleteZone.Draw(batch, elapsed);
        }

        public override bool HandleInput(InputState input)
        {
            if (EditColonies.HandleInput(input))
                return true;
            if (input.LeftMouseClick)
            {
                foreach ((Rectangle rect, Planet colony) in NameHits)
                {
                    if (rect.HitTest(input.CursorPosition))
                    {
                        Screen.PanTo(colony);
                        return true;
                    }
                }
            }
            if (DeleteZone.HandleInput(input))
                return true;
            return base.HandleInput(input);
        }

        void OnDeleteClicked(UIButton b)
        {
            Screen.ScreenManager.AddScreen(new MessageBoxScreen(Screen, Localizer.Token(GameText.TzDeleteZoneConfirm))
            {
                Accepted = () => Screen.DeleteZone(Zone)
            });
        }
    }
}
