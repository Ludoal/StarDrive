using SDGraphics;
using SDUtils;
using Ship_Game.AI;
using Ship_Game.Data.Serialization;
using Ship_Game.Ships;
using System.Collections.Generic;
using static Ship_Game.Planet;
using Vector2 = SDGraphics.Vector2;

namespace Ship_Game;

// Data used for saved Blueprints
[StarDataType]
public sealed class BlueprintsTemplate
{
    [StarData] public string Name;
    [StarData] public string ModName;
    [StarData] public bool Exclusive;
    [StarData] public string LinkTo;
    // ⚠ ORDERED, and that is the whole point (maintainer, bench 531). It was a set - no order
    // at all, not even an accidental one - so the plan could not say what to raise first, and
    // neither of the two questions the maintainer kept asking had an answer.
    //
    // The list reads as a CHRONOLOGY: the order a colony grows in, which is the order the
    // buildings unlock in. Built from the top, replaced from the top (the primitive makes way),
    // rebuilt from the bottom (the precious first). One list, three gestures.
    //
    // Uniqueness is no longer the collection's job: the design screen never offers a building
    // already on the plan's tiles, which is what kept it unique before.
    [StarData] public Array<string> PlannedBuildings;
    [StarData] public ColonyType ColonyType;
    public static string CurrentModName =>  GlobalStats.HasMod ? GlobalStats.ModName : "BBplus";

    [StarDataConstructor] public BlueprintsTemplate() { }
    public BlueprintsTemplate(string name, bool exclusive, string linkTo, Array<string> plannedBuildings, ColonyType cType) 
    {
        Name = name;
        ModName = CurrentModName;
        Exclusive = exclusive;
        LinkTo = linkTo;
        PlannedBuildings = plannedBuildings;
        ColonyType = cType == ColonyType.TradeHub ? ColonyType.Colony : cType;
    }

    public bool Validated => ResourceManager.BlueprintsValid(this, out _);

    public bool CanSafelyLinkFor(string requestingTemplateName)
    {
        if (string.IsNullOrEmpty(LinkTo))
            return true;

        if (LinkTo == requestingTemplateName)
            return false;

        if (ResourceManager.TryGetBlueprints(LinkTo, out BlueprintsTemplate nextTemplate))
            return nextTemplate.CanSafelyLinkFor(requestingTemplateName);

        Log.Error($"Could not find template for {LinkTo} in Resource Manager");
        return true;
    }
}
