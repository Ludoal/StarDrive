using System;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;
using SDUtils;
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
            Title = Add(new UILabel("Environment Preferences", font, Color.BurlyWood));
            Title.SetLocalPos(35, 15);
            Title.Tooltip = "Some races have modifiers to their Max Population and Fertility based on the planet type.";

            BestType = Add(new UILabel("Best Planet Type", font, Color.BurlyWood));
            // Ludoal fork (maintainer feedback): -78 total to clear the widened value columns; the
            // planet icon reads BestType.LocalPos, so it follows on its own.
            BestType.SetLocalPos(35 + 275 - 78, 15);
            BestType.Tooltip = "This is the best suited environment for this race, Terraforming a planet will transform it to this planet type.";
            

            // Ludoal fork (maintainer feedback): pull the value columns left to fit the tab at 900p
            // - column1 at 5, column2 pulled a further 15 (135 -> 120).
            UIList column1 = Add(new UIList(ListLayoutStyle.ResizeList));
            UIList column2 = Add(new UIList(ListLayoutStyle.ResizeList));
            column1.SetLocalPos(5, 35);
            column2.SetLocalPos(15 + 140 - 20 - 15, 35);
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
                list.AddSplit(key, val).Split = 75; // maintainer: values -5 in each column
                return val;
            }

            AddEnvSplitter(column1, "{Terran}: ", () => EnvTerran);
            AddEnvSplitter(column1, "{Steppe}: ", () => EnvSteppe);
            AddEnvSplitter(column1, "{Oceanic}: ",() => EnvOceanic);
            AddEnvSplitter(column1, "{Swamp}: ",  () => EnvSwamp);
            AddEnvSplitter(column1, "{Volcanic}: ", () => EnvVolcanic);

            AddEnvSplitter(column2, "{Tundra}: ", () => EnvTundra);
            AddEnvSplitter(column2, "{Ice}: ",    () => EnvIce);
            AddEnvSplitter(column2, "{Desert}: ", () => EnvDesert);
            AddEnvSplitter(column2, "{Barren}: ", () => EnvBarren);
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
            // Ludoal fork (maintainer feedback, 7 Aug): the Environment panel stays shown at all
            // times. It used to hide itself for a race with no environment modifiers (neutral
            // Terran, every Env* == 1); now it always displays, a neutral race simply reading 1.00
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
