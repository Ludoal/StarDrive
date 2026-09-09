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

        // the defensive buildings drawn in the last column, with the rect each icon occupies so
        // the row can name it on hover. Rebuilt on layout: a colony gains and loses buildings.
        Building[] Defences = Empty<Building>.Array;
        Rectangle[] DefenceRects = Empty<Rectangle>.Array;

        const int IconSize = 24;
        const int IconGap = 3;

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
            Cell(cols[2], GarrisonText(P, Player), color);

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

            Cell(cols[4], PlatformsText(P), color);
            Cell(cols[5], StationsText(P), color);

            // the buildings column shows WHICH, not how many - the question it answers is
            // "what is missing here", and a count cannot answer that
            Defences = P.FilterBuildings(IsDefensive);
            DefenceRects = new Rectangle[Defences.Length];
            Rectangle band = cols[6].Rect;
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

        UILabel Cell(UITable.Column c, string text, Color color)
        {
            return Label(UITable.CellPos(Fonts.Arial12Bold, c.Rect, Y, Height, text, c.Align),
                         text, Fonts.Arial12Bold, color);
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
