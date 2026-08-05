using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.AI.Budget;
using Ship_Game.Audio;
using Ship_Game.Commands.Goals;
using Ship_Game.Ships;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public sealed class PlanetInfoUIElement : UIElement
    {
        // Ludoal fork: the sculpted texture spent this top band on antenna machinery; with it
        // gone the frame starts this far under the housing. Anything aligned on the visible
        // frame top derives from this, not from the housing. 26 covered the machinery;
        // the rest trims the dead margin the old frame left (maintainer benches 311-313).
        // Public with SpriteBox and TopLineIconY: the star cartouche wears the same frame.
        public const int FrameShave = 61;
        public const int RightTrim  = 10; // same trim on the plate's right edge
        public const int TopLineIconY = 76; // the pop/flag line - the top text row
        public static Rectangle SpriteBox(in Rectangle housing)
            => new Rectangle(housing.X + 85, housing.Y + 128, 80, 80);
        const int LaborW = 180;    // labor block width - its rail is (W * 0.6).RoundTo10()
        int LaborX   => PlanetIconRect.Right + 30;
        // the lock/tool column: labor housing inset (+10), the rail, the lock gap (+10) -
        // the same walk AssignLaborComponent and ColonySlider take to place the locks
        int LockColX => LaborX + 10 + (LaborW * 0.6f).RoundTo10() + 10;
        Planet P;
        readonly UniverseScreen Screen;
        Empire Player => Screen.Player;
        readonly Array<TippedItem> ToolTipItems = new Array<TippedItem>();

        Rectangle MoneyRect;
        Rectangle ExoticRect;            // re-anchored per draw on the adaptive variants
        Rectangle ExoticResourceIconRect;
        Rectangle PopRect;
        readonly Selector Sel;
        int PlateTop;              // the visible frame's top - ADAPTIVE (bench 308)
        readonly SkinnableButton Inspect;
        readonly SkinnableButton Invade;
        readonly ToggleButton PrevColony; // walk the player's colony list from the cartouche
        readonly ToggleButton NextColony;
        readonly UIButton BtnSendTroops;  // the Planets page's pair, on the colonisable page
        readonly UIButton BtnColonize;
        readonly Rectangle Housing;
        readonly Rectangle DefenseRect;
        readonly Rectangle InjuryRect;
        readonly Rectangle OffenseRect;
        readonly Rectangle ShieldRect;
        readonly Rectangle DefenseShipsRect;
        readonly Rectangle RightRect;
        readonly Rectangle PlanetIconRect;

        readonly Rectangle TilesRect;
        readonly Rectangle PopPerTileRect;
        readonly Rectangle BiospheredPopRect;
        readonly Rectangle TerraformedPopRect;
        AssignLaborComponent AssignLabor;

        readonly Graphics.Font Font8  = Fonts.Arial8Bold;
        readonly Graphics.Font Font12 = Fonts.Arial12Bold;
        readonly Color ButtonTextColor   = new Color(174, 202, 255);
                                                                                  
        public PlanetInfoUIElement(in Rectangle r, ScreenManager sm, UniverseScreen screen)
        {
            Screen = screen;
            ScreenManager = sm;
            ElementRect = r;
            Sel = new Selector(r, Color.Black);
            Housing = r;
            TransitionOnTime = TimeSpan.FromSeconds(0.25);
            TransitionOffTime = TimeSpan.FromSeconds(0.25);
            var leftRect = new Rectangle(r.X, r.Y + 44, 200, r.Height - 44);
            RightRect = new Rectangle(r.X + 200, r.Y + 44, 200, r.Height - 44);
            PlanetIconRect = SpriteBox(r);
            Inspect = new SkinnableButton(new Rectangle(PlanetIconRect.CenterX() - 16, PlanetIconRect.Y, 32, 32), "UI/viewPlanetIcon")
            {
                HoverColor = tColor,
                IsToggle = false
            };
            Invade = new SkinnableButton(new Rectangle(PlanetIconRect.X + PlanetIconRect.Width / 2 - 16, PlanetIconRect.Y + 48, 32, 32), "UI/ColonizeIcon")
            {
                HoverColor = tColor,
                IsToggle = false
            };

            DefenseRect      = new Rectangle(leftRect.X + 13, Housing.Y + 122, 22, 22);
            OffenseRect      = new Rectangle(leftRect.X + 13, Housing.Y + 122 + 22, 22, 22);
            InjuryRect       = new Rectangle(leftRect.X + 13, Housing.Y + 122 + 44, 22, 22);
            ShieldRect       = new Rectangle(leftRect.X + 13, Housing.Y + 122 + 66, 22, 22);
            DefenseShipsRect = new Rectangle(leftRect.X + 13, Housing.Y + 122 + 88, 22, 22);

            // Use the same positions for unexplored planet data
            TilesRect          = DefenseRect;
            PopPerTileRect     = OffenseRect;
            BiospheredPopRect  = InjuryRect;
            TerraformedPopRect = ShieldRect;

            ExoticRect = new Rectangle(RightRect.X - 17, Housing.Y + 130, 182, 25);
            ExoticResourceIconRect = new Rectangle(RightRect.X - 17, Housing.Y + 165, 20, 20);
            // the colony arrows flank the name line, just outside the sprite column
            PrevColony = new ToggleButton(new Vector2(r.X + 40, Housing.Y + 76), ToggleButtonStyle.ArrowLeft)
            {
                Tooltip = GameText.ViewPreviousColony,
                OnClick = b => OnChangeColony(-1)
            };
            PrevColony.SetAbsSize(14, 20);
            NextColony = new ToggleButton(new Vector2(r.X + 196, Housing.Y + 76), ToggleButtonStyle.ArrowRight)
            {
                Tooltip = GameText.ViewNextColony,
                OnClick = b => OnChangeColony(+1)
            };
            NextColony.SetAbsSize(14, 20);

            // the colonisable page borrows the Planets page's buttons: same width formula
            // (PlanetListScreen sizes the slot off the widest text either can wear), 24
            // high, centred text - stacked where the colony page keeps its sliders
            int btnW = 24 + (int)new[] { "Colonize", "Cancel Colonize", "Send Troops",
                                         "Recall Troops (99)", "Invading: 99" }
                                .Max(t => Fonts.Arial12Bold.TextWidth(t));
            BtnSendTroops = new UIButton(ButtonStyle.Wide, "Send Troops")
            {
                Rect = new Rectangle(LaborX + 20, Housing.Y + 130, btnW, 24),
                Tooltip = GameText.SendAvailableTroopsToThis,
                OnClick = OnSendTroopsClicked
            };
            BtnColonize = new UIButton(ButtonStyle.WideActive, GameText.Colonize)
            {
                Rect = new Rectangle(LaborX + 20, Housing.Y + 165, btnW, 24),
                OnClick = OnColonizeClicked
            };
        }

        void OnColonizeClicked(UIButton b)
        {
            if (Player.AI.HasGoal(g => g.IsColonizationGoal(P)))
                Player.AI.CancelColonization(P);
            else
                Player.AI.AddGoalAndEvaluate(new MarkForColonization(P, Player, isManual: true));
            GameAudio.EchoAffirmative();
        }

        void OnSendTroopsClicked(UIButton b)
        {
            if (Player.GetTroopShipForRebase(out Ship troopShip, P.Position, P.Name))
            {
                if (!troopShip.AI.OrderLandAllTroops(P, clearOrders: true, Screen.Input.CursorPosition))
                    GameAudio.NegativeClick();
                else
                {
                    GameAudio.EchoAffirmative();
                    if (Player.Universe.Paused)
                        Player.Universe.Objects.UpdateLists();
                }
            }
            else
                GameAudio.BlipClick();
        }

        void UpdateColonisableButtons()
        {
            bool marked = Player.AI.HasGoal(g => g.IsColonizationGoal(P));
            BtnColonize.Text    = marked ? GameText.CancelColonize : GameText.Colonize;
            BtnColonize.Style   = marked ? ButtonStyle.WideHostile : ButtonStyle.WideActive;
            BtnColonize.Tooltip = marked ? GameText.CancelTheColonizationMissionThat
                                         : GameText.MarkThisPlanetForColonization;
            BtnColonize.OnClick = OnColonizeClicked;
            int landing = IncomingTroops;
            BtnSendTroops.Text    = landing > 0 ? $"Landing: {landing}" : "Send Troops";
            BtnSendTroops.Style   = ButtonStyle.Wide;
            BtnSendTroops.Tooltip = GameText.SendAvailableTroopsToThis;
        }

        // the enemy page wears the same pair (bench 314): same size, same seats, centred
        // text - Invade on the gold plate, Cancel Invasion in the hostile red (the
        // Colonize toggle's own convention: the action in its colour, the cancel in red)
        void UpdateEnemyButtons(int invading)
        {
            BtnSendTroops.Text    = invading > 0 ? $"Invading: {invading}" : "Invade";
            BtnSendTroops.Style   = ButtonStyle.Wide;
            BtnSendTroops.Tooltip = default;
            BtnColonize.Text    = "Cancel Invasion";
            BtnColonize.Style   = ButtonStyle.WideHostile;
            BtnColonize.Tooltip = default;
            BtnColonize.OnClick = OnCancelInvasionClicked;
        }

        void OnCancelInvasionClicked(UIButton b)
        {
            foreach (Ship ship in Player.OwnedShips)
            {
                if (ship.AI.State == AIState.AssaultPlanet && ship.AI.OrderQueue.Any(g => g.TargetPlanet == P))
                {
                    if (ship.DesignRole == RoleName.troopShip)
                        ship.AI.OrderOrbitNearest(true);
                    else
                        ship.AI.OrderRebaseToNearest();
                }
            }
            GameAudio.EchoAffirmative();
        }

        void OnChangeColony(int change)
        {
            // the colony screen's walk: the owner's colony list, wrapping at both ends
            var planets = P.Owner.GetPlanets();
            int newIndex = planets.IndexOf(P) + change;
            if (newIndex >= planets.Count) newIndex = 0;
            else if (newIndex < 0) newIndex = planets.Count - 1;
            Planet next = planets[newIndex];
            if (next != P)
            {
                Screen.SetSelectedPlanet(next);
                // the arrows also glide the camera onto the colony now displayed
                Screen.SnapViewTo(new(next.Position.X, next.Position.Y,
                    Screen.GetZfromScreenState(UniverseScreen.UnivScreenState.PlanetView)), 5f, 2f);
            }
        }

        public override void Update(UpdateTimes elapsed)
        {
            AssignLabor?.Update(elapsed.RealTime.Seconds);
            base.Update(elapsed);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            if (P == null)
                return;

            if (Screen.Debug)
                DrawDebugPlanetBudget();

            0f.SmoothStep(1f, TransitionPosition);
            ToolTipItems.Clear();
            ToolTipItems.Add(new TippedItem(PopRect, GameText.PopulationInBillionsVsMax));

            // Ludoal fork: the minimap's recipe instead of the sculpted unitselmenu texture -
            // a near-opaque flat ground and a rounded grey rule (maintainer, last reskin)
            // ⚠ the frame starts 26 under the housing's top: the sculpted texture spent that
            // band on antenna machinery, and with it gone the plate framed empty space
            // (maintainer: "beaucoup de vide au-dessus"). The housing keeps its size - every
            // inner anchor is an offset from it - only the visible frame shrinks.
            // One fixed plate for every status now, the unexplored page included
            // (maintainer bench 313) - only the content degrades down the ladder.
            bool explored = P.IsExploredBy(Player);
            PlateTop = Housing.Y + FrameShave;
            var frame = new Rectangle(Housing.X, PlateTop, Housing.Width - RightTrim, Housing.Height - FrameShave);
            Rectangle plate = frame;
            plate.Inflate(-2, -2);
            batch.FillRectangle(plate, new Color(8, 10, 14).Alpha(0.94f));
            UITheme.DrawPlate(batch, frame, Color.Transparent,
                              new Color(150, 150, 150).Alpha(0.85f), radiusOverride: 8,
                              ruleWidthOverride: 3);

            P.UpdateMaxPopulation();
            if (explored && P.Owner != null)  AddExploredTips();
            else if (explored && P.Habitable) AddUnExploredTips();

            // one grammar for every status (maintainer benches 312-313): the pop line
            // right-aligned with its flag, the name sharing its line - bottom-aligned, the
            // bigger font grows upward - and the governance on the money/research level,
            // both centred over the sprite
            string name = explored ? P.Name : Localizer.Token(GameText.Unexplored).Trim();
            // fixed 20 bold (maintainer bench 314) - the length-adaptive downsizing read as
            // random shrinking; if a name ever overflows, widen the arrows or bring the
            // adaptive back with bench-proven thresholds
            Graphics.Font nameFont = Fonts.Arial20Bold;

            int frameRight = Housing.Right - RightTrim;
            // the faction flag keeps its own right anchor ("parfaitement placé" - bench
            // 314); the pop block anchors LEFT, 20px right of the arrow, so its variable
            // width stops moving every icon column keyed on it
            var flagRect = new Rectangle(frameRight - 40, Housing.Y + TopLineIconY, 26, 26);
            Empire flagOwner = !explored ? null : P.Owner ?? (P.IsMineable ? P.Mining.Owner : null);
            if (flagOwner != null)
                batch.Draw(ResourceManager.Flag(flagOwner), flagRect, flagOwner.EmpireColor);
            float topTextY = Housing.Y + TopLineIconY + 13 - Font12.LineSpacing / 2;
            if (explored && P.Habitable)
            {
                PopRect = new Rectangle(NextColony.Rect.Right + 10, Housing.Y + TopLineIconY, 22, 22);
                batch.Draw(ResourceManager.Texture("UI/icon_pop_22"), PopRect, Color.White);
                batch.DrawString(Font12, P.PopulationStringForPlayer,
                                 new Vector2(PopRect.Right + 4, topTextY), tColor);
            }
            else
            {
                PopRect = default; // no pop line, no tooltip
            }

            int spriteCX = PlanetIconRect.CenterX();
            var namePos = new Vector2(spriteCX - nameFont.TextWidth(name) / 2f,
                                      topTextY + Font12.LineSpacing - nameFont.LineSpacing);
            batch.DrawString(nameFont, name, namePos,
                             !explored ? Color.Gray : P.Owner?.EmpireColor ?? tColor);

            float mrTextY = Housing.Y + 102 + 11 - Font12.LineSpacing / 2;
            if (explored && P.Owner != null) // no governance before there is a colony
            {
                string worldType = P.WorldType.Text;
                batch.DrawString(Font12, worldType,
                    new Vector2(spriteCX - Font12.TextWidth(worldType) / 2f, mrTextY), tColor);
            }
            else if (explored && !P.Habitable)
            {
                const string notHab = "Not habitable";
                batch.DrawString(Font12, notHab,
                    new Vector2(spriteCX - Font12.TextWidth(notHab) / 2f, mrTextY), Color.Gray);
            }

            if (P.Owner == Player)
            {
                PrevColony.Draw(batch, elapsed);
                NextColony.Draw(batch, elapsed);

                // money rides the pop icon's column, research just right of it (bench 313)
                MoneyRect = new Rectangle(PopRect.X, Housing.Y + 102, 22, 22);
                batch.Draw(ResourceManager.Texture("UI/icon_money_22"), MoneyRect, Color.White);
                string sNetIncome = P.Money.NetRevenue.String(2);
                batch.DrawString(Font12, sNetIncome, new Vector2(MoneyRect.Right + 4, mrTextY),
                                 P.Money.NetRevenue > 0.0 ? Color.LightGreen : Color.Salmon);
                var researchRect = new Rectangle(MoneyRect.X + 75, Housing.Y + 102, 22, 22);
                batch.Draw(ResourceManager.Texture("NewUI/icon_science"), researchRect, Color.White);
                batch.DrawString(Font12, P.Res.NetIncome.String(2),
                                 new Vector2(researchRect.Right + 4, mrTextY), tColor);
            }

            batch.Draw(P.PlanetTexture, PlanetIconRect, Color.White);
            // the class caption under the sprite; a mineable's richness lives on its
            // resource line, an unexplored planet only shows its generic category
            string cls = !explored || P.IsMineable ? P.LocalizedCategory : P.LocalizedRichness;
            batch.DrawString(Font12, cls,
                new Vector2(spriteCX - Font12.TextWidth(cls) / 2f, PlanetIconRect.Bottom + 5), tColor);

            if (explored && P.Owner != null)
            {
                P.UpdateIncomes();

                DrawPlanetStats(DefenseRect, ((float)P.TotalDefensiveStrength).String(1), "UI/icon_shield", Color.White, Color.White);

                // Added by Fat Bastard - display total injury level inflicted automatically to invading troops
                if (P.TotalInvadeInjure > 0)
                    DrawPlanetStats(InjuryRect, ((float)P.TotalInvadeInjure).String(1), "UI/icon_injury", Color.White, Color.White);

                // Added by Fat Bastard - display total space offense of the planet
                if (P.TotalGeodeticOffense > 0)
                {
                    string offenseNumberString = ((float) Math.Round(P.TotalGeodeticOffense, 0)).GetNumberString();
                    DrawPlanetStats(OffenseRect, offenseNumberString, "UI/icon_offense", Color.White, Color.White);
                }

                if (P.ShieldStrengthMax > 0f)
                    DrawPlanetStats(ShieldRect, P.ShieldStrengthCurrent.String(0), "NewUI/icon_planetshield", Color.White, Color.Green);

                // Added by Fat Bastard - display total defense ships stationed on this planet
                int maxDefenseShips = P.MaxDefenseShips;
                if (maxDefenseShips > 0 )
                {
                    int currentDefenseShips = P.CurrentDefenseShips;
                    if (currentDefenseShips == maxDefenseShips)
                        DrawPlanetStats(DefenseShipsRect, currentDefenseShips.ToString(), "UI/icon_hangar", Color.White, Color.White);
                    else
                        DrawPlanetStats(DefenseShipsRect, currentDefenseShips + "/" + maxDefenseShips , "UI/icon_hangar", Color.Yellow, Color.White);
                }
            }
            else if (explored && P.Habitable)
            {
                // the colonisable page: the ground survey in the left column
                float fertEnvMultiplier = Player.PlayerEnvModifier(P.Category);
                int numHabitableTile    = P.TotalHabitableTiles;
                float popPerTile        = P.BasePopPerTile * fertEnvMultiplier;
                float biospherePop      = P.PotentialMaxPopBillionsFor(Player, true);
                float terraformedPop    = P.PotentialMaxPopBillionsWithTerraformFor(Player);

                DrawPlanetStats(TilesRect, $"{numHabitableTile}", "NewUI/icon_tiles", Color.White, Color.White);
                DrawPlanetStats(PopPerTileRect, $"{popPerTile.String(0)}m", "NewUI/icon_poppertile", Color.White, Color.White);
                DrawPlanetStats(BiospheredPopRect, biospherePop.String(2), "NewUI/icon_biospheres", Color.White, Color.White);
                DrawPlanetStats(TerraformedPopRect, terraformedPop.String(1), "NewUI/icon_terraformer", Color.White, Color.White);
            }

            if (!explored)
                return; // sprite and generic class are all an unexplored planet shows

            if (P.Habitable)
            {
                DrawFertProdStats(batch);
                Inspect.Draw(batch);
                Invade.Draw(batch);
            }

            if (P.Owner == null && P.Habitable)
            {
                // the Planets page's pair, stacked where the colony keeps its sliders
                UpdateColonisableButtons();
                BtnSendTroops.Draw(batch, elapsed);
                BtnColonize.Draw(batch, elapsed);
            }
            else if (P.Owner != null && P.Owner != Player)
            {
                if (Player.IsAtWarWith(P.Owner))
                {
                    int invading = IncomingTroops;
                    UpdateEnemyButtons(invading);
                    BtnSendTroops.Draw(batch, elapsed);
                    if (invading > 0)
                        BtnColonize.Draw(batch, elapsed);
                }
            }
            else if (P.Owner == null)
            {
                // the exotic block rides the slider zone, kept as it was on the compact card
                ExoticRect = new Rectangle(LaborX + 10, Housing.Y + 120, 182, 25);
                ExoticResourceIconRect = new Rectangle(LaborX + 10, Housing.Y + 155, 20, 20);
                if (P.IsResearchable)
                    DrawResearchStation(batch, Screen.Input.CursorPosition);
                else if (P.IsMineable)
                    DrawMiningOps(batch, Screen.Input.CursorPosition);
            }

            AssignLabor?.Draw(batch, elapsed);
        }

        void DrawDebugPlanetBudget()
        {
            if (P.Owner != null)
            {
                var budget = P.Owner.AI.PlanetBudgets?.Filter(b => b.P == P) ?? Array.Empty<PlanetBudget>();
                if (budget.Length == 1)
                    budget[0].DrawBudgetInfo(Screen);
            }
        }

        int IncomingTroops
        {
            get
            {
                // todo: double loop sum. 
                var ships = Screen.Player.OwnedShips;
                return ships
                    .Where(s => s != null && s.HasOurTroops &&
                                s.AI.OrderQueue.Any(g => g.Plan == ShipAI.Plan.LandTroop && g.TargetPlanet == P))
                    .Sum(s => s.TroopCount);
            }
        }


        void DrawResearchStation(SpriteBatch batch, Vector2 mousePos)
        {
            if (P.IsResearchStationDeployedBy(Player))
            {
                // Ludoal fork: show the deployed state (mirrors the star cartouche) instead of nothing
                var okPos = new Vector2(ExoticRect.X + 13, ExoticRect.Y + 13 - Font12.LineSpacing / 2 - 2);
                batch.DrawString(Font12, "Research station operational", okPos, Color.LightGreen);
                return;
            }

            Vector2 textPos = new Vector2(ExoticRect.X + 13, ExoticRect.Y + 13 - Font12.LineSpacing / 2 - 2);
            UIButton.DrawPlate(batch, ExoticRect, Player.CanBuildResearchStations ? UIButton.PlateActive
                                                                                 : UIButton.PlateNeutral);

            LocalizedText tip = Player.CanBuildResearchStations ? GameText.DeployResearchStationTip : GameText.CannotBuildResearchStationTip;
            LocalizedText tipText = GameText.DeployResearchStation;
            if (Player.AI.HasGoal(g => g.IsResearchStationGoal(P)))
            {
                tip = GameText.CancelDeployResearchStationTip;
                tipText = GameText.AbortDeployent;
            }

            ToolTipItems.Add(new TippedItem(ExoticRect, tip));
            // lit text always - gray only for the unavailable action (maintainer bench 318)
            batch.DrawString(Font12, tipText, textPos, Player.CanBuildResearchStations ? ButtonTextColor : Color.Gray);
        }

        void DrawMiningOps(SpriteBatch batch, Vector2 mousePos)
        {
            // the shared header already drew the rig owner's flag in the flag slot
            batch.Draw(P.Mining.ExoticResourceIcon, ExoticResourceIconRect);
            // Ludoal fork: the block lives in the right column now — two short lines
            Vector2 resourceStatPos = new Vector2(ExoticResourceIconRect.X + 23, ExoticResourceIconRect.Y + 2);
            Vector2 resourceStatRefine = new Vector2(ExoticResourceIconRect.X + 23, ExoticResourceIconRect.Y + 17);
            Vector2 resourceStatDeployed = new Vector2(ExoticResourceIconRect.X + 23, ExoticResourceIconRect.Y + 32);
            string stats = $"{P.Mining.TranslatedResourceName.Text}: Richness {P.Mining.Richness}";
            string refine = $"Refine Ratio: {(P.Mining.RefiningRatio * Player.data.RefiningRatioMultiplier).UpperBound(1)}";
            batch.DrawString(Font12, stats, resourceStatPos, Color.White);
            batch.DrawString(Font12, refine, resourceStatRefine, Color.White);

            int numDeployed = P.OrbitalStations.Filter(s => s.IsMiningStation && s.Loyalty == Player).Length;
            int numInProgress = Player.AI.CountGoals(g => g.IsMiningOpsGoal(P) && g.TargetShip == null);
            string statsDeployed = $"{numDeployed}/{Mineable.MaximumMiningStations} Deployed    ";
            batch.DrawString(Font12, statsDeployed, resourceStatDeployed, numDeployed > 0 ? Color.Green : Color.Gray);
            if (numInProgress > 0)
            {
                string statsInProgress = $"{numInProgress} In Progress";
                batch.DrawString(Font12, statsInProgress,
                                 resourceStatDeployed + new Vector2(Font12.MeasureString(statsDeployed).X, 0f), Color.Gold);
            }
            ToolTipItems.Add(new TippedItem(ExoticResourceIconRect, $"{P.Mining.ResourceDescription.Text}\n{new LocalizedText(GameText.MineableRichnessTip).Text}"));
            if (P.Mining.Owner != null && P.Mining.Owner != Player)
                return;

            Vector2 textPos = new Vector2(ExoticRect.X + 13, ExoticRect.Y + 13 - Font12.LineSpacing / 2 - 2);
            UIButton.DrawPlate(batch, ExoticRect,
                Player.CanBuildMiningStations && P.Mining.CanAddMiningStationFor(Player)
                ? UIButton.PlateActive   // active like every other action button
                : UIButton.PlateNeutral);

            LocalizedText tip = Player.CanBuildMiningStations ? GameText.DeployMiningStationTip : GameText.CannotBuildMiningStationTip;
            LocalizedText tipText = P.Mining.Owner != null && P.Mining.Owner != Player ? GameText.CannotDeployMiningStationNotOwnerTip : GameText.DeployMiningStation;


            ToolTipItems.Add(new TippedItem(ExoticRect, tip));
            // lit text always - gray only for the unavailable action (maintainer bench 318)
            batch.DrawString(Font12, tipText, textPos, Player.CanBuildMiningStations ? ButtonTextColor : Color.Gray);
        }

        void DrawFertProdStats(SpriteBatch batch)
        {
            var foodTex = ResourceManager.Texture("NewUI/icon_food");
            var fIcon = new Rectangle(PopRect.X, Housing.Y + 218 + Fonts.Arial12Bold.LineSpacing - foodTex.Height, foodTex.Width, foodTex.Height);
            batch.Draw(foodTex, fIcon, Color.White);
            ToolTipItems.Add(new TippedItem(fIcon, GameText.IndicatesHowMuchFoodThis));

            var tcurs = new Vector2(fIcon.X + 25, Housing.Y + 213);
            float fertility   = P.FertilityFor(Player);
            float maxFert     = P.MaxFertilityFor(Player);
            string fertString = fertility.AlmostEqual(maxFert) ? fertility.String(2) : $"{fertility.String(2)}/{maxFert.String(2)}";
            batch.DrawString(Fonts.Arial12Bold, fertString, tcurs, tColor);

            float fertEnvMultiplier = Player.PlayerEnvModifier(P.Category);
            if (!fertEnvMultiplier.AlmostEqual(1))
            {
                Color fertEnvColor = fertEnvMultiplier.Less(1) ? Color.Pink : Color.LightGreen;
                var fertMultiplier = new Vector2(tcurs.X + Font12.MeasureString(fertString).X + 3, tcurs.Y + 2);
                batch.DrawString(Font8, $"(x {fertEnvMultiplier.String(2)})", fertMultiplier, fertEnvColor);
            }

            var prodTex = ResourceManager.Texture("NewUI/icon_production");
            var pIcon = new Rectangle(LockColX, Housing.Y + 218 + Fonts.Arial12Bold.LineSpacing - prodTex.Height, prodTex.Width, prodTex.Height);
            batch.Draw(prodTex, pIcon, Color.White);
            ToolTipItems.Add(new TippedItem(pIcon, GameText.APlanetsMineralRichnessDirectly));

            tcurs = new Vector2(pIcon.X + 25, Housing.Y + 213);
            batch.DrawString(Fonts.Arial12Bold, P.MineralRichness.String(), tcurs, tColor);
        }

        void AddExploredTips()
        {
            ToolTipItems.Add(new TippedItem(DefenseRect, GameText.IndicatesThisColonysTotalStrength));
            ToolTipItems.Add(new TippedItem(InjuryRect, GameText.EveryTroopInvadingThisPlanet));
            ToolTipItems.Add(new TippedItem(OffenseRect, GameText.ThePlanetsSpaceOffenseVs));
            ToolTipItems.Add(new TippedItem(ShieldRect, GameText.IndicatesTheCurrentStrengthOf));
            ToolTipItems.Add(new TippedItem(DefenseShipsRect, GameText.PLanetInfoNumberOfDefenseShips));
        }

        void AddUnExploredTips()
        {
            ToolTipItems.Add(new TippedItem(TilesRect, GameText.ThisIndicatesHowManyTiles));
            ToolTipItems.Add(new TippedItem(PopPerTileRect, GameText.ThisIndicatesHowMuchPopulation));
            ToolTipItems.Add(new TippedItem(BiospheredPopRect, GameText.ThisIndicatesWhatWouldThe));
            ToolTipItems.Add(new TippedItem(TerraformedPopRect, GameText.ThisIndicatesWhatWouldThe2));
        }

        void DrawPlanetStats(Rectangle rect, string data, string texturePath, Color color, Color texColor)
        {
            Graphics.Font font = Fonts.Arial12Bold;
            Vector2 pos     = new Vector2((rect.X + rect.Width + 2), (rect.Y + 11 - font.LineSpacing / 2));
            ScreenManager.SpriteBatch.Draw(ResourceManager.Texture(texturePath), rect, texColor);
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12Bold, data, pos, color);
        }

        public override bool HandleInput(InputState input)
        {
            if (P == null)
            {
                return false;
            }
            if (P.Owner == Player && P.IsExploredBy(Player)
                && (PrevColony.HandleInput(input) || NextColony.HandleInput(input)))
            {
                return true; // the click may have swapped P for the next colony
            }
            if (P.Owner == null && P.Habitable && P.IsExploredBy(Player))
            {
                UpdateColonisableButtons(); // input can run before the first draw of a fresh selection
                if (BtnSendTroops.HandleInput(input) || BtnColonize.HandleInput(input))
                    return true;
            }
            if (P.Owner != null && P.Owner != Player && P.IsExploredBy(Player) && Player.IsAtWarWith(P.Owner))
            {
                int invading = IncomingTroops;
                UpdateEnemyButtons(invading);
                if (BtnSendTroops.HandleInput(input) || (invading > 0 && BtnColonize.HandleInput(input)))
                    return true;
            }
            foreach (TippedItem ti in ToolTipItems)
            {
                if (ti.Rect.HitTest(input.CursorPosition))
                    ToolTip.CreateTooltip(ti.Tooltip);
            }

            if (P.IsResearchable && ExoticRect.HitTest(input.CursorPosition) && input.InGameSelect)
            {
                if      (Player.AI.HasGoal(g => g.IsResearchStationGoal(P))) Player.AI.CancelResearchStation(P);
                else if (Player.CanBuildResearchStations)                    Player.AI.AddGoalAndEvaluate(new ProcessResearchStation(Player, P));
                else                                                         GameAudio.NegativeClick();

                GameAudio.EchoAffirmative();
            }
            else if (P.IsMineable && ExoticRect.HitTest(input.CursorPosition) && input.InGameSelect)
            {
                if (P.Mining.CanAddMiningStationFor(Player))
                {
                    Player.AI.AddGoalAndEvaluate(new MiningOps(Player, P));
                    GameAudio.EchoAffirmative();
                }
                else
                { 
                    GameAudio.NegativeClick(); 
                }
                return true;
            }

            if (Inspect.Hover && P.Habitable)
            {
                if (P.Owner == null || P.Owner != Player)
                {
                    ToolTip.CreateTooltip(GameText.ViewPlanetDetails);
                }
                else
                {
                    ToolTip.CreateTooltip(GameText.OpensColonyOverviewScreen);
                }
            }
            if (Invade.Hover && P.Habitable)
            {
                ToolTip.CreateTooltip(GameText.OpenTheGroundAssaultView);
            }
            if (P.Habitable || P.Universe.Debug)
            {
                if (Inspect.HandleInput(input))
                {
                    Screen.SnapViewColony(P, combatView: false);
                }
                if (Invade.HandleInput(input))
                {
                    Screen.SnapViewColony(P, combatView: true);
                }
            }
            if (!ElementRect.HitTest(input.CursorPosition))
                return false;

            if (AssignLabor != null && AssignLabor.HandleInput(input))
                return true;

            return true;
        }

        public void SetPlanet(Planet p)
        {
            if (P != p)
            {
                P = p;
                if (p != null && P.Owner == Player)
                {
                    var sliderRect = new RectF(LaborX, Housing.Y + 88, LaborW, 130);
                    AssignLabor = new AssignLaborComponent(p, sliderRect, useTitleFrame: false);
                }
                else
                {
                    AssignLabor = null;
                }
            };
        }
    }
}
