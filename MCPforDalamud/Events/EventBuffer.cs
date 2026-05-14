using System.Collections.Concurrent;

namespace MCPforDalamud.Events;

public class EventBuffer
{
    private readonly ConcurrentQueue<EventRecord> _queue = new();
    private readonly int _maxSize;
    private long _nextId;

    public EventBuffer(int maxSize = 2000) { _maxSize = maxSize; }

    public void Add(EventRecord record)
    {
        record.Id = Interlocked.Increment(ref _nextId);
        _queue.Enqueue(record);
        while (_queue.Count > _maxSize) _queue.TryDequeue(out _);
    }

    public List<EventRecord> Query(string[]? types = null, int count = 50, long? since = null, long? before = null)
    {
        var filtered = new List<EventRecord>();
        foreach (var e in _queue)
        {
            if (types != null && types.Length > 0 && !types.Contains(e.Type)) continue;
            if (since.HasValue && e.Timestamp < since.Value) continue;
            if (before.HasValue && e.Timestamp > before.Value) continue;
            filtered.Add(e);
        }
        filtered.Reverse();
        if (count > 0 && filtered.Count > count) filtered = filtered.GetRange(0, count);
        return filtered;
    }

    public int Count => _queue.Count;
    public int MaxSize => _maxSize;
}
