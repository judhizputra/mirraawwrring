namespace Mirraawwrring.Core.Logging;

/// <summary>
/// Penyangga log di memori (ring buffer) untuk panel Log di jendela utama.
/// Thread-safe; event <see cref="EntryAdded"/> dipanggil dari thread penulis, UI harus marshal sendiri.
/// </summary>
public sealed class LogBuffer
{
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _entries;
    private readonly int _capacity;

    public LogBuffer(int capacity = 2000)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _entries = new Queue<LogEntry>(capacity);
    }

    public event Action<LogEntry>? EntryAdded;

    public void Add(LogEntry entry)
    {
        lock (_gate)
        {
            if (_entries.Count >= _capacity)
            {
                _entries.Dequeue();
            }

            _entries.Enqueue(entry);
        }

        EntryAdded?.Invoke(entry);
    }

    public IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }
}
