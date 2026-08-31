using System;
using System.Globalization;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.UI; // UITable: the shared table charte
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public sealed class ColoniesListItem : ScrollListItem<ColoniesListItem>
    {
        readonly EmpireManagementScreen Screen;
        readonly UniverseScreen Universe;

        public Planet P;
        public Rectangle SysNameRect;
        public Rectangle PlanetNameRect;
        public Rectangle SliderRect;
        public Rectangle StorageRect;
        public Rectangle QueueRect;
        public Rectangle PopRect;
        public Rectangle GrowthRect; // Ludoal fork: population growth per turn
        public Rectangle FoodRect;
        public Rectangle ProdRect;
        public Rectangle ResRect;
        public Rectangle MoneyRect;
        public Rectangle GovernorRect; // governor type, wide displays only
        public Rectangle FertRect;   // Ludoal fork: fertility / richness columns
        public Rectangle RichRect;

        AssignLaborComponent AssignLabor;

        ProgressBar FoodStorage;
        ProgressBar ProdStorage;
        ProgressBar PopStorage; // bench 427: the population bar joins the Supply column
        Rectangle ApplyProductionRect;
        Rectangle CancelProductionRect;
        // Policies phase 0: real dropdowns replace the click-rotation relics
        DropOptions<Planet.GoodState> FoodDropDown;
        DropOptions<Planet.GoodState> ProdDropDown;
        DropOptions<Planet.GoodState> PopDropDown;
        Rectangle FoodStorageIcon;
        Rectangle ProdStorageIcon;
        Rectangle PopStorageIcon;
        Rectangle SpacePortIconRect; // wide displays: capability icons right-aligned in the Planet cell
        Rectangle TroopIconRect;

        bool ApplyProdHover;
        bool CancelProdHover;

        public ColoniesListItem(EmpireManagementScreen screen, Planet planet)
        {
            Screen = screen;
            Universe = screen.Universe;
            P = planet;

            //UIList columns = Add(new UIList());
            //foreach (int columnWidth in new [] { 200, 200, 30, 30, 30, 30, 30, 375, 375 } )
            //{
            //    columns.Add(new UIPanel(0, 0, columnWidth, 80)).Border = ;
            //}
            //columns.PerformLayout();
        }

        public override void PerformLayout()
        {
            int y = (int)Y;

            P.UpdateIncomes();
            // the shared table charte owns the columns - the row reads its bands off them
            // and only keeps its own vertical quirks (the labor slider block runs taller than the row)
            UITable.Column[] cols = Screen.Table.Columns;
            Rectangle Band(int i) => new Rectangle(cols[i].Rect.X, y, cols[i].Rect.Width, Rect.Height);
            SysNameRect    = Band(0);
            PlanetNameRect = Band(1);
            // two regimes (bench 408): wide displays (14 columns) carry Pop Growth between
            // Pop and Food, and Governor ahead of Labor; the bands shift with them
            bool wideCols = cols.Length >= 14;
            int g = wideCols ? 1 : 0;
            FertRect    = Band(2);
            RichRect    = Band(3);
            PopRect     = Band(4);
            GrowthRect  = wideCols ? Band(5) : default;
            FoodRect    = Band(5 + g);
            ProdRect    = Band(6 + g);
            MoneyRect   = Band(7 + g); // money before research, the top bar's order
            ResRect     = Band(8 + g);
            GovernorRect = wideCols ? Band(10) : default;
            int g2 = wideCols ? 2 : 0;
            SliderRect  = new Rectangle(cols[9 + g2].Rect.X + 4, y - 30, cols[9 + g2].Rect.Width - 8, Rect.Height + 25);
            // the Storage content starts 5px further left (its whole content
            // is placed off StorageRect.X, so shifting the rect shifts all of it at once)
            StorageRect = Band(10 + g2);
            StorageRect.X -= 5;
            QueueRect   = Band(11 + g2);

            if (AssignLabor == null)
            {
                AssignLabor = Add(new AssignLaborComponent(P, new RectF(SliderRect), useTitleFrame: false, showMaxValue: wideCols, maxSliderRatio: 0.42f));
            }
            else
                AssignLabor.Rect = SliderRect;

            // bench 427: the two elders tighten and rise so the population bar seats third
            FoodStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, StorageRect.Y + (int)(0.12 * StorageRect.Height) - 2, (int)(0.4f * StorageRect.Width), 18))
            {
                Max = P.Storage.Max,
                Progress = P.FoodHere,
                color = "green"
            };

            // bench 455: .30 - the real dropdown's 10px text indent plus the arrow made
            // Export truncate at a quarter. bench 458: Resettle still clipped - the box
            // eats 14px of its LEFT margin (the bar-to-box gap shrinks 20 to 6), the
            // right edge does not move so the column layout holds.
            int ddwidth = (int)(0.30f * StorageRect.Width) + 14;
            int ddX = StorageRect.X + 50 + (int)(0.4f * StorageRect.Width) + 6;
            FoodDropDown = new DropOptions<Planet.GoodState>(new Rectangle(ddX, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            FoodDropDown.AddOption(GameText.Store, Planet.GoodState.STORE);
            FoodDropDown.AddOption(GameText.Import, Planet.GoodState.IMPORT);
            FoodDropDown.AddOption(GameText.Export, Planet.GoodState.EXPORT);
            FoodDropDown.ActiveIndex = (int)P.FS;
            FoodDropDown.OnValueChange = v => Universe.RunOnSimThread(() => P.FS = v); // mutated on the sim thread like before
            FoodStorageIcon = new Rectangle(StorageRect.X + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_food").Height / 2, ResourceManager.Texture("NewUI/icon_food").Width, ResourceManager.Texture("NewUI/icon_food").Height);
            ProdStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, FoodStorage.pBar.Y + FoodStorage.pBar.Height + 6, (int)(0.4f * StorageRect.Width), 18))
            {
                Max = P.Storage.Max,
                Progress = P.ProdHere
            };
            ProdStorageIcon = new Rectangle(StorageRect.X + 20, ProdStorage.pBar.Y + ProdStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_production").Height / 2, ResourceManager.Texture("NewUI/icon_production").Width, ResourceManager.Texture("NewUI/icon_production").Height);
            ProdDropDown = new DropOptions<Planet.GoodState>(new Rectangle(ddX, ProdStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            ProdDropDown.AddOption(GameText.Store, Planet.GoodState.STORE);
            ProdDropDown.AddOption(GameText.Import, Planet.GoodState.IMPORT);
            ProdDropDown.AddOption(GameText.Export, Planet.GoodState.EXPORT);
            ProdDropDown.ActiveIndex = (int)P.PS;
            ProdDropDown.OnValueChange = v => Universe.RunOnSimThread(() => P.PS = v);
            PopStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, ProdStorage.pBar.Y + ProdStorage.pBar.Height + 6, (int)(0.4f * StorageRect.Width), 18))
            {
                Max = P.MaxPopulationBillionFor(P.Owner),
                Progress = P.PopulationBillion,
                color = "blue",
                FillTint = ColonyScreen.PopBarTint, // one tint, both screens
            };
            PopDropDown = new DropOptions<Planet.GoodState>(new Rectangle(ddX, PopStorage.pBar.Y + PopStorage.pBar.Height / 2 - 9, ddwidth, 18));
            // auto-supplies: QUI decides lives on the colony screen's Auto checkbox; here
            // the list mirrors it - three people-words, greyed live pick while Auto holds
            PopDropDown.AddOption(GameText.Stay, Planet.GoodState.STORE);
            PopDropDown.AddOption(GameText.BringIn, Planet.GoodState.IMPORT);
            PopDropDown.AddOption(GameText.Resettle, Planet.GoodState.EXPORT);
            PopDropDown.ActiveIndex = (int)(P.ColonistsManual ? P.CS : P.GetGoodState(Goods.Colonists));
            PopDropDown.OnValueChange = v => Universe.RunOnSimThread(() => P.CS = v);
            PopStorageIcon = new Rectangle(StorageRect.X + 20, PopStorage.pBar.Y + PopStorage.pBar.Height / 2 - ResourceManager.Texture("UI/icon_pop_22").Height / 2, ResourceManager.Texture("UI/icon_pop_22").Width, ResourceManager.Texture("UI/icon_pop_22").Height);
            // on the PROGRESS BAR's line, not the name's - the item name gets the column's
            // full width. The bar sits one bold line under the name anchor, which itself
            // rides at mid-height - 30 (QueueItem.DrawAt).
            int iconY = QueueRect.Y + QueueRect.Height / 2 - 30 + Fonts.Arial12Bold.LineSpacing + 3;
            ApplyProductionRect = new Rectangle(QueueRect.X + QueueRect.Width - 50, iconY, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Width, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Height);
            CancelProductionRect = new Rectangle(QueueRect.X + QueueRect.Width - 20, iconY, ResourceManager.Texture("NewUI/icon_queue_delete").Width, ResourceManager.Texture("NewUI/icon_queue_delete").Height);

            base.PerformLayout();
        }

        void DrawStatValue(SpriteBatch batch, Rectangle rect, string value, Color color)
        {
            var cursor = new Vector2(rect.X + rect.Width - UITable.PadX - Fonts.Arial12.MeasureString(value).X,
                                     PlanetNameRect.Y + PlanetNameRect.Height / 2 - Fonts.Arial12.LineSpacing / 2).ToFloored();
            batch.DrawString(Fonts.Arial12, value, cursor, color);
        }

        public override bool HandleInput(InputState input)
        {
            P.UpdateIncomes();

            ApplyProdHover  = ApplyProductionRect.HitTest(input.CursorPosition);
            CancelProdHover = CancelProductionRect.HitTest(input.CursorPosition);

            if (ApplyProductionRect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.ClickToRushProductionFrom);

            if (CancelProductionRect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.CancelProductionAndRemoveThis);

            // capability icons name the building that unlocks them (empty rects never hit)
            if (SpacePortIconRect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.ColonyCanBuildShipsTip);
            if (TroopIconRect.HitTest(input.CursorPosition))
                ToolTip.CreateTooltip(GameText.ColonyCanBuildTroopsTip);

            if (input.LeftMouseClick)
            {
                if (CancelProdHover && P.IsConstructing)
                {
                    Screen.Universe.RunOnSimThread(() =>
                    {
                        QueueItem item = P.Construction.GetConstructionQueue()[0];
                        if (!item.IsComplete)
                        {
                            P.Construction.Cancel(item);
                            GameAudio.AcceptClick();
                        }
                        else
                        {
                            GameAudio.NegativeClick();
                            Log.Warning($"Deferred Action: Cancel Queue Item: Failed at index 0");
                        }
                        GameAudio.AcceptClick();
                    });
                }

                if (ApplyProdHover && P.IsConstructing)
                {
                    float maxAmount = input.IsCtrlKeyDown ? 10000f : 10f;
                    Universe.RunOnSimThread(() =>
                    {
                        bool hasValidConstruction = P.Construction.NotEmpty && !P.ConstructionQueue[0].IsComplete;
                        if (input.IsShiftKeyDown)
                        {
                            P.ConstructionQueue[0].Rush = !P.ConstructionQueue[0].Rush;
                            return;
                        }
                        if (hasValidConstruction && P.Construction.RushProduction(0, maxAmount, rushButton: true))
                        {
                            GameAudio.AcceptClick();
                        }
                        else
                        {
                            if (!hasValidConstruction)
                                Log.Warning($"Deferred Action: ColonyListItem: Rush failed");
                            GameAudio.NegativeClick();
                        }
                    });

                    return true;
                }

                // Policies phase 0: the real dropdowns answer for themselves - ReadOnly
                // (set at draw from the Auto flags) refuses their input, OnValueChange
                // mutates on the sim thread. bench 458: only CLOSED lists open from here -
                // an OPEN list is fed by the screen (HandleOpenLists) before the table,
                // so it hears clicks landing outside this row; handling it twice in one
                // frame would close-then-reopen on a title click.
                if (!FoodDropDown.Open && FoodDropDown.HandleInput(input))
                    return true;
                if (!ProdDropDown.Open && ProdDropDown.HandleInput(input))
                    return true;
                if (!PopDropDown.Open && PopDropDown.HandleInput(input))
                    return true;
            }
            return base.HandleInput(input);
        }

        // bench 458: the scroll list only routes input to rows under the cursor - the
        // screen calls this FIRST every frame so an open supply list behaves like the
        // Colony governor list (a real child there): entry hover tracks the mouse, a
        // click elsewhere folds it without changing the value (DropOptions handles both).
        public bool HandleOpenLists(InputState input)
        {
            if (FoodDropDown == null) // row never laid out - see DrawOpenLists
                return false;
            if (FoodDropDown.Open) return FoodDropDown.HandleInput(input);
            if (ProdDropDown.Open) return ProdDropDown.HandleInput(input);
            if (PopDropDown.Open)  return PopDropDown.HandleInput(input);
            return false;
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ProdStorage.Progress = P.ProdHere;
            FoodStorage.Progress = P.FoodHere;
            // the shared chrome delimits rows; only the SELECTED colony keeps a marker fill
            if (P == Screen.SelectedPlanet)
            {
                batch.FillRectangle(Rect, new Color(118, 102, 67, 50).Premultiplied());
            }

            Color TextColor = Colors.Cream;
            // System name in the regular body face, from the left
            var SysNameCursor = new Vector2(SysNameRect.X + UITable.PadX,
                                            SysNameRect.Y + SysNameRect.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2);
            batch.DrawString(Fonts.Arial12Bold, P.System.Name, SysNameCursor, TextColor);
            Rectangle planetIconRect = new Rectangle(PlanetNameRect.X + 5, PlanetNameRect.Y + 25, PlanetNameRect.Height - 50, PlanetNameRect.Height - 50);
            batch.Draw(P.PlanetTexture, planetIconRect, Color.White);
            if (P.PrioritizedPort)
            {
                batch.DrawString(Fonts.Arial12, GameText.PrioritizedPort,
                    new Vector2(planetIconRect.X + planetIconRect.Width + 10, planetIconRect.Top - 22), Screen.ApplyCurrentAlphaToColor(Color.Purple));
            }

            if (P.HasBlueprints) 
            {
                var color = BlueprintsScreen.GetBlueprintsIconColor(P.Blueprints.ColonyType);
                var namePos = new Vector2(planetIconRect.X + planetIconRect.Width+10, planetIconRect.Bottom+5);
                batch.DrawString(Fonts.Arial12, P.Blueprints.Name, namePos, color);

                // Ludoal fork (maintainer feedback): how far the plan got, in buildings rather than
                // percent. Pink when everything reachable here is up and the list still is not -
                // the colony is waiting on something it cannot fix; green when an exclusive plan
                // has the whole list up and no successor, which is its finished state.
                string ratio = $" ({P.Blueprints.BuiltCount}/{P.Blueprints.PlannedCount})";
                Color ratioColor = P.Blueprints.FinalState ? Color.LightGreen
                                 : P.Blueprints.Blocked ? Color.HotPink
                                 : Color.White;
                var ratioPos = new Vector2(namePos.X + Fonts.Arial12.MeasureString(P.Blueprints.Name).X, namePos.Y);
                batch.DrawString(Fonts.Arial12, ratio, ratioPos, ratioColor);
                batch.Draw(ResourceManager.Texture("NewUI/blueprints"), 
                    new Vector2(planetIconRect.X+2, planetIconRect.Bottom), new Vector2(25, 25), color);
            }

            // every stat with a fixed decimal - right-aligned + constant fraction =
            // aligned on the point; population reads "x / y" like the Planets tab
            // the value the cell SAYS is the value that picks the colour: -0.04 rounds to
            // "0.0" at one decimal, so it must neither read "-0.0" nor wear pink
            float R1(float v) => Math.Abs(v) < 0.05f ? 0f : v;
            string F1(float v) => R1(v).ToString("0.0", CultureInfo.InvariantCulture);
            // Ludoal fork: Pop Growth reads in millions with one decimal
            string F2(float v) => (Math.Abs(v) < 0.05f ? 0f : v).ToString("0.0", CultureInfo.InvariantCulture);
            // give each number of an "a / b" string one decimal place (12 -> 12.0), leaving text as-is
            string OneDecimalEachSide(string s)
            {
                string[] parts = s.Split('/');
                for (int i = 0; i < parts.Length; ++i)
                {
                    string t = parts[i].Trim();
                    if (float.TryParse(t, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
                        parts[i] = f.ToString("0.0", CultureInfo.InvariantCulture);
                    else
                        parts[i] = t;
                }
                return string.Join(" / ", parts);
            }
            string popString = P.PopulationStringForPlayer;
            int popParen = popString.IndexOf(" (");
            if (popParen >= 0) popString = popString.Substring(0, popParen);
            // a ".0" on the whole numbers of "x / y" so a mixed row does not
            // jump between integer and decimal widths.
            popString = OneDecimalEachSide(popString);
            DrawStatValue(batch, PopRect, popString, Color.White);
            // Ludoal fork: population growth per turn in MILLIONS, one decimal. EstimatedPopGrowthPerTurn
            // is already in millions (Population is millions, /1000 = billions per MaxPopulationBillion) -
            // show the raw value; tooltip says "(millions)".
            if (GrowthRect.Width > 0) // wide displays only (bench 408)
                DrawStatValue(batch, GrowthRect, F2(P.EstimatedPopGrowthPerTurn),
                              P.EstimatedPopGrowthPerTurn > 0.5f ? Color.LightGreen : Color.Gray);
            DrawStatValue(batch, FoodRect, F1(P.Food.NetIncome), R1(P.Food.NetIncome) >= 0f ? Color.White : Color.LightPink);
            DrawStatValue(batch, ProdRect, F1(P.Prod.NetIncome), R1(P.Prod.NetIncome) >= 0f ? Color.White : Color.LightPink);
            DrawStatValue(batch, ResRect, F1(P.Res.NetIncome), Color.White);
            DrawStatValue(batch, MoneyRect, F1(P.Money.NetRevenue), R1(P.Money.NetRevenue) >= 0f ? Color.White : Color.LightPink);

            // Ludoal fork: fertility (env-adjusted, tinted by racial multiplier), richness
            float envMult = Universe.Player.PlayerEnvModifier(P.Category);
            Color fertColor = envMult.AlmostEqual(1) ? Color.White : envMult < 1f ? Color.LightPink : Color.LightGreen;
            DrawStatValue(batch, FertRect, F1(P.FertilityFor(Universe.Player)), fertColor);
            DrawStatValue(batch, RichRect, F1(P.MineralRichness), Color.White);

            // the governor type, centred, wearing the type colour the governor
            // portrait border already uses
            if (GovernorRect.Width > 0)
            {
                // one bold letter, the governor portrait's type colour (bench 407)
                string gov; Color govColor;
                switch (P.CType)
                {
                    case Planet.ColonyType.Colony:       gov = "--"; govColor = Color.Gray; break;
                    case Planet.ColonyType.TradeHub:     gov = "T";  govColor = Color.Yellow; break;
                    case Planet.ColonyType.Industrial:   gov = "I";  govColor = Color.Orange; break;
                    case Planet.ColonyType.Agricultural: gov = "A";  govColor = Color.Green; break;
                    case Planet.ColonyType.Research:     gov = "R";  govColor = Color.CornflowerBlue; break;
                    case Planet.ColonyType.Military:     gov = "M";  govColor = Color.Red; break;
                    default:                             gov = "C";  govColor = Color.White; break; // Core
                }
                var govPos = new Vector2(GovernorRect.X + (GovernorRect.Width - Fonts.Arial12Bold.MeasureString(gov).X) / 2,
                                         PlanetNameRect.Y + PlanetNameRect.Height / 2 - Fonts.Arial12Bold.LineSpacing / 2).ToFloored();
                batch.DrawString(Fonts.Arial12Bold, gov, govPos, govColor);
            }

            // two lines like the Planets tab: the name in 14, the class with
            // its richness word under it in gray
            var namePos = new Vector2(planetIconRect.X + planetIconRect.Width + 10,
                                      SysNameRect.Y + SysNameRect.Height / 2 - (Fonts.Arial14Bold.LineSpacing + Fonts.Arial12.LineSpacing + 2) / 2);
            batch.DrawString(Fonts.Arial14Bold, P.Name, namePos, TextColor);
            // wide displays only: what this colony can build - Space Port (ships) then Military
            // Outpost (troops). RIGHT-ALIGNED in the Planet cell at FIXED slots, so the icons form
            // one clean vertical column that scans at a glance even when a colony has only one of
            // the two. Absent capability = empty slot (nothing, not a greyed ghost).
            SpacePortIconRect = TroopIconRect = Rectangle.Empty;
            if (Screen.Table.Columns.Length >= 14)
            {
                const int iconSize = 18, gap = 4, edge = 6;
                int iy = (int)namePos.Y + (Fonts.Arial14Bold.LineSpacing - iconSize) / 2;
                int troopX = PlanetNameRect.Right - edge - iconSize;          // rightmost slot: troops
                int portX  = troopX - gap - iconSize;                         // left slot: space port
                if (P.HasSpacePort)
                {
                    SpacePortIconRect = new Rectangle(portX, iy, iconSize, iconSize);
                    batch.Draw(ResourceManager.Texture("Buildings/icon_spaceport_48x48"), SpacePortIconRect, TextColor);
                }
                if (P.CanBuildInfantry)
                {
                    TroopIconRect = new Rectangle(troopX, iy, iconSize, iconSize);
                    batch.Draw(ResourceManager.Texture("Buildings/icon_military_outpost_48x48"), TroopIconRect, TextColor);
                }
            }
            namePos.Y += Fonts.Arial14Bold.LineSpacing + 2;
            string cls = P.LocalizedRichness;
            int clsPar = cls.IndexOf(" (");
            if (clsPar >= 0) cls = cls.Substring(0, clsPar);
            batch.DrawString(Fonts.Arial12, cls, namePos, Color.Gray);
            // the environment multiplier rides the class line, like the Planets tab
            if (!envMult.AlmostEqual(1))
                batch.DrawString(Fonts.Arial8Bold, $" (x {envMult.String(2)})",
                                 new Vector2(namePos.X + Fonts.Arial12.MeasureString(cls).X + 5, namePos.Y + 2),
                                 envMult < 1f ? Color.Pink : Color.LightGreen);

            base.Draw(batch, elapsed);

            DrawStorage(batch, elapsed);

            // Snapshot under lock: the sim thread mutates the live queue while we draw.
            QueueItem[] queue = P.ConstructionQueueSnapshot;
            if (queue.Length > 0)
            {
                QueueItem qi = queue[0];
                qi.DrawAt(P.Universe, batch, new Vector2(QueueRect.X + 10, QueueRect.Y + QueueRect.Height / 2 - 30));
                batch.Draw((ApplyProdHover ? ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover1") : ResourceManager.Texture("NewUI/icon_queue_rushconstruction")), ApplyProductionRect, Color.White);
                batch.Draw((CancelProdHover ? ResourceManager.Texture("NewUI/icon_queue_delete_hover1") : ResourceManager.Texture("NewUI/icon_queue_delete")), CancelProductionRect, Color.Red); // destruction reads red
                DrawQueueStats(batch, queue);
            }
        }

        void DrawQueueStats(SpriteBatch batch, QueueItem[] queue)
        {
            if (queue.Length < 2)
                return;

            // Live: read the queue each frame so the summary shrinks as items build, instead of
            // freezing at the last tab-rebuild or rush-click.
            int ships     = queue.Count(q => q.isShip);
            int buildings = queue.Count(q => q.isBuilding);
            int troops    = queue.Count(q => q.isTroop);
            int totalProd = (int)queue.Sum(q => q.ProductionNeeded);

            string stats = $"Queue ({queue.Length}):";
            if (ships > 0)
                stats = $"{stats} ships ({ships}),";

            if (buildings > 0)
                stats = $"{stats} buildings ({buildings}),";

            if (troops > 0)
                stats = $"{stats} Troops ({troops}),";

            stats   = stats.TrimEnd(',');
            stats   = $"{stats}. Total: {totalProd}";
            var pos = new Vector2(QueueRect.X + 10, QueueRect.Y + QueueRect.Height / 2 + 15);

            Graphics.Font font = Fonts.Arial12;
            batch.DrawString(font, stats, pos, Color.Gray);
        }

        // Policies phase 0: an OPEN list must paint over the rows below - the screen
        // calls this after the whole table has drawn
        public void DrawOpenLists(SpriteBatch batch, DrawTimes elapsed)
        {
            // bench 458 log: the dropdowns are born in PerformLayout, which only ever
            // runs for rows that reached the screen - on a 400+ colony save the whole-
            // table sweep hit a never-laid-out row's null dropdown and died every frame
            if (FoodDropDown == null)
                return;
            if (FoodDropDown.Open) FoodDropDown.Draw(batch, elapsed);
            if (ProdDropDown.Open) ProdDropDown.Draw(batch, elapsed);
            if (PopDropDown.Open)  PopDropDown.Draw(batch, elapsed);
        }

        void DrawStorage(SpriteBatch batch, DrawTimes elapsed)
        {
            FoodDropDown.ActiveIndex = (int)P.FS;
            ProdDropDown.ActiveIndex = (int)P.PS;
            PopDropDown.ActiveIndex  = (int)(P.ColonistsManual ? P.CS : P.GetGoodState(Goods.Colonists));
            FoodDropDown.ReadOnly = P.AutoFood || P.IsCybernetic;
            ProdDropDown.ReadOnly = P.AutoProd;
            PopDropDown.ReadOnly  = P.AutoColonists;

            if (P.IsCybernetic) FoodStorage.DrawGrayed(batch);
            else                FoodStorage.Draw(batch);

            ProdStorage.Draw(batch);
            PopStorage.Max = P.MaxPopulationBillionFor(P.Owner);
            PopStorage.Progress = P.PopulationBillion;
            PopStorage.Draw(batch);

            FoodDropDown.Draw(batch, elapsed);
            ProdDropDown.Draw(batch, elapsed);
            PopDropDown.Draw(batch, elapsed);
            batch.Draw(ResourceManager.Texture("UI/icon_pop_22"), PopStorageIcon, Color.White);
            batch.Draw(ResourceManager.Texture("NewUI/icon_food"), FoodStorageIcon,
                (P.NonCybernetic ? Color.White : new Color(110, 110, 110, 255)));
            batch.Draw(ResourceManager.Texture("NewUI/icon_production"), ProdStorageIcon, Color.White);

            if (FoodStorageIcon.HitTest(Screen.Input.CursorPosition))
            {
                ToolTip.CreateTooltip(P.IsCybernetic ? GameText.YourPeopleAreCyberneticAnd
                                                       : GameText.IndicatesTheAmountOfFood);
            }

            if (ProdStorageIcon.HitTest(Screen.Input.CursorPosition))
            {
                ToolTip.CreateTooltip(GameText.IndicatesTheAmountOfProduction);
            }
        }

    }
}
