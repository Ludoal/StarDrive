using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using Ship_Game.GameScreens; // ScreenGroups: the group geometry
using SDGraphics.Input;
using SDUtils;
using Ship_Game.Audio;
using Ship_Game.UI; // UITable: the shared table charte
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game
{
    public sealed class EmpireManagementScreen : GameScreen
    {
        public readonly UniverseScreen Universe;
        EmpireUIOverlay eui;
        private readonly ScrollList<ColoniesListItem> ColoniesList;
        private readonly GovernorDetailsComponent GovernorDetails;
        private readonly RectF ERect;
        // Ludoal fork (maintainer feedback): the little "sort by Homeworld then distance" button,
        // sitting at the right end of the Planet header. Amber when that sort is active.
        Rectangle HomeSortButton;

        public readonly UITable Table; // the shared table charte owns geometry, headers and rules

        private RectF GovernorRect;
        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row, this screen being one tab
        Submenu EmpireSummaryTab; // Ludoal fork (bench 339): the EMPIRE totals tab at the band's left

        // Ludoal fork (maintainer bench 339): the bottom band is laid out LEFT to RIGHT now, all of
        // it anchored to the left - extra width falls to the RIGHT of the governor. Fixed block
        // widths (the map derives its own from the fixed band height), so nothing floats. The X of
        // each block is computed ONCE (BandLayout) and shared by the ctor's GovernorRect and Draw.
        const float EmpireBoxW = 265f;  // the EMPIRE totals box (bench 343: +15, grown left)
        const float PlanetBoxW = 340f;  // icon + name + the four stat lines
        const float BandGap    = 10f;
        // set in the ctor from the fixed band height, reused in Draw
        float BandMapW, BandEmpireX, BandPlanetX, BandMapX, BandGovX;

        private readonly Color Cream           = Colors.Cream;
        private readonly Color White           = Color.White;

        public Planet SelectedPlanet { get; private set; }
        
        public EmpireManagementScreen(UniverseScreen parent, EmpireUIOverlay empUI)
            : base(parent, toPause: parent)
        {
            Universe = parent;
            TransitionOnTime = 0.25f;
            TransitionOffTime = 0.25f;
            IsPopup = true;
            eui = empUI;

            // Ludoal fork: the Colonies tab of the Empire group, on the shared table charte
            // (maintainer bench 293 - the surgical pass left the old skeleton showing). The
            // colony count drives the height, while the bottom band (planet cartouche, tile
            // map, governor frame) keeps the size it has on a full frame - reserved as it
            // is, to be revisited. The cascade holds because the band derives from the
            // list's bottom and the frame's own foot, not from constants.
            var planets = Universe.Player.GetPlanets();
            // the muted in-block separator, darker than plain Gray (bench 295)
            Color MutedSep = new Color(70, 70, 70);
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.System), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Planet), MinWidth = 150, Sortable = true },
                // the ORIGINAL food/production icons again, each wearing a small corner
                // badge so the intrinsic pair still tells apart from the yield columns
                // (Lek's review, bench 305); the muted separator sub-groups them
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_food"), Badge = Color.LightGreen,
                                     Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.Fertility), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_production"), Badge = Color.Orange,
                                     Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.Richness), SepColor = MutedSep },
                // population reads "x / y" like the Planets tab - Max Pop merged in; the
                // whole stat block keeps MUTED gray separators (bench 294, like the old
                // look), and Money rides before Research, the top bar's own order
                new UITable.Column { Icon = ResourceManager.Texture("UI/icon_pop"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.IndicatesThisColonysCurrentPopulation) },
                // Ludoal fork (maintainer bench 339): population growth per turn, between Pop and the
                // Food yield. icon_poppertile reads as "extra population"; tooltip spells it out.
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_poppertile"), Align = TableAlign.Number,
                                     Sortable = true, Tip = "Population growth per turn", SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_food"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetAmountOfFood), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_production"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetAmountOfProduction), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_money"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetIncomeOfThis), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_science"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetAmountOfResearch), SepColor = MutedSep },
                new UITable.Column { Title = Localizer.Token(GameText.Labor), Width = 225, Align = TableAlign.Center },
                new UITable.Column { Title = Localizer.Token(GameText.Storage2), Width = 240, Align = TableAlign.Center },
                new UITable.Column { Title = Localizer.Token(GameText.Construction2), Width = 282, Align = TableAlign.Center },
            });
            var sys = new Array<string>(); var names = new Array<string>();
            // eight numeric columns now: Fertility, Richness, Pop, GROWTH (new), Food, Prod, Money, Research
            var stats = new Array<string>[8];
            for (int i = 0; i < 8; ++i) stats[i] = new Array<string>();
            foreach (Planet p in planets)
            {
                sys.Add(p.System.Name);
                names.Add(p.Name);
                stats[0].Add(p.FertilityFor(Universe.Player).ToString("0.0", CultureInfo.InvariantCulture));
                stats[1].Add(p.MineralRichness.ToString("0.0", CultureInfo.InvariantCulture));
                stats[2].Add(PopCombined(p));
                stats[3].Add((p.EstimatedPopGrowthPerTurn / 1000f).ToString("0.00", CultureInfo.InvariantCulture)); // per-turn, billions
                stats[4].Add(p.Food.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
                stats[5].Add(p.Prod.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
                stats[6].Add(p.Money.NetRevenue.ToString("0.0", CultureInfo.InvariantCulture));
                stats[7].Add(p.Res.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, sys);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial14Bold, names);
            Table.Columns[1].Width += 44; // the planet icon rides ahead of the name
            for (int i = 0; i < 8; ++i)
                UITable.AutoSize(Table.Columns[2 + i], Fonts.Arial12, stats[i]);
            int widthCap = (int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66;
            Table.FitToWidth(widthCap);
            // Construction absorbs what the cap leaves (maintainer bench 307): Planet is
            // data-sized, so a save full of short names shrank the whole tab - the queue
            // column can always use the room instead
            int slack = widthCap - Table.TableWidth;
            if (slack > 0)
                Table.Columns[12].Width += slack; // Construction, now index 12 (Pop Growth pushed it +1)

            // capped at the 1080p footprint like the frame width, and a FIXED bottom band
            // (maintainer bench 298): the band holds the governor cartouche, which keeps the
            // Colony screen's own fixed height (222) - a band cut as a fraction of the screen
            // stretched everything in it with the resolution
            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight); // capped at 1080p (shared helper)
            const float GovernorH = 222;
            float bandH = GovernorH + 7; // the 7px the rect derivation below eats back
            float contentH = UITable.ContentHeightFor(102 + bandH, Math.Max(3, planets.Count), 84, fullAvail);
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.EmpireTabTitles, 0,
                                                    OnEmpireTabChanged, Table.ContentWidth, contentH);
            RectF client = EmpireTabs.ClientArea;
            Table.RowPitch = 84;
            Table.Layout(client, client.Y + 10, client.Bottom - bandH - 8);
            ERect = new(Table.TableRect.X, Table.TableRect.Y, Table.TableRect.Width, Table.TableRect.Height);

            ColoniesList = Add(new ScrollList<ColoniesListItem>(Table.ListRect, 80));
            ColoniesList.OnClick       = OnColonyListItemClicked;
            ColoniesList.OnDoubleClick = OnColonyListItemDoubleClicked;
            ColoniesList.EnableItemHighlight = true;
            Table.ApplyHighlightTo(ColoniesList);
            // FIXED, the Colony screen's own governor width (maintainer bench 298) - a panel
            // cut as 0.3 x screen swallowed the description at 1080p. Same per-tab arithmetic:
            // TextWidth + 2 + the header_right texture (33px), +8 wrap slack, floored at 380.
            float govTabsW = Fonts.Arial12Bold.TextWidth("GOVERNOR") + Fonts.Arial12Bold.TextWidth("DEFENSE")
                           + Fonts.Arial12Bold.TextWidth("BUDGET") + Fonts.Arial12Bold.TextWidth("BP")
                           + 4 * (2 + 33) + 8;
            int sidePanelWidths = (int)(Math.Max(govTabsW, 380) + 40);
            // Ludoal fork: its height stops at the FRAME's foot, not the screen's - inside a framed
            // tab it would otherwise run 10px past the bottom border. 10px of margin off the
            // frame's right and under the table (maintainer bench 293).
            // Ludoal fork (bench 339): the band runs LEFT to RIGHT now - EMPIRE, Planet, map,
            // governor - all anchored to the left, extra width spilling right. The block heights are
            // fixed, so the map's width (7:5) is known here and the whole cascade resolves at the
            // ctor; Draw reads the same X values. GovernorRect keeps its fixed width, only its X
            // changes from a right anchor to the end of the cascade.
            float bandTop    = ColoniesList.Bottom + 20; // bench 343: the bottom band drops 10px
            float bandBottom = client.Bottom - 15;
            float govBandH   = bandBottom - bandTop; // real band height (bandH above is the layout reserve)
            BandMapW    = (govBandH - 10) * (700f / 500f) + 20f;
            BandEmpireX = ERect.X + 7; // bench 343: EMPIRE box grew 15 to the LEFT (X -15, width +15), so its right edge and the rest of the cascade stay put
            BandPlanetX = BandEmpireX + EmpireBoxW + BandGap;
            BandMapX    = BandPlanetX + PlanetBoxW + BandGap;
            BandGovX    = BandMapX + BandMapW + BandGap;
            GovernorRect = new RectF(BandGovX, bandTop, sidePanelWidths, govBandH);

            // the EMPIRE totals tab at the band's left - a one-tab Submenu, like the group frames
            EmpireSummaryTab = Add(new Submenu(new RectF(BandEmpireX, bandTop, EmpireBoxW, govBandH),
                                               new LocalizedText[] { "EMPIRE" }));
            // Ludoal fork: guard against an empty colony list — seen live (crash at
            // StarDate 1163: GetPlanets() returned 0 for the player on the UI thread,
            // opened from the Infiltration screen). An empire with no colonies is also
            // a legitimate state (defeated-but-alive). The governor panel just stays off.
            if (planets.Count > 0)
                GovernorDetails = Add(new GovernorDetailsComponent(this, Universe,  planets[0], GovernorRect));
            else
                Log.Warning("EmpireManagementScreen: player planet list is EMPTY at ctor");
            // the STANDING sort survives the screen for the session (maintainer bench 307); the
            // Homeworld sort (StandingSort==-1) is the factory default now, and it highlights no
            // column - only a real column click marks a header sorted.
            if (!HomeworldSort)
            {
                Table.Columns[StandingSort].Sorted = true;
                Table.Columns[StandingSort].Ascending = StandingAsc;
            }
            ResetColoniesList(SortedPlanets(planets, StandingSort, StandingAsc));
            // the troop count and its food bill left this screen (maintainer, 4 Aug):
            // the Troops Array carries both on its own filter line now
        }

        // "current / max", the Planets tab's population shape
        string PopCombined(Planet p)
        {
            string ps = p.PopulationStringForPlayer;
            int paren = ps.IndexOf(" (");
            return paren < 0 ? ps : ps.Substring(0, paren);
        }

        // Ludoal fork: the other tabs live in their own screen, so leaving Colonies hands over to
        // it. Its own index is a no-op: we are already here.
        void OnEmpireTabChanged(int index)
        {
            // one factory for the whole group (ScreenGroups) - this screen only says which tab it is
            ScreenGroups.SwitchEmpireTab(index, self: 0, Universe, this);
        }

        public override void Draw(SpriteBatch batch, DrawTimes elapsed)
        {
            ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);
            batch.SafeBegin();

            // Ludoal fork: the frame fill FIRST - before base.Draw and before the bottom row this
            // method paints by hand, or it would cover one of them.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);

            base.Draw(batch, elapsed);
            
            // Ludoal fork (maintainer bench 339): the bottom band, LEFT to RIGHT - the EMPIRE totals
            // box, the planet cartouche, the ground map, then the fixed governor frame; all anchored
            // left, extra width spilling right. The block X's were fixed in the ctor (BandLayout) so
            // the ctor's GovernorRect and this row share one arithmetic. The planet DESCRIPTION is
            // gone from the band - it rides the planet icon's tooltip now.
            float blockTop = ERect.Y + ERect.H + 10; // bench 343: the bottom band drops 10px (matches bandTop's +10 in the ctor)
            float blockH   = GovernorRect.Bottom - blockTop;
            float mapH     = blockH - 10;

            // the EMPIRE box: colony count, total population, total per-turn growth, at the far left
            DrawEmpireSummary(batch, BandEmpireX, blockTop, blockH);

            // the planet block: icon + name + the four stat lines, pushed right of the EMPIRE box
            int iconSize = (int)(blockH * 0.6f);
            var PlanetInfoRect = new Rectangle((int)BandPlanetX, (int)blockTop, (int)PlanetBoxW, (int)blockH);
            var PlanetIconRect = new Rectangle(PlanetInfoRect.X + 10, PlanetInfoRect.Y + PlanetInfoRect.Height / 2 - iconSize / 2, iconSize, iconSize);
            var nameCursor = new Vector2(PlanetIconRect.X + PlanetIconRect.Width / 2 - Fonts.Pirulen16.MeasureString(SelectedPlanet.Name).X / 2f, PlanetInfoRect.Y + 15);
            batch.Draw(SelectedPlanet.PlanetTexture, PlanetIconRect, White);
            batch.DrawString(Fonts.Pirulen16, SelectedPlanet.Name, nameCursor, White);
            // the planet's flavour description now lives on the icon's tooltip (maintainer bench 339)
            if (PlanetIconRect.HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(SelectedPlanet.Description);

            // the four stat lines centre on the planet image (maintainer bench 294)
            var PNameCursor = new Vector2(PlanetIconRect.X + PlanetIconRect.Width + 5,
                                          PlanetIconRect.Y + PlanetIconRect.Height / 2 - 2 * (Fonts.Arial12Bold.LineSpacing + 2));
            var InfoCursor = new Vector2(PNameCursor.X + 80f, PNameCursor.Y);
            batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.Class)+":", PNameCursor, Color.Orange);
            batch.DrawString(Fonts.Arial12Bold, SelectedPlanet.CategoryName, InfoCursor, Cream);
            PNameCursor.Y += (Fonts.Arial12Bold.LineSpacing + 2);

            InfoCursor = new Vector2(PNameCursor.X + 80f, PNameCursor.Y);
            batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.Population)+":", PNameCursor, Color.Orange);
            batch.DrawString(Fonts.Arial12Bold, SelectedPlanet.PopulationStringForPlayer, InfoCursor, Cream);
            var hoverRect = new Rectangle((int)PNameCursor.X, (int)PNameCursor.Y, (int)Fonts.Arial12Bold.MeasureString(Localizer.Token(GameText.Population)+":").X, Fonts.Arial12Bold.LineSpacing);
            if (hoverRect.HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(GameText.AColonysPopulationIsA);

            PNameCursor.Y += (Fonts.Arial12Bold.LineSpacing + 2);
            InfoCursor = new Vector2(PNameCursor.X + 80f, PNameCursor.Y);
            batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.Fertility)+":", PNameCursor, Color.Orange);
            batch.DrawString(Fonts.Arial12Bold, SelectedPlanet.FertilityFor(Universe.Player).String(), InfoCursor, Cream);
            hoverRect = new Rectangle((int)PNameCursor.X, (int)PNameCursor.Y, (int)Fonts.Arial12Bold.MeasureString(Localizer.Token(GameText.Fertility)+":").X, Fonts.Arial12Bold.LineSpacing);
            if (hoverRect.HitTest(MousePos))
                ToolTip.CreateTooltip(GameText.IndicatesHowMuchFoodThis);

            PNameCursor.Y += (Fonts.Arial12Bold.LineSpacing + 2);
            InfoCursor = new Vector2(PNameCursor.X + 80f, PNameCursor.Y);
            batch.DrawString(Fonts.Arial12Bold, Localizer.Token(GameText.Richness)+":", PNameCursor, Color.Orange);
            batch.DrawString(Fonts.Arial12Bold, SelectedPlanet.MineralRichness.String(), InfoCursor, Cream);
            hoverRect = new Rectangle((int)PNameCursor.X, (int)PNameCursor.Y, (int)Fonts.Arial12Bold.MeasureString(Localizer.Token(GameText.Richness)+":").X, Fonts.Arial12Bold.LineSpacing);
            if (hoverRect.HitTest(MousePos))
            {
                ToolTip.CreateTooltip(GameText.APlanetsMineralRichnessDirectly);
            }

            var MapRect = new Rectangle((int)BandMapX, (int)blockTop + 10, (int)BandMapW, (int)mapH);
            int desiredWidth = 700;
            int desiredHeight = 500;
            var buildingsRect = new Rectangle(MapRect.X, MapRect.Y, desiredWidth, desiredHeight);
            while (!MapRect.Contains(buildingsRect))
            {
                desiredWidth -= 7;
                desiredHeight -= 5;
                buildingsRect = new Rectangle(MapRect.X, MapRect.Y, desiredWidth, desiredHeight);
            }
            buildingsRect = new Rectangle(MapRect.CenterX() - desiredWidth/2, MapRect.Y, desiredWidth, desiredHeight);
            MapRect.X = buildingsRect.X;
            MapRect.Width = buildingsRect.Width;
            int xSize = buildingsRect.Width / 7;
            int ySize = buildingsRect.Height / 5;

            batch.Draw(ResourceManager.Texture("PlanetTiles/" + SelectedPlanet.PlanetTileId), buildingsRect, White);
            batch.DrawRectangle(MapRect, new Color(118, 102, 67, 255));

            foreach (PlanetGridSquare tile in SelectedPlanet.TilesList)
            {
                var rect = new Rectangle(buildingsRect.X + tile.X * xSize, buildingsRect.Y + tile.Y * ySize, xSize, ySize);

                if (!tile.Habitable)
                {
                    batch.FillRectangle(rect, new Color(0, 0, 0, 200));
                }
                batch.DrawRectangle(rect, new Color(211, 211, 211, 100).Premultiplied(), 0.5f);

                if (tile.Building != null)
                {
                    Color c = tile.QItem != null ? White : new Color(White, 128).Premultiplied();
                    batch.Draw(tile.Building.IconTex, rect.CenterF - new Vector2(18), new Vector2(36), c);
                }

                DrawTileIcons(tile, rect);
            }

            // draw some border around the governor component
            /*
            var GovernorRect = new Rectangle(MapRect.Right, MapRect.Y, e1.Rect.Right - MapRect.Right, MapRect.Height);
            batch.DrawRectangle(GovernorRect, new Color(118, 102, 67, 255));*/

            // the shared charte draws the headers, the rule and the separators
            Table.DrawChrome(batch);

            // Ludoal fork (maintainer feedback): the Homeworld-sort button on the LEFT of the Planet
            // header, centred over the column's planet-icon lane (icons draw at col.X + 5, ~34 wide).
            // Amber when that sort is active, dim otherwise; a tooltip explains it.
            Rectangle planetHdr = Table.Columns[1].Rect;
            HomeSortButton = new Rectangle(planetHdr.X + 5 + 17 - 7, planetHdr.Y, 14, 14);
            SubTexture homeIcon = ResourceManager.Texture("UI/icon_home");
            batch.Draw(homeIcon, HomeSortButton, HomeworldSort ? Color.Orange : new Color(150, 150, 150));
            if (HomeSortButton.HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip("Sort colonies by distance from the Homeworld (Homeworld first)");

            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            eui.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        // Ludoal fork (maintainer bench 339): the EMPIRE totals - colony count, total population and
        // total per-turn growth, summed across the player's colonies. Drawn inside the EMPIRE tab's
        // client area; the tab frame itself is a child, painted by base.Draw before this.
        void DrawEmpireSummary(SpriteBatch batch, float boxX, float bandTop, float bandH)
        {
            IReadOnlyList<Planet> planets = Universe.Player.GetPlanets();
            // bench 345: total pop reads the empire's own TotalPopBillion - the SAME source the
            // Intelligence screen uses - so the two agree. Summing the planets here missed the
            // colonists in transit aboard ships, which TotalPopBillion counts. Growth has no such
            // aggregate, so it is still summed per planet.
            float totalPop = Universe.Player.TotalPopBillion;
            float totalGrowth = 0f;
            for (int i = 0; i < planets.Count; ++i)
                totalGrowth += planets[i].EstimatedPopGrowthPerTurn / 1000f; // per-turn, in billions

            RectF client = EmpireSummaryTab.ClientArea;
            float labelX = client.X + 14;
            float valueX = client.X + 120;
            float y      = client.Y + 14;
            void Row(string label, string value)
            {
                batch.DrawString(Fonts.Arial12Bold, label, new Vector2(labelX, y), Color.Orange);
                batch.DrawString(Fonts.Arial12Bold, value, new Vector2(valueX, y), Cream);
                y += Fonts.Arial12Bold.LineSpacing + 8;
            }
            Row("Colonies:",   planets.Count.ToString());
            Row("Population:", totalPop.String(1) + "B");
            Row("Growth:",     "+" + totalGrowth.String(2) + "B/turn");
        }

        void DrawTileIcons(PlanetGridSquare pgs, Rectangle rect)
        {
            if (pgs.Biosphere)
            {
                Rectangle biosphere = new Rectangle(rect.X, rect.Y, 10, 10);
                ScreenManager.SpriteBatch.Draw(ResourceManager.Texture("Buildings/icon_biosphere_48x48"), biosphere, White);
                ScreenManager.SpriteBatch.FillRectangle(rect, Universe.Player.EmpireColor.Alpha(0.4f));
            }

            if (Universe.Player.IsBuildingUnlocked(Building.TerraformerId) && (pgs.CanTerraform || pgs.BioCanTerraform))
            {
                var terraform = new Rectangle(rect.X + rect.Width - 10, rect.Y, 10, 10);
                ScreenManager.SpriteBatch.Draw(ResourceManager.Texture("Buildings/icon_terraformer_48x48"), terraform, Color.White);
            }
        }

        void OnColonyListItemClicked(ColoniesListItem item)
        {
            SelectedPlanet = item.P;
            GovernorDetails?.SetPlanetDetails(SelectedPlanet, GovernorRect, (int)(GovernorDetails?.CurrentTabIndex ?? 0));
            GovernorDetails?.PerformLayout();
        }

        void OnColonyListItemDoubleClicked(ColoniesListItem item)
        {
            Universe.SnapViewColony(item.P, combatView: false);
            // Ludoal fork (bench 191): closing that colony comes back HERE, not to the map
            // (maintainer feedback). ⚠ Set AFTER the snap: opening a colony clears this hook, so a line placed
            // above would be wiped by the very call it is meant to follow.
            Universe.ReturnToListScreen = () => Universe.ScreenManager.AddScreen(new EmpireManagementScreen(Universe, eui));
            Universe.ReturnToListTabs   = EmpireTabs; // the dimmed silhouette behind the colony
            Universe.ReturnToListGroup  = ScreenGroups.GroupOf(this); // keep the group button lit (maintainer)
            ExitScreen();
        }

        public override bool HandleInput(InputState input)
        {
            if (eui.HandleInput(input, caller: this)) // Ludoal fork: live top bar
                return true;

            if (input.KeyPressed(Keys.U) && !GlobalStats.TakingInput)
            {
                GameAudio.EchoAffirmative();
                ExitScreen();
                return true;
            }

            // Ludoal fork (maintainer feedback): the Homeworld-sort button, tested before the
            // headers so the Planet column click below never swallows it.
            if (input.LeftMouseClick && HomeSortButton.HitTest(input.CursorPosition))
            {
                GameAudio.BlipClick();
                foreach (UITable.Column c in Table.Columns)
                    c.Sorted = false;              // no column owns the sort now
                StandingSort = -1;                 // back to the Homeworld sort
                StandingAsc = true;
                ResetColoniesList(SortedPlanets(Universe.Player.GetPlanets(), -1, true));
                return true;
            }

            // headers - tooltips, hover and sort clicks - through the shared charte
            int clicked = Table.HandleInput(input);
            if (clicked >= 0)
            {
                bool asc = Table.SetSorted(clicked);
                GameAudio.BlipClick();
                StandingSort = clicked;
                StandingAsc = asc;
                ResetColoniesList(SortedPlanets(Universe.Player.GetPlanets(), clicked, asc));
                return true;
            }

            return base.HandleInput(input);
        }

        // one arithmetic for the ctor and the header clicks - the pair that must agree
        static IEnumerable<Planet> SortedPlanets(IReadOnlyList<Planet> planets, int col, bool asc)
        {
            if (col < 0) // the Homeworld sort: the capital first, then the rest by distance from it
            {
                Planet capital = planets.Count > 0 ? planets[0].Universe.Player.Capital : null;
                if (capital == null)
                    return planets; // capital lost - leave the native order
                return planets.Sorted(p => p == capital ? -1f : p.Position.SqDist(capital.Position));
            }
            if (col <= 1) // the two name columns sort as text
            {
                Func<Planet, string> name = col == 0 ? p => p.System.Name : p => p.Name;
                return asc ? planets.OrderBy(name) : planets.OrderByDescending(name);
            }
            // maintainer bench 339: a Pop Growth column at index 5 shifts Food..Research by one
            Func<Planet, float> selector = col switch
            {
                2 => p => p.FertilityFor(p.Universe.Player),
                3 => p => p.MineralRichness,
                4 => p => p.PopulationBillion,
                5 => p => p.EstimatedPopGrowthPerTurn,
                6 => p => p.Food.NetIncome,
                7 => p => p.Prod.NetIncome,
                8 => p => p.Money.NetRevenue,
                _ => p => p.Res.NetIncome,
            };
            return asc ? planets.OrderBy(selector) : planets.OrderByDescending(selector);
        }

        static int StandingSort = -1;    // session-persistent (bench 307); -1 = the Homeworld sort
        static bool StandingAsc = true;
        // Ludoal fork (maintainer feedback): the DEFAULT order is the Homeworld first, then the
        // other colonies by distance from it. It is not a table column - a header button toggles
        // it, and clicking any real column leaves it. HomeworldSort is on whenever StandingSort==-1.
        static bool HomeworldSort => StandingSort == -1;

        void ResetColoniesList(IEnumerable<Planet> sortedList)
        {
            ColoniesList.Reset();
            foreach (Planet p in sortedList)
            {
                ColoniesList.AddItem(new ColoniesListItem(this, p));
            }

            SelectedPlanet = ColoniesList.AllEntries[0].P;
            GovernorDetails?.SetPlanetDetails(SelectedPlanet, GovernorRect, (int)(GovernorDetails?.CurrentTabIndex ?? 0));
            GovernorDetails?.PerformLayout();
        }
    }
}
