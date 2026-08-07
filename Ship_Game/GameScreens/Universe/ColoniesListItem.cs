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
        public Rectangle GrowthRect; // Ludoal fork (bench 339): population growth per turn
        public Rectangle FoodRect;
        public Rectangle ProdRect;
        public Rectangle ResRect;
        public Rectangle MoneyRect;
        public Rectangle FertRect;   // Ludoal fork (wishlist): fertility / richness columns
        public Rectangle RichRect;

        AssignLaborComponent AssignLabor;

        ProgressBar FoodStorage;
        ProgressBar ProdStorage;
        Rectangle ApplyProductionRect;
        Rectangle CancelProductionRect;
        DropDownMenu FoodDropDown;
        DropDownMenu ProdDropDown;
        Rectangle FoodStorageIcon;
        Rectangle ProdStorageIcon;
        int NumShipsInQueue;
        int NumBuildingsInQueue;
        int NumTroopsInQueue;
        int TotalProdNeeded;

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
            // the shared table charte owns the columns (maintainer bench 293) - the row
            // reads its bands off them and only keeps its own vertical quirks (the labor
            // slider block runs taller than the row)
            UITable.Column[] cols = Screen.Table.Columns;
            Rectangle Band(int i) => new Rectangle(cols[i].Rect.X, y, cols[i].Rect.Width, Rect.Height);
            SysNameRect    = Band(0);
            PlanetNameRect = Band(1);
            // Ludoal fork (bench 339): a Pop Growth column sits between Pop (4) and Food, so every
            // band from Food onward is one index later than before.
            FertRect    = Band(2);
            RichRect    = Band(3);
            PopRect     = Band(4);
            GrowthRect  = Band(5);
            FoodRect    = Band(6);
            ProdRect    = Band(7);
            MoneyRect   = Band(8); // money before research, the top bar's order (bench 294)
            ResRect     = Band(9);
            SliderRect  = new Rectangle(cols[10].Rect.X + 4, y - 30, cols[10].Rect.Width - 8, Rect.Height + 25);
            // maintainer bench 339: the Storage content starts 5px further left (its whole content
            // is placed off StorageRect.X, so shifting the rect shifts all of it at once)
            StorageRect = Band(11);
            StorageRect.X -= 5;
            QueueRect   = Band(12);

            if (AssignLabor == null)
            {
                AssignLabor = Add(new AssignLaborComponent(P, new RectF(SliderRect), useTitleFrame: false));
            }
            else
                AssignLabor.Rect = SliderRect;

            FoodStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, StorageRect.Y + (int)(0.25 * StorageRect.Height), (int)(0.4f * StorageRect.Width), 18))
            {
                Max = P.Storage.Max,
                Progress = P.FoodHere,
                color = "green"
            };

            // a quarter, not a fifth: Import and Export overflowed the fifth
            // (maintainer bench 296)
            int ddwidth = (int)(0.25f * StorageRect.Width);
            FoodDropDown = new DropDownMenu(new Rectangle(StorageRect.X + 50 + (int)(0.4f * StorageRect.Width) + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            FoodDropDown.AddOption(Localizer.Token(GameText.Store));
            FoodDropDown.AddOption(Localizer.Token(GameText.Import));
            FoodDropDown.AddOption(Localizer.Token(GameText.Export));
            FoodDropDown.ActiveIndex = (int)P.FS;
            FoodStorageIcon = new Rectangle(StorageRect.X + 20, FoodStorage.pBar.Y + FoodStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_food").Height / 2, ResourceManager.Texture("NewUI/icon_food").Width, ResourceManager.Texture("NewUI/icon_food").Height);
            ProdStorage = new ProgressBar(new Rectangle(StorageRect.X + 50, FoodStorage.pBar.Y + FoodStorage.pBar.Height + 10, (int)(0.4f * StorageRect.Width), 18))
            {
                Max = P.Storage.Max,
                Progress = P.ProdHere
            };
            ProdStorageIcon = new Rectangle(StorageRect.X + 20, ProdStorage.pBar.Y + ProdStorage.pBar.Height / 2 - ResourceManager.Texture("NewUI/icon_production").Height / 2, ResourceManager.Texture("NewUI/icon_production").Width, ResourceManager.Texture("NewUI/icon_production").Height);
            ProdDropDown = new DropDownMenu(new Rectangle(StorageRect.X + 50 + (int)(0.4f * StorageRect.Width) + 20, ProdStorage.pBar.Y + FoodStorage.pBar.Height / 2 - 9, ddwidth, 18));
            ProdDropDown.AddOption(Localizer.Token(GameText.Store));
            ProdDropDown.AddOption(Localizer.Token(GameText.Import));
            ProdDropDown.AddOption(Localizer.Token(GameText.Export));
            ProdDropDown.ActiveIndex = (int)P.PS;
            // on the PROGRESS BAR's line, not the name's (maintainer bench 299) - the item
            // name gets the column's full width. The bar sits one bold line under the name
            // anchor, which itself rides at mid-height - 30 (QueueItem.DrawAt).
            int iconY = QueueRect.Y + QueueRect.Height / 2 - 30 + Fonts.Arial12Bold.LineSpacing + 3;
            ApplyProductionRect = new Rectangle(QueueRect.X + QueueRect.Width - 50, iconY, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Width, ResourceManager.Texture("NewUI/icon_queue_rushconstruction").Height);
            CancelProductionRect = new Rectangle(QueueRect.X + QueueRect.Width - 20, iconY, ResourceManager.Texture("NewUI/icon_queue_delete").Width, ResourceManager.Texture("NewUI/icon_queue_delete").Height);
            UpdateQueueItemsList();

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
                            UpdateQueueItemsList();
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

                if (P.NonCybernetic && FoodDropDown.r.HitTest(input.CursorPosition))
                {
                    GameAudio.AcceptClick();
                    FoodDropDown.Toggle();
                    Universe.RunOnSimThread(() =>
                    {
                        P.FS = (Planet.GoodState)((int)P.FS + (int)Planet.GoodState.IMPORT);
                        if (P.FS > Planet.GoodState.EXPORT)
                            P.FS = Planet.GoodState.STORE;
                    });
                    return true;
                }

                if (ProdDropDown.r.HitTest(input.CursorPosition))
                {
                    GameAudio.AcceptClick();
                    ProdDropDown.Toggle();
                    Universe.RunOnSimThread(() =>
                    {
                        P.PS = (Planet.GoodState)((int)P.PS + (int)Planet.GoodState.IMPORT);
                        if (P.PS > Planet.GoodState.EXPORT)
                            P.PS = Planet.GoodState.STORE;
                    });
                    return true;
                }
            }
            return base.HandleInput(input);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ProdStorage.Progress = P.ProdHere;
            FoodStorage.Progress = P.FoodHere;
            // charte (bench 293): the zebra fill and the per-row border are gone - the
            // shared chrome delimits; only the SELECTED colony keeps its marker fill
            if (P == Screen.SelectedPlanet)
            {
                batch.FillRectangle(Rect, new Color(118, 102, 67, 50).Premultiplied());
            }

            Color TextColor = Colors.Cream;
            // System in the regular body face, from the left (maintainer bench 293)
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
                batch.DrawString(Fonts.Arial12, P.Blueprints.Name, new Vector2(planetIconRect.X + planetIconRect.Width+10, planetIconRect.Bottom+5), color);
                batch.Draw(ResourceManager.Texture("NewUI/blueprints"), 
                    new Vector2(planetIconRect.X+2, planetIconRect.Bottom), new Vector2(25, 25), color);
            }

            // every stat with a fixed decimal - right-aligned + constant fraction =
            // aligned on the point (maintainer bench 293); population reads "x / y"
            // like the Planets tab, Max Pop merged in (bench 294)
            // the value the cell SAYS is the value that picks the colour: -0.04 rounds to
            // "0.0" at one decimal, so it must neither read "-0.0" nor wear pink (bench 305)
            float R1(float v) => Math.Abs(v) < 0.05f ? 0f : v;
            string F1(float v) => R1(v).ToString("0.0", CultureInfo.InvariantCulture);
            string F2(float v) => (Math.Abs(v) < 0.005f ? 0f : v).ToString("0.00", CultureInfo.InvariantCulture);
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
            // maintainer bench 339: a ".0" on the whole numbers of "x / y" so a mixed row does not
            // jump between integer and decimal widths (a first step toward aligning on the slash).
            popString = OneDecimalEachSide(popString);
            DrawStatValue(batch, PopRect, popString, Color.White);
            // Ludoal fork (bench 339): population growth per turn, in billions, between Pop and Food
            DrawStatValue(batch, GrowthRect, F2(P.EstimatedPopGrowthPerTurn / 1000f),
                          P.EstimatedPopGrowthPerTurn > 0.5f ? Color.LightGreen : Color.Gray);
            DrawStatValue(batch, FoodRect, F1(P.Food.NetIncome), R1(P.Food.NetIncome) >= 0f ? Color.White : Color.LightPink);
            DrawStatValue(batch, ProdRect, F1(P.Prod.NetIncome), R1(P.Prod.NetIncome) >= 0f ? Color.White : Color.LightPink);
            DrawStatValue(batch, ResRect, F1(P.Res.NetIncome), Color.White);
            DrawStatValue(batch, MoneyRect, F1(P.Money.NetRevenue), R1(P.Money.NetRevenue) >= 0f ? Color.White : Color.LightPink);

            // Ludoal fork (wishlist): fertility (env-adjusted, tinted by racial multiplier), richness
            float envMult = Universe.Player.PlayerEnvModifier(P.Category);
            Color fertColor = envMult.AlmostEqual(1) ? Color.White : envMult < 1f ? Color.LightPink : Color.LightGreen;
            DrawStatValue(batch, FertRect, F1(P.FertilityFor(Universe.Player)), fertColor);
            DrawStatValue(batch, RichRect, F1(P.MineralRichness), Color.White);

            // two lines like the Planets tab (bench 294): the name in 14, the class with
            // its richness word under it in gray
            var namePos = new Vector2(planetIconRect.X + planetIconRect.Width + 10,
                                      SysNameRect.Y + SysNameRect.Height / 2 - (Fonts.Arial14Bold.LineSpacing + Fonts.Arial12.LineSpacing + 2) / 2);
            batch.DrawString(Fonts.Arial14Bold, P.Name, namePos, TextColor);
            namePos.Y += Fonts.Arial14Bold.LineSpacing + 2;
            string cls = P.LocalizedRichness;
            int clsPar = cls.IndexOf(" (");
            if (clsPar >= 0) cls = cls.Substring(0, clsPar);
            batch.DrawString(Fonts.Arial12, cls, namePos, Color.Gray);
            // the environment multiplier rides the class line, like the Planets tab
            // (maintainer bench 296)
            if (!envMult.AlmostEqual(1))
                batch.DrawString(Fonts.Arial8Bold, $" (x {envMult.String(2)})",
                                 new Vector2(namePos.X + Fonts.Arial12.MeasureString(cls).X + 5, namePos.Y + 2),
                                 envMult < 1f ? Color.Pink : Color.LightGreen);

            base.Draw(batch, elapsed);

            DrawStorage(batch);

            // Snapshot under lock: the sim thread mutates the live queue while we draw.
            QueueItem[] queue = P.ConstructionQueueSnapshot;
            if (queue.Length > 0)
            {
                QueueItem qi = queue[0];
                qi.DrawAt(P.Universe, batch, new Vector2(QueueRect.X + 10, QueueRect.Y + QueueRect.Height / 2 - 30));
                batch.Draw((ApplyProdHover ? ResourceManager.Texture("NewUI/icon_queue_rushconstruction_hover1") : ResourceManager.Texture("NewUI/icon_queue_rushconstruction")), ApplyProductionRect, Color.White);
                batch.Draw((CancelProdHover ? ResourceManager.Texture("NewUI/icon_queue_delete_hover1") : ResourceManager.Texture("NewUI/icon_queue_delete")), CancelProductionRect, Color.Red); // destruction reads red (bench 305)
                DrawQueueStats(batch, queue.Length);
            }
        }

        void DrawQueueStats(SpriteBatch batch, int queueCount)
        {
            if (queueCount < 2)
                return;

            string stats = $"In Queue ({queueCount}):";
            if (NumShipsInQueue > 0)
                stats = $"{stats} ships ({NumShipsInQueue}),";

            if (NumBuildingsInQueue > 0)
                stats = $"{stats} buildings ({NumBuildingsInQueue}),";

            if (NumTroopsInQueue > 0)
                stats = $"{stats} Troops ({NumTroopsInQueue}),";

            stats   = stats.TrimEnd(',');
            stats   = $"{stats}. Total: {TotalProdNeeded}";
            var pos = new Vector2(QueueRect.X + 10, QueueRect.Y + QueueRect.Height / 2 + 15);

            Graphics.Font font = Fonts.Arial12;
            batch.DrawString(font, stats, pos, Color.Gray);
        }

        void DrawStorage(SpriteBatch batch)
        {
            if (P.IsCybernetic)
            {
                FoodStorage.DrawGrayed(batch);
                FoodDropDown.DrawGrayed(batch);
            }
            else
            {
                FoodStorage.Draw(batch);
                FoodDropDown.Draw(batch);
            }

            ProdStorage.Draw(batch);
            ProdDropDown.Draw(batch);
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

        void UpdateQueueItemsList()
        {
            // Snapshot once under lock; the live queue is mutated on the sim thread.
            QueueItem[] queue = P.ConstructionQueueSnapshot;
            if (queue.Length < 2)
                return;

            NumShipsInQueue     = queue.Count(q => q.isShip);
            NumBuildingsInQueue = queue.Count(q => q.isBuilding);
            NumTroopsInQueue    = queue.Count(q => q.isTroop);
            TotalProdNeeded     = (int)queue.Sum(q => q.ProductionNeeded);
        }
    }
}
