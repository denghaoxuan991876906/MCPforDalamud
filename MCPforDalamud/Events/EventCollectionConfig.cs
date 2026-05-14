using System.Text.Json.Serialization;

namespace MCPforDalamud.Events;

public class EventCollectionConfig
{
    [JsonPropertyName("playerStats")] public List<string> PlayerStats { get; set; } = new() { "hp", "mp", "job", "position" };
    [JsonPropertyName("targetStats")] public List<string> TargetStats { get; set; } = new() { "hp", "type", "targetChange" };
    [JsonPropertyName("objectRange")] public int ObjectRange { get; set; } = 30;
    [JsonPropertyName("objectTypes")] public List<string> ObjectTypes { get; set; } = new() { "enemy" };
    [JsonPropertyName("nearbyPlayerRange")] public int NearbyPlayerRange { get; set; } = 0;
    [JsonPropertyName("combatEvents")] public List<string> CombatEvents { get; set; } = new() { "action", "damage", "startEnd" };
    [JsonPropertyName("systemEvents")] public List<string> SystemEvents { get; set; } = new() { "duty", "fate" };
    [JsonPropertyName("throttleMs")] public int ThrottleMs { get; set; } = 500;

    public bool HasPlayerStat(string stat) => PlayerStats.Contains(stat);
    public bool HasTargetStat(string stat) => TargetStats.Contains(stat);
    public bool HasObjectType(string type) => ObjectTypes.Contains(type);
    public bool HasCombatEvent(string evt) => CombatEvents.Contains(evt);
    public bool HasSystemEvent(string evt) => SystemEvents.Contains(evt);
}
