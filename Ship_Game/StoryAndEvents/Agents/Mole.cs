using System.Linq;
using SDUtils;
using Ship_Game.Data.Serialization;

namespace Ship_Game
{
    [StarDataType]
    public sealed class Mole
    {
        [StarData] public int PlanetId;
        [StarData] public bool Sticky { get; set; } // cannot be removed with counter espionage in new espionage system

        public static Mole PlantMole(Empire owner, Empire target, out Planet targetPlanet)
        {
            targetPlanet = null;
            if (target.IsDefeated) 
                return null;

            // Ludoal fork: no fallback onto the full planet list - a mole cannot infiltrate a
            // planet its owner never explored (nor stack onto an already infiltrated one). The
            // operation reports its stock "no colony for infiltration" failure text instead.
            var potentials = target.GetPlanets().Filter(p => p.IsExploredBy(owner)
                                                             && !owner.data.MoleList.Any(m => m.PlanetId == p.Id));
            if (potentials.Length == 0)
                return null;

            targetPlanet = target.Random.Item(potentials);
            Mole mole = new()
            {
                PlanetId = targetPlanet.Id,
            };

            owner.data.MoleList.Add(mole);
            if (owner.NewEspionageEnabled)
                owner.GetEspionage(target).IncreasePlantedMoleCount();

            return mole;
        }

        public static Mole PlantStickyMoleAtHomeworld(Empire owner, Empire target, out Planet targetPlanet)
        {
            targetPlanet = null;
            // Ludoal fork: only planets the owner explored qualify. The level perk retries on
            // the espionage tick, so the sticky mole simply lands once the homeworld is found.
            var planets = target.GetPlanets().Filter(p => (p.IsHomeworld || p.HasCapital) && p.IsExploredBy(owner));

            targetPlanet = planets.Length == 0 ? target.GetPlanets().Filter(p => p.IsExploredBy(owner))
                                                                    .FindMax(p => p.PopulationBillion)
                                                : target.Random.Item(planets);

            if (targetPlanet == null)
                return null;

            Mole mole = new()
            {
                PlanetId = targetPlanet.Id, Sticky = true,  
            };

            owner.data.MoleList.Add(mole);
            if (owner.NewEspionageEnabled)
                owner.GetEspionage(target).IncreasePlantedMoleCount();

            return mole;
        }
    }
}