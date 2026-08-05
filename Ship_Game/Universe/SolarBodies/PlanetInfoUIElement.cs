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
        // the rest trims the dead margin the old frame left (maintainer benches 311-312).
        const int FrameShave = 51;
        const int RightTrim  = 10; // same trim on the plate's right edge
        const int LaborW = 200;    // labor block width - its rail is (W * 0.6).RoundTo10()
        int LaborX   => PlanetIconRect.Right + 20;
        int FoodColX => Housing.X + 200; // the foot line's food icon; money aligns on it
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
        Rectangle UninhabIconRect; // Ludoal fork: planet sprite for uninhabitables
        int PlateTop;              // the visible frame's top - ADAPTIVE (bench 308)
        readonly SkinnableButton Inspect;
        readonly SkinnableButton Invade;
        readonly ToggleButton PrevColony; // walk the player's colony list from the cartouche
        readonly ToggleButton NextColony;
        readonly Rectangle Housing;
        readonly Rectangle DefenseRect;
        readonly Rectangle InjuryRect;
        readonly Rectangle OffenseRect;
        readonly Rectangle ShieldRect;
        readonly Rectangle DefenseShipsRect;
        readonly Rectangle RightRect;
        readonly Rectangle PlanetIconRect;
        readonly Rectangle FlagRect;

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

            FlagRect         = new Rectangle(r.X + r.Width - 44, Housing.Y + 96, 26, 26); // under the R/F/P lanes (bench 308)
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

            SendTroops = new Rectangle(RightRect.X - 17, Housing.Y + 130, 182, 25);
            MarkedRect = new Rectangle(RightRect.X - 17, Housing.Y + 160, 182, 25);
            CancelInvasionRect = MarkedRect; // Replaces the colonization rect when invading
            ExoticRect = new Rectangle(RightRect.X - 17, Housing.Y + 130, 182, 25);
            ExoticResourceIconRect = new Rectangle(RightRect.X - 17, Housing.Y + 165, 20, 20);
            UninhabIconRect = new Rectangle(leftRect.X + 75, Housing.Y + 120, 80, 80); // Ludoal fork: sprite left, buttons right — same grammar as colonies

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

        // ── the unified header (spec cartouches, bench 308) ──────────────────────────
        // Name in 20 bold (owner's colour), class in gray under it, and the star
        // cartouche's own R/F/P lanes top right - one grammar for every variant.
        void DrawHeader(SpriteBatch batch, bool lanes)
        {
            var namePos = new Vector2(Housing.X + 16, PlateTop + 8);
            string name = UI.UITable.FitText(Fonts.Arial20Bold, P.Name, 190);
            batch.DrawString(Fonts.Arial20Bold, name, namePos, P.Owner?.EmpireColor ?? tColor);
            string cls = P.IsMineable ? P.LocalizedCategory : P.LocalizedRichness;
            batch.DrawString(Fonts.Arial12, cls,
                             new Vector2(namePos.X + 2, namePos.Y + Fonts.Arial20Bold.LineSpacing + 2),
                             Color.Gray);
            if (!lanes)
                return;

            int laneP = Housing.Right - 46, laneF = laneP - 58, laneR = laneF - 44;
            int iy = (int)namePos.Y + 2;
            void LaneIcon(string tex, int lane)
                => batch.Draw(ResourceManager.Texture(tex), new Rectangle(lane + 4, iy, 14, 14), Color.White);
            void LaneVal(string v, int lane)
                => batch.DrawString(Fonts.Arial12, v,
                                    new Vector2(lane + 18 - Fonts.Arial12.TextWidth(v), iy + 18), Color.White);
            LaneIcon("NewUI/icon_production", laneR);
            LaneVal(P.MineralRichness.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), laneR);
            LaneIcon("NewUI/icon_food", laneF);
            LaneVal(P.FertilityFor(Player).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), laneF);
            LaneIcon("UI/icon_pop_22", laneP);
            string popShort = P.Habitable
                ? (P.PopulationBillion > 0
                    ? $"{P.PopulationBillion.String(1)}/{P.MaxPopulationBillionFor(Player).String(1)}"
                    : P.MaxPopulationBillionFor(Player).String(1))
                : "-";
            LaneVal(popShort, laneP);
            PopRect = new Rectangle(laneP + 4, iy, 14, 14); // the pop tooltip anchors the lane icon
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
            else if (P.Owner == null && !P.Habitable)
                plateH = 172;
            PlateTop = Housing.Bottom - plateH;
            var frame = new Rectangle(Housing.X, PlateTop, Housing.Width - RightTrim, plateH);
            Rectangle plate = frame;
            plate.Inflate(-2, -2);
            batch.FillRectangle(plate, new Color(8, 10, 14).Alpha(0.94f));
            UITheme.DrawPlate(batch, frame, Color.Transparent,
                              new Color(150, 150, 150).Alpha(0.85f), radiusOverride: 8,
                              ruleWidthOverride: 3);

            P.UpdateMaxPopulation();
            if (P.Owner == null || !explored)
            {
                DrawUnexploredUninhabited(Screen.Input.CursorPosition);
                return;
            }

            AddExploredTips();

            // one grammar for own and enemy colonies (maintainer bench 312): the pop line
            // right-aligned with its flag, the name sharing its line - bottom-aligned, the
            // bigger font grows upward - and the governance on the money/research level,
            // both centred over the sprite
            Graphics.Font nameFont = Fonts.Arial8Bold;
            if (P.Name.Length < 12)      nameFont = Fonts.Arial20Bold;
            else if (P.Name.Length < 13) nameFont = Fonts.Arial12Bold;
            else if (P.Name.Length < 17) nameFont = Fonts.Arial10;

            string worldType = P.WorldType.Text;
            int frameRight = Housing.Right - RightTrim;
            var flagRect = new Rectangle(frameRight - 50, Housing.Y + 76, 26, 26);
            batch.Draw(ResourceManager.Flag(P.Owner), flagRect, P.Owner.EmpireColor);
            string pop = P.PopulationStringForPlayer;
            var popPos = new Vector2(flagRect.X - 5 - Font12.TextWidth(pop), flagRect.Y + 13 - Font12.LineSpacing / 2);
            batch.DrawString(Font12, pop, popPos, tColor);
            PopRect = new Rectangle((int)popPos.X - 23, (int)popPos.Y - 3, 22, 22);
            batch.Draw(ResourceManager.Texture("UI/icon_pop_22"), PopRect, Color.White);

            // the ball sits left of its box's centre - centring on the box lands 10px right
            // of the visible sprite (maintainer bench 312), so every image-centred string
            // centres on this instead
            int spriteCX = PlanetIconRect.CenterX() - 10;
            var namePos = new Vector2(spriteCX - nameFont.TextWidth(P.Name) / 2f,
                                      popPos.Y + Font12.LineSpacing - nameFont.LineSpacing);
            batch.DrawString(nameFont, P.Name, namePos, P.Owner.EmpireColor);

            float mrTextY = Housing.Y + 102 + 11 - Font12.LineSpacing / 2;
            batch.DrawString(Font12, worldType,
                new Vector2(spriteCX - Font12.TextWidth(worldType) / 2f, mrTextY), tColor);

            if (P.Owner == Player)
            {
                PrevColony.Draw(batch, elapsed);
                NextColony.Draw(batch, elapsed);

                // money rides the foot line's food column, research the lock column
                MoneyRect = new Rectangle(FoodColX, Housing.Y + 102, 22, 22);
                batch.Draw(ResourceManager.Texture("UI/icon_money_22"), MoneyRect, Color.White);
                string sNetIncome = P.Money.NetRevenue.String(2);
                batch.DrawString(Font12, sNetIncome, new Vector2(MoneyRect.Right + 4, mrTextY),
                                 P.Money.NetRevenue > 0.0 ? Color.LightGreen : Color.Salmon);
                var researchRect = new Rectangle(LockColX, Housing.Y + 102, 22, 22);
                batch.Draw(ResourceManager.Texture("NewUI/icon_science"), researchRect, Color.White);
                batch.DrawString(Font12, P.Res.NetIncome.String(2),
                                 new Vector2(researchRect.Right + 4, mrTextY), tColor);
            }

            batch.Draw(P.PlanetTexture, PlanetIconRect, Color.White);
            string richness = P.LocalizedRichness; // the class caption back under the sprite (308)
            batch.DrawString(Font12, richness,
                new Vector2(spriteCX - Font12.TextWidth(richness) / 2f, PlanetIconRect.Bottom + 5), tColor);
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

            DrawFertProdStats(batch);
            DrawColonization(batch, Screen.Input.CursorPosition);
            DrawSendTroops(batch, Screen.Input.CursorPosition);
            Inspect.Draw(batch);
            Invade.Draw(batch);

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

        bool DrawUnexploredUninhabited(Vector2 mousePos)
        {
            SpriteBatch batch = ScreenManager.SpriteBatch;

            if (!P.IsExploredBy(Player))
            {
                // the compact plate: a title and one line - nothing else is known
                batch.DrawString(Fonts.Arial20Bold,
                    Localizer.Token(GameText.Unexplored) + P.LocalizedCategory,
                    new Vector2(Housing.X + 16, PlateTop + 12), tColor);
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.SendAShipToThis),
                                 new Vector2(Housing.X + 16, PlateTop + 48), Color.Gray);
                return true;
            }

            if (!P.Habitable)
            {
                // compact plate: unified header (no lanes - the ground stats mean nothing
                // here), the sprite at left, the actions and mining/research block at right
                DrawHeader(batch, lanes: false);
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.ThisPlanetIsNotHabitable),
                                 new Vector2(Housing.X + 16, PlateTop + 46), Color.Gray);

                UninhabIconRect = new Rectangle(Housing.X + 60, PlateTop + 74, 80, 80);
                batch.Draw(P.PlanetTexture, UninhabIconRect, Color.White);

                ExoticRect = new Rectangle(RightRect.X - 17, PlateTop + 74, 182, 25);
                ExoticResourceIconRect = new Rectangle(RightRect.X - 17, PlateTop + 108, 20, 20);
                if (P.IsResearchable)
                    DrawResearchStation(batch, mousePos);
                else if (P.IsMineable)
                    DrawMiningOps(batch, mousePos);

                return true;
            }

            DrawHeader(batch, lanes: true);
            batch.Draw(P.PlanetTexture, PlanetIconRect, Color.White); // class lives in the header
            DrawFertProdStats(batch);
            AddUnExploredTips();

            float fertEnvMultiplier = Player.PlayerEnvModifier(P.Category);
            int numHabitableTile    = P.TotalHabitableTiles;
            float popPerTile        = P.BasePopPerTile * fertEnvMultiplier;
            float biospherePop      = P.PotentialMaxPopBillionsFor(Player, true);

            DrawPlanetStats(TilesRect, $"{numHabitableTile}", "NewUI/icon_tiles", Color.White, Color.White);
            DrawPlanetStats(PopPerTileRect, $"{popPerTile.String(0)}m", "NewUI/icon_poppertile", Color.White, Color.White);
            DrawPlanetStats(BiospheredPopRect, biospherePop.String(2), "NewUI/icon_biospheres", Color.White, Color.White);

            float terraformedPop = P.PotentialMaxPopBillionsWithTerraformFor(Player);
            DrawPlanetStats(TerraformedPopRect, terraformedPop.String(1),
                "NewUI/icon_terraformer", Color.White, Color.White);

            DrawColonization(batch, mousePos);
            DrawSendTroops(batch, mousePos);
            Inspect.Draw(batch);
            Invade.Draw(batch);
            return false;
        }

        void DrawSendTroops(SpriteBatch batch, Vector2 mousePos)
        {
            if (P.Owner == Player || P.Owner != null && !Player.IsAtWarWith(P.Owner))
                return; // Cannot send troops to this planet or different UI for player owner.

            Vector2 textPos        = new Vector2(SendTroops.X + 25, SendTroops.Y + 12 - Font12.LineSpacing / 2 - 2);
            int incomingTroops     = IncomingTroops;
            Color buttonBaseColor  = ButtonTextColor;
            Color buttonHoverColor = ButtonHoverColor;
            Color plate            = UIButton.PlateActive;
            string text = "Invade";
            if (P.Owner != null)
            {
                if (incomingTroops > 0)
                {
                    text             = $"Invading: {incomingTroops}";
                    buttonBaseColor  = Color.Red;
                    plate            = UIButton.PlateHostile;
                    buttonHoverColor = Color.White;
                    DrawCancelInvasion(batch, mousePos);
                }
            }
            else
                text = incomingTroops > 0 ? $"Enroute: {incomingTroops}" : "Send Troops";

            UIButton.DrawPlate(batch, SendTroops, plate);
            batch.DrawString(Font12, text, textPos, SendTroops.HitTest(mousePos) ? buttonBaseColor
                                                                                 : buttonHoverColor);
        }

        void DrawCancelInvasion(SpriteBatch batch, Vector2 mousePos)
        {
            Vector2 textPos = new Vector2(RightRect.X - 12, CancelInvasionRect.Y + 12 - Font12.LineSpacing / 2 - 2);
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

        void DrawColonization(SpriteBatch batch, Vector2 mousePos)
        {
            if (P.Owner != null)
                return;

            Vector2 textPos = new Vector2(RightRect.X + 18, MarkedRect.Y + 12 - Font12.LineSpacing / 2 - 2);
            UIButton.DrawPlate(batch, MarkedRect, UIButton.PlateActive);

            LocalizedText tip = GameText.MarkThisPlanetForColonization;
            LocalizedText tipText = GameText.Colonize;
            if (Player.AI.HasGoal(g => g.IsColonizationGoal(P)))
            {
                tip = GameText.CancelTheColonizationMissionThat;
                tipText = GameText.CancelColonize;
            }

            ToolTipItems.Add(new TippedItem(MarkedRect, tip));
            batch.DrawString(Font12, tipText, textPos, MarkedRect.HitTest(mousePos) ? ButtonTextColor 
                                                                                    : ButtonHoverColor);
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
            if (P.Mining.Owner != null) // the header wrote the name; the flag says whose rig
                batch.Draw(ResourceManager.Flag(P.Mining.Owner), FlagRect, P.Mining.Owner.EmpireColor);

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
            var fIcon = new Rectangle(FoodColX, Housing.Y + 218 + Fonts.Arial12Bold.LineSpacing - foodTex.Height, foodTex.Width, foodTex.Height);
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
            foreach (TippedItem ti in ToolTipItems)
            {
                if (ti.Rect.HitTest(input.CursorPosition))
                    ToolTip.CreateTooltip(ti.Tooltip);
            }
            if (P.Owner == null && MarkedRect.HitTest(input.CursorPosition) && input.InGameSelect)
            {
                if (Player.AI.HasGoal(g => g.IsColonizationGoal(P)))
                {
                    Player.AI.CancelColonization(P);
                    GameAudio.EchoAffirmative();
                }
                else
                {
                    GameAudio.EchoAffirmative();
                    Player.AI.AddGoalAndEvaluate(new MarkForColonization(P, Player, isManual:true));
                }
            }
            if (SendTroops.HitTest(input.CursorPosition) && input.InGameSelect)
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
