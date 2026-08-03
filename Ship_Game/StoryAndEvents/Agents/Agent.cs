using Ship_Game.Data.Serialization;

namespace Ship_Game
{
    // Ludoal fork: SAVE BALLAST ONLY. The legacy agent system is gone; this type and
    // EmpireData.AgentList remain so that saves written under the legacy espionage rule
    // still deserialize. Nothing reads or updates agents any more.
    [StarDataType]
    public sealed class Agent
    {
        [StarData] public string Name;
        [StarData] public int Level = 1;
        [StarData] public int Experience;
        [StarData] public AgentMission Mission;
        [StarData] public AgentMission PrevisousMission = AgentMission.Training;
        [StarData] public string PreviousTarget;
        [StarData] public int TurnsRemaining;
        [StarData] public string TargetEmpire = "";
        [StarData] public int TargetPlanetId;
        [StarData] public bool spyMute;
        [StarData] public string HomePlanet = "";
        [StarData] public float Age = 30f;
        [StarData] public float ServiceYears = 0f;
        [StarData] public short Assassinations;
        [StarData] public short Training;
        [StarData] public short Infiltrations;
        [StarData] public short Sabotages;
        [StarData] public short TechStolen;
        [StarData] public short Robberies;
        [StarData] public short Rebellions;
    }

    public enum AgentMission
    {
        Defending,
        Training,
        Infiltrate,
        Assassinate,
        Sabotage,
        StealTech,
        Robbery,
        InciteRebellion,
        Undercover,
        Recovering
    }
}
