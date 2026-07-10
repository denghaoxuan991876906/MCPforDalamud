using System.Text.Json.Serialization;

namespace MCPforDalamud.Events;

public class EventCollectionConfig
{
    [JsonPropertyName("playerStats")] public List<string> PlayerStats { get; set; } = new() { "hp", "mp", "gp", "job", "position" };
    [JsonPropertyName("targetStats")] public List<string> TargetStats { get; set; } = new() { "hp", "type", "targetChange" };
    [JsonPropertyName("objectRange")] public int ObjectRange { get; set; } = 30;
    [JsonPropertyName("objectTypes")] public List<string> ObjectTypes { get; set; } = new() { "enemy" };
    [JsonPropertyName("nearbyPlayerRange")] public int NearbyPlayerRange { get; set; } = 0;
    [JsonPropertyName("combatEvents")] public List<string> CombatEvents { get; set; } = new() { "damage", "startEnd" };
    [JsonPropertyName("systemEvents")] public List<string> SystemEvents { get; set; } = new() { "duty", "fate" };
    [JsonPropertyName("throttleMs")] public int ThrottleMs { get; set; } = 500;

    public bool HasPlayerStat(string stat) => PlayerStats.Contains(stat);
    public bool HasTargetStat(string stat) => TargetStats.Contains(stat);
    public bool HasObjectType(string type) => ObjectTypes.Contains(type);
    public bool HasCombatEvent(string evt) => CombatEvents.Contains(evt);
    public bool HasSystemEvent(string evt) => SystemEvents.Contains(evt);

    public void Validate()
    {
        ValidateValues(PlayerStats, new[] { "hp", "mp", "gp", "job", "position" }, "playerStats");
        ValidateValues(TargetStats, new[] { "hp", "type", "targetChange" }, "targetStats");
        ValidateValues(ObjectTypes, new[] { "enemy" }, "objectTypes");
        ValidateValues(CombatEvents, new[] { "damage", "startEnd" }, "combatEvents");
        ValidateValues(SystemEvents, new[] { "duty", "fate" }, "systemEvents");
        if (ObjectRange is < 0 or > 200) throw new ArgumentOutOfRangeException(nameof(ObjectRange), "objectRange 必须在 0 到 200 之间");
        if (NearbyPlayerRange is < 0 or > 200) throw new ArgumentOutOfRangeException(nameof(NearbyPlayerRange), "nearbyPlayerRange 必须在 0 到 200 之间");
        if (ThrottleMs is < 50 or > 60000) throw new ArgumentOutOfRangeException(nameof(ThrottleMs), "throttleMs 必须在 50 到 60000 之间");
    }

    private static void ValidateValues(IEnumerable<string> values, IReadOnlyCollection<string> allowed, string name)
    {
        var invalid = values.Where(value => !allowed.Contains(value)).Distinct().ToArray();
        if (invalid.Length > 0) throw new ArgumentException($"{name} 包含不支持的值: {string.Join(", ", invalid)}");
    }
}
