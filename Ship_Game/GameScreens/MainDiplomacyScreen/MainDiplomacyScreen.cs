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
using Color = Microsoft.Xna.Framework.Color;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game
{
    // Ludoal fork v3 (player design): full-surface dashboard — one COLUMN per major
    // empire, everything visible at once. Each column: header (portrait, click to
    // contact), fixed INFO block, a global INTELLIGENCE/BONUSES switch (intelligence
    // by default, one click flips every column so rows stay comparable), and a
    // TREATIES mini-matrix at the bottom (rows W/P, A/N, O, T — the pairs the treaty
    // exclusivity rules allow to share a line — versus every other empire).
    public sealed class MainDiplomacyScreen : GameScreen
    {
        UniverseScreen Universe;

        Menu2 TitleBar;
        Vector2 TitlePos;
        Menu2 DMenu;
        Rectangle LeftRect;

        UIButton ToggleButton; // labeled with the view you would switch TO (player design)
        UIButton DiagramButton;
        bool ShowBonuses;

        Empire Player;
        readonly bool UsingNewEspioange;
        Array<Empire> Friends;
        Array<Empire> Traders;
        HashSet<Empire> Moles;

        Array<RaceEntry> Races = new();

        Font Font12 = Fonts.Arial12;
        Font Font12Bold = Fonts.Arial12Bold;

        const int TreatyBlockH = 136; // player design: block sits lower, row labels gone

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
            foreach (Empire empire in screen.UState.Empires)
            {
                if (empire.isPlayer || empire.IsFaction)
                    continue;

                if (Player.data.MoleList.Any(m => empire.FindPlanet(m.PlanetId) != null))
                {
                    empires.Add(empire);
                }
                else
                {
                    foreach (Empire friend in Friends)
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

            foreach (Empire empire in Friends)
            {
                if (!empire.GetRelations(e, out Relationship rel))
                    continue;

                if (rel.Treaty_Trade && rel.Treaty_Trade_TurnsExisted > 30)
                    intelligence = 1;

                if (rel.Treaty_Alliance && rel.TurnsAllied > 3)
                    return 2;
            }

            if (intelligence == 0)
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

        public override void LoadContent()
        {
            float screenWidth = ScreenWidth;
            // Empire-style title bar: wide left cartouche, controls in the empty right third
            var titleRect = new Rectangle(2, 44, ScreenWidth * 2 / 3, 80);
            TitleBar = new Menu2(titleRect);
            TitlePos = new Vector2(titleRect.X + titleRect.Width / 2 - Fonts.Laserian14.MeasureString(Localizer.Token(GameText.DiplomaticOverview)).X / 2f, titleRect.Y + titleRect.Height / 2 - Fonts.Laserian14.LineSpacing / 2);

            LeftRect = new Rectangle(2, titleRect.Bottom + 5, (int)screenWidth - 10, ScreenHeight - titleRect.Bottom - 7);
            DMenu = new Menu2(LeftRect);
            Add(new CloseButton(LeftRect.Right - 40, LeftRect.Y + 20));

            // the global view toggle (labeled with what you would switch TO) and the
            // diagram button, right of the title cartouche
            ToggleButton = Add(new UIButton(ButtonStyle.Default, new Vector2(titleRect.Right + 30, titleRect.Y + 26), "Bonuses"));
            ToggleButton.OnClick = b =>
            {
                ShowBonuses = !ShowBonuses;
                ToggleButton.Text = ShowBonuses ? "Intelligence" : "Bonuses";
            };
            DiagramButton = Add(new UIButton(ButtonStyle.Default, new Vector2(titleRect.Right + 250, titleRect.Y + 26), "Diagram view"));
            DiagramButton.OnClick = b => AddRelationShipDiagramScreen();

            foreach (Empire e in Universe.UState.Empires)
            {
                if (e != Player && e.IsFaction)
                    continue;
                Races.Add(new RaceEntry { e = e });
            }

            // one column per major empire, full frame height, sized to fit them all
            int n = Races.Count.LowerBound(1);
            int colW = ((LeftRect.Width - 40) / n).UpperBound(230);
            int totalW = colW * n;
            int x0 = LeftRect.X + (LeftRect.Width - totalW) / 2;
            int j = 0;
            foreach (RaceEntry re in Races)
            {
                re.container = new Rectangle(x0 + j * colW, LeftRect.Y + 16, colW - 8, LeftRect.Height - 32);
                j++;
            }

            GameAudio.MuteRacialMusic();
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();
            TitleBar.Draw(batch, elapsed);
            batch.DrawString(Fonts.Laserian14, Localizer.Token(GameText.DiplomaticOverview), TitlePos, Colors.Cream);
            DMenu.Draw(batch, elapsed);

            foreach (RaceEntry race in Races)
                DrawColumn(batch, race);

            base.Draw(batch, elapsed);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        void DrawColumn(SpriteBatch batch, RaceEntry race)
        {
            Empire e = race.e;
            Rectangle col = race.container;
            batch.FillRectangle(col, new Color(23, 20, 14));
            batch.DrawRectangle(col, new Color(60, 54, 40));

            bool known = e == Player || Player.IsKnown(e);
            var portrait = new Rectangle(col.X + (col.Width - 56) / 2, col.Y + 6, 56, 70);

            if (!known)
            {
                batch.Draw(ResourceManager.Texture("Portraits/unknown"), portrait, Color.White);
                batch.DrawString(Font12Bold, "Unknown", new Vector2(col.X + (col.Width - Font12Bold.TextWidth("Unknown")) / 2f, portrait.Bottom + 4), Color.Gray);
                return;
            }

            if (e != Player && !e.IsDefeated && Player.IsAtWarWith(e))
                batch.DrawRectangle(new Rectangle(portrait.X - 2, portrait.Y - 2, portrait.Width + 4, portrait.Height + 4), Color.Red);

            batch.Draw(ResourceManager.Texture("Portraits/" + e.data.PortraitName), portrait, Color.White);
            string name = e.data.Traits.Name;
            float nameW = Font12Bold.TextWidth(name) + 22; // race flag rides left of the name
            float nameX = col.X + (col.Width - nameW) / 2f;
            batch.Draw(ResourceManager.Flag(e.data.Traits.FlagIndex), new Rectangle((int)nameX, portrait.Bottom + 3, 16, 16), e.EmpireColor);
            batch.DrawDropShadowText1(name, new Vector2(nameX + 22, portrait.Bottom + 4), Font12Bold, e.EmpireColor);

            float y = portrait.Bottom + 24;

            if (e.IsDefeated)
            {
                batch.Draw(ResourceManager.ErrorTexture, portrait, Color.White);
                string status = e.data.AbsorbedBy != null ? "Absorbed by " + e.data.AbsorbedBy : "Defeated";
                batch.DrawString(Font12, Font12.ParseText(status, col.Width - 16), new Vector2(col.X + 8, y), Color.Gray);
                return;
            }

            // FIXED section offsets: the bands align across columns whatever the content
            float infoY = col.Y + 104;
            float positionY = infoY + 24 + 3 * (Font12.LineSpacing + 3) + 4;
            float intelY = positionY + 24 + 4 * (Font12.LineSpacing + 3) + 4;

            y = infoY;
            SectionBand(batch, col, ref y, "INFO");
            DrawInfoBlock(batch, e, col, ref y);

            y = positionY;
            SectionBand(batch, col, ref y, "POSITION");
            DrawPositionBlock(batch, e, col, ref y);

            float maxY = col.Bottom - TreatyBlockH - 6;
            y = intelY;
            SectionBand(batch, col, ref y, ShowBonuses ? "BONUSES" : "INTELLIGENCE");
            if (ShowBonuses)
                DrawBonusRows(batch, e, col, ref y, maxY);
            else
                DrawIntelRows(batch, e, col, ref y, maxY);

            float ty = col.Bottom - TreatyBlockH;
            SectionBand(batch, col, ref ty, "TREATIES");
            DrawTreatyMatrix(batch, e, col, ty);
        }

        void SectionBand(SpriteBatch batch, Rectangle col, ref float y, string title)
        {
            var band = new Rectangle(col.X + 1, (int)y, col.Width - 2, 18);
            batch.FillRectangle(band, new Color(54, 46, 24));
            batch.DrawString(Font12Bold, title, new Vector2(band.X + (band.Width - Font12Bold.TextWidth(title)) / 2f, band.Y + 2), Colors.Cream);
            y += 24;
        }

        // tabulated row: label left, value right-aligned (player design: readability)
        void TableRow(SpriteBatch batch, Rectangle col, ref float y, float maxY, string label, string value, Color valueColor)
        {
            if (y > maxY - Font12.LineSpacing)
                return;
            string lbl = Truncate(label, col.Width - 16 - (int)Font12Bold.TextWidth(value) - 8);
            batch.DrawString(Font12, lbl, new Vector2(col.X + 8, y), Color.Wheat);
            batch.DrawString(Font12Bold, value, new Vector2(col.Right - 8 - Font12Bold.TextWidth(value), y), valueColor);
            y += Font12.LineSpacing + 3;
        }

        string Truncate(string text, int width)
        {
            if (Font12.TextWidth(text) <= width)
                return text;
            while (text.Length > 3 && Font12.TextWidth(text + "..") > width)
                text = text.Substring(0, text.Length - 1);
            return text + "..";
        }

        void DrawInfoBlock(SpriteBatch batch, Empire e, Rectangle col, ref float y)
        {
            float maxY = float.MaxValue;
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);

            if (e == Player)
            {
                TableRow(batch, col, ref y, maxY, "Status", Localizer.Token(GameText.You), Color.White);
            }
            else
            {
                Relationship rel = Player.GetRelations(e);
                string status; Color c;
                if (rel.AtWar) { status = Localizer.Token(GameText.AtWar); c = Color.LightPink; }
                else if (rel.Treaty_Alliance) { status = Localizer.Token(GameText.Alliance); c = Color.Gold; }
                else if (rel.Treaty_Peace) { status = $"Peace ({rel.PeaceTurnsRemaining})"; c = Color.LightGreen; }
                else if (rel.Treaty_OpenBorders) { status = Localizer.Token(GameText.OpenBorders); c = Color.Cyan; }
                else if (rel.Treaty_NAPact) { status = "NA Pact"; c = Color.LightGreen; }
                else { status = "Neutral"; c = Color.White; }
                TableRow(batch, col, ref y, maxY, "Status", status, c);
                TableRow(batch, col, ref y, maxY, "Trade", rel.Treaty_Trade ? "Yes" : "No", rel.Treaty_Trade ? Color.LightGreen : Color.Gray);
            }

            if (!e.isPlayer && (UsingNewEspioange ? espionage.CanViewPersonality : IntelligenceLevel(e) > 0))
            {
                string perso = $"{e.data.DiplomaticPersonality.Name} {e.data.EconomicPersonality.Name}";
                TableRow(batch, col, ref y, maxY, "Personality", Truncate(perso, col.Width - 80), Color.White);
            }
        }

        // POSITION: the empire's rank in each domain — same visibility rules as the
        // legacy screen
        void DrawPositionBlock(SpriteBatch batch, Empire e, Rectangle col, ref float y)
        {
            float maxY = float.MaxValue;
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);
            if (e.isPlayer || !UsingNewEspioange || espionage.CanViewRanks)
            {
                Empire[] pool = UsingNewEspioange
                    ? Universe.UState.ActiveMajorEmpires.Filter(x => x.isPlayer || Player.GetRelations(x).Espionage.CanViewRanks)
                    : Universe.UState.ActiveMajorEmpires.Filter(x => x.isPlayer || Player.IsKnown(x));
                TableRow(batch, col, ref y, maxY, "Economy", "#" + RankOf(e, pool.OrderByDescending(x => x.GrossIncome)), Color.White);
                TableRow(batch, col, ref y, maxY, "Science", "#" + RankOf(e, pool.OrderByDescending(GetScientificStr)), Color.White);
                TableRow(batch, col, ref y, maxY, "Military", "#" + RankOf(e, pool.OrderByDescending(x => x.CurrentMilitaryStrength)), Color.White);
                TableRow(batch, col, ref y, maxY, "Population", "#" + RankOf(e, pool.OrderByDescending(GetPop)), Color.White);
            }
            else
            {
                batch.DrawString(Font12, "No intelligence", new Vector2(col.X + 8, y), Color.Gray);
            }
        }

        int RankOf(Empire e, IEnumerable<Empire> ordered)
        {
            int i = 1;
            foreach (Empire x in ordered)
            {
                if (x == e) return i;
                ++i;
            }
            return i;
        }

        void DrawIntelRows(SpriteBatch batch, Empire e, Rectangle col, ref float y, float maxY)
        {
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);
            bool alwaysShow = e.isPlayer || !UsingNewEspioange;
            bool anyIntel = e.isPlayer || UsingNewEspioange || IntelligenceLevel(e) > 0;

            if (!anyIntel)
            {
                batch.DrawString(Font12, "No intelligence", new Vector2(col.X + 8, y), Color.Gray);
                return;
            }

            TableRow(batch, col, ref y, maxY, "Homeworld", Truncate(e.data.Traits.HomeworldName, 90), Color.White);
            if (e.Capital != null)
                TableRow(batch, col, ref y, maxY, "Controls HW", e.Capital.Owner == e ? Localizer.Token(GameText.Yes) : Localizer.Token(GameText.No), Color.White);

            if (alwaysShow || espionage.CanViewNumPlanets)
                TableRow(batch, col, ref y, maxY, "Planets", e.GetPlanets().Count.ToString(), Color.White);
            if (alwaysShow || espionage.CanViewNumShips)
                TableRow(batch, col, ref y, maxY, "Ships", e.OwnedShips.Count.ToString(), Color.White);
            if (alwaysShow || espionage.CanViewMoneyAndMaint)
            {
                TableRow(batch, col, ref y, maxY, "Treasury", e.Money.String(1) + " BC", Color.White);
                TableRow(batch, col, ref y, maxY, "Maintenance", e.BuildingAndShipMaint.String(1), Color.White);
            }

            TableRow(batch, col, ref y, maxY, "Population", GetPop(e).String(1) + " bn", Color.White);

            if (e.Research.HasTopic)
            {
                if (e.isPlayer || UsingNewEspioange && espionage.CanViewResearchTopic || IntelligenceLevel(e) > 1)
                    TableRow(batch, col, ref y, maxY, "Research", Truncate(e.Research.Current.Tech.Name.Text, 110), Color.White);
                else if (UsingNewEspioange && espionage.CanViewTechType || IntelligenceLevel(e) > 0)
                    TableRow(batch, col, ref y, maxY, "Research", e.Research.Current.TechnologyType.ToString(), Color.White);
                else
                    TableRow(batch, col, ref y, maxY, "Research", "Unknown", Color.Gray);
            }

            if (!UsingNewEspioange)
            {
                string spies = IntelligenceLevel(e) > 1 ? e.data.AgentList.Count.ToString()
                             : IntelligenceLevel(e) > 0 ? (e.data.AgentList.Count >= Player.data.AgentList.Count ? "Many" : "Few")
                             : "Unknown";
                TableRow(batch, col, ref y, maxY, "Spies", spies, Color.Wheat);
            }
            else if (e != Player)
            {
                TableRow(batch, col, ref y, maxY, "Infiltration", espionage.InfiltrationLevelSummary(), Color.Wheat);
            }

            if (e != Player && (UsingNewEspioange && espionage?.CanViewTheirMoles == true || IntelligenceLevel(e) > 1))
                TableRow(batch, col, ref y, maxY, "Their moles", Player.GetNumOfTheirMoles(e).ToString(), Color.Wheat);

            if (!UsingNewEspioange || e.isPlayer || espionage.CanViewArtifacts)
                TableRow(batch, col, ref y, maxY, "Artifacts", e.data.OwnedArtifacts.Count.ToString(), Color.White);
        }

        void DrawBonusRows(SpriteBatch batch, Empire e, Rectangle col, ref float y, float maxY)
        {
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);
            if (!(e.isPlayer || UsingNewEspioange && espionage.CanViewBonuses || IntelligenceLevel(e) > 0))
            {
                batch.DrawString(Font12, "No intelligence", new Vector2(col.X + 8, y), Color.Gray);
                return;
            }

            float yy = y;
            void Row(string label, float value, bool opposite = false)
            {
                if (value == 0f) return;
                float v = value;
                if (v <= 10f && v >= -10f) v *= 100f;
                Color c = (v > 0f && !opposite) || (v < 0f && opposite) ? Color.LightGreen : Color.LightPink;
                TableRow(batch, col, ref yy, maxY, label, v.ToString("#.##") + "%", c);
            }

            var t = e.data.Traits;
            if (t.PopGrowthMax > 0f) TableRow(batch, col, ref yy, maxY, "Max pop growth", "+" + t.PopGrowthMax.ToString(".##"), Color.LightPink);
            if (t.PopGrowthMin > 0f) TableRow(batch, col, ref yy, maxY, "Min pop growth", "+" + t.PopGrowthMin.ToString(".##"), Color.LightGreen);
            Row("Reproduction", t.ReproductionMod);
            Row("Consumption", t.ConsumptionModifier, opposite: true);
            Row("Production", t.ProductionMod);
            Row("Research", t.ResearchMod);
            Row("Diplomacy", t.DiplomacyMod);
            Row("Ongoing diplomacy", e.data.OngoingDiplomaticModifier);
            Row("Ground combat", t.GroundCombatModifier);
            Row("Ship cost", t.ShipCostMod, opposite: true);
            Row("Module HP", t.ModHpModifier);
            Row("Repair rate", t.RepairMod);
            Row("Reactor power", e.data.PowerFlowMod);
            Row("Shield power", e.data.ShieldPowerMod);
            Row("Ship mass", e.data.MassModifier - 1f, opposite: true);
            Row("Tax income", t.TaxMod);
            if (t.MaintMod != 0) Row("Maintenance", t.MaintMod, opposite: true);
            if (t.MaintMod != 0 || t.ShipMaintMultiplier < 1)
                Row("Ship maintenance", (1 + t.MaintMod) * t.ShipMaintMultiplier - 1, opposite: true);
            Row("In-borders FTL", t.InBordersSpeedBonus);
            TableRow(batch, col, ref yy, maxY, "FTL speed", e.data.FTLModifier + "x", Color.White);
            TableRow(batch, col, ref yy, maxY, "FTL power drain", e.data.FTLPowerDrainModifier + "x", Color.White);
            Row("Fuel cells", e.data.FuelCellModifier);
            if (e.data.SubLightModifier != 1) Row("Sublight speed", e.data.SubLightModifier - 1f);
            if (e.data.SensorModifier != 1) Row("Sensor range", e.data.SensorModifier - 1f);
            Row("Ship experience", e.data.ExperienceMod);
            if (e.data.SpyModifier > 0f) TableRow(batch, col, ref yy, maxY, "Spy effectiveness", "+" + e.data.SpyModifier.ToString("#"), Color.LightGreen);
            else if (e.data.SpyModifier < 0f) TableRow(batch, col, ref yy, maxY, "Spy effectiveness", "-" + e.data.SpyModifier.ToString("#"), Color.LightPink);
            Row("Artifact bonus", t.Spiritual);
            Row("Cannon accuracy", t.TargetingModifier);
            if (t.DodgeMod > 0) Row("Dodge", t.DodgeMod);
            Row("Ordnance damage", e.data.OrdnanceEffectivenessBonus);
            if (e.data.MissileHPModifier != 1) Row("Missile HP", e.data.MissileHPModifier - 1f);
            Row("Missile dodge", e.data.MissileDodgeChance);
            if (e.data.ExoticStorageMultiplier != 1) Row("Exotic storage", e.data.ExoticStorageMultiplier - 1);
            if (e.data.MiningSpeedMultiplier != 1) Row("Mining speed", e.data.MiningSpeedMultiplier - 1);
            if (e.data.RefiningRatioMultiplier != 1) Row("Refining", e.data.RefiningRatioMultiplier - 1);
            y = yy;
        }

        // per-column mini-matrix: x = the other empires, y = treaty families that
        // share a line because they exclude or imply each other (W/P, A/N), plus O, T
        void DrawTreatyMatrix(SpriteBatch batch, Empire e, Rectangle col, float top)
        {
            Empire[] others = Universe.UState.ActiveMajorEmpires.Filter(x => x != e);
            if (others.Length == 0)
                return;

            int cellW = ((col.Width - 20) / others.Length).UpperBound(26);
            float x0 = col.X + (col.Width - cellW * others.Length) / 2f;

            // flag header
            for (int jx = 0; jx < others.Length; ++jx)
            {
                var flag = new Rectangle((int)(x0 + jx * cellW) + (cellW - 14) / 2, (int)top, 14, 14);
                if (Player.IsKnown(others[jx]) || others[jx].isPlayer)
                    batch.Draw(ResourceManager.Flag(others[jx].data.Traits.FlagIndex), flag, others[jx].EmpireColor);
                else
                    batch.DrawString(Font12, "?", new Vector2(flag.X + 3, flag.Y), Color.Gray);
            }

            for (int iy = 0; iy < 4; ++iy)
            {
                float ry = top + 20 + iy * 22;
                for (int jx = 0; jx < others.Length; ++jx)
                {
                    string glyph = "?";
                    Color c = Color.Gray;
                    if (CanSeeRelation(e, others[jx]) && e.GetRelations(others[jx], out Relationship rel) && rel.Known)
                    {
                        switch (iy)
                        {
                            case 0:
                                if (rel.AtWar) { glyph = "W"; c = Color.Red; }
                                else if (rel.Treaty_Peace) { glyph = "P"; c = Color.LightGreen; }
                                else { glyph = "-"; c = new Color(90, 90, 90); }
                                break;
                            case 1:
                                if (rel.Treaty_Alliance) { glyph = "A"; c = Color.Gold; }
                                else if (rel.Treaty_NAPact) { glyph = "N"; c = Color.SteelBlue; }
                                else { glyph = "-"; c = new Color(90, 90, 90); }
                                break;
                            case 2:
                                if (rel.Treaty_OpenBorders) { glyph = "O"; c = Color.Cyan; }
                                else { glyph = "-"; c = new Color(90, 90, 90); }
                                break;
                            case 3:
                                if (rel.Treaty_Trade) { glyph = "T"; c = Color.LightGreen; }
                                else { glyph = "-"; c = new Color(90, 90, 90); }
                                break;
                        }
                    }
                    batch.DrawString(Font12Bold, glyph, new Vector2(x0 + jx * cellW + (cellW - Font12Bold.TextWidth(glyph)) / 2f, ry), c);
                }
            }
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

            // click a column header portrait to contact that empire
            foreach (RaceEntry race in Races)
            {
                var portrait = new Rectangle(race.container.X + (race.container.Width - 56) / 2, race.container.Y + 6, 56, 70);
                if (HelperFunctions.ClickedRect(portrait, input))
                {
                    if (race.e != Player && !race.e.IsDefeated && Player.IsKnown(race.e))
                    {
                        GameAudio.EchoAffirmative();
                        DiplomacyScreen.Show(race.e, "Greeting", parent: this);
                        return true;
                    }
                }
            }

            return base.HandleInput(input);
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

        void AddRelationShipDiagramScreen()
        {
            Array<EmpireAndIntelLevel> empiresAndIntel = new Array<EmpireAndIntelLevel>();
            foreach (Empire empire in Universe.UState.ActiveMajorEmpires)
            {
                int intel = empire.isPlayer ? 3
                                            : UsingNewEspioange ? Player.GetRelations(empire).Espionage.Level
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
            Empire = empire;
            IntelLevel = level;
        }
    }
}
