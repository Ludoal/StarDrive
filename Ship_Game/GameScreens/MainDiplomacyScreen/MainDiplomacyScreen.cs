using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using SDGraphics;
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.Gameplay;
using Ship_Game.GameScreens;
using Ship_Game.GameScreens.DiplomacyScreen;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;
using Ship_Game.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Font = Ship_Game.Graphics.Font;

namespace Ship_Game
{
    // Ludoal fork: full-surface dashboard — one COLUMN per major
    // empire, everything visible at once. Each column: header (portrait, click to
    // contact), fixed INFO block, a global INTELLIGENCE/BONUSES switch (intelligence
    // by default, one click flips every column so rows stay comparable), and a
    // TREATIES mini-matrix at the bottom (icon rows: state W/A/N/P — one line, war
    // breaks every treaty and alliance implies NA — then borders, then trade).
    public sealed class MainDiplomacyScreen : GameScreen
    {
        UniverseScreen Universe;

        Rectangle LeftRect;

        // Ludoal fork: the group's four tabs. Intelligence and Bonuses are two arrangements of
        // the same columns, drawn here; Relationships and Espionage hand over to their own screen
        // and snap the tab back, so the row never lies about where you are.
        Submenu GroupTabs;
        // Ludoal fork (bench 387): this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => GroupTabs?.Rect ?? base.PageFrame;
        readonly Tab OpenOn;
        public enum Tab { Intelligence = 0, Bonuses = 1, Relationships = 2, Espionage = 3 }
        bool ShowBonuses;
        int MaxTraitLines = 1; // longest trait list of the row, so BONUSES stays level

        Empire Player;
        readonly bool UsingNewEspioange;
        Array<Empire> Friends;
        Array<Empire> Traders;
        HashSet<Empire> Moles;

        Array<RaceEntry> Races = new();

        Font Font12 = Fonts.Arial12;
        Font Font12Bold = Fonts.Arial12Bold;
        Font NameFont = Fonts.Arial14Bold; // player call: bigger race names
        // Ludoal fork: where each empire's portrait landed this frame, so HandleInput can test it.
        // Filled by the draw because the columns are laid out there; the portrait is the control.
        readonly Map<Empire, Rectangle> PortraitRects = new();
        // Combined Arms fields more than eight majors (maintainer bench 299): the row
        // scrolls by whole columns behind this bar
        readonly ScreenGroups.RaceRowScroller Scroller = new();

        const int TreatyBlockH = 114; // player design: 3 icon rows (state / borders / trade), labels gone

        public MainDiplomacyScreen(UniverseScreen screen, Tab openOn = Tab.Intelligence)
            : base(screen, toPause: screen)
        {
            Universe = screen;
            OpenOn = openOn;
            ShowBonuses = openOn == Tab.Bonuses;
            IsPopup = true;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            Player = screen.Player;
            Friends = screen.UState.GetAllies(Player);
            Traders = screen.UState.GetTradePartners(Player);
            // legacy espionage is gone - constant true, kept as a field to spare its 25 call
            // sites; folding the dead IntelligenceLevel paths is a later simplification pass
            UsingNewEspioange = true;

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
            // Ludoal fork: the Diplomacy group of the unified top bar - four tabs where this
            // screen had a title cartouche, a view toggle and a diagram button. The tab row takes
            // the title's place and rides the same line as the top bar's Help and speed buttons,
            // to their left: those move into the unified bar later, so leaving a band free above
            // the frame would be building for a state that is going away (maintainer decision).
            // Y=64 is where EmpireUIOverlay draws that row, on a 24px texture.
            // The races come FIRST now: the frame hugs their columns (maintainer, 4 Aug), so
            // their count is an input to its geometry.
            foreach (Empire e in Universe.UState.Empires)
            {
                if (e != Player && e.IsFaction)
                    continue;
                Races.Add(new RaceEntry { e = e });
            }

            LeftRect = ScreenGroups.RaceColumnsFrame(ScreenWidth, ScreenHeight, Races.Count);
            GroupTabs = Add(new Submenu(new RectF(LeftRect.X, LeftRect.Y, LeftRect.Width, LeftRect.Height),
                                        ScreenGroups.LiveTitles(ScreenGroups.Group.Diplomacy, Universe)));
            GroupTabs.OnTabChange = OnGroupTabChanged;
            GroupTabs.PerformLayout(); // necessary: ClientArea is only known once the tabs are laid out

            Vector2 closePos = ScreenGroups.GroupClosePos(GroupTabs.ClientArea);
            Add(new CloseButton(closePos.X, closePos.Y));

            // Ludoal fork: the race-column doctrine - one bounded pitch from ScreenGroups, the
            // row centred in a frame that was sized on it. With more majors than the frame
            // shows (Combined Arms), the row scrolls by whole columns and the columns give
            // the scrollbar its lane at the foot.
            RectF client = GroupTabs.ClientArea;
            int colW = ScreenGroups.RaceColumnPitch(ScreenWidth, Races.Count);
            int visCols = ScreenGroups.RaceVisibleColumns(ScreenWidth, Races.Count);
            int x0 = ScreenGroups.RaceColumnsLeft(client, colW, visCols);
            Scroller.Count = Races.Count;
            Scroller.VisibleCols = visCols;
            Scroller.Pitch = colW;
            // the rail sits INSIDE the frame, its foot 5px off the bottom border, and the
            // columns give it the room (maintainer bench 301)
            Scroller.Track = new Rectangle(x0, (int)client.Bottom - 5, visCols * colW - ScreenGroups.ColumnGap, 9);
            Scroller.WheelArea = new Rectangle((int)client.X, (int)client.Y, (int)client.W, (int)client.H);
            int colH = ScreenGroups.GroupColumnHeight(client) - (Scroller.Overflowing ? 14 : 0);
            int j = 0;
            foreach (RaceEntry re in Races)
            {
                // Ludoal fork: inside the tab frame's client area, top and bottom - the Submenu is
                // the only thing that knows how tall its own tab row is.
                re.container = new Rectangle(x0 + j * colW, ScreenGroups.GroupColumnTop(client),
                                             colW - ScreenGroups.ColumnGap, colH);
                j++;
            }

            // how many trait lines the widest column needs, so the BONUSES band below them is
            // level across the row whatever each empire's trait set holds
            MaxTraitLines = 1;
            foreach (RaceEntry re in Races)
            {
                string set = $"{re.e.data.SelectedTraitSet}";
                if (set.Length == 0 && re.e.isPlayer && re.e.data.Traits.PlayerTraitOptions != null)
                    set = string.Join(", ", re.e.data.Traits.PlayerTraitOptions);
                int n2 = set.Split(',').Count(s => s.Trim().Length > 0);
                if (n2 > MaxTraitLines)
                    MaxTraitLines = n2;
            }

            // after the columns exist: selecting a tab only switches which blocks are drawn
            GroupTabs.SelectedIndex = (int)OpenOn;

            GameAudio.MuteRacialMusic();
        }

        void OnGroupTabChanged(int index)
        {
            // Ludoal fork (bench 379): the hosted colony's tab, appended past the stock four
            if (ScreenGroups.IsHostedTab(ScreenGroups.Group.Diplomacy, index, Universe))
            {
                ExitScreen();
                Universe.OpenHostedTabPanel?.Invoke();
                return;
            }
            switch ((Tab)index)
            {
                case Tab.Intelligence: ShowBonuses = false; break;
                case Tab.Bonuses:      ShowBonuses = true;  break;
                // These two live in their own screen, which carries the same tab row - so this one
                // steps aside rather than stacking on top.
                case Tab.Relationships:
                    AddRelationShipDiagramScreen(); // exits this screen itself
                    break;
                case Tab.Espionage:
                    ExitScreen();
                    // the concrete screen, not ScreenGroups.Espionage - that factory now points
                    // back at this group, which would loop
                    ScreenManager.AddScreen(new InfiltrationScreen(Universe));
                    break;
            }
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            batch.SafeBegin();

            // Ludoal fork: the frame fill goes down FIRST, by hand. As a Submenu background it is
            // one of the screen's children, so base.Draw painted it AFTER these columns and covered
            // them - SendToBackZOrder only orders it among the other children.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(GroupTabs), ScreenGroups.GroupFrameFill);

            // ⚠ cleared every pass: an empire that gets defeated, or drops out of what you know,
            // stops drawing its portrait - and a rect left behind would stay clickable over
            // whatever took its place.
            PortraitRects.Clear();
            for (int i = 0; i < Races.Count; ++i)
            {
                if (!Scroller.Overflowing || Scroller.Shows(i))
                    DrawColumn(batch, Races[i], Scroller.Overflowing ? Scroller.OffsetX : 0);
            }

            base.Draw(batch, elapsed);
            Scroller.Draw(batch); // over the border band, after the frame painted it
            ScreenGroups.DrawGroupTabTip(GroupTabs, Input.CursorPosition);
            Universe.EmpireUI.Draw(batch); // Ludoal fork: live top bar
            batch.SafeEnd();
        }

        void DrawColumn(SpriteBatch batch, RaceEntry race, int scrollX)
        {
            Empire e = race.e;
            Rectangle col = race.container;
            col.X -= scrollX; // whole-column scroll: the grid holds, only the window slides
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

            // Ludoal fork: the portrait IS the way in to negotiation, the way it is in the
            // Relationships diagram - framed in the empire's colour, thicker under the cursor.
            // It replaces the Contact button that used to sit below it, which cost a whole row
            // of every column to say what the portrait already stands for.
            if (e != Player && !e.IsDefeated)
            {
                PortraitRects[e] = portrait;
                bool hovered = portrait.HitTest(Input.CursorPosition);
                batch.DrawRectangle(portrait, e.EmpireColor, hovered ? 3 : 1);
            }
            // the race flag rides LEFT OF THE PORTRAIT, a touch bigger, so the name gets
            // the column's full width - some races did not fit a 900p column with the flag
            // on their line (maintainer bench 296)
            batch.Draw(ResourceManager.Flag(e.data.Traits.FlagIndex),
                       new Rectangle(portrait.X - 30, portrait.Y, 24, 24), e.EmpireColor);
            string name = e.data.Traits.Name;
            float nameX = col.X + (col.Width - NameFont.TextWidth(name)) / 2f;
            batch.DrawDropShadowText1(name, new Vector2(nameX, portrait.Bottom + 4), NameFont, e.EmpireColor);

            float y = portrait.Bottom + 24;

            if (e.IsDefeated)
            {
                batch.Draw(ResourceManager.ErrorTexture, portrait, Color.White);
                string status = e.data.AbsorbedBy != null ? "Absorbed by " + e.data.AbsorbedBy : "Defeated";
                batch.DrawString(Font12, Font12.ParseText(status, col.Width - 16), new Vector2(col.X + 8, y), Color.Gray);
                return;
            }

            // FIXED section offsets: the bands align across columns whatever the content.
            // Ludoal fork: 150 -> 108. The portrait ends at +76 and the name sits just under it;
            // the rest was the room the Contact button needed, and the portrait carries that job
            // now. Every column gains the row.
            float infoY = col.Y + 108;

            if (ShowBonuses)
            {
                // Bonuses tab: TRAITS, one line per trait, then BONUSES. Neither RACE INFO nor
                // RANK is repeated here - each block belongs to exactly one tab.
                float bonusMaxY = col.Bottom - 6;
                y = infoY;
                SectionBand(batch, col, ref y, "TRAITS");
                DrawTraitRows(batch, e, col, ref y);
                // BONUSES sits below the LONGEST trait list of any column, so the two bands stay
                // level across the row - a per-column offset would stagger them.
                float bonusesY = infoY + 24 + MaxTraitLines * (Font12.LineSpacing + 2) + 8;
                y = bonusesY;
                SectionBand(batch, col, ref y, "BONUSES");
                DrawBonusRows(batch, e, col, ref y, bonusMaxY);
                return;
            }

            // Intelligence tab.
            // Ludoal fork: the bands FOLLOW each other - every block advances y and the next one
            // starts where it stopped. They used to be placed at heights computed from a graven
            // row count apiece, so adding three rows to one of them did not push the rest down,
            // it drew over them. Every block here paints a CONSTANT number of rows whatever the
            // empire (a value you may not see reads "---"), which is what keeps the bands level
            // across columns without anyone having to count them.
            // ARTIFACTS is the one variable block, so it comes last and takes whatever is left.
            y = infoY;
            if (e == Player)
                y += 24; // no DISPOSITION band on our own column - but the cascade stays level
            else
                SectionBand(batch, col, ref y, "DISPOSITION");
            DrawInfoBlock(batch, e, col, ref y);

            y += 4;
            SectionBand(batch, col, ref y, "RANK");
            DrawPositionBlock(batch, e, col, ref y);

            y += 4;
            SectionBand(batch, col, ref y, "EMPIRE DATA");
            DrawIntelRows(batch, e, col, ref y, col.Bottom - 6);

            // Ludoal fork: TREATIES before ARTIFACTS. The treaty matrix is a fixed three rows, the
            // artifact list is as long as the empire's holdings - so the fixed block takes its
            // place first and the variable one runs to the bottom of the column, where growing
            // costs nothing.
            y += 4;
            float treatyY = y;
            SectionBand(batch, col, ref treatyY, "TREATIES");
            DrawTreatyMatrix(batch, e, col, treatyY);

            y = treatyY + TreatyBlockH - 24;
            SectionBand(batch, col, ref y, "ARTIFACTS");
            DrawArtifactRows(batch, e, col, ref y, col.Bottom - 6);

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
            // a folded label hangs its full self on hover (bench 305)
            if (lbl != label && new Rectangle(col.X + 8, (int)y, col.Width - 16, Font12.LineSpacing)
                                    .HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(label);
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

        // Personality and Research are the two rows whose values outgrow a 900p column
        // (maintainer bench 296): the label folds to its initial first, then the value keeps
        // its first word plus ".." - and any fold hangs the full pair on a hover tooltip.
        void FoldingRow(SpriteBatch batch, Rectangle col, ref float y, string label, string shortLabel,
                        string value, Color valueColor)
        {
            int room = col.Width - 16;
            string lbl = label, val = value;
            bool folded = false;
            if (Font12.TextWidth(lbl) + 8 + Font12Bold.TextWidth(val) > room)
            {
                lbl = shortLabel;
                folded = true;
                int valRoom = room - (int)Font12.TextWidth(lbl) - 8;
                if (Font12Bold.TextWidth(val) > valRoom)
                {
                    int cut = val.IndexOf(' ');
                    val = (cut > 0 ? val.Substring(0, cut) : val) + "..";
                    while (val.Length > 3 && Font12Bold.TextWidth(val) > valRoom)
                        val = val.Substring(0, val.Length - 3) + "..";
                }
            }
            if (folded && new Rectangle(col.X + 8, (int)y, room, Font12.LineSpacing).HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip($"{label}: {value}");
            batch.DrawString(Font12, lbl, new Vector2(col.X + 8, y), Color.Wheat);
            batch.DrawString(Font12Bold, val, new Vector2(col.Right - 8 - Font12Bold.TextWidth(val), y), valueColor);
            y += Font12.LineSpacing + 3;
        }

        void DrawInfoBlock(SpriteBatch batch, Empire e, Rectangle col, ref float y)
        {
            float maxY = float.MaxValue;
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);

            // Status and Trade retired (maintainer bench 299): the TREATIES matrix carries
            // both - each column's rows run against every empire, the player included - and
            // their two lines go to ARTIFACTS, which takes whatever is left.
            // ⚠ The four slots below are what an empire has ABOUT you and you cannot have
            // about yourself - reserved rather than skipped: this column has to stay level
            // with the others, which is why every unavailable value in this screen keeps
            // its line.
            if (e == Player)
            {
                BlankRow(ref y); // (personality slot)
                BlankRow(ref y); // (trust slot)
                BlankRow(ref y); // (anger slot)
                BlankRow(ref y); // (threat slot)
                return;
            }
            if (UsingNewEspioange ? espionage.CanViewPersonality : IntelligenceLevel(e) > 0)
            {
                string perso = $"{e.data.DiplomaticPersonality.Name} {e.data.EconomicPersonality.Name}";
                FoldingRow(batch, col, ref y, "Personality", "P.", perso, Color.White);
            }
            else
            {
                HiddenRow(batch, col, ref y, maxY, "Personality", 1);
            }

            // Ludoal fork: what this empire actually FEELS about you, in the room the Contact
            // button used to take - the three the negotiation screen graphs, at a glance and for
            // every empire at once instead of one at a time. Same colours it uses (green, yellow,
            // red) and the same 0-100 clamp, so the numbers here and the bars there agree.
            // NO espionage gate: the negotiation screen has always drawn these three bars for
            // anyone you can talk to, so gating the same three numbers here would make the
            // overview say LESS than a screen one click away (maintainer feedback).
            // ⚠ Every path through this draws THREE rows, whatever it can show - the column has to
            // stay level with its neighbours, and a branch that quietly drew none would shift
            // every band below it in that one column. An empire we have no relationship with at
            // all still gets its three placeholders.
            if (e.GetRelations(Player, out Relationship toUs))
            {
                BarRow(batch, col, ref y, Localizer.Token(GameText.Trust), toUs.Trust, Color.Green);
                BarRow(batch, col, ref y, Localizer.Token(GameText.Anger), toUs.TotalAnger, Color.Yellow);
                BarRow(batch, col, ref y, Localizer.Token(GameText.Threat), toUs.Threat, Color.Red);
            }
            else
            {
                // no spy badge here - this is "we have never met them", not "your espionage is
                // too low". The badge would promise a level that unlocks nothing.
                HiddenRow(batch, col, ref y, maxY, Localizer.Token(GameText.Trust));
                HiddenRow(batch, col, ref y, maxY, Localizer.Token(GameText.Anger));
                HiddenRow(batch, col, ref y, maxY, Localizer.Token(GameText.Threat));
            }
        }

        void BlankRow(ref float y)
        {
            y += Font12.LineSpacing + 3;
        }

        // Trust/Anger/Threat wear a progress bar now (maintainer bench 297): half the column
        // wide, the negotiation screen's own gradient, palette and 0-100 clamp, with the figure
        // keeping its right-aligned lane after the bar.
        void BarRow(SpriteBatch batch, Rectangle col, ref float y, string label, float value, Color color)
        {
            batch.DrawString(Font12, label, new Vector2(col.X + 8, y), Color.Wheat);
            float v = value.Clamped(0, 100);
            string num = v.String(0);
            float numLane = Font12Bold.TextWidth("100") + 4; // one lane for the three rows
            batch.DrawString(Font12Bold, num, new Vector2(col.Right - 8 - Font12Bold.TextWidth(num), y), color);
            int barW = col.Width / 2;
            var bar = new Rectangle(col.Right - 8 - (int)numLane - barW, (int)y + 2, barW, Font12.LineSpacing - 4);
            batch.FillRectangle(bar, new Color(10, 10, 10));
            if (v > 0f)
                batch.Draw(ResourceManager.Texture("UI/bw_bargradient_2"),
                           new Rectangle(bar.X, bar.Y, (int)(bar.Width * v / 100f), bar.Height), color);
            batch.DrawRectangle(bar, new Color(60, 54, 40));
            y += Font12.LineSpacing + 3;
        }

        // spy icon + "lvl x" — every espionage-locked placeholder wears the badge
        void SpyLvl(SpriteBatch batch, float x, float y, byte lvl)
        {
            SubTexture spy = ResourceManager.Texture("UI/icon_spy");
            int h = Font12.LineSpacing;
            var r = new Rectangle((int)x, (int)y, spy.Width * h / spy.Height, h);
            batch.Draw(spy, r, new Color(105, 105, 105));
            batch.DrawString(Font12Bold, $"lvl {lvl}", new Vector2(r.Right + 4, y), new Color(105, 105, 105));
        }

        float SpyLvlWidth(byte lvl)
        {
            SubTexture spy = ResourceManager.Texture("UI/icon_spy");
            int h = Font12.LineSpacing;
            return spy.Width * h / spy.Height + 4 + Font12Bold.TextWidth($"lvl {lvl}");
        }

        // aligned placeholder row for data the current infiltration level hides
        void HiddenRow(SpriteBatch batch, Rectangle col, ref float y, float maxY, string label, byte lvl = 0)
        {
            if (y > maxY - Font12.LineSpacing)
                return;
            batch.DrawString(Font12, label, new Vector2(col.X + 8, y), new Color(105, 105, 105));
            if (lvl > 0)
                SpyLvl(batch, col.Right - 8 - SpyLvlWidth(lvl), y, lvl);
            else
                batch.DrawString(Font12Bold, "---", new Vector2(col.Right - 8 - Font12Bold.TextWidth("---"), y), new Color(105, 105, 105));
            y += Font12.LineSpacing + 3;
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
                HiddenRow(batch, col, ref y, maxY, "Economy", 2);
                HiddenRow(batch, col, ref y, maxY, "Science", 2);
                HiddenRow(batch, col, ref y, maxY, "Military", 2);
                HiddenRow(batch, col, ref y, maxY, "Population", 2);
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

            // fixed row set, ordered by the infiltration level that unlocks each
            // datum (player call); hidden values read "---" so every empire's data
            // sits on the same line across columns
            if (anyIntel)
                TableRow(batch, col, ref y, maxY, "Homeworld", Truncate(e.data.Traits.HomeworldName, 90), Color.White);
            else
                HiddenRow(batch, col, ref y, maxY, "Homeworld", 1);

            if (anyIntel && e.Capital != null)
                TableRow(batch, col, ref y, maxY, "Controls HW", e.Capital.Owner == e ? Localizer.Token(GameText.Yes) : Localizer.Token(GameText.No), Color.White);
            else
                HiddenRow(batch, col, ref y, maxY, "Controls HW", 1);

            if (!UsingNewEspioange)
            {
                string spies = IntelligenceLevel(e) > 1 ? e.data.AgentList.Count.ToString()
                             : IntelligenceLevel(e) > 0 ? (e.data.AgentList.Count >= Player.data.AgentList.Count ? "Many" : "Few")
                             : "Unknown";
                TableRow(batch, col, ref y, maxY, "Spies", spies, Color.Wheat);
            }
            else if (e != Player)
            {
                // THEIR network in YOUR empire - the precision follows your own level on
                // them, which is why the value mixes words and figures (bench 305: the
                // old "Infiltration" label read as yours)
                float rowY = y;
                TableRow(batch, col, ref y, maxY, "Spies", espionage.InfiltrationLevelSummary(), Color.White);
                if (new Rectangle(col.X + 8, (int)rowY, col.Width - 16, Font12.LineSpacing).HitTest(Input.CursorPosition))
                    ToolTip.CreateTooltip("How deep THEY have infiltrated YOUR empire.\n"
                                        + "Your own infiltration level on them sets the precision:\n"
                                        + "level 2 says whether their network exists, 3-4 reads it\n"
                                        + "Shallow or Deep, and 5 gives their exact level.");
            }
            else
            {
                BlankRow(ref y); // the player's own column: just space
            }

            // level 1: planets, population
            if (anyIntel && (alwaysShow || espionage.CanViewNumPlanets))
                TableRow(batch, col, ref y, maxY, "Planets", e.GetPlanets().Count.ToString(), Color.White);
            else
                HiddenRow(batch, col, ref y, maxY, "Planets", 1);

            // the estimate path sums the pop of THEIR planets we explored, so an empire
            // with none explored read "0 bn" as if known (maintainer bench 297) - a zero
            // estimate with no viewing right is a placeholder, not a figure
            float pop = GetPop(e);
            if (pop > 0f || e.isPlayer || Traders.Contains(e) || UsingNewEspioange && espionage?.CanViewPop == true)
                TableRow(batch, col, ref y, maxY, "Population", pop.String(1) + " bn", Color.White);
            else
                HiddenRow(batch, col, ref y, maxY, "Population", 1);

            // level 2: ships, research (tech type; the exact topic is level 3)
            if (anyIntel && (alwaysShow || espionage.CanViewNumShips))
                TableRow(batch, col, ref y, maxY, "Ships", e.OwnedShips.Count.ToString(), Color.White);
            else
                HiddenRow(batch, col, ref y, maxY, "Ships", 2);

            if (e.Research.HasTopic && (e.isPlayer || UsingNewEspioange && espionage.CanViewResearchTopic || IntelligenceLevel(e) > 1))
                FoldingRow(batch, col, ref y, "Research", "R.", e.Research.Current.Tech.Name.Text, Color.White);
            else if (e.Research.HasTopic && (UsingNewEspioange && espionage.CanViewTechType || IntelligenceLevel(e) > 0))
                // this level sees the CATEGORY, not the topic - suffixed, because the
                // category named Research read "Research Research" (maintainer bench 297)
                TableRow(batch, col, ref y, maxY, "Research", e.Research.Current.TechnologyType + " tech", Color.White);
            else if (e.isPlayer && !e.Research.HasTopic)
                TableRow(batch, col, ref y, maxY, "Research", "None", Color.Gray);
            else
                HiddenRow(batch, col, ref y, maxY, "Research", 2);

            // level 3: money
            if (anyIntel && (alwaysShow || espionage.CanViewMoneyAndMaint))
            {
                TableRow(batch, col, ref y, maxY, "Treasury", e.Money.String(1) + " BC", Color.White);
                TableRow(batch, col, ref y, maxY, "Maintenance", e.BuildingAndShipMaint.String(1), Color.White);
            }
            else
            {
                HiddenRow(batch, col, ref y, maxY, "Treasury", 3);
                HiddenRow(batch, col, ref y, maxY, "Maintenance", 3);
            }

            // level 5: their moles
            if (e != Player && (UsingNewEspioange && espionage?.CanViewTheirMoles == true || IntelligenceLevel(e) > 1))
                TableRow(batch, col, ref y, maxY, "Their moles", Player.GetNumOfTheirMoles(e).ToString(), Color.Wheat);
            else if (e == Player)
                BlankRow(ref y);
            else
                HiddenRow(batch, col, ref y, maxY, "Their moles", 5);
        }

        // racial traits (lvl 4 intel), between INTELLIGENCE and ARTIFACTS (player call)
        void DrawTraitRows(SpriteBatch batch, Empire e, Rectangle col, ref float y)
        {
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);
            if (e.isPlayer || (UsingNewEspioange ? espionage.CanViewTraitSet : IntelligenceLevel(e) > 1))
            {
                string traitSet = $"{e.data.SelectedTraitSet}"; // null-safe: interpolation, like the legacy screen
                if (traitSet.Length == 0 && e.isPlayer && e.data.Traits.PlayerTraitOptions != null)
                    traitSet = string.Join(", ", e.data.Traits.PlayerTraitOptions); // saves generated before the 46 fix
                if (traitSet.Length == 0)
                {
                    batch.DrawString(Font12, "None", new Vector2(col.X + 8, y), Color.Gray);
                    return;
                }
                // Ludoal fork: one line per trait. The set is comma separated, so it splits on the
                // separator rather than on the column width - wrapping packed several traits per
                // line and cut the list at three. The Bonuses tab has the room for all of them.
                foreach (string trait in traitSet.Split(','))
                {
                    string t = trait.Trim();
                    if (t.Length == 0)
                        continue;
                    // the trait's own record serves twice: its Cost signs the COLOUR (green
                    // bonus, pink malus - the race tint said nothing, bench 305) and its
                    // Description hangs on hover (bench 296)
                    var opt = ResourceManager.RaceTraits.TraitList.Find(o => o.LocalizedName.Text == t);
                    if (opt != null && opt.Description != 0
                        && new Rectangle(col.X + 8, (int)y, col.Width - 16, Font12.LineSpacing)
                               .HitTest(Input.CursorPosition))
                    {
                        ToolTip.CreateTooltip(new LocalizedText(opt.Description));
                    }
                    Color tc = opt == null ? Color.White
                             : opt.Cost > 0 ? Color.LightGreen
                             : opt.Cost < 0 ? Color.LightPink : Color.White;
                    batch.DrawString(Font12, Font12.ParseText(t, col.Width - 16),
                                     new Vector2(col.X + 8, y), tc);
                    y += Font12.LineSpacing + 2;
                }
            }
            else
            {
                SpyLvl(batch, col.X + 8, y, 4);
            }
        }

        // nominative artifact list (player design) — same visibility as the legacy list
        void DrawArtifactRows(SpriteBatch batch, Empire e, Rectangle col, ref float y, float maxY)
        {
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);
            if (!(!UsingNewEspioange || e.isPlayer || espionage.CanViewArtifacts))
            {
                SpyLvl(batch, col.X + 8, y, 2);
                return;
            }
            if (e.data.OwnedArtifacts.Count == 0)
            {
                batch.DrawString(Font12, "None", new Vector2(col.X + 8, y), Color.Gray);
                return;
            }
            // duplicates collapse to one line with a count, "(x2)" (maintainer bench 298)
            var counts = new Map<string, int>();
            var order = new Array<Artifact>();
            foreach (Artifact a in e.data.OwnedArtifacts)
            {
                if (counts.ContainsKey(a.Name)) counts[a.Name] += 1;
                else { counts[a.Name] = 1; order.Add(a); }
            }
            foreach (Artifact art in order)
            {
                if (y > maxY - Font12.LineSpacing)
                {
                    batch.DrawString(Font12, "...", new Vector2(col.X + 8, y), Color.Wheat);
                    break;
                }
                // its icon ahead of the name (maintainer bench 297), line-height sized -
                // the artifact icons are keyed by the INTERNAL name, like the event popup's
                int ih = Font12.LineSpacing + 2;
                // and its own localized description on hover (maintainer bench 298)
                if (new Rectangle(col.X + 8, (int)y - 1, col.Width - 16, ih).HitTest(Input.CursorPosition))
                    ToolTip.CreateTooltip(new LocalizedText(art.DescriptionIndex));
                batch.Draw(ResourceManager.Texture("Artifact Icons/" + art.Name),
                           new Rectangle(col.X + 8, (int)y - 1, ih, ih), Color.White);
                int n = counts[art.Name];
                string label = n > 1 ? $"{art.NameText.Text} (x{n})" : art.NameText.Text;
                batch.DrawString(Font12, Truncate(label, col.Width - 20 - ih - 4),
                                 new Vector2(col.X + 8 + ih + 4, y), Color.Wheat);
                y += Font12.LineSpacing + 3;
            }
        }

        void DrawBonusRows(SpriteBatch batch, Empire e, Rectangle col, ref float y, float maxY)
        {
            Espionage espionage = e.isPlayer || !UsingNewEspioange ? null : Player.GetEspionage(e);
            if (!(e.isPlayer || UsingNewEspioange && espionage.CanViewBonuses || IntelligenceLevel(e) > 0))
            {
                SpyLvl(batch, col.X + 8, y, 3);
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
                Row("Ship Maint", (1 + t.MaintMod) * t.ShipMaintMultiplier - 1, opposite: true); // short: the long label folded mid-word (bench 305)
            Row("In-borders FTL", t.InBordersSpeedBonus);
            TableRow(batch, col, ref yy, maxY, "FTL speed", e.data.FTLModifier + "x", Color.White);
            TableRow(batch, col, ref yy, maxY, "FTL power drain", e.data.FTLPowerDrainModifier + "x", Color.White);
            Row("Fuel cells", e.data.FuelCellModifier);
            if (e.data.SubLightModifier != 1) Row("Sublight speed", e.data.SubLightModifier - 1f);
            if (e.data.SensorModifier != 1) Row("Sensor range", e.data.SensorModifier - 1f);
            Row("Ship experience", e.data.ExperienceMod);
            // ⚠ no hand-prefixed sign on the negative branch: ToString("#") already carries
            // it, and the pair printed "--10" (bench 305)
            if (e.data.SpyModifier > 0f) TableRow(batch, col, ref yy, maxY, "Spy effectiveness", "+" + e.data.SpyModifier.ToString("#"), Color.LightGreen);
            else if (e.data.SpyModifier < 0f) TableRow(batch, col, ref yy, maxY, "Spy effectiveness", e.data.SpyModifier.ToString("#"), Color.LightPink);
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

            // separator between the race-flag header and the treaty rows
            int sepY = (int)top + 17;
            int sepX = (int)x0;
            int sepW = (int)(cellW * others.Length);
            batch.DrawLine(new Vector2(sepX, sepY), new Vector2(sepX + sepW, sepY), new Color(255, 255, 255, 40).Premultiplied());

            // merged status row (maintainer feedback): war BREAKS every treaty at declaration
            // (DeclareWarOn → BreakAllTreatiesWith includingPeace) and alliance
            // auto-signs NA — so one row with priority W > A > N > P loses nothing
            // but a truce still ticking under a fresher alliance. Icons, not letters.
            for (int iy = 0; iy < 3; ++iy)
            {
                float ry = top + 20 + iy * 22;
                for (int jx = 0; jx < others.Length; ++jx)
                {
                    SubTexture icon = null;
                    Color tint = Color.White;
                    string glyph = "?";
                    string tip = null;   // maintainer bench 336: hover text for the treaty icon
                    if (CanSeeRelation(e, others[jx]) && e.GetRelations(others[jx], out Relationship rel) && rel.Known)
                    {
                        // tints match the Relationships Cross Reference palette:
                        // War red, Peace white, Alliance green, NA blue, Open Borders purple, Trade yellow
                        glyph = "-";
                        string them = others[jx].data.Traits.Name;
                        switch (iy)
                        {
                            case 0: // the state of the relation, strongest bond first
                                if (rel.AtWar) { icon = ResourceManager.Texture("UI/icon_fighting_small"); tint = Color.Red; tip = $"At war with {them}"; }
                                else if (rel.Treaty_Alliance) { icon = ResourceManager.Texture("UI/flagicon"); tint = Color.Green; tip = $"Allied with {them}"; }
                                else if (rel.Treaty_NAPact) { icon = ResourceManager.Texture("UI/icon_shield"); tint = Color.DeepSkyBlue; tip = $"Non-Aggression Pact with {them}"; }
                                else if (rel.Treaty_Peace) { icon = ResourceManager.Texture("UI/icon_peace"); tint = Color.White; tip = $"At peace with {them}"; }
                                break;
                            case 1:
                                if (rel.Treaty_OpenBorders) { icon = ResourceManager.Texture("NewUI/icon_intertrade"); tint = Color.Violet; tip = $"Open Borders with {them}"; }
                                break;
                            case 2:
                                if (rel.Treaty_Trade) { icon = ResourceManager.Texture("NewUI/icon_money"); tint = Color.Yellow; tip = $"Trade Treaty with {them}"; }
                                break;
                        }
                    }
                    var iconRect = new Rectangle((int)(x0 + jx * cellW) + (cellW - 14) / 2, (int)ry, 14, 14);
                    if (icon != null)
                        batch.Draw(icon, iconRect, tint);
                    else
                        batch.DrawString(Font12Bold, glyph, new Vector2(x0 + jx * cellW + (cellW - Font12Bold.TextWidth(glyph)) / 2f, ry), new Color(90, 90, 90));
                    if (tip != null && iconRect.HitTest(Input.CursorPosition))
                        ToolTip.CreateTooltip(tip);
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
            // Ludoal fork (bench 46.173): the closing key is tested BEFORE the top bar, not
            // after. The bar reads the same key to OPEN this screen and returns true, so with the
            // bar first the key never reached the line below and the screen would not close on
            // its own hotkey (maintainer feedback). The stock screen has no bar, which is why it never showed.
            if (input.KeyPressed(Keys.I) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            if (Universe.EmpireUI.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;


            if (Scroller.HandleInput(input))
                return true;

            // Ludoal fork: the portrait opens negotiation, as it does in the Relationships
            // diagram. The rects come from the last draw, which is where the columns are laid
            // out - and a portrait only lands in there if it is a live empire you can talk to.
            if (input.LeftMouseClick)
            {
                foreach (var kv in PortraitRects)
                {
                    if (kv.Value.HitTest(input.CursorPosition))
                    {
                        GameAudio.AcceptClick();
                        DiplomacyScreen.Show(kv.Key, "Greeting", parent: this);
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

            // Ludoal fork: our own copy, which carries the group's tab row. The shared
            // RelationshipsDiagramScreen stays untouched for the stock diplomacy screen.
            ExitScreen();
            ScreenManager.AddScreen(new RelationshipsDiagramScreen(Universe, empiresAndIntel));
        }
    }

    // an empire paired with how well we know it - the diplomacy screen builds these,
    // the relationships diagram consumes them
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
