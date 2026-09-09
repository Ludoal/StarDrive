using System;                // Action, for the button handlers
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDGraphics.Input; // InputState
using SDUtils;
using Ship_Game.Ships;  // RoleName
using Ship_Game.UI;     // UITable: the shared table charte
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    // Ludoal fork (wishlist): one colony = one row of the Defense tab. What holds the ground
    // (troops), what holds the orbit (platforms, stations) and what the ground holds by itself
    // (the defensive buildings), so an undefended world shows at a glance rather than after a
    // tour of every colony screen.
    public sealed class DefenseListItem : ScrollListItem<DefenseListItem>
    {
        public readonly Planet P;
        readonly DefenseScreen Screen;
        readonly Empire Player;

        // ⚠ ADDED ONCE, then only moved: PerformLayout runs again on every scroll and sort, and a
        // second Add would stack a duplicate under the first (the same guard the colonies row
        // carries for its own switches).
        UICheckBox AutoTrain;
        FloatSlider GarrisonRail;
        UICheckBox SpaceDef;

        // ★ the three act on the SAME row but not on the same kind of thing: "<" brings a troop
        // that already exists here (the colony screen's Call Troops), "+" orders a hull that does
        // not exist yet. Two signs for two gestures - one sign would read as one gesture
        // (maintainer feedback).
        UIButton CallTroops, AddPlatform, AddStation;
        public const int ActionLane = 22; // kept at each figure column's right end, for its own button

        // the defensive buildings drawn in the last column, with the rect each icon occupies so
        // the row can name it on hover. Rebuilt on layout: a colony gains and loses buildings.
        Building[] Defences = Empty<Building>.Array;
        Rectangle[] DefenceRects = Empty<Rectangle>.Array;

        const int IconSize = 24;
        const int IconGap = 3;
        const int ActionSize = 16;

        public DefenseListItem(DefenseScreen screen, Planet planet, Empire player)
        {
            Screen = screen;
            P = planet;
            Player = player;
        }

        // ★ what makes a building count as defensive, and it is deliberately WIDER than the
        // engine's own military test: that one asks for combat strength, so a shield generator or
        // a bombardment shelter - exactly what one looks for before an attack - would not appear.
        // The three protections a building can carry are all admitted, and the tooltip says which.
        public static bool IsDefensive(Building b)
            => b.CombatStrength > 0 || b.Defense > 0 || b.PlanetaryShieldStrengthAdded > 0;

        public static string DefenceTip(Building b)
        {
            string what = "";
            if (b.CombatStrength > 0)
                what = $"{what}, {Localizer.Token(GameText.CombatStrength)}";
            if (b.Defense > 0)
                what = $"{what}, {Localizer.Token(GameText.Bombard)}";
            if (b.PlanetaryShieldStrengthAdded > 0)
                what = $"{what}, {Localizer.Token(GameText.ShieldPower)}";
            return $"{b.TranslatedName.Text} ({what.TrimStart(',', ' ')})";
        }

        // "12 (+3)" - what stands, and what is on its way. The parenthesis is dropped when
        // nothing is coming: a "(+0)" on every row of the table is noise on the one figure
        // that matters.
        public static string HereAndComing(int here, int coming)
            => coming > 0 ? $"{here} (+{coming})" : here.ToString();

        public static string GarrisonText(Planet p, Empire player)
            => HereAndComing(p.CountEmpireTroops(player), p.NumTroopsInTheWorks);

        public static string PlatformsText(Planet p)
            => HereAndComing(p.NumPlatforms, p.OrbitalsBeingBuilt(RoleName.platform));

        public static string StationsText(Planet p)
            => HereAndComing(p.NumStations, p.OrbitalsBeingBuilt(RoleName.station));

        public override void PerformLayout()
        {
            UITable.Column[] cols = Screen.Table.Columns;
            Color color = Color.White;

            Cell(cols[0], P.System.Name, color);
            Cell(cols[1], P.Name, color);
            // the figure keeps clear of the lane its button rides in, so the two never overlap
            CellIn(Inset(cols[2].Rect), GarrisonText(P, Player), cols[2].Align, color);
            CallTroops = ActionButton(CallTroops, cols[2].Rect, "<", GameText.CallTroops, () =>
            {
                if (Player.GetTroopShipForRebase(out Ship troop, P.Position, P.Name))
                {
                    Audio.GameAudio.EchoAffirmative();
                    Screen.Universe.RunOnSimThread(() => troop.AI.OrderRebase(P, true));
                }
                else
                {
                    Audio.GameAudio.NegativeClick();
                }
            });

            // the rail and its switch share one cell: the switch says WHO decides, the rail WHAT
            // it aims for. The rail is greyed while the governor is off the militia, so the
            // figure still reads but does not invite a click that would not be honoured.
            Rectangle auto = cols[3].Rect;
            int railY = (int)(Y + Height / 2 - 9);
            if (AutoTrain == null)
            {
                AutoTrain = Add(new UICheckBox(auto.X + 4, railY, () => P.AutoBuildTroops,
                                               v => Screen.Universe.RunOnSimThread(() => P.AutoBuildTroops = v),
                                               Fonts.Arial12Bold, "", GameText.TheGovernorWillCreateA));
                GarrisonRail = Add(new FloatSlider(SliderStyle.Decimal,
                                                   new Rectangle(auto.X + 26, railY, auto.Width - 34, 18),
                                                   "", 0, MaxGarrison, P.GarrisonSize));
                // the track rides at the slider's own mid-height, so no offset on a row this
                // short; and the value sits nearer its rail than the default lane, which is cut
                // for four-figure numbers rather than a garrison of at most two
                GarrisonRail.TrackYOffset = 0;
                GarrisonRail.ValueLane = 24;
                GarrisonRail.OnChange = s =>
                {
                    int wanted = (int)s.AbsoluteValue;
                    Screen.Universe.RunOnSimThread(() => P.GarrisonSize = wanted);
                };
            }
            else
            {
                AutoTrain.SetAbsPos(auto.X + 4, railY);
                GarrisonRail.SetAbsPos(auto.X + 26, railY);
                GarrisonRail.Width = auto.Width - 34;
                // the rail follows the colony while the page is open: the governor moves it too
                GarrisonRail.AbsoluteValue = P.GarrisonSize;
            }
            GarrisonRail.Greyed = !P.AutoBuildTroops;

            // one bold letter in the governor's own colour, the mark the Colonies tab already uses
            Cell(cols[4], GovernorLetter(P), Colors.Governor(P.CType));

            // and whether that governor runs the ORBIT too. Greyed without a governor: there would
            // be nobody to honour the switch, and a live-looking box that changes nothing lies.
            Rectangle sp = cols[5].Rect;
            int boxY = (int)(Y + Height / 2 - 6);
            if (SpaceDef == null)
            {
                SpaceDef = Add(new UICheckBox(sp.X + sp.Width / 2 - 6, boxY, () => P.GovOrbitals,
                                              v => Screen.Universe.RunOnSimThread(() => P.GovOrbitals = v),
                                              Fonts.Arial12Bold, "", GameText.DvDefenseSpaceDefTip));
            }
            else
            {
                SpaceDef.SetAbsPos(sp.X + sp.Width / 2 - 6, boxY);
            }
            SpaceDef.Greyed = !P.GovernorOn;

            CellIn(Inset(cols[6].Rect), PlatformsText(P), cols[6].Align, color);
            AddPlatform = ActionButton(AddPlatform, cols[6].Rect, "+", GameText.BuildAPlatformTheStrongest,
                                 () => Order(Player.BestPlatformWeCanBuild));
            CellIn(Inset(cols[7].Rect), StationsText(P), cols[7].Align, color);
            AddStation = ActionButton(AddStation, cols[7].Rect, "+", GameText.BuildAStationTheStrongest,
                                () => Order(Player.BestStationWeCanBuild));

            // the buildings column shows WHICH, not how many - the question it answers is
            // "what is missing here", and a count cannot answer that
            Defences = P.FilterBuildings(IsDefensive);
            DefenceRects = new Rectangle[Defences.Length];
            Rectangle band = cols[8].Rect;
            int lane = band.X + 4;
            for (int i = 0; i < Defences.Length; ++i)
            {
                DefenceRects[i] = new Rectangle(lane, (int)(Y + Height / 2 - IconSize / 2), IconSize, IconSize);
                lane += IconSize + IconGap;
            }

            base.PerformLayout();
        }

        // the rail's ceiling is the colony screen's own, so the two never promise a different
        // garrison for the same world
        public const int MaxGarrison = 25;

        // the governor's type in one letter, as the Colonies tab draws it (bench 407)
        public static string GovernorLetter(Planet p) => p.CType switch
        {
            Planet.ColonyType.Colony       => "--",
            Planet.ColonyType.TradeHub     => "T",
            Planet.ColonyType.Industrial   => "I",
            Planet.ColonyType.Agricultural => "A",
            Planet.ColonyType.Research     => "R",
            Planet.ColonyType.Military     => "M",
            _                              => "C", // Core
        };

        UILabel Cell(UITable.Column c, string text, Color color)
            => CellIn(c.Rect, text, c.Align, color);

        UILabel CellIn(Rectangle rect, string text, TableAlign align, Color color)
        {
            return Label(UITable.CellPos(Fonts.Arial12Bold, rect, Y, Height, text, align),
                         text, Fonts.Arial12Bold, color);
        }

        // the cell minus its button lane: a right-aligned figure would otherwise sit under it
        static Rectangle Inset(Rectangle r) => new Rectangle(r.X, r.Y, r.Width - ActionLane, r.Height);

        // ADDED ONCE then only moved, like the switches above
        UIButton ActionButton(UIButton b, Rectangle cell, string glyph, in LocalizedText tip, Action onClick)
        {
            if (b == null)
            {
                b = Add(new UIButton(ButtonStyle.Default, Vector2.Zero, glyph));
                b.Tooltip = tip;
                b.OnClick = _ => onClick();
            }
            b.SetAbsPos(cell.Right - ActionLane + 2, (int)(Y + Height / 2 - ActionSize / 2));
            b.SetAbsSize(ActionSize + 2, ActionSize);
            return b;
        }

        // ordering an orbital goes through the planet's own guard: over the limit, nothing happens
        // and the click says so rather than queueing a hull that would be refused
        void Order(IShipDesign orbital)
        {
            if (orbital == null || P.IsOutOfOrbitalsLimit(orbital))
            {
                Audio.GameAudio.NegativeClick();
                return;
            }
            Audio.GameAudio.AffirmativeClick();
            Screen.Universe.RunOnSimThread(() => P.AddOrbital(orbital));
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            base.Draw(batch, elapsed);
            for (int i = 0; i < Defences.Length && i < DefenceRects.Length; ++i)
                batch.Draw(Defences[i].IconTex, DefenceRects[i], Color.White);
        }

        public override bool HandleInput(InputState input)
        {
            for (int i = 0; i < Defences.Length && i < DefenceRects.Length; ++i)
            {
                if (DefenceRects[i].HitTest(input.CursorPosition))
                {
                    ToolTip.CreateTooltip(DefenceTip(Defences[i]));
                    break;
                }
            }
            return base.HandleInput(input);
        }
    }
}
