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

        public readonly UITable Table; // the shared table charte owns geometry, headers and rules

        private RectF GovernorRect;
        Submenu EmpireTabs; // Ludoal fork: the Empire group's tab row, this screen being one tab

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
                new UITable.Column { Title = Localizer.Token(GameText.System) },
                new UITable.Column { Title = Localizer.Token(GameText.Planet), MinWidth = 150 },
                // biospheres/crystal, not food/production: the INTRINSIC pair wears its own
                // icons and a muted gray separator to sub-group it (maintainer bench 294)
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_biospheres"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.Fertility), SepColor = MutedSep },
                new UITable.Column { Icon = ResourceManager.Texture("NewUI/icon_exotic_resource"), Align = TableAlign.Number,
                                     Sortable = true, Tip = Localizer.Token(GameText.Richness), SepColor = MutedSep },
                // population reads "x / y" like the Planets tab - Max Pop merged in; the
                // whole stat block keeps MUTED gray separators (bench 294, like the old
                // look), and Money rides before Research, the top bar's own order
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
                new UITable.Column { Title = Localizer.Token(GameText.Labor), Width = 240, Align = TableAlign.Center },
                new UITable.Column { Title = Localizer.Token(GameText.Storage2), Width = 240, Align = TableAlign.Center },
                new UITable.Column { Title = Localizer.Token(GameText.Construction2), Width = 260, Align = TableAlign.Center },
            });
            var sys = new Array<string>(); var names = new Array<string>();
            var stats = new Array<string>[7];
            for (int i = 0; i < 7; ++i) stats[i] = new Array<string>();
            foreach (Planet p in planets)
            {
                sys.Add(p.System.Name);
                names.Add(p.Name);
                stats[0].Add(p.FertilityFor(Universe.Player).ToString("0.0", CultureInfo.InvariantCulture));
                stats[1].Add(p.MineralRichness.ToString("0.0", CultureInfo.InvariantCulture));
                stats[2].Add(PopCombined(p));
                stats[3].Add(p.Food.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
                stats[4].Add(p.Prod.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
                stats[5].Add(p.Money.NetRevenue.ToString("0.0", CultureInfo.InvariantCulture));
                stats[6].Add(p.Res.NetIncome.ToString("0.0", CultureInfo.InvariantCulture));
            }
            UITable.AutoSize(Table.Columns[0], Fonts.Arial12Bold, sys);
            UITable.AutoSize(Table.Columns[1], Fonts.Arial14Bold, names);
            Table.Columns[1].Width += 44; // the planet icon rides ahead of the name
            for (int i = 0; i < 7; ++i)
                UITable.AutoSize(Table.Columns[2 + i], Fonts.Arial12, stats[i]);
            Table.FitToWidth((int)(Math.Min(ScreenWidth, 1920) - 2 * ScreenGroups.FrameMargin) - 66);

            float fullAvail = ScreenHeight - ScreenGroups.TabRowY - ScreenGroups.FrameMargin;
            float bandH = 0.3f * (fullAvail - 60);
            float contentH = Math.Min(fullAvail, 105 + Math.Max(3, planets.Count) * 84 + bandH);
            EmpireTabs = ScreenGroups.AddGroupTabs(this, ScreenGroups.EmpireTabTitles, 0,
                                                    OnEmpireTabChanged, Table.ContentWidth, contentH);
            RectF client = EmpireTabs.ClientArea;
            Table.Layout(client, client.Y + 10, client.Bottom - bandH - 8);
            ERect = new(Table.TableRect.X, Table.TableRect.Y, Table.TableRect.Width, Table.TableRect.Height);

            ColoniesList = Add(new ScrollList<ColoniesListItem>(Table.ListRect, 80));
            ColoniesList.OnClick       = OnColonyListItemClicked;
            ColoniesList.OnDoubleClick = OnColonyListItemDoubleClicked;
            ColoniesList.EnableItemHighlight = true;
            Table.ApplyHighlightTo(ColoniesList);
            int sidePanelWidths = (int)(ScreenWidth * 0.3f);
            // Ludoal fork: its height stops at the FRAME's foot, not the screen's - inside a framed
            // tab it would otherwise run 10px past the bottom border. 10px of margin off the
            // frame's right and under the table (maintainer bench 293).
            GovernorRect = new RectF(client.Right - sidePanelWidths - 10, ColoniesList.Bottom + 10,
                                     sidePanelWidths, client.Bottom - ColoniesList.Bottom - 15);
            // Ludoal fork: guard against an empty colony list — seen live (crash at
            // StarDate 1163: GetPlanets() returned 0 for the player on the UI thread,
            // opened from the Infiltration screen). An empire with no colonies is also
            // a legitimate state (defeated-but-alive). The governor panel just stays off.
            if (planets.Count > 0)
                GovernorDetails = Add(new GovernorDetailsComponent(this, Universe,  planets[0], GovernorRect));
            else
                Log.Warning("EmpireManagementScreen: player planet list is EMPTY at ctor");
            ResetColoniesList(planets);
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
            
            // Ludoal fork: the bottom band, four blocks left to right - planet cartouche,
            // description on the band's FULL height, ground map, governor frame (fixed,
            // placed in the constructor with its 10px margins). The cascade runs right to
            // left from the governor's edge: the map takes its height-driven width, the
            // DESCRIPTION absorbs what is left (maintainer bench 293).
            float blockTop = ERect.Y + ERect.H;
            // ⚠ off the FRAME's foot, not the screen's: inside a framed tab this row would run past
            // the bottom border, and the governor frame beside it already stops there.
            float blockH = GovernorRect.Bottom - blockTop;
            float infoX = ERect.X + 22;
            float rowRight = GovernorRect.X;
            // the ground map steps 10px down (maintainer) and hugs the governor block; it is
            // drawn on a 7:5 grid, so its height decides its width
            float mapH = blockH - 10;
            float mapW = mapH * (700f / 500f) + 20f;
            float mapX = rowRight - mapW;

            // the planet block: icon + name + the four stat lines, at the band's left
            int iconSize = (int)(blockH * 0.6f);
            var PlanetInfoRect = new Rectangle((int)infoX, (int)blockTop, iconSize + 210, (int)blockH);
            var PlanetIconRect = new Rectangle(PlanetInfoRect.X + 10, PlanetInfoRect.Y + PlanetInfoRect.Height / 2 - iconSize / 2, iconSize, iconSize);
            var nameCursor = new Vector2(PlanetIconRect.X + PlanetIconRect.Width / 2 - Fonts.Pirulen16.MeasureString(SelectedPlanet.Name).X / 2f, PlanetInfoRect.Y + 15);
            batch.Draw(SelectedPlanet.PlanetTexture, PlanetIconRect, White);
            batch.DrawString(Fonts.Pirulen16, SelectedPlanet.Name, nameCursor, White);
            
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

            // the description in its OWN block, right of the cartouche, on the band's full
            // height (maintainer bench 293) - the smaller font first if it runs long, then
            // as many whole lines as fit with the cut marked, and the FULL text in a
            // tooltip whenever it was cut
            var DescRect = new Rectangle(PlanetInfoRect.Right + 10, (int)blockTop + 8,
                                         (int)mapX - PlanetInfoRect.Right - 30, (int)blockH - 16);
            // the 14 first (maintainer bench 295: the description was hard to read),
            // stepping down to 12 only when the room runs out
            var descFont = Fonts.Arial14Bold;
            string text = descFont.ParseText(SelectedPlanet.Description, DescRect.Width);
            if (descFont.MeasureString(text).Y > DescRect.Height)
            {
                descFont = Fonts.Arial12Bold;
                text = descFont.ParseText(SelectedPlanet.Description, DescRect.Width);
            }
            bool descCut = false;
            if (descFont.MeasureString(text).Y > DescRect.Height)
            {
                int maxLines = (int)(DescRect.Height / descFont.LineSpacing);
                string[] lines = text.Split('\n');
                if (maxLines > 0 && lines.Length > maxLines)
                {
                    text = string.Join("\n", lines, 0, maxLines - 1) + "\n...";
                    descCut = true;
                }
            }
            // the description centres vertically in its block (maintainer bench 294)
            float descY = DescRect.Y + Math.Max(0f, (DescRect.Height - descFont.MeasureString(text).Y) / 2f);
            batch.DrawString(descFont, text, new Vector2(DescRect.X, descY), White);
            if (descCut && DescRect.HitTest(Input.CursorPosition))
                ToolTip.CreateTooltip(SelectedPlanet.Description);

            var MapRect = new Rectangle((int)mapX, (int)blockTop + 10, (int)mapW, (int)mapH);
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

            ScreenGroups.DrawEmpireTabTip(EmpireTabs, Input.CursorPosition);
            eui.Draw(batch); // Ludoal fork: live top bar on every full-screen panel
            batch.SafeEnd();
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

            // headers - tooltips, hover and sort clicks - through the shared charte
            int clicked = Table.HandleInput(input);
            if (clicked >= 0)
            {
                Func<Planet, float> selector = clicked switch
                {
                    2 => p => p.FertilityFor(Universe.Player),
                    3 => p => p.MineralRichness,
                    4 => p => p.PopulationBillion,
                    5 => p => p.Food.NetIncome,
                    6 => p => p.Prod.NetIncome,
                    7 => p => p.Money.NetRevenue,
                    _ => p => p.Res.NetIncome,
                };
                bool asc = Table.SetSorted(clicked);
                GameAudio.BlipClick();
                var planets = Universe.Player.GetPlanets();
                ResetColoniesList(asc ? planets.OrderBy(selector) : planets.OrderByDescending(selector));
                return true;
            }

            return base.HandleInput(input);
        }

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
