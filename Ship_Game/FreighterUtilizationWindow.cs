using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.Audio;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using SDUtils;
using Ship_Game.Ships;
using Ship_Game.Commands.Goals;

namespace Ship_Game
{
    public sealed class FreighterUtilizationWindow : GameScreen
    {
        public bool IsOpen { get; private set; }
        public float TotalUtilizedCargo;
        readonly UniverseScreen Screen;
        Submenu ConstructionSubMenu;
        ProgressBar UtilizationBar;
        Map<Goods, GoodsUtilization> GoodsUtilizationMap = new Map<Goods, GoodsUtilization>();
        UIButton BuildFreighter;
        Empire Player => Screen.Player;
        float UpdateTimer;
        // ★ THE WHOLE freighter fleet, zone-held hulls INCLUDED. Empire.TotalFreighters
        // deliberately excludes them - the reserve and the refit ceiling derive from it -
        // and pairing that with a utilised count which includes them understated the idle
        // figure by exactly the hulls a zone was flying, and could drive it below zero.
        // What a zone holds is stated on its own line instead (maintainer bench 582).
        int FleetFreighters;
        int NumUtilizedFreighters;
        UILabel FreighterConstructingLabel;
        UILabel FreightersInZonesLabel;
        UILabel NumIdleFreightersLabel;
        // Ludoal fork (maintainer bench 336): a "Total freighters:" row under the goods rows.
        // Every cell of it is the plain SUM of the three rows above, in their own unit - runs over
        // possible runs, then planets over planets. The fleet's own utilisation is a different
        // notion and is stated on the left, as a percentage.
        UILabel TotalFreightersLabel;
        UILabel TotalFreightersValue;
        UILabel TotalImportingValue;
        UILabel TotalExportingValue;
        UILabel TotalFreightersDen;
        UILabel TotalImportingDen;
        UILabel TotalExportingDen;

        // Ludoal fork (maintainer bench 339): the numbers are right-aligned on a 3-digit column
        // centred under each header. These are the RIGHT edges of those columns (absolute X), the
        // ONE source both the per-goods rows and the totals row align on, so they cannot disagree.
        const float NumberColW = 24f; // room for three digits in Arial12Bold
        // Ludoal fork (maintainer, bench 578): the cells hold "served / total" now, so the lane is
        // wide enough for both halves and the SLASH sits on a fixed column inside it. The font is
        // proportional: right-aligning the whole string lines up the ends, which is exactly what
        // made the fractions look ragged. Two halves, one fixed column, same trick as the Labor
        // numbers on the colony screen.
        const float FractionColW = 62f, SlashLane = 30f;
        float FreightersRightX, ImportingRightX, ExportingRightX;

        // Ludoal fork (maintainer feedback): the ZONE FILTER. A null selection is the whole
        // empire, which is what this window has always shown - so "All zones" is not a new mode,
        // it is the old one given a name. All three goods narrow: what the zone measure leaves
        // colonists out of is its QUOTA, where heads and cargo loads cannot be added; counting
        // the colonies of a zone and the runs delivering into it is honest in any unit.
        DropOptions<TradeZone> ZoneFilter;
        TradeZone SelectedZone;
        // what the picker currently SHOWS - the player edits the empire's zones on another page,
        // so the two are compared each tick and the picker rebuilt when they part company. Built
        // once at open, it went on offering the list as it stood the moment the window came up.
        readonly Array<TradeZone> ZonesShown = new();

        // the right edge that centres a FractionColW-wide lane under a header at headerX
        static float ColumnRightUnder(float headerX, GameText header)
            => headerX + Fonts.Arial12Bold.TextWidth(new LocalizedText(header).Text) * 0.5f + FractionColW * 0.5f;

        // a white number label right-aligned so its right edge lands on rightX
        static UILabel RightAlignedValue(float rightX, float y)
            => new(new Vector2(rightX - NumberColW, y), "", Fonts.Arial12Bold, Color.White)
               { TextAlign = TextAlign.Right, Width = NumberColW };

        // the numerator ends on the slash column, the denominator starts on it - so every row's
        // "/" stands in the same place whatever the digits either side
        static void LayoutFraction(UILabel num, UILabel den, float rightX, float y)
        {
            float laneX = rightX - FractionColW;
            num.TextAlign = TextAlign.Right;
            num.Pos = new Vector2(laneX, y);
            num.Width = SlashLane;
            num.PerformLayout();
            den.TextAlign = TextAlign.Default;   // Default IS left here; there is no Left member
            den.Pos = new Vector2(laneX + SlashLane, y);
            den.Width = FractionColW - SlashLane;
            den.PerformLayout();
        }

        public FreighterUtilizationWindow(UniverseScreen screen) : base(screen, toPause: null)
        {
            Screen = screen;
            SeatByMinimap();
            CanEscapeFromScreen = false;
            if (Player.NonCybernetic)
                GoodsUtilizationMap.Add(Goods.Food, new GoodsUtilization(Goods.Food, this));

            GoodsUtilizationMap.Add(Goods.Production, new GoodsUtilization(Goods.Production, this));
            GoodsUtilizationMap.Add(Goods.Colonists, new GoodsUtilization(Goods.Colonists, this));
            UtilizationBar = new ProgressBar(new Rectangle(-100, -100, 150, 18), 0, 0) { DrawPercentage = true };
            BuildFreighter = Button(ButtonStyle.DefaultActive, GameText.BuildFreighter, OnBuildFreighterClick);
        }

        // Ludoal fork (bench 406): the minimap can be resized live from Options - the window
        // re-anchors on it, and reflows its content if it is already built
        public void SeatByMinimap()
        {
            const int windowWidth = 650;
            // Ludoal fork (maintainer, bench 578): FIVE rows, not four - the left column gained
            // the freighters-in-zones line and it landed on the Build Freighter button. The height
            // is counted in rows on purpose, so a row added here needs no second number changed.
            int windowHeight = 5 * (Fonts.Arial12Bold.LineSpacing + 25);
            Rect = new Rectangle((int)Screen.Minimap.X - 5 - windowWidth, (int)Screen.Minimap.Y +
                (int)Screen.Minimap.Height - windowHeight, windowWidth, windowHeight); // foot flush with the minimap frame
            if (HasContent)
                LoadContent();
        }
        bool HasContent;

        public override void LoadContent()
        {
            base.LoadContent();
            RemoveAll();
            HasContent = true;

            RectF win = new(Rect);
            // Ludoal fork: window title is "Freighters"
            ConstructionSubMenu = new(win, "Freighters");
            float titleOffset = win.Y + 40;
            // Ludoal fork (maintainer, bench 578): the zone picker comes down off the title bar and
            // takes the first line of the RIGHT column; its headers move down a row into the space
            // the fifth row opened. The left column keeps the first line for its own heading.
            const float HeaderDrop = 25f;
            float headerY = titleOffset + HeaderDrop;
            Add(new UILabel(new Vector2(win.X + 15, titleOffset), GameText.TotalFreighterUtilization, Fonts.Arial12Bold, Color.Gold, GameText.TotalUtilizationTip));
            Add(new UILabel(new Vector2(win.X + 210, headerY), GameText.CargoDistribution, Fonts.Arial12Bold, Color.White, GameText.CargoDistributionTip));
            Add(new UILabel(new Vector2(win.X + 370, headerY), GameText.Freighters, Fonts.Arial12Bold, Color.White, GameText.TzFreightersTip));
            // these two columns count PLANETS (open import/export slots), not freighters -
            // the only headers of this window without a tooltip, and the mixed units confused readers
            // the columns count WORLDS, so they are named for worlds. ⚠ our own tokens: the
            // upstream Importing/Exporting ids stay untouched for the next merge.
            Add(new UILabel(new Vector2(win.X + 470, headerY), GameText.TzImporters, Fonts.Arial12Bold, Color.White, GameText.TzImportersTip));
            Add(new UILabel(new Vector2(win.X + 570, headerY), GameText.TzExporters, Fonts.Arial12Bold, Color.White, GameText.TzExportersTip));
            // the 3-digit number columns, centred under each header - shared by the goods rows and
            // the totals row (maintainer bench 339)
            FreightersRightX = ColumnRightUnder(win.X + 370, GameText.Freighters);
            ImportingRightX  = ColumnRightUnder(win.X + 470, GameText.TzImporters);
            ExportingRightX  = ColumnRightUnder(win.X + 570, GameText.TzExporters);
            Add(new UILabel(new Vector2(win.X + 15, titleOffset + 50), GameText.IdleFrieghters, Fonts.Arial12Bold, Color.Wheat));
            Add(new UILabel(new Vector2(win.X + 15, titleOffset + 70), GameText.FreightersUnderConstruction, Fonts.Arial12Bold, Color.Wheat));
            // ⚠ the idle count above is taken from the pool, and the pool EXCLUDES hulls held by a
            // zone - so assigning freighters made the fleet appear to shrink under the player's
            // hand, during the very operation this window is meant to guide. The part in zones is
            // stated rather than subtracted, on the same rhythm as the two rows above it.
            Add(new UILabel(new Vector2(win.X + 15, titleOffset + 90), GameText.TzInZones, Fonts.Arial12Bold, Color.Wheat));

            NumIdleFreightersLabel     = new UILabel(new Vector2(win.X + 150, titleOffset + 50), "", Fonts.Arial12Bold, Color.White);
            FreighterConstructingLabel = new UILabel(new Vector2(win.X + 150, titleOffset + 70), "", Fonts.Arial12Bold, Color.White);
            FreightersInZonesLabel     = new UILabel(new Vector2(win.X + 150, titleOffset + 90), "", Fonts.Arial12Bold, Color.White);

            UIList utilizationData = AddList(new(win.X + 5f, win.Y + 40 + HeaderDrop));
            utilizationData.Padding = new(2f, 25f);
            foreach (GoodsUtilization gu in  GoodsUtilizationMap.Values)
                utilizationData.Add(gu);

            // Ludoal fork (maintainer bench 339): the totals row under the goods rows. The caption
            // left-aligns on the Cargo Distribution bars (win.X + 210); the values right-align on the
            // SAME 3-digit columns as the goods rows above (centred under each header).
            float totalsY = win.Y + Height - 25;
            TotalFreightersLabel = Add(new UILabel(new Vector2(win.X + 210, totalsY), "Total freighters:", Fonts.Arial12Bold, Color.Wheat));
            TotalFreightersValue = Add(RightAlignedValue(FreightersRightX, totalsY));
            TotalImportingValue  = Add(RightAlignedValue(ImportingRightX, totalsY));
            TotalExportingValue  = Add(RightAlignedValue(ExportingRightX, totalsY));
            TotalFreightersDen   = Add(RightAlignedValue(FreightersRightX, totalsY));
            TotalImportingDen    = Add(RightAlignedValue(ImportingRightX, totalsY));
            TotalExportingDen    = Add(RightAlignedValue(ExportingRightX, totalsY));
            // the foot's fractions stand on the same slash columns as the rows above it
            LayoutFraction(TotalFreightersValue, TotalFreightersDen, FreightersRightX, totalsY);
            LayoutFraction(TotalImportingValue,  TotalImportingDen,  ImportingRightX, totalsY);
            LayoutFraction(TotalExportingValue,  TotalExportingDen,  ExportingRightX, totalsY);

            // the zone picker rides the title bar's right end, the way STARVATION rides Supply's -
            // the window's four rows are spoken for. Added LAST on purpose: an open list draws
            // over the rows only if it is the last child, and a dropdown seated earlier would
            // unfold UNDER them (bench 505, the same trap on three lists).
            const float ZoneBoxW = 170;
            float zoneBoxX = win.Right - ZoneBoxW - 12;
            // the tooltip rides the CAPTION, not the list: a DropOptions carries none of its own
            // (it is a bare UIElementV2), which is why every picker in the game is labelled. The
            // caption's X is MEASURED off its own text rather than guessed at a round number.
            // singular here: the picker chooses ONE zone to look through, where the shared token
            // names the feature. Its own token rather than an edit to that one, which titles two
            // other screens (maintainer feedback).
            string zoneCap = Localizer.Token(GameText.TzZoneFilter);
            Add(new UILabel(new Vector2(zoneBoxX - Fonts.Arial12Bold.TextWidth(zoneCap) - 8, titleOffset),
                            GameText.TzZoneFilter, Fonts.Arial12Bold, Color.Wheat, GameText.TzWindowZoneTip));
            ZoneFilter = Add(new DropOptions<TradeZone>(
                new Vector2(zoneBoxX, titleOffset - 2), (int)ZoneBoxW, 18));
            ZoneFilter.OnValueChange = z => SelectedZone = z;
            RebuildZoneOptions();
        }

        public override void PerformLayout()
        {
            const int utilColX = 10, utilColW = 150, buildBtnW = 130;
            UtilizationBar.SetRect(new Rectangle((int)Pos.X + utilColX, (int)Pos.Y+65, utilColW, 18));
            // Ludoal fork (maintainer feedback): the Build Freighter button centred on the util column
            // ⚠ anchored to the FOOT, not to a constant off the top: it sat at a fixed 135 and the
            // window has since gained a row, so it landed on the line that row was added for
            // (bench 580). Off the bottom it follows whatever height the window takes.
            BuildFreighter.SetAbsSize(buildBtnW, 24);
            BuildFreighter.Pos = new Vector2(Pos.X + utilColX + (utilColW - buildBtnW) / 2,
                                             Pos.Y + Height - 24 - 8);
            base.PerformLayout();
        }

        public void ToggleVisibility(bool playSound = true)
        {
            if (playSound) // silent when restored from a save
                GameAudio.AcceptClick();
            IsOpen = !IsOpen;
            if (IsOpen)
            {
                Screen.ExoticBonusesWindow.CloseWindow();
                LoadContent();
            }
        }

        public void CloseWindow()
        {
            IsOpen = false;
            Visible = false;
        }

        // bench 406: the overlay steps aside during ground combat and returns with the view
        bool HiddenByGroundCombat => Screen.LookingAtPlanet && Screen.workersPanel is CombatScreen;
        // the visible-band pass (open page) asks this before handing the window the cursor
        public bool AcceptsBandInput => IsOpen && !HiddenByGroundCombat;

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (!Visible || HiddenByGroundCombat)
                return;

            Rectangle r = ConstructionSubMenu.Rect;
            r.Y += 25;
            r.Height -= 25;
            var sel = new Selector(r, new Color(0, 0, 0, 210));
            sel.Draw(batch, elapsed);
            ConstructionSubMenu.Draw(batch, elapsed);
            base.Draw(batch, elapsed);
            UtilizationBar.Draw(batch);
            BuildFreighter.Draw(batch, elapsed);
            FreighterConstructingLabel.Draw(batch, elapsed);
            NumIdleFreightersLabel.Draw(batch, elapsed);
            FreightersInZonesLabel.Draw(batch, elapsed);
            DrawLine(new Vector2(Pos.X + 180, Pos.Y + 35), new Vector2(Pos.X + 180, Pos.Y + Height - 10), Color.Wheat, 2);
        }

        public override bool HandleInput(InputState input)
        {
            if (!IsOpen || HiddenByGroundCombat)
                return false;

            // the open list overlays every other control, so it is offered the cursor first
            if (ZoneFilter != null && ZoneFilter.HandleInput(input))
                return true;

            if (BuildFreighter.HandleInput(input))
                return true;

            base.HandleInput(input);
            return false;
        }

        public override void Update(float fixedDeltaTime)
        {
            if (!IsOpen) 
                return;

            UpdateTimer -= fixedDeltaTime;
            if (UpdateTimer <= 0)
            {
                UpdateTimer = 1;
                if (ZoneFilter != null && ZoneOptionsStale())
                    RebuildZoneOptions();

                FleetFreighters = Player.OwnedShips.Count(s => s?.IsFreighter == true);
                float totalUtilizedCargo = 0;
                foreach (GoodsUtilization goodsUtilization in GoodsUtilizationMap.Values)
                    goodsUtilization.Reset();


                // ★★ THE NEED IS NOT COMPUTED HERE ANY MORE. A zone's is READ off the zone, where
                // the empire wrote it this turn; the empire's own is asked of the same function the
                // Trade table uses. Two screens that compute cannot agree for long - these two were
                // three definitions apart (maintainer bench 582-584).
                var perimeter = new Array<Planet>();
                foreach (Planet p in Player.GetPlanets())
                    if (SelectedZone == null || SelectedZone.Serves(p))
                        perimeter.Add(p);

                if (SelectedZone != null)
                {
                    if (Player.NonCybernetic)
                        GoodsUtilizationMap[Goods.Food].SetNeed(SelectedZone.NeedFood);
                    GoodsUtilizationMap[Goods.Production].SetNeed(SelectedZone.NeedProd);
                    GoodsUtilizationMap[Goods.Colonists].SetNeed(SelectedZone.NeedColonists);
                }
                else
                {
                    // no zone picked: the perimeter is the whole realm. The figure is what the
                    // realm WANTS - the same book the zone lines above read, uncapped, so the
                    // overlay never writes "nobody can serve me" as a nought
                    if (Player.NonCybernetic)
                        GoodsUtilizationMap[Goods.Food].SetNeed(Player.PerimeterNeed(perimeter, Goods.Food));
                    GoodsUtilizationMap[Goods.Production].SetNeed(Player.PerimeterNeed(perimeter, Goods.Production));
                    GoodsUtilizationMap[Goods.Colonists].SetNeed(Player.PerimeterNeed(perimeter, Goods.Colonists));
                }

                // ⚠ A WORLD WITH A CARGO IN THE AIR COUNTS, even if its store filled while that
                // cargo was flying. Counted only while its slots stood open, a colony dropped out
                // of Importing at the very moment it was being served, and the row read "1 / 0" -
                // one hull, no importer (maintainer feedback). The runs column never had that
                // guard, which is why the two disagreed rather than both being wrong.
                foreach (Planet planet in Player.GetPlanets())
                {
                    if (SelectedZone != null && !SelectedZone.Serves(planet))
                        continue;

                    if (Player.NonCybernetic)
                    {
                        if (planet.FoodImportSlots > 0 || planet.IncomingFoodFreighters > 0) GoodsUtilizationMap[Goods.Food].IncreaseNumImportingPlanets();
                        GoodsUtilizationMap[Goods.Food].AddServedBerths(planet.IncomingFoodFreighters);
                        // ⚠ ONE per world, never the hull count: this column pairs served worlds
                        // with importing worlds, and a numerator counting hulls made "2 / 2" mean
                        // two hulls over two worlds (maintainer feedback, bench 590).
                        if (planet.FoodImportSlots > 0 || planet.IncomingFoodFreighters > 0) GoodsUtilizationMap[Goods.Food].AddServedImporting(planet.IncomingFoodFreighters > 0 ? 1 : 0);
                    }

                    if (planet.ProdImportSlots > 0 || planet.IncomingProdFreighters > 0)           GoodsUtilizationMap[Goods.Production].IncreaseNumImportingPlanets();
                    if (planet.ColonistsImportSlots > 0 || planet.IncomingColonistsFreighters > 0) GoodsUtilizationMap[Goods.Colonists].IncreaseNumImportingPlanets();
                    GoodsUtilizationMap[Goods.Production].AddServedBerths(planet.IncomingProdFreighters);
                    GoodsUtilizationMap[Goods.Colonists].AddServedBerths(planet.IncomingColonistsFreighters);
                    if (planet.ProdImportSlots > 0 || planet.IncomingProdFreighters > 0)           GoodsUtilizationMap[Goods.Production].AddServedImporting(planet.IncomingProdFreighters > 0 ? 1 : 0);
                    if (planet.ColonistsImportSlots > 0 || planet.IncomingColonistsFreighters > 0) GoodsUtilizationMap[Goods.Colonists].AddServedImporting(planet.IncomingColonistsFreighters > 0 ? 1 : 0);
                }

                // ★ THE EXPORTERS COLUMN FOLLOWS THE REGIME, exactly as the need's ceiling does:
                // an enclave loads among its own worlds, a soft zone on the common ground, and
                // with no zone picked the ground is the realm. Counted on its OWN set, because the
                // loop above only walks the zone's colonies - which is how "0 / 0" came to sit
                // beside a need of 24 (maintainer feedback, bench 583).
                if (Player.NonCybernetic)
                    CountExporters(Goods.Food, perimeter);
                CountExporters(Goods.Production, perimeter);
                CountExporters(Goods.Colonists, perimeter);

                var allUtilizedFreightesr = Player.OwnedShips.Filter(s => s.IsFreighter && s.AI.State == AI.AIState.SystemTrader);
                NumUtilizedFreighters = allUtilizedFreightesr.Length;
                foreach (Ship freighter in allUtilizedFreightesr)
                {
                    if (Player.NonCybernetic && ServesSelectedZone(freighter, Goods.Food))
                        GoodsUtilizationMap[Goods.Food].AddGoodsTransported(freighter, ref totalUtilizedCargo);

                    if (ServesSelectedZone(freighter, Goods.Production))
                        GoodsUtilizationMap[Goods.Production].AddGoodsTransported(freighter, ref totalUtilizedCargo);

                    if (ServesSelectedZone(freighter, Goods.Colonists))
                        GoodsUtilizationMap[Goods.Colonists].AddGoodsTransported(freighter, ref totalUtilizedCargo);
                }

                TotalUtilizedCargo = totalUtilizedCargo;
                UtilizationBar.Progress = FleetFreighters == 0 ? 0 : (float)NumUtilizedFreighters/FleetFreighters*100;
                FreighterConstructingLabel.Text = Player.FreightersBeingBuilt.String();
                NumIdleFreightersLabel.Text = (FleetFreighters - NumUtilizedFreighters).String();
                FreightersInZonesLabel.Text = Player.OwnedShips.Count(s => s?.IsFreighter == true && s.InTradeZone).String();

                // ★ the totals row SUMS the rows above it, in their own unit. It used to count
                // utilised hulls over the fleet - a second notion in the same column, which is how
                // three rows adding to 14/66 sat under a total reading 13/14. The fleet's own
                // utilisation is already stated on the left, as a percentage.
                int servedBerths = 0, possibleRuns = 0, servedImp = 0, imp = 0, servedExp = 0, exp = 0;
                foreach (GoodsUtilization gu in GoodsUtilizationMap.Values)
                {
                    servedBerths += gu.ServedBerths; possibleRuns += gu.Runs;
                    servedImp += gu.ServedImporting; imp += gu.NumImportingPlanets;
                    servedExp += gu.ServedExporting; exp += gu.NumExportingPlanets;
                }
                TotalFreightersValue.Text = servedBerths.String();
                TotalFreightersDen.Text   = $" / {possibleRuns}";
                TotalImportingValue.Text  = servedImp.String();
                TotalImportingDen.Text    = $" / {imp}";
                TotalExportingValue.Text  = servedExp.String();
                TotalExportingDen.Text    = $" / {exp}";
            }

            base.Update(fixedDeltaTime);
        }

        // The picker's entries, rebuilt from the empire's own list. A zone dissolved elsewhere
        // must not survive as a selection, so the held zone is re-checked against the list it
        // came from - and the entry order follows the list, which IS the dispatch priority.
        void RebuildZoneOptions()
        {
            ZoneFilter.Clear();
            ZoneFilter.AddOption(GameText.TzAllZones, null);
            ZonesShown.Clear();
            foreach (TradeZone zone in Player.TradeZones)
            {
                ZoneFilter.AddOption(zone.Name, zone);
                ZonesShown.Add(zone);
            }

            if (SelectedZone != null && !ZonesShown.Contains(SelectedZone))
                SelectedZone = null;

            ZoneFilter.ActiveIndex = SelectedZone == null ? 0 : ZonesShown.IndexOf(SelectedZone) + 1;
        }

        // the picker shows a list the player edits on another page: it is stale the moment that
        // list has a zone more, a zone less, or the same zones in another order
        bool ZoneOptionsStale()
        {
            if (ZonesShown.Count != Player.TradeZones.Count)
                return true;

            for (int i = 0; i < ZonesShown.Count; ++i)
                if (ZonesShown[i] != Player.TradeZones[i])
                    return true;

            return false;
        }

        // a run belongs to a zone by the end it DELIVERS to - the same end the zone's own dispatch
        // serves. No selection means the whole empire, so everything belongs.
        bool ServesSelectedZone(Ship freighter, Goods goods)
        {
            if (SelectedZone == null)
                return true;

            Planet importTo = freighter.AI.TradeImportFor(goods);
            return importTo != null && SelectedZone.Serves(importTo);
        }

        void OnBuildFreighterClick(UIButton b)
        {
            Player.AI.AddGoalAndEvaluate(new IncreaseFreighters(Player));
            FreighterConstructingLabel.Text = Player.FreightersBeingBuilt.String();
        }

        // The worlds a run of this good may LOAD from, under the regime of the selected zone.
        void CountExporters(Goods goods, Array<Planet> realm)
        {
            Array<Planet> ground = SelectedZone == null   ? realm
                                 : SelectedZone.Exclusive ? SelectedZone.ColonyPlanets(Player)
                                 : Player.CommonExportGround(goods);

            GoodsUtilization row = GoodsUtilizationMap[goods];
            foreach (Planet p in ground)
            {
                int slots    = goods == Goods.Food       ? p.FoodExportSlots
                             : goods == Goods.Production ? p.ProdExportSlots
                             : p.ColonistsExportSlots;
                int outgoing = goods == Goods.Food       ? p.OutgoingFoodFreighters
                             : goods == Goods.Production ? p.OutgoingProdFreighters
                             : p.OutGoingColonistsFreighters;

                if (slots > 0 || outgoing > 0)
                {
                    row.IncreaseNumExportingPlanets();
                    row.AddServedExporting(outgoing > 0 ? 1 : 0);
                }
            }
        }

        // ★ THE ONE COLOUR RULE for a pair of numbers, shared with the Trade table so an eye that
        // learned it here does not learn another one there. Above what is wanted is not a fault -
        // more on its way than there is room for is worth seeing, not flagging - and a pair
        // wanting NOTHING is neutral: there is nothing to cover, so nothing to miss.
        public static Color PairColor(int served, int wanted)
            => wanted <= 0      ? Color.Wheat
             : served >= wanted ? Color.White
             : served == 0      ? Color.Red
             :                    Color.Yellow;

        class GoodsUtilization : UIElementV2
        {
            readonly ProgressBar UtilizationBar;
            readonly UILabel NumFreightersLabel;
            readonly UILabel NumImportingLabel;
            readonly UILabel NumExportingLabel;
            readonly UILabel DenFreightersLabel;
            readonly UILabel DenImportingLabel;
            readonly UILabel DenExportingLabel;
            readonly UIPanel IconPanel;
            readonly FreighterUtilizationWindow Window;
            readonly Goods Goods;
            public int NumImportingPlanets { get; private set; }
            public int NumExportingPlanets { get; private set; }
            public int NumFreighters { get; private set; }
            // Ludoal fork (maintainer feedback): the SAME two numbers the Trade table shows, one
            // level down - import berths open for this good, and freighters already on their way
            // to them. Planets would not do: a colony with three Food berths is one planet and
            // three berths, so the two screens would answer the same question differently.
            // what the empire says this perimeter needs, in whole hulls - written from outside,
            // never computed here
            public int NeedRuns { get; private set; }
            public void SetNeed(int runs) => NeedRuns = runs;
            public int ServedBerths { get; private set; }
            // ★ a run needs a berth at BOTH ends, so what the fleet can actually do is the smaller
            // of the two. Import berths alone are a ceiling: a galaxy can offer 29 places to unload
            // production while a single planet is able to send any.
            public int Runs => NeedRuns;
            public int ServedImporting { get; private set; }
            public int ServedExporting { get; private set; }
            public float TotalEmpireUtilizedCargo { get; private set; }
            public float GoodsTransported { get; private set; }


            public GoodsUtilization(Goods goods, FreighterUtilizationWindow parent)
            {
                Window = parent;
                Goods  = goods;
                UtilizationBar     = new ProgressBar(new Rectangle(-100, -100, 150, 18), 0, 0) { DrawPercentage = true };
                NumFreightersLabel = new UILabel(new Vector2(-100, -100), GameText.HullBonus, Fonts.Arial12Bold, Color.Wheat);
                NumImportingLabel  = new UILabel(new Vector2(-100, -100), GameText.HullBonus, Fonts.Arial12Bold, Color.Wheat);
                NumExportingLabel  = new UILabel(new Vector2(-100, -100), GameText.HullBonus, Fonts.Arial12Bold, Color.Wheat);
                DenFreightersLabel = new UILabel(new Vector2(-100, -100), GameText.HullBonus, Fonts.Arial12Bold, Color.Wheat);
                DenImportingLabel  = new UILabel(new Vector2(-100, -100), GameText.HullBonus, Fonts.Arial12Bold, Color.Wheat);
                DenExportingLabel  = new UILabel(new Vector2(-100, -100), GameText.HullBonus, Fonts.Arial12Bold, Color.Wheat);

                SubTexture Icon = ResourceManager.Texture("Goods/Production");
                if (goods == Goods.Food)
                {
                    Icon = ResourceManager.Texture("Goods/Food");
                    UtilizationBar.color = "green";
                }
                else if (goods == Goods.Colonists)
                {
                    Icon = ResourceManager.Texture("Goods/Colonists_1000");
                    UtilizationBar.color = "blue";
                }

                IconPanel = new UIPanel(new Rectangle(-100, -100, 25, 25), Icon);
            }

            public override void PerformLayout()
            {
                IconPanel.Pos = new Vector2(Pos.X + 175, Pos.Y - 5);
                IconPanel.PerformLayout();
                UtilizationBar.SetRect(new Rectangle((int)Pos.X + 200, (int)Pos.Y, 150, 18));
                // maintainer bench 339: numbers RIGHT-aligned on the SAME columns as the totals row
                // (centred under each header, room for 3 digits). Window owns the right edges, so
                // the goods rows and the totals cannot disagree.
                LayoutFraction(NumFreightersLabel, DenFreightersLabel, Window.FreightersRightX, Pos.Y);
                LayoutFraction(NumImportingLabel,  DenImportingLabel,  Window.ImportingRightX, Pos.Y);
                LayoutFraction(NumExportingLabel,  DenExportingLabel,  Window.ExportingRightX, Pos.Y);
                base.PerformLayout();
            }

            public override bool HandleInput(InputState input)
            {
                return false;
            }


            public override void Draw(SpriteBatch batch, DrawTimes elapsed)
            {
                UtilizationBar.Draw(batch);
                IconPanel.Draw(batch, elapsed);
                NumFreightersLabel.Draw(batch, elapsed);
                NumImportingLabel.Draw(batch, elapsed);
                NumExportingLabel.Draw(batch, elapsed);
                DenFreightersLabel.Draw(batch, elapsed);
                DenImportingLabel.Draw(batch, elapsed);
                DenExportingLabel.Draw(batch, elapsed);
                // every column reads its OWN pair now. The two others used to colour off the
                // importers and the traffic - figures the cell does not show - so the same "0 / 0"
                // wore two colours in one panel and neither could be explained from the line
                // (maintainer feedback, bench 590).
                NumFreightersLabel.Color = PairColor(ServedBerths, Runs);
                NumImportingLabel.Color  = PairColor(ServedImporting, NumImportingPlanets);
                NumExportingLabel.Color  = PairColor(ServedExporting, NumExportingPlanets);
            }

            public override void Update(float fixedDeltaTime)
            {
                TotalEmpireUtilizedCargo = Window.TotalUtilizedCargo;
                UtilizationBar.Progress  = TotalEmpireUtilizedCargo == 0 ? 0 : GoodsTransported/TotalEmpireUtilizedCargo *100;
                NumFreightersLabel.Text  = ServedBerths.String();
                DenFreightersLabel.Text  = $" / {Runs}";
                DenFreightersLabel.Color = NumFreightersLabel.Color;
                DenImportingLabel.Color  = NumImportingLabel.Color;
                DenExportingLabel.Color  = NumExportingLabel.Color;
                NumImportingLabel.Text   = ServedImporting.String();
                DenImportingLabel.Text   = $" / {NumImportingPlanets}";
                NumExportingLabel.Text   = ServedExporting.String();
                DenExportingLabel.Text   = $" / {NumExportingPlanets}";
                base.Update(fixedDeltaTime);
            }


            public void IncreaseNumImportingPlanets()
            {
                NumImportingPlanets++;
            }

            public void IncreaseNumExportingPlanets()
            {
                NumExportingPlanets++;
            }

            public void AddServedBerths(int incoming)
            {
                ServedBerths += incoming;
            }


            // a planet counts as served the moment something is on its way to it, which is the
            // question the column answers: how many of the ones asking are being answered
            public void AddServedImporting(int incoming) { if (incoming > 0) ++ServedImporting; }
            public void AddServedExporting(int outgoing) { if (outgoing > 0) ++ServedExporting; }

            public void AddGoodsTransported(Ship freighter, ref float totalUtilized)
            {
                if (freighter.AI.HasTradeGoal(Goods))
                {
                    GoodsTransported += freighter.CargoSpaceMax;
                    totalUtilized    += freighter.CargoSpaceMax;
                    NumFreighters++;
                }
            }

            public void SetMaxEmpireCargo(float value)
            {
                TotalEmpireUtilizedCargo = value;
            }

            public void Reset()
            {
                NumImportingPlanets = 0;
                NumExportingPlanets = 0;
                NumFreighters       = 0;
                ServedBerths        = 0;
                ServedImporting     = 0;
                ServedExporting     = 0;
                GoodsTransported    = 0;
                TotalEmpireUtilizedCargo      = 0;
            }
        }
    }
}
