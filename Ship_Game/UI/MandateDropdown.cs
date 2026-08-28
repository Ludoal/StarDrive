using System;
using Ship_Game.UI;

namespace Ship_Game
{
    // Ludoal fork: the build/scrap mandate picker, in ONE place. It is shown twice - on a colony's
    // GOVERNOR tab and on the empire's Policies > Colony page - and two copies of a list of options
    // is how the two ends come to disagree about what a mandate means.
    //
    // The colony's picker carries an extra position, Auto, which defers to the empire's. The
    // empire's does not: a policy that could defer to itself has nowhere to defer to.
    public static class MandateDropdown
    {
        // withByBlueprint: the colony's pickers carry the delegated position so they can DISPLAY
        // it while an exclusive plan commands. It is never reachable by a click - the picker is
        // read-only exactly while it shows this - and the model refuses to store it either way.
        public static DropOptions<Planet.BuildMandate> Make(Planet.BuildMandate active,
                                                           Action<Planet.BuildMandate> apply,
                                                           bool withAuto,
                                                           bool withByBlueprint = false)
        {
            // 120, not 110: "Economic only" was clipped to "Economic onl..."
            var list = new DropOptions<Planet.BuildMandate>(120, 18);
            if (withAuto)
                list.AddOption(option: GameText.MandateAuto, Planet.BuildMandate.Auto);
            list.AddOption(option: GameText.MandateAll, Planet.BuildMandate.All);
            list.AddOption(option: GameText.MandateEconomicOnly, Planet.BuildMandate.EconomicOnly);
            list.AddOption(option: GameText.MandateDefenseOnly, Planet.BuildMandate.DefenseOnly);
            list.AddOption(option: GameText.MandateNone, Planet.BuildMandate.None);
            if (withByBlueprint)
                list.AddOption(option: GameText.MandateByBlueprint, Planet.BuildMandate.ByBlueprint);
            list.ActiveValue = active;
            list.OnValueChange = apply;
            return list;
        }
    }
}
