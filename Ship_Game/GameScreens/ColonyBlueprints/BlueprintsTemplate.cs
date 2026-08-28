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

    // ⚠ SAVE COMPATIBILITY (bench 532). PlannedBuildings was a HashSet<string> until build
    // 532. The binary reader resolves the stored collection type fine, then fails to put a
    // HashSet into an Array field - and it LOGS that failure and carries on rather than
    // throwing, so the field simply arrives NULL. Every save written before 532 therefore
    // loads a template with no plan at all, and the first colony to ask whether a building is
    // required dies on it, in the middle of deserialization.
    //
    // The plan itself is not lost, which is why this recovers instead of resetting: a template
    // lives in its own yaml under Colony Blueprints/<mod>/, already parsed into the
    // ResourceManager by the time any save is read. We take the plan back from there, by name.
    // An empty list only when there is genuinely nothing to take back - never a crash.
    [StarDataDeserialized]
    void OnDeserialized()
    {
        if (PlannedBuildings != null)
            return;

        PlannedBuildings = Name != null
                        && ResourceManager.TryGetBlueprints(Name, out BlueprintsTemplate onDisk)
                        && !ReferenceEquals(onDisk, this) && onDisk.PlannedBuildings != null
                         ? new Array<string>(onDisk.PlannedBuildings)
                         : new Array<string>();
    }
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
