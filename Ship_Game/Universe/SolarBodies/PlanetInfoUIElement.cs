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
        const int FrameShave = 61;
        const int RightTrim  = 10; // same trim on the plate's right edge
        const int LaborW = 200;    // labor block width - its rail is (W * 0.6).RoundTo10()
        int LaborX   => PlanetIconRect.Right + 20;
        // the lock/tool column: labor housing inset (+10), the rail, the lock gap (+10) -
        // the same walk AssignLaborComponent and ColonySlider take to place the locks
        int LockColX => LaborX + 10 + (LaborW * 0.6f).RoundTo10() + 10;
        Planet P;
        readonly UniverseScreen Screen;
        Empire Player => Screen.Player;
        readonly Array<TippedItem> ToolTipItems = new Array<TippedItem>();

        Rectangle MoneyRect;
        readonly Rectangle SendTroops;
        readonly Rectangle MarkedRect;
        readonly Rectangle CancelInvasionRect;
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
        readonly Color ButtonHoverColor  = new Color(88, 108, 146);
                                                                                  
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
            PlanetIconRect = new Rectangle(leftRect.X + 85, Housing.Y + 128, 80, 80);
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

            // the action buttons sit where the colony page keeps its sliders (bench 312)
            SendTroops = new Rectangle(LaborX + 10, Housing.Y + 120, 182, 25);
            MarkedRect = new Rectangle(LaborX + 10, Housing.Y + 155, 182, 25);
            CancelInvasionRect = MarkedRect; // Replaces the colonization rect when invading
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
                Rect = new Rectangle(LaborX + 10, Housing.Y + 120, btnW, 24),
                Tooltip = GameText.SendAvailableTroopsToThis,
                OnClick = OnSendTroopsClicked
            };
            BtnColonize = new UIButton(ButtonStyle.WideActive, GameText.Colonize)
            {
                Rect = new Rectangle(LaborX + 10, Housing.Y + 155, btnW, 24),
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
            int landing = IncomingTroops;
            BtnSendTroops.Text = landing > 0 ? $"Landing: {landing}" : "Send Troops";
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
            // ADAPTIVE height (maintainer bench 308): bottom-anchored on the housing like
            // the star cartouche - the sparse variants stop framing dead space. The owned
            // colony (sliders) and the habitable-unowned page keep the full plate.
            bool explored = P.IsExploredBy(Player);
            int plateH = Housing.Height - FrameShave;
            if (!explored)
                plateH = 96;
            PlateTop = Housing.Bottom - plateH;
            var frame = new Rectangle(Housing.X, PlateTop, Housing.Width - RightTrim, plateH);
            Rectangle plate = frame;
            plate.Inflate(-2, -2);
            batch.FillRectangle(plate, new Color(8, 10, 14).Alpha(0.94f));
            UITheme.DrawPlate(batch, frame, Color.Transparent,
                              new Color(150, 150, 150).Alpha(0.85f), radiusOverride: 8,
                              ruleWidthOverride: 3);

            P.UpdateMaxPopulation();
            if (!explored)
            {
                DrawUnexplored();
                return;
            }

            // every explored planet shares the grammar below, whatever its status
            if (P.Owner != null)      AddExploredTips();
            else if (P.Habitable)     AddUnExploredTips();

            // one grammar for own and enemy colonies (maintainer bench 312): the pop line
            // right-aligned with its flag, the name sharing its line - bottom-aligned, the
            // bigger font grows upward - and the governance on the money/research level,
            // both centred over the sprite
            Graphics.Font nameFont = Fonts.Arial8Bold;
            if (P.Name.Length < 12)      nameFont = Fonts.Arial20Bold;
            else if (P.Name.Length < 13) nameFont = Fonts.Arial12Bold;
            else if (P.Name.Length < 17) nameFont = Fonts.Arial10;

            int frameRight = Housing.Right - RightTrim;
            var flagRect = new Rectangle(frameRight - 40, Housing.Y + 76, 26, 26);
            Empire flagOwner = P.Owner ?? (P.IsMineable ? P.Mining.Owner : null);
            if (flagOwner != null)
                batch.Draw(ResourceManager.Flag(flagOwner), flagRect, flagOwner.EmpireColor);
            string pop = P.PopulationStringForPlayer;
            var popPos = new Vector2(flagRect.X - 5 - Font12.TextWidth(pop), flagRect.Y + 13 - Font12.LineSpacing / 2);
            if (P.Habitable)
            {
                batch.DrawString(Font12, pop, popPos, tColor);
                PopRect = new Rectangle((int)popPos.X - 23, (int)popPos.Y - 3, 22, 22);
                batch.Draw(ResourceManager.Texture("UI/icon_pop_22"), PopRect, Color.White);
            }
            else
            {
                PopRect = default; // no pop line on a dead rock, no tooltip either
            }

            int spriteCX = PlanetIconRect.CenterX();
            var namePos = new Vector2(spriteCX - nameFont.TextWidth(P.Name) / 2f,
                                      popPos.Y + Font12.LineSpacing - nameFont.LineSpacing);
            batch.DrawString(nameFont, P.Name, namePos, P.Owner?.EmpireColor ?? tColor);

            float mrTextY = Housing.Y + 102 + 11 - Font12.LineSpacing / 2;
            if (P.Owner != null) // no governance before there is a colony
            {
                string worldType = P.WorldType.Text;
                batch.DrawString(Font12, worldType,
                    new Vector2(spriteCX - Font12.TextWidth(worldType) / 2f, mrTextY), tColor);
            }
            else if (!P.Habitable)
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
                var researchRect = new Rectangle(MoneyRect.X + 90, Housing.Y + 102, 22, 22);
                batch.Draw(ResourceManager.Texture("NewUI/icon_science"), researchRect, Color.White);
                batch.DrawString(Font12, P.Res.NetIncome.String(2),
                                 new Vector2(researchRect.Right + 4, mrTextY), tColor);
            }

            batch.Draw(P.PlanetTexture, PlanetIconRect, Color.White);
            // the class caption under the sprite; a mineable's richness lives on its resource line
            string cls = P.IsMineable ? P.LocalizedCategory : P.LocalizedRichness;
            batch.DrawString(Font12, cls,
                new Vector2(spriteCX - Font12.TextWidth(cls) / 2f, PlanetIconRect.Bottom + 5), tColor);

            if (P.Owner != null)
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
            else if (P.Habitable)
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
                DrawSendTroops(batch, Screen.Input.CursorPosition);
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

        void DrawUnexplored()
        {
            // the compact plate: a title and one line - nothing else is known
            SpriteBatch batch = ScreenManager.SpriteBatch;
            batch.DrawString(Fonts.Arial20Bold,
                Localizer.Token(GameText.Unexplored) + P.LocalizedCategory,
                new Vector2(Housing.X + 16, PlateTop + 12), tColor);
            batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.SendAShipToThis),
                             new Vector2(Housing.X + 16, PlateTop + 48), Color.Gray);
        }

        void DrawSendTroops(SpriteBatch batch, Vector2 mousePos)
        {
            // enemy colonies only - the colonisable page wears the Planets page's buttons
            if (P.Owner == Player || !Player.IsAtWarWith(P.Owner))
                return;

            Vector2 textPos        = new Vector2(SendTroops.X + 25, SendTroops.Y + 12 - Font12.LineSpacing / 2 - 2);
            int incomingTroops     = IncomingTroops;
            Color buttonBaseColor  = ButtonTextColor;
            Color buttonHoverColor = ButtonHoverColor;
            Color plate            = UIButton.PlateActive;
            string text = "Invade";
            if (incomingTroops > 0)
            {
                text             = $"Invading: {incomingTroops}";
                buttonBaseColor  = Color.Red;
                plate            = UIButton.PlateHostile;
                buttonHoverColor = Color.White;
                DrawCancelInvasion(batch, mousePos);
            }

            UIButton.DrawPlate(batch, SendTroops, plate);
            batch.DrawString(Font12, text, textPos, SendTroops.HitTest(mousePos) ? buttonBaseColor
                                                                                 : buttonHoverColor);
        }

        void DrawCancelInvasion(SpriteBatch batch, Vector2 mousePos)
        {
            Vector2 textPos = new Vector2(CancelInvasionRect.X + 5, CancelInvasionRect.Y + 12 - Font12.LineSpacing / 2 - 2);
            UIButton.DrawPlate(batch, CancelInvasionRect, UIButton.PlateActive);
            batch.DrawString(Font12, "Cancel Invasion", textPos, CancelInvasionRect.HitTest(mousePos) ? ButtonTextColor
                                                                                                      : ButtonHoverColor);
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
            batch.DrawString(Font12, tipText, textPos, Player.CanBuildResearchStations ? ExoticRect.HitTest(mousePos) ? ButtonTextColor : ButtonHoverColor
                                                                                       : Color.Gray);
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
            batch.DrawString(Font12, tipText, textPos, Player.CanBuildMiningStations ? ExoticRect.HitTest(mousePos) ? ButtonTextColor : ButtonHoverColor
                                                                                     : Color.Gray);
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
            if (P.Owner == null && P.Habitable && P.IsExploredBy(Player)
                && (BtnSendTroops.HandleInput(input) || BtnColonize.HandleInput(input)))
            {
                return true;
            }
            foreach (TippedItem ti in ToolTipItems)
            {
                if (ti.Rect.HitTest(input.CursorPosition))
                    ToolTip.CreateTooltip(ti.Tooltip);
            }
            if (P.Owner != null && SendTroops.HitTest(input.CursorPosition) && input.InGameSelect)
            {
                if (Player.GetTroopShipForRebase(out Ship troopShip, P.Position, P.Name))
                {
                    if (!troopShip.AI.OrderLandAllTroops(P, clearOrders: true, input.CursorPosition))
                    {
                        GameAudio.NegativeClick();
                    }
                    else
                    {
                        GameAudio.EchoAffirmative();
                        if (Player.Universe.Paused)
                            Player.Universe.Objects.UpdateLists();
                    }
                }
                else
                {
                    GameAudio.BlipClick();
                }
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

            if (P.Owner != null 
                && !P.IsResearchable
                && !P.IsMineable
                && P.Owner != Player 
                && CancelInvasionRect.HitTest(input.CursorPosition) 
                && input.InGameSelect)
            {
                var shipList = Player.OwnedShips;
                foreach (Ship ship in shipList)
                {
                    if (ship.AI.State == AIState.AssaultPlanet && ship.AI.OrderQueue.Any(g => g.TargetPlanet == P))
                    {
                        if (ship.DesignRole == RoleName.troopShip)
                            ship.AI.OrderOrbitNearest(true);
                        else
                            ship.AI.OrderRebaseToNearest();
                    }
                }
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
