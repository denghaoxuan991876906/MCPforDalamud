using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin.Services;
using System.Numerics;

namespace MCPforDalamud.Events;

public class EventCollector : IDisposable
{
    private readonly EventBuffer _buffer;
    private EventCollectionConfig _config;
    private uint _lastHp, _lastMp;
    private uint _lastJobId;
    private Vector3 _lastPosition;
    private ulong _lastTargetId, _lastFocusTargetId;
    private ushort _lastTerritoryId;
    private bool _lastInCombat, _lastMounted, _lastDutyStarted;
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

    public void UpdateConfig(EventCollectionConfig config) { _config = config; }

    private void OnUpdate(IFramework framework)
    {
        if (!Service.IsReady) return;
        var now = NowMs();
        var lp = Service.ObjectTable.LocalPlayer;
        if (lp == null) return;
        CollectPlayerStats(lp, now);
        CollectTargetStats(now);
        CollectCombatEvents(now);
        CollectMapEvents(now);
        CollectObjects(now, lp);
    }

    private void CollectPlayerStats(IPlayerCharacter lp, long now)
    {
        if (_config.HasPlayerStat("hp") && _lastHp != lp.CurrentHp)
        {
            if (!TryThrottle(EventTypes.HpChange, now))
            {
                var from = _lastHp;
                var to = lp.CurrentHp;
                if (from > 0 && from != to)
                {
                    _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.HpChange, Data = new { from, to, max = lp.MaxHp } });
                    if (from > to)
                        _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.CombatDamage, Data = new { amount = from - to, currentHp = to, maxHp = lp.MaxHp } });
                }
                _lastHp = lp.CurrentHp;
            }
        }
        else if (!_config.HasPlayerStat("hp")) { _lastHp = lp.CurrentHp; }

        if (_config.HasPlayerStat("mp") && _lastMp != lp.CurrentMp)
        {
            if (!TryThrottle(EventTypes.MpChange, now))
                _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.MpChange, Data = new { from = _lastMp, to = lp.CurrentMp, max = lp.MaxMp } });
            _lastMp = lp.CurrentMp;
        }

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
        if (!_config.HasTargetStat("targetChange")) return;
        var t = Service.TargetManager.Target;
        var newTargetId = t?.GameObjectId ?? 0u;
        var newFocusId = Service.TargetManager.FocusTarget?.GameObjectId ?? 0u;
        if (_lastTargetId != newTargetId)
        {
            _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.TargetChange, Data = new { from = _lastTargetId, to = newTargetId, name = t?.Name.TextValue ?? "", kind = t?.ObjectKind.ToString() ?? "" } });
            _lastTargetId = newTargetId;
        }
        if (_lastFocusTargetId != newFocusId)
        {
            _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.FocusTargetChange, Data = new { from = _lastFocusTargetId, to = newFocusId } });
            _lastFocusTargetId = newFocusId;
        }
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
        if (_config.ObjectRange <= 0) return;
        var enemies = new List<object>();
        if (_config.HasObjectType("enemy"))
        {
            foreach (var o in Service.ObjectTable)
            {
                if (o == null || o == lp) continue;
                var dist = Vector3.Distance(lp.Position, o.Position);
                if (dist > _config.ObjectRange) continue;
                var chara = o as ICharacter;
                if (chara == null || chara.MaxHp == 0) continue;
                enemies.Add(new { name = o.Name.TextValue, id = o.GameObjectId, hp = chara.CurrentHp, maxHp = chara.MaxHp, distance = Math.Round(dist, 1) });
            }
        }
        if (enemies.Count > 0)
            _buffer.Add(new EventRecord { Timestamp = now, Type = EventTypes.NearbyEnemy, Data = new { range = _config.ObjectRange, count = enemies.Count, enemies } });
    }

    private bool TryThrottle(string type, long now)
    {
        if (_lastEventTime.TryGetValue(type, out var last) && (now - last) < _config.ThrottleMs) return true;
        _lastEventTime[type] = now;
        return false;
    }

    private static long NowMs() => (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;

    public void Dispose() { Service.Framework.Update -= OnUpdate; }
}
