using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace MCPforDalamud.Ipc;

public class PushDataEntry
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("key")] public string Key { get; set; } = string.Empty;
    [JsonPropertyName("data")] public string JsonData { get; set; } = string.Empty;
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
}

public class PushDataBuffer
{
    private readonly ConcurrentQueue<PushDataEntry> _queue = new();
    private readonly int _maxSize;
    private long _nextId;

    public PushDataBuffer(int maxSize = 2000) { _maxSize = maxSize; }

    public void Push(string key, string jsonData)
    {
        var entry = new PushDataEntry { Id = Interlocked.Increment(ref _nextId), Key = key, JsonData = jsonData, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        _queue.Enqueue(entry);
        while (_queue.Count > _maxSize) _queue.TryDequeue(out _);
    }

    public List<PushDataEntry> Query(string? key = null, int count = 50)
    {
        var filtered = new List<PushDataEntry>();
        foreach (var e in _queue) { if (key != null && e.Key != key) continue; filtered.Add(e); }
        filtered.Reverse();
        if (count > 0 && filtered.Count > count) filtered = filtered.GetRange(0, count);
        return filtered;
    }

    public int Count => _queue.Count;
}
