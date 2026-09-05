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
        // Ludoal fork (maintainer): `deferredTo` is what the EMPIRE currently mandates. The Auto
        // position names it in brackets - Auto alone says a decision is made elsewhere without
        // saying which, and the player would have to open Policies to find out.
        public static LocalizedText AutoOption(Planet.BuildMandate? deferredTo)
        {
            if (deferredTo == null)
                return GameText.MandateAuto;
            GameText s = deferredTo switch
            {
                Planet.BuildMandate.EconomicOnly  => GameText.MandateShortEconomic,
                Planet.BuildMandate.DefenseOnly   => GameText.MandateShortDefense,
                Planet.BuildMandate.None          => GameText.MandateShortNone,
                Planet.BuildMandate.BlueprintOnly => GameText.MandateShortBlueprint,
                _                                 => GameText.MandateShortAll,
            };
            return $"{Localizer.Token(GameText.MandateAuto)} ({Localizer.Token(s)})";
        }

        public static DropOptions<Planet.BuildMandate> Make(Planet.BuildMandate active,
                                                           Action<Planet.BuildMandate> apply,
                                                           bool withAuto,
                                                           Planet.BuildMandate? deferredTo = null)
        {
            // 120, not 110: "Economic only" was clipped to "Economic onl..."
            var list = new DropOptions<Planet.BuildMandate>(120, 18);
            if (withAuto)
                list.AddOption(option: AutoOption(deferredTo), Planet.BuildMandate.Auto);
            list.AddOption(option: GameText.MandateAll, Planet.BuildMandate.All);
            list.AddOption(option: GameText.MandateEconomicOnly, Planet.BuildMandate.EconomicOnly);
            list.AddOption(option: GameText.MandateDefenseOnly, Planet.BuildMandate.DefenseOnly);
            list.AddOption(option: GameText.MandateBlueprintOnly, Planet.BuildMandate.BlueprintOnly);
            list.AddOption(option: GameText.MandateNone, Planet.BuildMandate.None);
            list.ActiveValue = active;
            list.OnValueChange = apply;
            return list;
        }

        // ⚠ bench 530: while an exclusive blueprint commands the colony, the picker holds ONE
        // entry - the word it displays. It used to carry that word permanently, which put a
        // choice in the list that did nothing when picked: the option that lies, in the very
        // feature built to remove one. The delegated picker is read-only, so a list of one is
        // never opened; the ordinary options come back with the colony's own right.
        public static void SetDelegated(DropOptions<Planet.BuildMandate> list, bool delegated,
                                        Planet.BuildMandate own, bool withAuto,
                                        Planet.BuildMandate? deferredTo = null)
        {
            list.Clear();
            if (delegated)
            {
                list.AddOption(option: GameText.MandateByBlueprint, Planet.BuildMandate.ByBlueprint);
                list.ActiveValue = Planet.BuildMandate.ByBlueprint;
            }
            else
            {
                if (withAuto)
                    list.AddOption(option: AutoOption(deferredTo), Planet.BuildMandate.Auto);
                list.AddOption(option: GameText.MandateAll, Planet.BuildMandate.All);
                list.AddOption(option: GameText.MandateEconomicOnly, Planet.BuildMandate.EconomicOnly);
                list.AddOption(option: GameText.MandateDefenseOnly, Planet.BuildMandate.DefenseOnly);
                list.AddOption(option: GameText.MandateBlueprintOnly, Planet.BuildMandate.BlueprintOnly);
                list.AddOption(option: GameText.MandateNone, Planet.BuildMandate.None);
                list.ActiveValue = own;
            }
        }
    }
}
