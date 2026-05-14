using System.Text.Json.Serialization;

namespace MCPforDalamud.Events;

public class EventRecord
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("data")] public object? Data { get; set; }
}

public static class EventTypes
{
    public const string HpChange = "hp_change";
    public const string MpChange = "mp_change";
    public const string GpChange = "gp_change";
    public const string PlayerMove = "player_move";
    public const string JobChange = "job_change";
    public const string TargetChange = "target_change";
    public const string FocusTargetChange = "focus_target_change";
    public const string CombatAction = "combat_action";
    public const string CombatDamage = "combat_damage";
    public const string CombatBuff = "combat_buff";
    public const string CombatCast = "combat_cast";
    public const string CombatStart = "combat_start";
    public const string CombatEnd = "combat_end";
    public const string MapChange = "map_change";
    public const string MountChange = "mount_change";
    public const string ChatMessage = "chat_message";
    public const string DutyUpdate = "duty_update";
    public const string FateUpdate = "fate_update";
    public const string NearbyEnemy = "nearby_enemy";
    public const string NearbyPlayer = "nearby_player";
    public const string SystemNotification = "system_notification";
}
