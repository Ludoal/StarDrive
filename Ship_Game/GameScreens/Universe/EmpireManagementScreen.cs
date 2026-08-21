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
        // Ludoal fork: sort-by-Homeworld-then-distance button, right end of the Planet header.
        // Amber when that sort is active.
        Rectangle HomeSortButton;

        public readonly UITable Table; // the shared table charte owns geometry, headers and rules

        private RectF GovernorRect;
        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row, this screen being one tab
        // Ludoal fork: this page's real frame is its tab row's rect -
        // the band excludes exactly what the page occupies, dynamic size included
        public override Rectangle PageFrame => EmpireTabs?.Rect ?? base.PageFrame;
        Submenu EmpireSummaryTab; // Ludoal fork: the EMPIRE totals tab at the band's left

        // Ludoal fork: the bottom band lays out LEFT to RIGHT, anchored left - extra width falls
        // to the RIGHT of the governor. Fixed block widths (the map derives its own from the fixed
        // band height), so nothing floats. Each block's X is computed ONCE (BandLayout) and shared
        // by the ctor's GovernorRect and Draw.
        const float EmpireBoxW = 265f;  // the EMPIRE totals box
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

            // Ludoal fork: the Colonies tab of the Empire group, on the shared table charte. The
            // colony count drives the height; the bottom band (planet cartouche, tile map,
            // governor frame) keeps a fixed size. The cascade holds because the band derives from
            // the list's bottom and the frame's own foot, not from constants.
            var planets = Universe.Player.GetPlanets();
            // the muted in-block separator, darker than plain Gray
            Color MutedSep = new Color(70, 70, 70);
            Table = new UITable(new[]
            {
                new UITable.Column { Title = Localizer.Token(GameText.System), Sortable = true },
                new UITable.Column { Title = Localizer.Token(GameText.Planet), MinWidth = 150, Sortable = true },
                // food/production icons each wear a small corner badge so the intrinsic pair
                // still tells apart from the yield columns; the muted separator sub-groups them
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_food"), Badge = Color.LightGreen,
                                     Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.Fertility), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_production"), Badge = Color.Orange,
                                     Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.Richness), SepColor = MutedSep },
                // population reads "x / y" like the Planets tab - Max Pop merged in; the
                // whole stat block keeps MUTED gray separators, and Money rides before
                // Research, the top bar's own order
                new UITable.Column { Icon = ResourceManager.Texture("UI/icon_pop"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.IndicatesThisColonysCurrentPopulation) },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_food"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetAmountOfFood), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_production"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetAmountOfProduction), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_money"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetIncomeOfThis), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_science"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.TheNetAmountOfResearch), SepColor = MutedSep },
                new UITable.Column { Title = Localizer.Token(GameText.Labor), Width = 225, Align = TableAlign.Center },
                new UITable.Column { Title = Localizer.Token(GameText.Supply), Width = 240, Align = TableAlign.Center }, // bench 426: stock AND flow
                new UITable.Column { Title = Localizer.Token(GameText.Construction2), Width = 282, Align = TableAlign.Center },
            });
            // Pop Growth and Governor ride wide displays only (bench 408): at 1440 the base
            // twelve columns already fill the cap. The row items and the sorter tell the two
            // regimes apart by the column count (12 vs 14).
            bool wideCols = ScreenWidth >= 1680;
            if (wideCols)
            {
                var cols = new Array<UITable.Column>(Table.Columns);
                cols.Insert(5, new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_poppertile"), Align = TableAlign.Number,
                                                    Sortable = true, Tip = GameText.EmPopGrowthTip, SepColor = MutedSep });
                // one bold letter per type (bench 407); gold rule on its left (default),
                // muted on its right - the muted one is Labor's, set below
                cols.Insert(10, new UITable.Column { Title = "Gov.", Width = 40, Align = TableAlign.Center, Sortable = true });
                cols[11].SepColor = MutedSep; // Labor
                Table = new UITable(cols.ToArray());
            }
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
                stats[3].Add(p.EstimatedPopGrowthPerTurn.ToString("0.0", CultureInfo.InvariantCulture)); // millions/turn, one decimal, like Colony's Stats+ "Net growth (M/turn)"
                stats[4].Add(p.Food.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
                stats[5].Add(p.Prod.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
                stats[6].Add(p.Money.NetRevenue.ToString("0.0", CultureInfo.InvariantCulture));
                stats[7].Add(p.Res.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, sys);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial14Bold, names);
            Table.Columns[1].Width += 44; // the planet icon rides ahead of the name
            // stats: 0=Fert 1=Rich 2=Pop 3=Growth 4=Food 5=Prod 6=Money 7=Res; the Growth
            // column only exists on wide displays
            for (int i = 0, c = 2; i < 8; ++i)
            {
                if (i == 3 && !wideCols) continue;
                UITable.AutoSize(Table.Columns[c++], Fonts.Arial12, stats[i]);
            }
            int widthCap = (int)(Math.Min(ScreenWidth, ScreenGroups.MaxFrameWidth) - 2 * ScreenGroups.FrameMargin) - 66;
            Table.FitToWidth(widthCap);
            // Construction absorbs what the cap leaves: Planet is data-sized, so a save full of
            // short names would shrink the whole tab otherwise - the queue column uses the room
            int slack = widthCap - Table.TableWidth;
            if (slack > 0)
                Table.Columns[Table.Columns.Length - 1].Width += slack; // Construction is always last (the Governor column can shift its index)

            // FULL display height (uncapped), and a FIXED bottom band: the band holds the governor
            // cartouche, which keeps the Colony screen's own fixed height (222) - cutting the band
            // as a fraction of the screen would stretch everything in it with the resolution
            float fullAvail = ScreenGroups.FullTableHeight(ScreenHeight);
            const float GovernorH = 222;
            float bandH = GovernorH + 7; // the 7px the rect derivation below eats back
            float contentH = UITable.ContentHeightFor(102 + bandH, Math.Max(3, planets.Count), 84, fullAvail);
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.LiveTitles(ScreenGroups.Group.Empire, Universe), 0,
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
            // FIXED, the Colony screen's own governor width - cutting it as 0.3 x screen would
            // swallow the description at 1080p. Per-tab arithmetic:
            // TextWidth + 2 + the header_right texture (33px), +8 wrap slack, floored at 380.
            float govTabsW = Fonts.Arial12Bold.TextWidth("GOVERNOR") + Fonts.Arial12Bold.TextWidth("DEFENSE")
                           + Fonts.Arial12Bold.TextWidth("BUDGET") + Fonts.Arial12Bold.TextWidth("BP")
                           + 4 * (2 + 33) + 8;
            int sidePanelWidths = (int)(Math.Max(govTabsW, 380) + 40);
            // Ludoal fork: its height stops at the FRAME's foot, not the screen's - inside a framed
            // tab it would otherwise run 10px past the bottom border. 10px of margin off the
            // frame's right and under the table.
            // Ludoal fork: the band runs LEFT to RIGHT - EMPIRE, Planet, map, governor - all
            // anchored to the left, extra width spilling right. The block heights are fixed, so the
            // map's width (7:5) is known here and the whole cascade resolves at the ctor; Draw reads
            // the same X values. GovernorRect keeps its fixed width, only its X changes from a right
            // anchor to the end of the cascade.
            float bandTop    = ColoniesList.Bottom + 20;
            float bandBottom = client.Bottom - 15;
            float govBandH   = bandBottom - bandTop; // real band height (bandH above is the layout reserve)
            BandMapW    = (govBandH - 10) * (700f / 500f) + 20f;
            BandEmpireX = ERect.X + 7;
            BandPlanetX = BandEmpireX + EmpireBoxW + BandGap;
            BandMapX    = BandPlanetX + PlanetBoxW + BandGap;
            BandGovX    = BandMapX + BandMapW + BandGap;
            GovernorRect = new RectF(BandGovX, bandTop, sidePanelWidths, govBandH);

            // the EMPIRE totals tab at the band's left - a one-tab Submenu, like the group frames
            EmpireSummaryTab = Add(new Submenu(new RectF(BandEmpireX, bandTop, EmpireBoxW, govBandH),
                                               new LocalizedText[] { "EMPIRE" }));
            // Ludoal fork: guard against an empty colony list. An empire with no colonies is a
            // legitimate state (defeated-but-alive) - the governor panel just stays off.
            if (planets.Count > 0)
                GovernorDetails = Add(new GovernorDetailsComponent(this, Universe,  planets[0], GovernorRect));
            else
                Log.Warning("EmpireManagementScreen: player planet list is EMPTY at ctor");
            // the STANDING sort survives the screen for the session; the Homeworld sort
            // (StandingSort==-1) is the factory default, and it highlights no column -
            // only a real column click marks a header sorted.
            if (!HomeworldSort)
            {
                Table.Columns[StandingSort].Sorted = true;
                Table.Columns[StandingSort].Ascending = StandingAsc;
            }
            ResetColoniesList(SortedPlanets(planets, StandingSort, StandingAsc, wideCols));
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
            batch.SafeBegin();

            // Ludoal fork: the frame fill FIRST - before base.Draw and before the bottom row this
            // method paints by hand, or it would cover one of them.
            batch.FillRectangle(ScreenGroups.GroupFrameFillRect(EmpireTabs), ScreenGroups.GroupFrameFill);

            base.Draw(batch, elapsed);
            // Policies phase 0: any OPEN supply dropdown re-paints after the whole table,
            // so its expanded list is never covered by the rows below it.
            // bench 457 probe: whatever kills the chrome and the bottom band logs its
            // trace here instead of dying silently every frame
            try
            {
                foreach (ColoniesListItem it in ColoniesList.AllEntries)
                    it.DrawOpenLists(batch, elapsed);
            }
            catch (Exception ex) { Log.Error(ex, "Colonies: open-list overdraw"); }
            
            // Ludoal fork: the bottom band, LEFT to RIGHT - the EMPIRE totals box, the planet
            // cartouche, the ground map, then the fixed governor frame; all anchored left, extra
            // width spilling right. The block X's are fixed in the ctor (BandLayout) so the ctor's
            // GovernorRect and this row share one arithmetic. The planet DESCRIPTION rides the
            // planet icon's tooltip, not the band.
            float blockTop = ERect.Y + ERect.H + 10;
            float blockH   = GovernorRect.Bottom - blockTop;
            float mapH     = blockH - 10;

            // the EMPIRE box: colony count, total population, total per-turn growth, at the far left
            try { DrawEmpireSummary(batch, BandEmpireX, blockTop, blockH); }
            catch (Exception ex) { Log.Error(ex, "Colonies: EMPIRE summary"); } // bench 457 probe

            // the planet block: icon + name + the four stat lines, pushed right of the EMPIRE box
            int iconSize = (int)(blockH * 0.6f);
            var PlanetInfoRect = new Rectangle((int)BandPlanetX, (int)blockTop, (int)PlanetBoxW, (int)blockH);
            var PlanetIconRect = new Rectangle(PlanetInfoRect.X + 10, PlanetInfoRect.Y + PlanetInfoRect.Height / 2 - iconSize / 2, iconSize, iconSize);
            var nameCursor = new Vector2(PlanetIconRect.X + PlanetIconRect.Width / 2 - Fonts.Pirulen16.MeasureString(SelectedPlanet.Name).X / 2f, PlanetInfoRect.Y + 15);
            batch.Draw(SelectedPlanet.PlanetTexture, PlanetIconRect, White);
            batch.DrawString(Fonts.Pirulen16, SelectedPlanet.Name, nameCursor, White);
            // the planet's flavour description lives on the icon's tooltip
            if (PlanetIconRect.HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(SelectedPlanet.Description);

            // the four stat lines centre on the planet image
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

            // Ludoal fork: the Homeworld-sort button, LEFT of the Planet header, centred over the
            // column's planet-icon lane (icons draw at col.X + 5, ~34 wide). Amber when that sort
            // is active, dim otherwise; a tooltip explains it.
            Rectangle planetHdr = Table.Columns[1].Rect;
            HomeSortButton = new Rectangle(planetHdr.X + 5 + 17 - 7, planetHdr.Y, 14, 14);
            SubTexture homeIcon = ResourceManager.Texture("UI/icon_home");
            batch.Draw(homeIcon, HomeSortButton, HomeworldSort ? Color.Orange : new Color(150, 150, 150));
            if (HomeSortButton.HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(GameText.EmSortByDistanceTip);

            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            eui.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
        }

        // Ludoal fork: the EMPIRE totals - colony count, total population and total per-turn growth,
        // summed across the player's colonies. Drawn inside the EMPIRE tab's client area; the tab
        // frame itself is a child, painted by base.Draw before this.
        void DrawEmpireSummary(SpriteBatch batch, float boxX, float bandTop, float bandH)
        {
            IReadOnlyList<Planet> planets = Universe.Player.GetPlanets();
            // total pop reads the empire's own TotalPopBillion - the SAME source the Intelligence
            // screen uses, so the two agree; it also counts colonists in transit aboard ships, which
            // summing the planets would miss. Growth has no such aggregate, so it is summed per planet.
            float totalPop = Universe.Player.TotalPopBillion;
            float totalGrowth = 0f, food = 0f, foodNet = 0f, prod = 0f, prodNet = 0f;
            // bench 452 (maintainer): the empire's larder AND its yard - both stores summed
            // with the per-turn surplus/deficit beside them. Both travel by freighter, and a
            // prod pile is what production rushes spend when a fleet has to exist quickly.
            // A cybernetic empire eats production, so its food line would be noise: it only
            // shows the prod line.
            bool cyber = Universe.Player.IsCybernetic;
            for (int i = 0; i < planets.Count; ++i)
            {
                Planet p = planets[i];
                totalGrowth += p.EstimatedPopGrowthPerTurn / 1000f; // per-turn, in billions
                food += p.FoodHere; foodNet += p.Food.NetIncome;
                // bench 453 (maintainer question): the prod delta must say whether the WAR
                // CHEST grows - so the queues' planned spend for the turn comes off the
                // inflow (NetIncome ignores construction, which eats surplus and stock apart)
                prod += p.ProdHere;
                prodNet += p.Prod.NetIncome - p.LimitedProductionExpenditure(p.CurrentProductionToQueue);
            }

            RectF client = EmpireSummaryTab.ClientArea;
            float labelX = client.X + 14;
            float valueX = client.X + 120;
            float y      = client.Y + 14;
            void Row(string label, string value, string suffix = null, Color suffixColor = default)
            {
                batch.DrawString(Fonts.Arial12Bold, label, new Vector2(labelX, y), Color.Orange);
                batch.DrawString(Fonts.Arial12Bold, value, new Vector2(valueX, y), Cream);
                if (suffix != null)
                    batch.DrawString(Fonts.Arial12Bold, suffix,
                        new Vector2(valueX + Fonts.Arial12Bold.TextWidth(value) + 8, y), suffixColor);
                y += Fonts.Arial12Bold.LineSpacing + 8;
            }
            Row("Colonies:",   planets.Count.ToString());
            Row("Population:", totalPop.String(1) + "B");
            Row("Growth:",     "+" + totalGrowth.String(2) + "B/turn");
            if (!cyber)
                Row("Food stock:", food.String(0),
                    (foodNet >= 0f ? "+" : "") + foodNet.String(1) + "/turn",
                    foodNet >= 0f ? Color.LightGreen : Color.Red);
            Row("Prod stock:", prod.String(0),
                (prodNet >= 0f ? "+" : "") + prodNet.String(1) + "/turn",
                prodNet >= 0f ? Color.LightGreen : Color.Red);
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
            // single-click = select on the map and pan at current zoom -
            // the governor panel keeps following the row
            Universe.PanToPlanetKeepZoom(item.P);
            SelectedPlanet = item.P;
            GovernorDetails?.SetPlanetDetails(SelectedPlanet, GovernorRect, (int)(GovernorDetails?.CurrentTabIndex ?? 0));
            GovernorDetails?.PerformLayout();
        }

        void OnColonyListItemDoubleClicked(ColoniesListItem item)
        {
            // Ludoal fork: armed BEFORE the snap - the colony's ctor reads the seat to wear the
            // EMPIRE row, planet tab appended, Colonies (0) as the Esc origin. The snap's map-open
            // path sees this planet's seat standing and leaves it be.
            Universe.HostColonyTab(item.P, ScreenGroups.Group.Empire, 0);
            Universe.SnapViewColony(item.P, combatView: false);
            // Ludoal fork: the colony inherits this list's automatic pause - consulting a colony
            // from a paused list must not restart the simulation. Before ExitScreen, which would
            // resume it; skipped when the snap did not open a colony.
            if (Universe.LookingAtPlanet)
                HandOverUniversePause(Universe.workersPanel);
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

            // Ludoal fork: the Homeworld-sort button, tested before the headers so the Planet
            // column click below never swallows it.
            if (input.LeftMouseClick && HomeSortButton.HitTest(input.CursorPosition))
            {
                GameAudio.BlipClick();
                foreach (UITable.Column c in Table.Columns)
                    c.Sorted = false;              // no column owns the sort now
                StandingSort = -1;                 // back to the Homeworld sort
                StandingAsc = true;
                ResetColoniesList(SortedPlanets(Universe.Player.GetPlanets(), -1, true, Table.Columns.Length >= 14));
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
                ResetColoniesList(SortedPlanets(Universe.Player.GetPlanets(), clicked, asc, Table.Columns.Length >= 14));
                return true;
            }

            return base.HandleInput(input);
        }

        // one arithmetic for the ctor and the header clicks - the pair that must agree
        static IEnumerable<Planet> SortedPlanets(IReadOnlyList<Planet> planets, int col, bool asc, bool wide)
        {
            if (col < 0) // the Homeworld sort: the shared spatial order (bench 431) - the old
            {            // planet-position key let an orbit contaminate the distance and split systems
                if (planets.Count == 0)
                    return planets;
                return planets[0].Universe.Player.SpatialColonyOrder();
            }
            if (col <= 1) // the two name columns sort as text
            {
                Func<Planet, string> name = col == 0 ? p => p.System.Name : p => p.Name;
                return asc ? planets.OrderBy(name) : planets.OrderByDescending(name);
            }
            // two regimes (bench 408): wide displays carry Pop Growth at 5 and Governor at 10,
            // which shifts Food..Research by one. "--" (no governor) sorts FIRST ascending.
            static float GovKey(Planet p) => p.CType == Planet.ColonyType.Colony ? -1f : (float)(int)p.CType;
            Func<Planet, float> selector = col switch
            {
                2 => p => p.FertilityFor(p.Universe.Player),
                3 => p => p.MineralRichness,
                4 => p => p.PopulationBillion,
                5 => wide ? p => p.EstimatedPopGrowthPerTurn : p => p.Food.NetIncome,
                6 => wide ? p => p.Food.NetIncome  : p => p.Prod.NetIncome,
                7 => wide ? p => p.Prod.NetIncome  : p => p.Money.NetRevenue,
                8 => wide ? p => p.Money.NetRevenue : (Func<Planet, float>)(p => p.Res.NetIncome),
                9 => p => p.Res.NetIncome,
                _ => GovKey, // 10 = the Governor column (wide displays)
            };
            return asc ? planets.OrderBy(selector) : planets.OrderByDescending(selector);
        }

        static int StandingSort = -1;    // session-persistent; -1 = the Homeworld sort
        static bool StandingAsc = true;
        // Ludoal fork: the DEFAULT order is the Homeworld first, then the other colonies by distance
        // from it. It is not a table column - a header button toggles it, and clicking any real
        // column leaves it. HomeworldSort is on whenever StandingSort==-1.
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
