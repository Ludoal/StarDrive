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
    // ⚠ ORDERED, and that is the whole point (bench 531): the list is a CHRONOLOGY - the order
    // a colony grows in, which is the order the buildings unlock in. Built from the top,
    // replaced from the top (the primitive makes way), rebuilt from the bottom (the precious
    // first). One list, three gestures. Uniqueness is not the collection's job: the design
    // screen never offers a building already on the plan's tiles.
    [StarData] public Array<string> PlannedBuildings;
    [StarData] public ColonyType ColonyType;
    // Every name this plan has answered to. A rename rewrites the references of the game in
    // hand; an older save, a chain or a governor's default still holds a former name and finds
    // the plan again through this list. ⚠ Absent from an older save, it arrives null.
    [StarData] public Array<string> FormerNames;

    // the plan answers to its name and to every name it has carried
    public bool KnownAs(string name) => Name == name || FormerNames?.Contains(name) == true;

    public void RecordFormerName(string previous)
    {
        if (previous.IsEmpty() || previous == Name)
            return;
        FormerNames ??= new Array<string>();
        if (!FormerNames.Contains(previous))
            FormerNames.Add(previous);
    }
    public static string CurrentModName =>  GlobalStats.HasMod ? GlobalStats.ModName : "BBplus";

    [StarDataConstructor] public BlueprintsTemplate() { }

    // ⚠ SAVE COMPATIBILITY (bench 532): a save holding PlannedBuildings as a HashSet<string>
    // deserializes into this Array field as NULL - the reader logs the type mismatch and carries
    // on instead of throwing. Recover rather than reset: the template also lives in its own yaml
    // under Colony Blueprints/<mod>/, parsed into the ResourceManager before any save is read, so
    // the plan is taken back from there by name. An empty list only when there is nothing to take
    // back - never a crash.
    [StarDataDeserialized]
    void OnDeserialized()
    {
        FormerNames ??= new Array<string>();

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
        FormerNames = new Array<string>();
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
