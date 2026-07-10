using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace MCPforDalamud.Events;

public class EventCollector : IDisposable
{
    private const byte EnemyBattleNpcKind = 2;
    private readonly EventBuffer _buffer;
    private EventCollectionConfig _config;
    private uint _lastHp, _lastMp, _lastGp, _lastTargetHp;
    private uint _lastJobId;
    private Vector3 _lastPosition;
    private ulong _lastTargetId, _lastFocusTargetId;
    private ushort _lastTerritoryId;
    private bool _lastInCombat, _lastMounted, _lastDutyStarted;
    private bool _initialized;
    private HashSet<ulong> _nearbyEnemyIds = new();
    private HashSet<ulong> _nearbyPlayerIds = new();
    private Dictionary<uint, byte> _fateProgress = new();
    private readonly Dictionary<string, long> _lastEventTime = new();
    private static readonly DateTime Epoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public EventBuffer Buffer => _buffer;
    public EventCollectionConfig Config => _config;

    public EventCollector(EventBuffer buffer, EventCollectionConfig config)
    {
        _buffer = buffer;
        _config = config;
        Service.Framework.Update += OnUpdate;
    }

    public void UpdateConfig(EventCollectionConfig config) { _config = config; _initialized = false; }

    private void OnUpdate(IFramework framework)
    {
        if (!Service.IsReady) { _initialized = false; return; }
        var now = NowMs();
        var lp = Service.ObjectTable.LocalPlayer;
        if (lp == null) return;
        if (!_initialized)
        {
            InitializeSnapshot(lp);
            _initialized = true;
            return;
        }
        CollectPlayerStats(lp, now);
        CollectTargetStats(now);
        CollectCombatEvents(now);
        CollectMapEvents(now);
        CollectObjects(now, lp);
        CollectFates(now);
    }

    private void InitializeSnapshot(IPlayerCharacter lp)
    {
        _lastHp = lp.CurrentHp;
        _lastMp = lp.CurrentMp;
        _lastGp = lp.CurrentGp;
        _lastJobId = lp.ClassJob.Value.RowId;
        _lastPosition = lp.Position;
        var target = Service.TargetManager.Target;
        _lastTargetId = target?.GameObjectId ?? 0;
        _lastTargetHp = (target as ICharacter)?.CurrentHp ?? 0;
        _lastFocusTargetId = Service.TargetManager.FocusTarget?.GameObjectId ?? 0;
        _lastTerritoryId = (ushort)Service.ClientState.TerritoryType;
        _lastInCombat = Service.Condition[ConditionFlag.InCombat];
        _lastMounted = Service.Condition[ConditionFlag.Mounted];
        _lastDutyStarted = Service.DutyState.IsDutyStarted;
        _nearbyEnemyIds = GetNearbyCharacters(lp, _config.ObjectRange, false).Select(x => x.Id).ToHashSet();
        _nearbyPlayerIds = GetNearbyCharacters(lp, _config.NearbyPlayerRange, true).Select(x => x.Id).ToHashSet();
        _fateProgress = GetFateProgress();
        _lastEventTime.Clear();
    }

    private void CollectPlayerStats(IPlayerCharacter lp, long now)
    {
        if (_config.HasPlayerStat("hp") && _lastHp != lp.CurrentHp)
        {
            var from = _lastHp;
            var to = lp.CurrentHp;
            if (!TryThrottle(EventTypes.HpChange, now))
            {
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.HpChange, Data = new { from, to, max = lp.MaxHp } });
                if (_config.HasCombatEvent("damage") && from > to)
                    _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.CombatDamage, Data = new { source = "player", amount = from - to, currentHp = to, maxHp = lp.MaxHp } });
            }
            _lastHp = to;
        }
        else if (!_config.HasPlayerStat("hp")) { _lastHp = lp.CurrentHp; }

        if (_config.HasPlayerStat("mp") && _lastMp != lp.CurrentMp)
        {
            if (!TryThrottle(EventTypes.MpChange, now))
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.MpChange, Data = new { from = _lastMp, to = lp.CurrentMp, max = lp.MaxMp } });
            _lastMp = lp.CurrentMp;
        }

        if (_config.HasPlayerStat("gp") && _lastGp != lp.CurrentGp)
        {
            if (!TryThrottle(EventTypes.GpChange, now))
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.GpChange, Data = new { from = _lastGp, to = lp.CurrentGp, max = lp.MaxGp } });
            _lastGp = lp.CurrentGp;
        }
        else if (!_config.HasPlayerStat("gp")) { _lastGp = lp.CurrentGp; }

        if (_config.HasPlayerStat("job") && _lastJobId != lp.ClassJob.Value.RowId)
        {
            _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.JobChange, Data = new { from = _lastJobId, to = lp.ClassJob.Value.RowId, name = lp.ClassJob.Value.Name.ToString() } });
            _lastJobId = lp.ClassJob.Value.RowId;
        }

        if (_config.HasPlayerStat("position"))
        {
            var dist = Vector3.Distance(_lastPosition, lp.Position);
            if (dist > 0.5f)
            {
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.PlayerMove, Data = new { x = Math.Round(lp.Position.X, 2), y = Math.Round(lp.Position.Y, 2), z = Math.Round(lp.Position.Z, 2), distance = Math.Round(dist, 2) } });
                _lastPosition = lp.Position;
            }
        }
    }

    private void CollectTargetStats(long now)
    {
        var t = Service.TargetManager.Target;
        var newTargetId = t?.GameObjectId ?? 0u;
        var newFocusId = Service.TargetManager.FocusTarget?.GameObjectId ?? 0u;
        if (_config.HasTargetStat("targetChange") && _lastTargetId != newTargetId)
        {
            _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.TargetChange, Data = new { from = _lastTargetId, to = newTargetId, name = t?.Name.TextValue ?? "", kind = t?.ObjectKind.ToString() ?? "" } });
            _lastTargetId = newTargetId;
            _lastTargetHp = (t as ICharacter)?.CurrentHp ?? 0;
        }
        if (_lastFocusTargetId != newFocusId)
        {
            _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.FocusTargetChange, Data = new { from = _lastFocusTargetId, to = newFocusId } });
            _lastFocusTargetId = newFocusId;
        }
        var targetCharacter = t as ICharacter;
        var targetHp = targetCharacter?.CurrentHp ?? 0;
        if (_config.HasTargetStat("hp") && newTargetId == _lastTargetId && targetHp != _lastTargetHp)
        {
            if (!TryThrottle(EventTypes.TargetHpChange, now))
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.TargetHpChange, Data = new { targetId = newTargetId, from = _lastTargetHp, to = targetHp, max = targetCharacter?.MaxHp ?? 0 } });
            _lastTargetHp = targetHp;
        }
        _lastTargetId = newTargetId;
    }

    private void CollectCombatEvents(long now)
    {
        if (_config.HasCombatEvent("startEnd"))
        {
            var inCombat = Service.Condition[ConditionFlag.InCombat];
            if (_lastInCombat != inCombat) { _buffer.Add(new EventRecord { Timestamp = now, Type = inCombat ? EventTypes.CombatStart : EventTypes.CombatEnd }); _lastInCombat = inCombat; }
        }
        var mounted = Service.Condition[ConditionFlag.Mounted];
        if (_lastMounted != mounted) { _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.MountChange, Data = new { mounted } }); _lastMounted = mounted; }
    }

    private void CollectMapEvents(long now)
    {
        var tt = Service.ClientState.TerritoryType;
        if (_lastTerritoryId != tt) { _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.MapChange, Data = new { from = _lastTerritoryId, to = tt } }); _lastTerritoryId = (ushort)tt; }
        if (_config.HasSystemEvent("duty"))
        {
            var dutyStarted = Service.DutyState.IsDutyStarted;
            if (_lastDutyStarted != dutyStarted) { _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.DutyUpdate, Data = new { isDutyStarted = dutyStarted } }); _lastDutyStarted = dutyStarted; }
        }
    }

    private void CollectObjects(long now, IPlayerCharacter lp)
    {
        if (_config.HasObjectType("enemy"))
        {
            var enemies = GetNearbyCharacters(lp, _config.ObjectRange, false);
            var ids = enemies.Select(enemy => enemy.Id).ToHashSet();
            if (!ids.SetEquals(_nearbyEnemyIds) && !TryThrottle(EventTypes.NearbyEnemy, now))
            {
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.NearbyEnemy, Data = new { range = _config.ObjectRange, count = enemies.Count, added = ids.Except(_nearbyEnemyIds).ToArray(), removed = _nearbyEnemyIds.Except(ids).ToArray(), enemies } });
                _nearbyEnemyIds = ids;
            }
        }

        if (_config.NearbyPlayerRange > 0)
        {
            var players = GetNearbyCharacters(lp, _config.NearbyPlayerRange, true);
            var ids = players.Select(player => player.Id).ToHashSet();
            if (!ids.SetEquals(_nearbyPlayerIds) && !TryThrottle(EventTypes.NearbyPlayer, now))
            {
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.NearbyPlayer, Data = new { range = _config.NearbyPlayerRange, count = players.Count, added = ids.Except(_nearbyPlayerIds).ToArray(), removed = _nearbyPlayerIds.Except(ids).ToArray(), players } });
                _nearbyPlayerIds = ids;
            }
        }
    }

    private List<NearbyCharacter> GetNearbyCharacters(IPlayerCharacter lp, int range, bool playersOnly)
    {
        if (range <= 0) return new List<NearbyCharacter>();
        var result = new List<NearbyCharacter>();
        foreach (var gameObject in Service.ObjectTable)
        {
            if (gameObject == null || gameObject.GameObjectId == lp.GameObjectId) continue;
            var character = gameObject as ICharacter;
            if (character == null) continue;
            var isPlayer = gameObject is IPlayerCharacter;
            if (playersOnly != isPlayer) continue;
            if (!playersOnly && (gameObject is not IBattleNpc battleNpc || (byte)battleNpc.BattleNpcKind != EnemyBattleNpcKind)) continue;
            var distance = Vector3.Distance(lp.Position, gameObject.Position);
            if (distance > range) continue;
            result.Add(new NearbyCharacter(gameObject.GameObjectId, gameObject.Name.TextValue, character.CurrentHp, character.MaxHp, Math.Round(distance, 1)));
        }
        return result;
    }

    private void CollectFates(long now)
    {
        if (!_config.HasSystemEvent("fate")) return;
        var current = GetFateProgress();
        if (current.Count == _fateProgress.Count && current.All(pair => _fateProgress.TryGetValue(pair.Key, out var progress) && progress == pair.Value)) return;
        if (TryThrottle(EventTypes.FateUpdate, now)) return;
        _buffer.Add(new EventRecord
        {
            Timestamp = now,
            Type = EventTypes.FateUpdate,
            Data = new
            {
                added = current.Keys.Except(_fateProgress.Keys).ToArray(),
                removed = _fateProgress.Keys.Except(current.Keys).ToArray(),
                fates = current.Select(pair => new { fateId = pair.Key, progress = pair.Value }).ToArray()
            }
        });
        _fateProgress = current;
    }

    private static Dictionary<uint, byte> GetFateProgress()
    {
        var result = new Dictionary<uint, byte>();
        foreach (var fate in Service.FateTable)
        {
            if (fate != null) result[fate.FateId] = fate.Progress;
        }
        return result;
    }

    private bool TryThrottle(string type, long now)
    {
        if (_lastEventTime.TryGetValue(type, out var last) && (now - last) < _config.ThrottleMs) return true;
        _lastEventTime[type] = now;
        return false;
    }

    private static long NowMs() => (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;

    public void Dispose() { Service.Framework.Update -= OnUpdate; }

    private sealed record NearbyCharacter(ulong Id, string Name, uint CurrentHp, uint MaxHp, double Distance);
}
