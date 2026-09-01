using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
using Ship_Game.UI;
using Vector2 = SDGraphics.Vector2;
using Rectangle = SDGraphics.Rectangle;

namespace Ship_Game.GameScreens.NewGame
{
    public class EnvPreferencesPanel : UIElementContainer
    {
        readonly RaceDesignScreen Screen;
        readonly UILabel Title;
        readonly UILabel BestType;
        UIPanel PlanetIcon;

        readonly Array<(SplitElement, UILabel)> EnvSplits = new();
        public PlanetCategory PreferredEnv = PlanetCategory.Terran;
        public float EnvTerran = 1;
        public float EnvOceanic = 1;
        public float EnvSteppe = 1;
        public float EnvTundra = 1;
        public float EnvSwamp = 1;
        public float EnvDesert = 1;
        public float EnvIce = 1;
        public float EnvBarren = 1;
        public float EnvVolcanic = 1;

        IEmpireData Data;

        public EnvPreferencesPanel(RaceDesignScreen parent, RacialTrait raceSummary, in Rectangle rect) : base(rect)
        {
            Screen = parent;
            Data = Screen.SelectedData;

            var font = Fonts.Arial12Bold;
            Title = Add(new UILabel(GameText.NgEnvironmentPreferences, font, Color.BurlyWood));
            // Ludoal fork: the 15 that used to sit here was this panel's own padding, and it
            // is the theme's TabPadInner now (maintainer measured ~15 on this panel while the
            // table said 0 - the inset was one level down, in these children). What is left is
            // the title's own indent over the two value columns, and the row it sits on.
            Title.SetLocalPos(25, 0);
            Title.Tooltip = GameText.NgEnvPreferencesTooltip;

            BestType = Add(new UILabel(GameText.NgBestPlanetType, font, Color.BurlyWood));
            // Ludoal fork (maintainer feedback): -78 total to clear the widened value columns; the
            // planet icon reads BestType.LocalPos, so it follows on its own.
            // clears the MEASURED title (the French title outgrew the stock offset)
            BestType.SetLocalPos(Math.Max(25 + 275 - 78, 25 + (int)font.TextWidth(Localizer.Token(GameText.NgEnvironmentPreferences)) + 16), 0);
            BestType.Tooltip = GameText.NgBestPlanetTypeTooltip;
            

            // Ludoal fork (maintainer feedback): the value columns are pulled left to fit the tab
            // at 900p - column2 lands at 120 from the padded edge.
            UIList column1 = Add(new UIList(ListLayoutStyle.ResizeList));
            UIList column2 = Add(new UIList(ListLayoutStyle.ResizeList));
            // the value rows: 20 under the title row, and flush with the padded edge - the 5
            // they carried was less than the padding, so they sat closer to the frame than
            // everything else in the window.
            column1.SetLocalPos(0, 20);
            column2.SetLocalPos(15 + 140 - 20 - 15 - 10, 20);
            column1.Padding = column2.Padding = new Vector2(4, 4);

            UILabel AddEnvSplitter(UIList list, string title, Func<float> getValue)
            {
                var key = new UILabel(LocalizedText.Parse(title), font, Color.Wheat);
                var val = new UILabel(getValue().String(2), font);
                val.DynamicText = (l) => getValue().String(2);
                val.DynamicColor = (l) =>
                {
                    float value = getValue();
                    if (value > 1) return Color.Green;
                    if (value < 1) return Color.Red;
                    return Color.White;
                };
                var se = list.AddSplit(key, val);
                se.Split = 75; // maintainer: values -5 in each column
                EnvSplits.Add((se, key));
                return val;
            }

            EnvSplits.Clear();
            AddEnvSplitter(column1, "{Terran}: ", () => EnvTerran);
            AddEnvSplitter(column1, "{Steppe}: ", () => EnvSteppe);
            AddEnvSplitter(column1, "{Oceanic}: ",() => EnvOceanic);
            AddEnvSplitter(column1, "{Swamp}: ",  () => EnvSwamp);
            AddEnvSplitter(column1, "{Volcanic}: ", () => EnvVolcanic);

            AddEnvSplitter(column2, "{Tundra}: ", () => EnvTundra);
            AddEnvSplitter(column2, "{Ice}: ",    () => EnvIce);
            AddEnvSplitter(column2, "{Desert}: ", () => EnvDesert);
            AddEnvSplitter(column2, "{Barren}: ", () => EnvBarren);

            // the split clears the widest LOCALIZED category label (French sweep:
            // 'Marécageuse' overran the stock 75)
            float maxKeyW = 75f;
            foreach (var (_, k) in EnvSplits) maxKeyW = Math.Max(maxKeyW, k.Size.X + 8f);
            foreach (var (se2, _) in EnvSplits) se2.Split = maxKeyW;
            UpdatePreferences(raceSummary);
        }

        public void UpdateArchetype(IEmpireData data, RacialTrait raceSummary)
        {
            Data = data;
            UpdatePlanetIcon();
            UpdatePreferences(raceSummary);
        }

        public void UpdatePreferences(RacialTrait raceSummary)
        {
            PreferredEnv = raceSummary.PreferredEnv;
            EnvTerran = raceSummary.EnvTerran;
            EnvOceanic = raceSummary.EnvOceanic;
            EnvSteppe = raceSummary.EnvSteppe;
            EnvTundra = raceSummary.EnvTundra;
            EnvSwamp = raceSummary.EnvSwamp;
            EnvDesert = raceSummary.EnvDesert;
            EnvIce = raceSummary.EnvIce;
            EnvBarren = raceSummary.EnvBarren;
            EnvVolcanic = raceSummary.EnvVolcanic;
            UpdatePlanetIcon();
            UpdateVisibility();
        }

        void UpdateVisibility()
        {
            // Ludoal fork: the Environment panel always stays shown, a neutral race reading 1.00
            // across the board.
            Visible = true;
        }

        void UpdatePlanetIcon()
        {
            PlanetIcon?.RemoveFromParent(true);

            int size = 100;
            PlanetIcon = Add(new UIPanel(BestType.LocalPos.Add(0, 20), new Vector2(size),
                                         GetPlanetIcon())
            {
                Name = "EnvPref.PlanetIcon",
                Tooltip = Planet.TextCategory(PreferredEnv)
            });
        }

        SubTexture GetPlanetIcon()
        {
            string path;
            switch (PreferredEnv)
            {
                default:
                case PlanetCategory.Terran:  path = "Planets/25"; break;
                case PlanetCategory.Steppe:  path = "Planets/18"; break;
                case PlanetCategory.Oceanic: path = "Planets/21"; break;
                case PlanetCategory.Swamp:   path = "Planets/19"; break;
                case PlanetCategory.Tundra:  path = "Planets/11"; break;
                case PlanetCategory.Ice:     path = "Planets/17"; break;
                case PlanetCategory.Desert:  path = "Planets/14"; break;
                case PlanetCategory.Barren:  path = "Planets/16"; break;
            }

            return ResourceManager.Texture(path);
        }
    }
}
