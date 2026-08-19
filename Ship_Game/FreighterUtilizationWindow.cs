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
        int TotalFreighters;
        int NumUtilizedFreighters;
        UILabel FreighterConstructingLabel;
        UILabel NumIdleFreightersLabel;
        // Ludoal fork (maintainer bench 336): a "Total freighters:" row under the goods rows.
        // The freighters value is the OPERATIONAL count (NumUtilizedFreighters), not a sum of the
        // per-goods needs; importing/exporting are the planet-slot totals across the goods.
        UILabel TotalFreightersLabel;
        UILabel TotalFreightersValue;
        UILabel TotalImportingValue;
        UILabel TotalExportingValue;

        // Ludoal fork (maintainer bench 339): the numbers are right-aligned on a 3-digit column
        // centred under each header. These are the RIGHT edges of those columns (absolute X), the
        // ONE source both the per-goods rows and the totals row align on, so they cannot disagree.
        const float NumberColW = 24f; // room for three digits in Arial12Bold
        float FreightersRightX, ImportingRightX, ExportingRightX;

        // the right edge that centres a NumberColW-wide column under a header at headerX
        static float ColumnRightUnder(float headerX, GameText header)
            => headerX + Fonts.Arial12Bold.TextWidth(new LocalizedText(header).Text) * 0.5f + NumberColW * 0.5f;

        // a white number label right-aligned so its right edge lands on rightX
        static UILabel RightAlignedValue(float rightX, float y)
            => new(new Vector2(rightX - NumberColW, y), "", Fonts.Arial12Bold, Color.White)
               { TextAlign = TextAlign.Right, Width = NumberColW };

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
            int windowHeight = 4 * (Fonts.Arial12Bold.LineSpacing + 25);
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
            Add(new UILabel(new Vector2(win.X + 15, titleOffset), GameText.TotalFreighterUtilization, Fonts.Arial12Bold, Color.Gold, GameText.TotalUtilizationTip));
            Add(new UILabel(new Vector2(win.X + 210, titleOffset), GameText.CargoDistribution, Fonts.Arial12Bold, Color.White, GameText.CargoDistributionTip));
            Add(new UILabel(new Vector2(win.X + 370, titleOffset), GameText.Freighters, Fonts.Arial12Bold, Color.White, GameText.NumberOfFreightersTip));
            // these two columns count PLANETS (open import/export slots), not freighters -
            // the only headers of this window without a tooltip, and the mixed units confused readers
            Add(new UILabel(new Vector2(win.X + 470, titleOffset), GameText.ImportingPlanets, Fonts.Arial12Bold, Color.White, GameText.ImportingPlanetsTip));
            Add(new UILabel(new Vector2(win.X + 570, titleOffset), GameText.ExportingPlanets, Fonts.Arial12Bold, Color.White, GameText.ExportingPlanetsTip));
            // the 3-digit number columns, centred under each header - shared by the goods rows and
            // the totals row (maintainer bench 339)
            FreightersRightX = ColumnRightUnder(win.X + 370, GameText.Freighters);
            ImportingRightX  = ColumnRightUnder(win.X + 470, GameText.ImportingPlanets);
            ExportingRightX  = ColumnRightUnder(win.X + 570, GameText.ExportingPlanets);
            Add(new UILabel(new Vector2(win.X + 15, titleOffset + 50), GameText.IdleFrieghters, Fonts.Arial12Bold, Color.Wheat));
            Add(new UILabel(new Vector2(win.X + 15, titleOffset + 70), GameText.FreightersUnderConstruction, Fonts.Arial12Bold, Color.Wheat));

            NumIdleFreightersLabel     = new UILabel(new Vector2(win.X + 150, titleOffset + 50), "", Fonts.Arial12Bold, Color.White);
            FreighterConstructingLabel = new UILabel(new Vector2(win.X + 150, titleOffset + 70), "", Fonts.Arial12Bold, Color.White);

            UIList utilizationData = AddList(new(win.X + 5f, win.Y + 40));
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
        }

        public override void PerformLayout()
        {
            const int utilColX = 10, utilColW = 150, buildBtnW = 130;
            UtilizationBar.SetRect(new Rectangle((int)Pos.X + utilColX, (int)Pos.Y+65, utilColW, 18));
            // Ludoal fork (maintainer feedback): the Build Freighter button centred on the util column
            BuildFreighter.Pos = new Vector2(Pos.X + utilColX + (utilColW - buildBtnW) / 2, Pos.Y + 135);
            BuildFreighter.SetAbsSize(buildBtnW, 24);
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
            DrawLine(new Vector2(Pos.X + 180, Pos.Y + 35), new Vector2(Pos.X + 180, Pos.Y + Height - 10), Color.Wheat, 2);
        }

        public override bool HandleInput(InputState input)
        {
            if (!IsOpen || HiddenByGroundCombat)
                return false;

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
                TotalFreighters = Player.TotalFreighters;
                float totalUtilizedCargo = 0;
                foreach (GoodsUtilization goodsUtilization in GoodsUtilizationMap.Values)
                    goodsUtilization.Reset();


                foreach (Planet planet in Player.GetPlanets())
                {
                    if (Player.NonCybernetic)
                    {
                        if (planet.FoodImportSlots > 0) GoodsUtilizationMap[Goods.Food].IncreaseNumImportingPlanets();
                        if (planet.FoodExportSlots > 0) GoodsUtilizationMap[Goods.Food].IncreaseNumExportingPlanets();
                    }

                    if (planet.ProdImportSlots > 0)      GoodsUtilizationMap[Goods.Production].IncreaseNumImportingPlanets();
                    if (planet.ProdExportSlots > 0)      GoodsUtilizationMap[Goods.Production].IncreaseNumExportingPlanets();
                    if (planet.ColonistsImportSlots > 0) GoodsUtilizationMap[Goods.Colonists].IncreaseNumImportingPlanets();
                    if (planet.ColonistsExportSlots > 0) GoodsUtilizationMap[Goods.Colonists].IncreaseNumExportingPlanets();
                }

                var allUtilizedFreightesr = Player.OwnedShips.Filter(s => s.IsFreighter && s.AI.State == AI.AIState.SystemTrader);
                NumUtilizedFreighters = allUtilizedFreightesr.Length;
                foreach (Ship freighter in allUtilizedFreightesr)
                {
                    if (Player.NonCybernetic)
                        GoodsUtilizationMap[Goods.Food].AddGoodsTransported(freighter, ref totalUtilizedCargo);

                    GoodsUtilizationMap[Goods.Production].AddGoodsTransported(freighter, ref totalUtilizedCargo);
                    GoodsUtilizationMap[Goods.Colonists].AddGoodsTransported(freighter, ref totalUtilizedCargo);
                }

                TotalUtilizedCargo = totalUtilizedCargo;
                UtilizationBar.Progress = TotalFreighters == 0 ? 0 : (float)NumUtilizedFreighters/TotalFreighters*100;
                FreighterConstructingLabel.Text = Player.FreightersBeingBuilt.String();
                NumIdleFreightersLabel.Text = (TotalFreighters - NumUtilizedFreighters).String();

                // the totals row (maintainer bench 339): all OPERATIONAL freighters, split by their
                // CURRENT phase so importing + exporting == the total. A freighter delivering counts
                // as importing; picking up or hauling, as exporting. (The per-goods Importing/
                // Exporting columns count PLANET slots, unrelated to the freighter count.)
                int importingFreighters = 0, exportingFreighters = 0;
                foreach (Ship freighter in allUtilizedFreightesr)
                {
                    if (freighter.AI.IsDeliveringTrade) importingFreighters++;
                    else                                exportingFreighters++;
                }
                TotalFreightersValue.Text = NumUtilizedFreighters.String();
                TotalImportingValue.Text  = importingFreighters.String();
                TotalExportingValue.Text  = exportingFreighters.String();
            }

            base.Update(fixedDeltaTime);
        }

        void OnBuildFreighterClick(UIButton b)
        {
            Player.AI.AddGoalAndEvaluate(new IncreaseFreighters(Player));
            FreighterConstructingLabel.Text = Player.FreightersBeingBuilt.String();
        }

        class GoodsUtilization : UIElementV2
        {
            readonly ProgressBar UtilizationBar;
            readonly UILabel NumFreightersLabel;
            readonly UILabel NumImportingLabel;
            readonly UILabel NumExportingLabel;
            readonly UIPanel IconPanel;
            readonly FreighterUtilizationWindow Window;
            readonly Goods Goods;
            public int NumImportingPlanets { get; private set; }
            public int NumExportingPlanets { get; private set; }
            public int NumFreighters { get; private set; }
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
                LayoutRightAligned(NumFreightersLabel, Window.FreightersRightX, Pos.Y);
                LayoutRightAligned(NumImportingLabel,  Window.ImportingRightX, Pos.Y);
                LayoutRightAligned(NumExportingLabel,  Window.ExportingRightX, Pos.Y);
                base.PerformLayout();
            }

            static void LayoutRightAligned(UILabel label, float rightX, float y)
            {
                label.TextAlign = TextAlign.Right;
                label.Pos = new Vector2(rightX - NumberColW, y);
                label.Width = NumberColW;
                label.PerformLayout();
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
                NumExportingLabel.Color = Color.White;
                NumImportingLabel.Color = Color.White;
                if (NumExportingPlanets == 0 && NumImportingPlanets > 0 && GoodsTransported <= 0)
                {
                    NumExportingLabel.Color = Color.Red;
                    NumImportingLabel.Color = Color.Yellow;
                }
                else if (NumImportingPlanets > NumExportingPlanets)
                {
                    NumExportingLabel.Color = Color.Yellow;
                    NumImportingLabel.Color = Color.Yellow;
                }

                if (NumImportingPlanets > 0 && NumFreighters < NumImportingPlanets)
                    NumFreightersLabel.Color = NumFreighters == 0 ? Color.Red : Color.Yellow;
                else
                    NumFreightersLabel.Color = NumFreighters > 0 ? Color.White : Color.Wheat;
            }

            public override void Update(float fixedDeltaTime)
            {
                TotalEmpireUtilizedCargo = Window.TotalUtilizedCargo;
                UtilizationBar.Progress  = TotalEmpireUtilizedCargo == 0 ? 0 : GoodsTransported/TotalEmpireUtilizedCargo *100;
                NumFreightersLabel.Text  = NumFreighters.String();
                NumImportingLabel.Text   = NumImportingPlanets.String();
                NumExportingLabel.Text   = NumExportingPlanets.String();
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
                GoodsTransported    = 0;
                TotalEmpireUtilizedCargo      = 0;
            }
        }
    }
}
