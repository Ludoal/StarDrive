using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Gameplay;
using Ship_Game.GameScreens.DiplomacyScreen;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Graphics;
using static Ship_Game.Data.Serialization.Types.RawArraySerializer;
using Color = Microsoft.Xna.Framework.Color;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game
{
    public sealed class MainDiplomacyScreen : GameScreen
    {
        UniverseScreen Universe;
        public DanButton Contact;

        Menu2 TitleBar;
        Vector2 TitlePos;
        Menu2 DMenu;

        public Rectangle SelectedInfoRect;
        public Rectangle IntelligenceRect;
        public Rectangle OperationsRect;

        public Empire SelectedEmpire;

        Array<RaceEntry> Races = new();
        ScrollList<ArtifactItemListItem> ArtifactsSL;

        Empire Player;
        readonly bool UsingNewEspioange;
        Array<Empire> Friends;
        Array<Empire> Traders;
        HashSet<Empire> Moles;

        UIButton DiagramButton;
        Rectangle LeftRect;

        // Ludoal fork: standard tabbed layout — empire rows left, Info/Intelligence/
        // Operations as tabs (Colony-style), relations matrix below the tabs.
        Submenu EmpiresPanel;
        Submenu DetailTabs;
        Submenu MatrixPanel;

        Font Font12 = Fonts.Arial12;
        Font Font12Bold = Fonts.Arial12Bold;
        Font Font20Bold = Fonts.Arial20Bold;


        public MainDiplomacyScreen(UniverseScreen screen) : base(screen, toPause: screen)
        {
            Universe = screen;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            Player = screen.Player;
            Friends = screen.UState.GetAllies(Player);
            Traders = screen.UState.GetTradePartners(Player);
            UsingNewEspioange = Player.NewEspionageEnabled;

            // find empires where player or friends have moles
            var empires = new HashSet<Empire>();
            foreach(Empire empire in screen.UState.Empires)
            {
                if (empire.isPlayer || empire.IsFaction)
                    continue;

                if (Player.data.MoleList.Any(m => empire.FindPlanet(m.PlanetId) != null))
                {
                    empires.Add(empire);
                }
                else
                {
                    foreach(Empire friend in Friends)
                    {
                        if (friend.data.MoleList.Any(m => empire.FindPlanet(m.PlanetId) != null))
                        {
                            empires.Add(empire);
                            break;
                        }
                    }
                }
            }
            Moles = empires;
        }

        private int IntelligenceLevel(Empire e)
        {
            if (UsingNewEspioange)
                return 0;

            int intelligence = 0;
            if (Friends.Contains(e) || Moles.Contains(e))
                return 2;

            if (Traders.Contains(e) && Player.GetRelations(e).Treaty_Trade_TurnsExisted > 30)
                return 1;

            if (e.isPlayer)
                return 3;

            foreach(Empire empire in Friends)
            {
                if (!empire.GetRelations(e, out Relationship rel))
                    continue;

                if (rel.Treaty_Trade && rel.Treaty_Trade_TurnsExisted > 30)
                    intelligence = 1;

                if (rel.Treaty_Alliance && rel.TurnsAllied > 3)
                    return 2;
            }

            if (intelligence ==0)
            {
                foreach (Empire empire in Traders)
                {
                    if (!empire.GetRelations(e, out Relationship rel))
                        continue;

                    if (rel.Treaty_Trade && rel.Treaty_Trade_TurnsExisted > 60)
                        intelligence = 1;

                    if (rel.Treaty_Alliance && rel.TurnsAllied > 60)
                        return 2;
                }
            }
            
            return intelligence;
        }

        void DrawDiploLine(SpriteBatch batch, Font font, string text, Color color, ref Vector2 textCursor)
        {
            batch.DrawString(font, text, textCursor, color);
            textCursor.Y += (font.LineSpacing + 2);
        }


        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            if (ScreenHeight > 766)
            {
                TitleBar.Draw(batch, elapsed);
                batch.DrawString(Fonts.Laserian14, Localizer.Token(GameText.DiplomaticOverview), TitlePos, Colors.Cream);
            }
            DMenu.Draw(batch, elapsed);

            // Ludoal fork: standard tabbed layout
            if (DetailTabs.SelectedIndex == -1)
                DetailTabs.SelectedIndex = 0;
            EmpiresPanel.Draw(batch, elapsed);
            DetailTabs.Draw(batch, elapsed);
            MatrixPanel.Draw(batch, elapsed);
            DrawEmpireRows(batch);
            DrawRelationsMatrix(batch);
            int tab = DetailTabs.SelectedIndex;

            batch.FillRectangle(SelectedInfoRect, new Color(23, 20, 14));
            var textCursor = new Vector2(SelectedInfoRect.X + 20, SelectedInfoRect.Y + 10);
            if (tab == 0) {
            batch.DrawDropShadowText1(SelectedEmpire.data.Traits.Name, textCursor, Fonts.Arial20Bold, SelectedEmpire.EmpireColor);
            var flagRect = new Rectangle(SelectedInfoRect.X + SelectedInfoRect.Width - 60, SelectedInfoRect.Y + 10, 40, 40);
            batch.Draw(ResourceManager.Flag(SelectedEmpire.data.Traits.FlagIndex), flagRect, SelectedEmpire.EmpireColor);
            textCursor.Y += (Fonts.Arial20Bold.LineSpacing + 4);

            if (SelectedEmpire.isPlayer)
            {
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.You), textCursor, Color.White);
                textCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                Rectangle artifactsRect = new Rectangle(SelectedInfoRect.X + 20, SelectedInfoRect.Y + 210, SelectedInfoRect.Width - 40, 130);
                Vector2 artifactsCursor = new Vector2(artifactsRect.X, artifactsRect.Y - 8);
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.OwnedArtifacts), artifactsCursor, Color.White);
                artifactsCursor.Y += Fonts.Arial12Bold.LineSpacing;
            }
            else if (SelectedEmpire.IsDefeated)
            {
                if (SelectedEmpire.data.AbsorbedBy != null)
                {
                    Empire absorbingEmpire = Universe.UState.GetEmpireByName(SelectedEmpire.data.AbsorbedBy);
                    DrawDiploLine(batch, Font12Bold, absorbingEmpire.data.Traits.Singular + " Federation", Color.White, ref textCursor);
                }
            }
            else if (!SelectedEmpire.IsDefeated)
            {
                Relationship relation = Player.GetRelations(SelectedEmpire);
                if (UsingNewEspioange && relation.Espionage.CanViewPersonality || IntelligenceLevel(SelectedEmpire) > 0)
                    DrawDiploLine(batch, Font12Bold, $"{SelectedEmpire.data.DiplomaticPersonality.Name} {SelectedEmpire.data.EconomicPersonality.Name}", Color.White, ref textCursor);
                else
                    DrawDiploLine(batch, Font12Bold, $"Unknown Unknown", Color.White, ref textCursor);

                if (relation.AtWar)
                    DrawDiploLine(batch, Font12Bold, Localizer.Token(GameText.AtWar), Color.LightPink, ref textCursor);
                else if (relation.Treaty_Peace)
                    DrawDiploLine(batch, Font12Bold, $"{Localizer.Token(GameText.PeaceTreaty)} ({relation.PeaceTurnsRemaining} {Localizer.Token(GameText.Turns)})", Color.LightGreen, ref textCursor);

                if (relation.Treaty_OpenBorders)
                    DrawDiploLine(batch, Font12Bold, Localizer.Token(GameText.OpenBorders), Color.LightGreen, ref textCursor);

                if (relation.Treaty_Trade)
                    DrawDiploLine(batch, Font12Bold, Localizer.Token(GameText.TradeTreaty2), Color.LightGreen, ref textCursor);

                if (relation.Treaty_NAPact)
                    DrawDiploLine(batch, Font12Bold, Localizer.Token(GameText.NonaggressionPact2), Color.LightGreen, ref textCursor);

                if (relation.Treaty_Alliance)
                    DrawDiploLine(batch, Font12Bold, Localizer.Token(GameText.Alliance), Color.LightGreen, ref textCursor);

                Rectangle artifactsRect = new Rectangle(SelectedInfoRect.X + 20, SelectedInfoRect.Y + 250, SelectedInfoRect.Width - 40, 130);
                Vector2 artifactsCursor = new Vector2(artifactsRect.X, artifactsRect.Y - 8);
                if (!UsingNewEspioange || relation.Espionage.CanViewArtifacts)
                    DrawDiploLine(batch, Font12Bold, Localizer.Token(GameText.OwnedArtifacts), Color.White, ref artifactsCursor);

            }

            if (!SelectedEmpire.isPlayer && Player.IsKnown(SelectedEmpire))
                Contact.Draw(ScreenManager);
            } // end Info tab part 1 (Ludoal fork)
            if (tab == 0) { // ranks share the Info tab

            if (SelectedEmpire.isPlayer || !UsingNewEspioange || UsingNewEspioange && Player.GetRelations(SelectedEmpire).Espionage.CanViewRanks)
            {

                Empire[] empireList = UsingNewEspioange
                    ? Universe.UState.ActiveMajorEmpires.Filter(e => e.isPlayer || Player.GetRelations(e).Espionage.CanViewRanks)
                    : Universe.UState.ActiveMajorEmpires.Filter(e => e.isPlayer || Player.IsKnown(e));

                Vector2 columnBCursor = textCursor;
                columnBCursor.X += 190f;
                columnBCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                textCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.EconomicStrength), textCursor, Color.White);
                empireList.Sort(e => -e.GrossIncome);
                batch.DrawString(Fonts.Arial12Bold, $"# {GetRank(SelectedEmpire, empireList)}", columnBCursor, Color.White);
                columnBCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                textCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;

                empireList.Sort(e => -GetScientificStr(e));
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.ScientificStrength), textCursor, Color.White);
                batch.DrawString(Fonts.Arial12Bold, $"# {GetRank(SelectedEmpire, empireList)}", columnBCursor, Color.White);
                columnBCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                textCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;

                empireList.Sort(e => -e.CurrentMilitaryStrength);
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.MilitaryStrength), textCursor, Color.White);
                batch.DrawString(Fonts.Arial12Bold, $"# {GetRank(SelectedEmpire, empireList)}", columnBCursor, Color.White);
                columnBCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                textCursor.Y += Fonts.Arial12Bold.LineSpacing + 2;
                empireList.Sort(e => -GetPop(e));
                batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.Population), textCursor, Color.White);
                batch.DrawString(Fonts.Arial12Bold, $"# {GetRank(SelectedEmpire, empireList)}", columnBCursor, Color.White);

                textCursor.Y += Fonts.Arial12Bold.LineSpacing + 4;
                batch.DrawString(Fonts.Arial12, $"(out of {empireList.Length} empires)", textCursor, Color.Wheat);
            }
            } // end Info tab (Ludoal fork)
            //Added by McShooterz:  intel report
            Espionage espionage = SelectedEmpire.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(SelectedEmpire);
            if (tab == 1) { // Ludoal fork: Intelligence tab
            textCursor = new Vector2(IntelligenceRect.X + 20, IntelligenceRect.Y + 10);
            string intReport = Localizer.Token(GameText.IntelligenceReport);
            if (UsingNewEspioange && !SelectedEmpire.isPlayer)
                intReport += espionage.EffectiveLevel == 0 ? " (basic)" : $" (level {espionage.EffectiveLevel})";

            batch.DrawDropShadowText(intReport, textCursor, Fonts.Arial20Bold, SelectedEmpire.EmpireColor);
            textCursor.Y += (Fonts.Arial20Bold.LineSpacing + 5);

            if (UsingNewEspioange || IntelligenceLevel(SelectedEmpire) > 0)
                DrawDiploLine(batch, Font12, Localizer.Token(GameText.HomeWorld)+SelectedEmpire.data.Traits.HomeworldName, Color.Wheat, ref textCursor);

            if (UsingNewEspioange || IntelligenceLevel(SelectedEmpire) > 0)
            {
                if (SelectedEmpire.Capital != null)
                {
                    string controlsHomeworld = Localizer.Token(GameText.ControlsHomeWorld) + ((SelectedEmpire.Capital.Owner == SelectedEmpire)
                        ? Localizer.Token(GameText.Yes)
                        : Localizer.Token(GameText.No));

                    DrawDiploLine(batch, Font12, controlsHomeworld, Color.Wheat, ref textCursor);
                }

                bool alwaysShow = SelectedEmpire.isPlayer || !UsingNewEspioange;
                if (alwaysShow || espionage.CanViewNumPlanets)
                    DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.TotalPlanets)} {SelectedEmpire.GetPlanets().Count}", Color.Wheat, ref textCursor);

                if (alwaysShow || espionage.CanViewNumShips)
                    DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.TotalStarships)} {SelectedEmpire.OwnedShips.Count}", Color.Wheat, ref textCursor);

                if (alwaysShow || espionage.CanViewMoneyAndMaint)
                {
                    DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.Treasury)} {SelectedEmpire.Money.String(2)}", Color.Wheat, ref textCursor);
                    DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.MaintenanceCosts)} {SelectedEmpire.BuildingAndShipMaint.String(2)}", Color.Wheat, ref textCursor);
                }

                if (SelectedEmpire.Research.HasTopic)
                {
                    if (SelectedEmpire.isPlayer || UsingNewEspioange && espionage.CanViewResearchTopic || IntelligenceLevel(SelectedEmpire) > 1)
                        DrawDiploLine(batch, Font12, $"Researching: {SelectedEmpire.Research.Current.Tech.Name.Text}", Color.Wheat, ref textCursor);
                    else if (UsingNewEspioange && espionage.CanViewTechType || IntelligenceLevel(SelectedEmpire) > 0)
                        DrawDiploLine(batch, Font12, $"Researching: {SelectedEmpire.Research.Current.TechnologyType}", Color.Wheat, ref textCursor);
                    else
                        DrawDiploLine(batch, Font12, "Researching: Unknown", Color.Wheat, ref textCursor);
                }
            }

            if (!UsingNewEspioange)
            {
                if (IntelligenceLevel(SelectedEmpire) > 1)
                    DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.TotalSpies)} {SelectedEmpire.data.AgentList.Count}", Color.LightGreen, ref textCursor);
                else if (IntelligenceLevel(SelectedEmpire) > 0)
                    DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.TotalSpies)} {(SelectedEmpire.data.AgentList.Count >= Player.data.AgentList.Count ? "Many" : "Few")}", Color.Yellow, ref textCursor);
                else
                    DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.TotalSpies)} Unknown", Color.Pink, ref textCursor);
            }
            else if (SelectedEmpire != Player)
            {
                DrawDiploLine(batch, Font12, $"Their Infiltration Level: {espionage.InfiltrationLevelSummary()}", Color.Wheat, ref textCursor);
            }

            DrawDiploLine(batch, Font12, $"{Localizer.Token(GameText.Population2)} {GetPop(SelectedEmpire).String(1)} {Localizer.Token(GameText.Billion)}", Color.Wheat, ref textCursor);

            if (UsingNewEspioange && espionage?.CanViewTheirMoles == true || IntelligenceLevel(SelectedEmpire) > 1)
            {
                string traitlist = Font12.ParseText($"Number Of Moles: {Player.GetNumOfTheirMoles(SelectedEmpire)}", IntelligenceRect.Width - 10);
                DrawDiploLine(batch, Font12, traitlist, Color.Wheat, ref textCursor);
            }

            //Diplomatic Relations
            foreach (Relationship rel in SelectedEmpire.AllRelations)
            {
                if (!rel.Known || rel.Them.IsFaction || rel.Them.IsDefeated)
                    continue;

                if (SelectedEmpire.isPlayer 
                    || UsingNewEspioange && espionage.CanViewTheirTreaties
                    || IntelligenceLevel(SelectedEmpire) > 0)
                {
                    Color color = rel.Them.EmpireColor;
                    string name = rel.Them.data.Traits.Name;
                    string andTrade = rel.Treaty_Trade ? Localizer.Token(GameText.AndTrade) : ""; // "and Trade"

                    if      (rel.Treaty_Alliance)    DrawDiploLine(batch, Font12, $"{name}: {Localizer.Token(GameText.Alliance)} {andTrade}", color, ref textCursor);
                    else if (rel.Treaty_OpenBorders) DrawDiploLine(batch, Font12, $"{name}: {Localizer.Token(GameText.OpenBorders)} {andTrade}", color, ref textCursor);
                    else if (rel.Treaty_NAPact)      DrawDiploLine(batch, Font12, $"{name}: {Localizer.Token(GameText.NonaggressionPact2)} {andTrade}", color, ref textCursor);
                    else if (rel.Treaty_Peace)       DrawDiploLine(batch, Font12, $"{name}: {Localizer.Token(GameText.PeaceTreaty)} {andTrade}", color, ref textCursor);
                    else if (rel.AtWar)              DrawDiploLine(batch, Font12, $"{name}: {Localizer.Token(GameText.AtWar)} {andTrade}", color, ref textCursor);
                }
            }

            if (SelectedEmpire.isPlayer || (UsingNewEspioange && espionage?.CanViewTraitSet == true) || IntelligenceLevel(SelectedEmpire) > 1)
            {
                textCursor.Y += Font12.LineSpacing + 2;
                string traitlist = Font12.ParseText($"Racial Traits: {SelectedEmpire.data.SelectedTraitSet}", IntelligenceRect.Width - 50);
                DrawDiploLine(batch, Font12, traitlist, SelectedEmpire.EmpireColor, ref textCursor);
            }

            } // end Intelligence tab (Ludoal fork)
            //End of intel report
            if (tab == 2) { // Ludoal fork: Operations tab
            textCursor = new Vector2(OperationsRect.X + 20, OperationsRect.Y + 10);
            batch.DrawDropShadowText((SelectedEmpire.isPlayer ? Localizer.Token(GameText.YourEmpiresBonuses) : Localizer.Token(GameText.TheirBonuses)), textCursor, Fonts.Arial20Bold, SelectedEmpire.EmpireColor);
            textCursor.Y += Fonts.Arial20Bold.LineSpacing + 5;
            //Added by McShooterz: Only display modified bonuses
            if (SelectedEmpire.isPlayer 
                || UsingNewEspioange && espionage.CanViewBonuses 
                || IntelligenceLevel(SelectedEmpire) > 0)
            {
                if (SelectedEmpire.data.Traits.PopGrowthMax > 0f)
                    DrawBadStat(Localizer.Token(GameText.MaximumPopulationGrowth), "+"+SelectedEmpire.data.Traits.PopGrowthMax.ToString(".##"), ref textCursor);
                if (SelectedEmpire.data.Traits.PopGrowthMin > 0f)
                    DrawGoodStat(Localizer.Token(GameText.MinimumPopulationGrowth), "+"+SelectedEmpire.data.Traits.PopGrowthMin.ToString(".##"), ref textCursor);
                if (SelectedEmpire.data.Traits.ReproductionMod != 0)
                    DrawStat(Localizer.Token(GameText.PopulationGrowthModifier), SelectedEmpire.data.Traits.ReproductionMod, ref textCursor, false);
                if (SelectedEmpire.data.Traits.ConsumptionModifier != 0)
                    DrawStat(Localizer.Token(GameText.FoodConsumptionModifier), SelectedEmpire.data.Traits.ConsumptionModifier, ref textCursor, true);
                if (SelectedEmpire.data.Traits.ProductionMod != 0)
                    DrawStat(Localizer.Token(GameText.ProductionModifier), SelectedEmpire.data.Traits.ProductionMod, ref textCursor, false);
                if (SelectedEmpire.data.Traits.ResearchMod != 0)
                    DrawStat(Localizer.Token(GameText.ResearchModifier), SelectedEmpire.data.Traits.ResearchMod, ref textCursor, false);
                if (SelectedEmpire.data.Traits.DiplomacyMod != 0)
                    DrawStat(Localizer.Token(GameText.DiplomacyModifier), SelectedEmpire.data.Traits.DiplomacyMod, ref textCursor, false);
                if (SelectedEmpire.data.OngoingDiplomaticModifier != 0)
                    DrawStat(Localizer.Token(GameText.OngoingDiplomacyModifier), SelectedEmpire.data.OngoingDiplomaticModifier, ref textCursor, false);
                if (SelectedEmpire.data.Traits.GroundCombatModifier != 0)
                    DrawStat(Localizer.Token(GameText.TroopStrengthModifier), SelectedEmpire.data.Traits.GroundCombatModifier, ref textCursor, false);
                if (SelectedEmpire.data.Traits.ShipCostMod != 0)
                    DrawStat(Localizer.Token(GameText.ShipCostModifier), SelectedEmpire.data.Traits.ShipCostMod, ref textCursor, true);
                if (SelectedEmpire.data.Traits.ModHpModifier != 0)
                    DrawStat(Localizer.Token(GameText.ShipHitpointsModifier), SelectedEmpire.data.Traits.ModHpModifier, ref textCursor, false);
                //Added by McShooterz: new races stats to display in diplomacy
                if (SelectedEmpire.data.Traits.RepairMod != 0)
                    DrawStat(Localizer.Token(GameText.RepairRateModifier), SelectedEmpire.data.Traits.RepairMod, ref textCursor, false);
                if (SelectedEmpire.data.PowerFlowMod != 0)
                    DrawStat(Localizer.Token(GameText.ReactorPowerModifier), SelectedEmpire.data.PowerFlowMod, ref textCursor, false);
                if (SelectedEmpire.data.ShieldPowerMod != 0)
                    DrawStat(Localizer.Token(GameText.ShieldStrengthModifier), SelectedEmpire.data.ShieldPowerMod, ref textCursor, false);
                if (SelectedEmpire.data.MassModifier != 1)
                    DrawStat(Localizer.Token(GameText.ShipMassModifier), SelectedEmpire.data.MassModifier - 1f, ref textCursor, true);
                if (SelectedEmpire.data.Traits.TaxMod != 0)
                    DrawStat(Localizer.Token(GameText.TaxIncomeModifier), SelectedEmpire.data.Traits.TaxMod, ref textCursor, false);
                if (SelectedEmpire.data.Traits.MaintMod != 0 || SelectedEmpire.data.Traits.ShipMaintMultiplier < 1)
                {
                    if (SelectedEmpire.data.Traits.MaintMod != 0 )
                        DrawStat(Localizer.Token(GameText.MaintenanceModifier), SelectedEmpire.data.Traits.MaintMod, ref textCursor, true);

                    float shipMaintTotal = ((1 + SelectedEmpire.data.Traits.MaintMod) * SelectedEmpire.data.Traits.ShipMaintMultiplier) - 1;
                    DrawStat(Localizer.Token(GameText.ShipMaintenanceModifier), shipMaintTotal, ref textCursor, true);
                }

                DrawStat(Localizer.Token(GameText.InbordersFtlBonus), SelectedEmpire.data.Traits.InBordersSpeedBonus, ref textCursor, false);
                if (Universe.UState.P.FTLModifier != 1f)
                {
                    float fTLModifier = Universe.UState.P.FTLModifier * 100f;
                    DrawBadStat(Localizer.Token(GameText.InsystemFtlSpeed), fTLModifier.ToString("##")+"%", ref textCursor);
                }
                DrawStat(Localizer.Token(GameText.FtlSpeedMultiplier), string.Concat(SelectedEmpire.data.FTLModifier, "x"), ref textCursor);
                DrawStat(Localizer.Token(GameText.FtlPowerDrainModifier), string.Concat(SelectedEmpire.data.FTLPowerDrainModifier, "x"), ref textCursor);
                if (SelectedEmpire.data.FuelCellModifier != 0)
                    DrawStat(Localizer.Token(GameText.FuelCellModifier), SelectedEmpire.data.FuelCellModifier, ref textCursor, false);
                if (SelectedEmpire.data.SubLightModifier != 1)
                    DrawStat(Localizer.Token(GameText.SublightSpeedBonus), SelectedEmpire.data.SubLightModifier - 1f, ref textCursor, false);
                if (SelectedEmpire.data.SensorModifier != 1)
                    DrawStat(Localizer.Token(GameText.SensorRangeModifier), SelectedEmpire.data.SensorModifier - 1f, ref textCursor, false);
                if (SelectedEmpire.data.ExperienceMod != 0)
                    DrawStat("Ship Experience Modifier", SelectedEmpire.data.ExperienceMod, ref textCursor, false);
                if (SelectedEmpire.data.SpyModifier > 0f)
                    DrawGoodStat(Localizer.Token(GameText.SpyEffectivenessModifier), "+"+SelectedEmpire.data.SpyModifier.ToString("#"), ref textCursor);
                else if (SelectedEmpire.data.SpyModifier < 0f)
                    DrawBadStat(Localizer.Token(GameText.SpyEffectivenessModifier), "-"+SelectedEmpire.data.SpyModifier.ToString("#"), ref textCursor);
                if (SelectedEmpire.data.Traits.Spiritual != 0)
                    DrawStat(Localizer.Token(GameText.ArtifactBonusModifier), SelectedEmpire.data.Traits.Spiritual, ref textCursor, false);
                if (SelectedEmpire.data.Traits.TargetingModifier != 0)
                    DrawStat(Localizer.Token(GameText.CannonAccuracyModifier), SelectedEmpire.data.Traits.TargetingModifier, ref textCursor, false);
                if (SelectedEmpire.data.Traits.DodgeMod > 0)
                    DrawStat(Localizer.Token(GameText.DodgeModifier), SelectedEmpire.data.Traits.DodgeMod , ref textCursor, false);
                if (SelectedEmpire.data.OrdnanceEffectivenessBonus != 0)
                    DrawStat(Localizer.Token(GameText.OrdnanceDamageradiusModifier), SelectedEmpire.data.OrdnanceEffectivenessBonus, ref textCursor, false);
                if (SelectedEmpire.data.MissileHPModifier != 1)
                    DrawStat(Localizer.Token(GameText.MissileHitpointsBonus), SelectedEmpire.data.MissileHPModifier - 1f, ref textCursor, false);
                if (SelectedEmpire.data.MissileDodgeChance != 0)
                    DrawStat(Localizer.Token(GameText.MissileDodgeChance), SelectedEmpire.data.MissileDodgeChance, ref textCursor, false); 
                if (SelectedEmpire.data.ExoticStorageMultiplier != 1)
                    DrawStat(Localizer.Token(GameText.EmpireExoticStorage), SelectedEmpire.data.ExoticStorageMultiplier-1, ref textCursor, false);
                if (SelectedEmpire.data.MiningSpeedMultiplier != 1)
                    DrawStat(Localizer.Token(GameText.EmpireMiningSpeed), SelectedEmpire.data.MiningSpeedMultiplier-1, ref textCursor, false);
                if (SelectedEmpire.data.RefiningRatioMultiplier != 1)
                    DrawStat(Localizer.Token(GameText.EmpireRefiningEfficiency), SelectedEmpire.data.RefiningRatioMultiplier-1, ref textCursor, false);
            }
            } // end Operations tab (Ludoal fork)
            ArtifactsSL.Visible = tab == 0; // Ludoal fork: artifacts belong to the Info tab
            base.Draw(batch, elapsed);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        // Ludoal fork: one row per empire — portrait, name, posture, treaties at a glance
        void DrawEmpireRows(SpriteBatch batch)
        {
            foreach (RaceEntry race in Races)
            {
                if (race.e.IsFaction)
                    continue;
                Rectangle c = race.container;
                batch.FillRectangle(c, new Color(23, 20, 14));
                var portrait = new Rectangle(c.X + 2, c.Y + 2, 46, 56);
                bool known = race.e == Player || Player.IsKnown(race.e);

                if (!known)
                {
                    batch.Draw(ResourceManager.Texture("Portraits/unknown"), portrait, Color.White);
                    batch.DrawString(Font12Bold, "Unknown", new Vector2(c.X + 56, c.Y + 8), Color.Gray);
                }
                else
                {
                    batch.Draw(ResourceManager.Texture("Portraits/" + race.e.data.PortraitName), portrait, Color.White);
                    batch.DrawDropShadowText1(race.e.data.Traits.Name, new Vector2(c.X + 56, c.Y + 8), Font12Bold, race.e.EmpireColor);

                    string posture;
                    Color postureColor = Color.Wheat;
                    if (race.e == Player)
                        posture = Localizer.Token(GameText.You);
                    else if (race.e.IsDefeated)
                    {
                        posture = race.e.data.AbsorbedBy != null ? "Absorbed" : "Defeated";
                        postureColor = Color.Gray;
                        batch.Draw(ResourceManager.ErrorTexture, portrait, Color.White);
                    }
                    else
                    {
                        Relationship rel = Player.GetRelations(race.e);
                        if (rel.AtWar) { posture = Localizer.Token(GameText.AtWar); postureColor = Color.LightPink; }
                        else if (rel.Treaty_Alliance) { posture = Localizer.Token(GameText.Alliance); postureColor = Color.LightGreen; }
                        else if (rel.Treaty_Peace) { posture = Localizer.Token(GameText.PeaceTreaty); postureColor = Color.LightGreen; }
                        else { posture = "Neutral"; }
                        if (rel.Treaty_NAPact) posture += " | NAP";
                        if (rel.Treaty_Trade) posture += " | Trade";
                        if (rel.Treaty_OpenBorders) posture += " | Borders";
                        if (rel.AtWar)
                            batch.DrawRectangle(c, Color.Red);
                    }
                    batch.DrawString(Font12, posture, new Vector2(c.X + 56, c.Y + 32), postureColor);
                }

                if (race.e == SelectedEmpire)
                    batch.DrawRectangle(c, Color.Orange);
            }
        }

        // Ludoal fork: option A — the relations matrix. One glyph per pair, W > A > O > N > P > T.
        void DrawRelationsMatrix(SpriteBatch batch)
        {
            Empire[] majors = Universe.UState.ActiveMajorEmpires;
            if (majors.Length < 2)
                return;
            const int cell = 26;
            var origin = new Vector2(MatrixPanel.X + 60, MatrixPanel.Y + 58);

            for (int i = 0; i < majors.Length; ++i)
            {
                bool known = majors[i].isPlayer || Player.IsKnown(majors[i]);
                var colHead = new Rectangle((int)(origin.X + i * cell) + 3, (int)origin.Y - 24, 20, 20);
                var rowHead = new Rectangle((int)origin.X - 24, (int)(origin.Y + i * cell) + 3, 20, 20);
                if (known)
                {
                    batch.Draw(ResourceManager.Flag(majors[i].data.Traits.FlagIndex), colHead, majors[i].EmpireColor);
                    batch.Draw(ResourceManager.Flag(majors[i].data.Traits.FlagIndex), rowHead, majors[i].EmpireColor);
                }
                else
                {
                    batch.DrawString(Font12Bold, "?", new Vector2(colHead.X + 6, colHead.Y + 4), Color.Gray);
                    batch.DrawString(Font12Bold, "?", new Vector2(rowHead.X + 6, rowHead.Y + 4), Color.Gray);
                }
            }

            for (int i = 0; i < majors.Length; ++i)
            for (int j = 0; j < majors.Length; ++j)
            {
                var box = new Rectangle((int)(origin.X + j * cell), (int)(origin.Y + i * cell), cell - 2, cell - 2);
                if (i == j)
                {
                    batch.FillRectangle(box, new Color(40, 36, 26));
                    continue;
                }
                Empire a = majors[i], b = majors[j];
                string glyph = "?";
                Color color = Color.Gray;
                if (CanSeeRelation(a, b) && a.GetRelations(b, out Relationship rel) && rel.Known)
                {
                    if (rel.AtWar) { glyph = "W"; color = Color.Red; }
                    else if (rel.Treaty_Alliance) { glyph = "A"; color = Color.Gold; }
                    else if (rel.Treaty_OpenBorders) { glyph = "O"; color = Color.Cyan; }
                    else if (rel.Treaty_NAPact) { glyph = "N"; color = Color.SteelBlue; }
                    else if (rel.Treaty_Peace) { glyph = "P"; color = Color.LightGreen; }
                    else if (rel.Treaty_Trade) { glyph = "T"; color = Color.LightGreen; }
                    else { glyph = "-"; color = new Color(90, 90, 90); }
                }
                batch.FillRectangle(box, new Color(23, 20, 14));
                batch.DrawRectangle(box, new Color(60, 54, 40));
                batch.DrawString(Font12Bold, glyph, new Vector2(box.X + 8, box.Y + 5), color);
            }

            batch.DrawString(Font12, "W war  A alliance  O borders  N pact  P peace  T trade  ? unknown",
                new Vector2(MatrixPanel.X + 30, MatrixPanel.Bottom - 24), Color.Wheat);
        }

        bool CanSeeRelation(Empire a, Empire b)
        {
            if (a.isPlayer || b.isPlayer)
                return true;
            if (!Player.IsKnown(a) || !Player.IsKnown(b))
                return false;
            if (UsingNewEspioange)
                return Player.GetRelations(a).Espionage.CanViewTheirTreaties
                    || Player.GetRelations(b).Espionage.CanViewTheirTreaties;
            return IntelligenceLevel(a) > 0 || IntelligenceLevel(b) > 0;
        }

        int GetRank(Empire selectedEmpire, Empire[] empireList)
        {
            for (int i = 0; i < empireList.Length; i++)
            {
                Empire e = empireList[i];
                if (selectedEmpire == e)
                {
                    return i + 1;
                }
            }

            return empireList.Length;
        }

        private void DrawBadStat(string text, string text2, ref Vector2 Position)
        {
            Position = Position.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12, text, Position, Color.LightPink);
            Vector2 nPos = new Vector2(Position.X + 310f, Position.Y);
            //{
            nPos.X = nPos.X - Fonts.Arial12Bold.MeasureString(text2).X;
            //};
            nPos = nPos.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12Bold, text2, nPos, Color.LightPink);
            Position.Y = Position.Y + (Fonts.Arial12Bold.LineSpacing + 2);
        }

        private void DrawGoodStat(string text, string text2, ref Vector2 Position)
        {
            Position = Position.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12, text, Position, Color.LightGreen);
            Vector2 nPos = new Vector2(Position.X + 310f, Position.Y);
            //{
            nPos.X = nPos.X - Fonts.Arial12Bold.MeasureString(text2).X;
            //};
            nPos = nPos.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12Bold, text2, nPos, Color.LightGreen);
            Position.Y = Position.Y + (Fonts.Arial12Bold.LineSpacing + 2);
        }

        private void DrawStat(string text, float value, ref Vector2 Position, bool OppositeBonuses)
        {
            Color color;
            if (value <= 10f)
            {
                value = value * 100f;
            }
            if ((value > 0f && !OppositeBonuses) || (value < 0f && OppositeBonuses))
            {
                color = Color.LightGreen;
            }
            else
            {
                color = (value == 0f ? Color.White : Color.LightPink);
            }
            Position = Position.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12, text, Position, color);

            string valuePercent = value.ToString("#.##")+"%";
            var nPos = new Vector2(Position.X + 310f, Position.Y);
            nPos.X -= Fonts.Arial12Bold.MeasureString(valuePercent).X;

            nPos = nPos.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12Bold, valuePercent, nPos, color);
            Position.Y += Fonts.Arial12Bold.LineSpacing;
        }

        private void DrawStat(string text, string text2, ref Vector2 Position)
        {
            Position = Position.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12, text, Position, Color.White);
            Vector2 nPos = new Vector2(Position.X + 310f, Position.Y);
            //{
                nPos.X = nPos.X - Fonts.Arial12Bold.MeasureString(text2).X;
            //};
            nPos = nPos.ToFloored();
            ScreenManager.SpriteBatch.DrawString(Fonts.Arial12Bold, text2, nPos, Color.White);
            Position.Y = Position.Y + (Fonts.Arial12Bold.LineSpacing + 2);
        }

        private float GetPop(Empire e)
        {
            if (Traders.Contains(e) || e.isPlayer || UsingNewEspioange && Player.GetRelations(e).Espionage.CanViewPop)
                return e.TotalPopBillion;

            float pop = GetPopInExploredPlanetsFor(Player, e);
            foreach (Empire tradePartner in Traders)
                pop = GetPopInExploredPlanetsFor(tradePartner, e).LowerBound(pop);

            return pop;
        }

        float GetPopInExploredPlanetsFor(Empire exploringEmpire, Empire empire)
        {
            float pop = 0;
            foreach (SolarSystem system in exploringEmpire.Universe.Systems.Filter(s => s.IsExploredBy(exploringEmpire)))
            {
                foreach (Planet p in system.PlanetList)
                {
                    if (p.Owner == empire && p.IsExploredBy(exploringEmpire))
                        pop += p.PopulationBillion;
                }
            }

            return pop;
        }

        float GetScientificStr(Empire e)
        {
            if (UsingNewEspioange && (e.isPlayer || Player.GetRelations(e).Espionage.CanViewRanks))
            {
                var techs = e.UnlockedTechs;
                return techs.Length == 0 ? 0 : techs.Sum(t => t.Tech.Cost);
            }

            if (Friends.Contains(e) || e.isPlayer || IntelligenceLevel(e) > 2)
            {
                var techs = e.UnlockedTechs;
                return techs.Length == 0 ? 0 : techs.Sum(t => t.Tech.Cost);
            }

            float scientificStr = 0f;
            var techList = new HashSet<string>();
            Player.AI.ThreatMatrix.GetTechsFromPins(techList, e);
            foreach (Empire ally in Friends)
                ally.AI.ThreatMatrix.GetTechsFromPins(techList, e);

            foreach (string tech in techList)
                scientificStr += ResourceManager.Tech(tech).Cost;

            return scientificStr;
        }

        void CreateArtifactsScrollList(Empire empire)
        {
            SelectedEmpire = empire;
            ArtifactsSL.Reset();
            if (UsingNewEspioange && SelectedEmpire != Player && !Player.GetRelations(SelectedEmpire).Espionage.CanViewArtifacts)
                return;

            var entry = new ArtifactEntry();
            for (int i = 0; i < SelectedEmpire.data.OwnedArtifacts.Count; i++)
            {
                Artifact art = SelectedEmpire.data.OwnedArtifacts[i];
                var button = new SkinnableButton(new Rectangle(0, 0, 32, 32), $"Artifact Icons/{art.Name}")
                {
                    IsToggle = false,
                    ReferenceObject = art,
                    BaseColor = Color.White
                };

                if (entry.ArtifactButtons.Count < 5)
                {
                    entry.ArtifactButtons.Add(button);
                }
                if (entry.ArtifactButtons.Count == 5 || i == SelectedEmpire.data.OwnedArtifacts.Count - 1)
                {
                    ArtifactsSL.AddItem(new ArtifactItemListItem(entry));
                    entry = new ArtifactEntry();
                }
            }
            GameAudio.EchoAffirmative();
        }

        public override bool HandleInput(InputState input)
        {
            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (input.KeyPressed(Keys.I) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (DetailTabs.HandleInput(input)) // Ludoal fork: tab strip
                return true;

            if (DetailTabs.SelectedIndex == 0 && SelectedEmpire != Player && !SelectedEmpire.IsDefeated && Contact.HandleInput(input))
            {
                DiplomacyScreen.Show(SelectedEmpire, "Greeting", parent: this);
            }

            foreach (RaceEntry race in Races)
            {
                if (HelperFunctions.ClickedRect(race.container, input))
                {
                    if (Player == race.e || !Player.IsKnown(race.e))
                    {
                        if (Player == race.e)
                            CreateArtifactsScrollList(race.e);
                    }
                    else
                    {
                        CreateArtifactsScrollList(race.e);
                    }
                }
            }

            return base.HandleInput(input);
        }

        public override void LoadContent()
        {
            float screenWidth = ScreenWidth;
            float screenHeight = ScreenHeight;
            Rectangle titleRect = new Rectangle((int)screenWidth / 2 - 200, 44, 400, 80);
            TitleBar = new Menu2(titleRect);
            TitlePos = new Vector2(titleRect.X + titleRect.Width / 2 - Fonts.Laserian14.MeasureString(Localizer.Token(GameText.DiplomaticOverview)).X / 2f, titleRect.Y + titleRect.Height / 2 - Fonts.Laserian14.LineSpacing / 2);
            // Ludoal fork: FULL-SURFACE layout (Colony-style, field feedback) — the
            // frame spans the whole screen: empire rows left, tabs + relations matrix
            // right. Sized for up to 9 majors (CombinedArms raises MaxOpponents to 8).
            int topY = screenHeight > 768f ? titleRect.Y + titleRect.Height + 5 : 44;
            LeftRect = new Rectangle(20, topY, (int)screenWidth - 40, (int)screenHeight - topY - 20);
            DMenu = new Menu2(LeftRect);
            Add(new CloseButton(LeftRect.Right - 40, LeftRect.Y + 20));

            int rightX = LeftRect.X + 470;
            int rightW = LeftRect.Width - 490;
            int tabsH = (int)((LeftRect.Height - 60) * 0.62f);
            EmpiresPanel = new Submenu(new RectF(LeftRect.X + 20, LeftRect.Y + 20, 430, LeftRect.Height - 40), Localizer.Token(GameText.DiplomaticOverview));
            DetailTabs = new Submenu(new RectF(rightX, LeftRect.Y + 20, rightW, tabsH),
                new LocalizedText[] { "Info", Localizer.Token(GameText.IntelligenceReport), "Operations" });
            MatrixPanel = new Submenu(new RectF(rightX, LeftRect.Y + 30 + tabsH, rightW, LeftRect.Height - tabsH - 50), "Relations");
            SelectedInfoRect = new Rectangle(rightX + 10, LeftRect.Y + 65, rightW - 20, tabsH - 55);
            IntelligenceRect = SelectedInfoRect;
            OperationsRect = SelectedInfoRect;
            
            RectF artifacts = new(SelectedInfoRect.X , SelectedInfoRect.Y + 250, SelectedInfoRect.Width - 40, 130);
            ArtifactsSL = Add(new ScrollList<ArtifactItemListItem>(artifacts));
            
            Contact = new DanButton(new Vector2(SelectedInfoRect.X + SelectedInfoRect.Width / 2 - 91, SelectedInfoRect.Y + SelectedInfoRect.Height - 45), Localizer.Token(GameText.Contact))
            {
                Toggled = true
            };
            foreach (Empire e in Universe.UState.Empires)
            {
                if (e != Player)
                {
                    if (e.IsFaction)
                        continue;
                }
                else
                {
                    CreateArtifactsScrollList(e);
                }
                Races.Add(new RaceEntry { e = e });
            }
            int j = 0;
            foreach (RaceEntry re in Races)
            {
                // Ludoal fork: vertical rows in the Empires panel (fits 9 majors)
                re.container = new Rectangle(LeftRect.X + 30, LeftRect.Y + 65 + j * 64, 410, 60);
                j++;
            }
            GameAudio.MuteRacialMusic();

            DiagramButton = Add(new UIButton(ButtonStyle.Default, new Vector2(rightX + rightW - 220, LeftRect.Y + 36 + tabsH), "Diagram view"));
            DiagramButton.OnClick = b => AddRelationShipDiagramScreen();
        }

        void AddRelationShipDiagramScreen()
        {
            Array<EmpireAndIntelLevel> empiresAndIntel = new Array<EmpireAndIntelLevel>();
            foreach (Empire empire in Universe.UState.ActiveMajorEmpires)
            {
                int intel = empire.isPlayer ? 3 
                                            :  UsingNewEspioange ? Player.GetRelations(empire).Espionage.Level
                                                                 : IntelligenceLevel(empire);
                empiresAndIntel.Add(new EmpireAndIntelLevel(empire, intel));
            }

            var diagram = new RelationshipsDiagramScreen(this, Universe, empiresAndIntel);
            ScreenManager.AddScreen(diagram);
        }
    }

    public readonly struct EmpireAndIntelLevel
    {
        public readonly Empire Empire;
        public readonly int IntelLevel;

        public EmpireAndIntelLevel(Empire empire, int level)
        {
            Empire     = empire;
            IntelLevel = level;
        }
    }

}
