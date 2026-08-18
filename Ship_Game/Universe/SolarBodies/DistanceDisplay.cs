using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using SDGraphics;

namespace Ship_Game.Universe.SolarBodies
{
    public struct DistanceDisplay
    {
        public readonly string Text;
        public readonly Color Color;
        Distances PlanetDistance;

        public DistanceDisplay(float distance) : this()
        {
            DeterminePlanetDistanceCategory(distance);
            switch (PlanetDistance)
            {
                case Distances.Local:   Text = Localizer.Token(GameText.UhDistanceLocal);   Color = Color.Green;         break;
                case Distances.Near:    Text = Localizer.Token(GameText.UhDistanceNear);    Color = Color.YellowGreen;   break;
                case Distances.Midway:  Text = Localizer.Token(GameText.UhDistanceMidway);  Color = Color.DarkGoldenrod; break;
                case Distances.Distant: Text = Localizer.Token(GameText.UhDistanceDistant); Color = Color.DarkRed;       break;
                default:                Text = Localizer.Token(GameText.UhDistanceBeyond);  Color = Color.DarkGray;      break;
            }
        }

        void DeterminePlanetDistanceCategory(float distance)
        {
            if      (distance.LessOrEqual(140))  PlanetDistance = Distances.Local;
            else if (distance.LessOrEqual(1200)) PlanetDistance = Distances.Near;
            else if (distance.LessOrEqual(3000)) PlanetDistance = Distances.Midway;
            else if (distance.LessOrEqual(6000)) PlanetDistance = Distances.Distant;
            else                                 PlanetDistance = Distances.Beyond;
        }

        enum Distances
        {
            Local,
            Near,
            Midway,
            Distant,
            Beyond
        }
    }
}
