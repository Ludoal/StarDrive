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
        // ⚠ drawn as flat GLYPHS, not buttons: a round plate on every figure of every row turns a
        // table into a control panel, and the eye stops finding the numbers (maintainer feedback).
        Rectangle CallTroopsRect, AddPlatformRect, AddStationRect;
        public const int ActionLane = 26; // each figure column's right end: the glyph plus its air

        // the defensive buildings drawn in the last column, with the rect each icon occupies so
        // the row can name it on hover. Rebuilt on layout: a colony gains and loses buildings.
        Building[] Defences = Empty<Building>.Array;
        Rectangle[] DefenceRects = Empty<Rectangle>.Array;

        const int IconSize = 24;
        const int IconGap = 3;
        const int ActionSize = 16;
        const int CellPad = 10;   // air at a cell's left edge, so nothing starts on the rule

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
            // the figure keeps clear of the lane its glyph rides in, so the two never touch
            CellIn(Inset(cols[2].Rect), GarrisonText(P, Player), cols[2].Align, color);
            CallTroopsRect = GlyphRect(cols[2].Rect);

            // the rail and its switch share one cell: the switch says WHO decides, the rail WHAT
            // it aims for. The rail is greyed while the governor is off the militia, so the
            // figure still reads but does not invite a click that would not be honoured.
            Rectangle auto = cols[3].Rect;
            int railY = (int)(Y + Height / 2 - 9);
            if (AutoTrain == null)
            {
                AutoTrain = Add(new UICheckBox(auto.X + CellPad, railY, () => P.AutoBuildTroops,
                                               v => Screen.Universe.RunOnSimThread(() => P.AutoBuildTroops = v),
                                               Fonts.Arial12Bold, "", GameText.TheGovernorWillCreateA));
                GarrisonRail = Add(new FloatSlider(SliderStyle.Decimal,
                                                   new Rectangle(auto.X + CellPad + 22, railY, auto.Width - CellPad - 30, 18),
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
                AutoTrain.SetAbsPos(auto.X + CellPad, railY);
                GarrisonRail.SetAbsPos(auto.X + CellPad + 22, railY);
                GarrisonRail.Width = auto.Width - CellPad - 30;
                // the rail follows the colony while the page is open: the governor moves it too
                GarrisonRail.AbsoluteValue = P.GarrisonSize;
            }
            GarrisonRail.Greyed = !P.AutoBuildTroops;

            // ONE cell, one question: the governor's letter, and beside it the switch that hands
            // him the orbit. No governor, no switch at all - a greyed box still asks to be read.
            Rectangle gov = cols[4].Rect;
            string letter = GovernorLetter(P);
            int letterX = gov.X + gov.Width / 2 - 18;
            Label(new Vector2(letterX, Y + Height / 2 - Fonts.Arial12Bold.LineSpacing / 2f).ToFloored(),
                  letter, Fonts.Arial12Bold, Colors.Governor(P.CType));

            int boxY = (int)(Y + Height / 2 - 6);
            if (SpaceDef == null)
            {
                SpaceDef = Add(new UICheckBox(gov.X + gov.Width / 2 + 6, boxY, () => P.GovOrbitals,
                                              v => Screen.Universe.RunOnSimThread(() => P.GovOrbitals = v),
                                              Fonts.Arial12Bold, "", GameText.DvDefenseSpaceDefTip));
            }
            else
            {
                SpaceDef.SetAbsPos(gov.X + gov.Width / 2 + 6, boxY);
            }
            SpaceDef.Visible = P.GovernorOn;

            CellIn(Inset(cols[5].Rect), PlatformsText(P), cols[5].Align, color);
            AddPlatformRect = GlyphRect(cols[5].Rect);
            CellIn(Inset(cols[6].Rect), StationsText(P), cols[6].Align, color);
            AddStationRect = GlyphRect(cols[6].Rect);

            // the buildings column shows WHICH, not how many - the question it answers is
            // "what is missing here", and a count cannot answer that
            Defences = P.FilterBuildings(IsDefensive);
            DefenceRects = new Rectangle[Defences.Length];
            Rectangle band = cols[7].Rect;
            int lane = band.X + CellPad;
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

        // the glyph's own square at a figure column's right end, with air on both sides
        Rectangle GlyphRect(Rectangle cell)
            => new Rectangle(cell.Right - ActionLane + 5, (int)(Y + Height / 2 - ActionSize / 2),
                             ActionSize, ActionSize);

        // ★ the game's OWN marks, not glyphs of my own (maintainer feedback): the build queues'
        // plus, and the folding lists' arrow - one asset turned a quarter turn, exactly as the list
        // headers turn it to point right while folded. A second dialect for a mark the game
        // already has is how two screens stop looking like one game.
        void DrawPlus(SpriteBatch batch, Rectangle at)
        {
            SubTexture plus = ResourceManager.Texture("NewUI/icon_build_add");
            batch.Draw(plus, at, Tint(at));
        }

        void DrawCallHere(SpriteBatch batch, Rectangle at)
        {
            SubTexture arrow = ResourceManager.Texture("NewUI/icon_queue_arrow_down");
            // +90 degrees points it LEFT - "to here" - where the headers use -90 to point right
            batch.Draw(arrow, new RectF(at.CenterX(), at.CenterY(), arrow.Width, arrow.Height),
                       Tint(at), 1.5707963f, new Vector2(arrow.Width / 2f, arrow.Height / 2f),
                       SpriteEffects.None, 1f);
        }

        Color Tint(Rectangle at) => at.HitTest(Screen.Input.CursorPosition) ? Color.White : Colors.Cream;

        void CallTroopsHere()
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
        }

        // a glyph names itself on hover and acts on a left click; it swallows the click so the
        // row underneath does not also pan the map
        bool Glyph(InputState input, Rectangle at, in LocalizedText tip, Action onClick)
        {
            if (!at.HitTest(input.CursorPosition))
                return false;

            ToolTip.CreateTooltip(tip);
            if (input.LeftMouseClick)
            {
                onClick();
                return true;
            }
            return false;
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

            DrawCallHere(batch, CallTroopsRect);
            DrawPlus(batch, AddPlatformRect);
            DrawPlus(batch, AddStationRect);
        }

        public override bool HandleInput(InputState input)
        {
            if (Glyph(input, CallTroopsRect, GameText.CallTroops, CallTroopsHere)
             || Glyph(input, AddPlatformRect, GameText.BuildAPlatformTheStrongest, () => Order(Player.BestPlatformWeCanBuild))
             || Glyph(input, AddStationRect, GameText.BuildAStationTheStrongest, () => Order(Player.BestStationWeCanBuild)))
                return true;

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
