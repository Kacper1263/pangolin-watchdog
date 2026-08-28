using Microsoft.Extensions.Logging;

namespace PangolinWatchdog.Helpers;

public sealed record ApplicationLogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    string Message);

/// <summary>
/// Volatile application log buffer. Logs intentionally live only for the lifetime
/// of the process and are bounded by both age and count.
/// </summary>
public sealed class InMemoryLogStore
{
    public static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private const int MaxEntries = 10_000;
    private readonly LinkedList<ApplicationLogEntry> _entries = new();
    private readonly object _sync = new();

    public void Add(ApplicationLogEntry entry)
    {
        lock (_sync)
        {
            RemoveExpired(entry.Timestamp);
            _entries.AddLast(entry);

            while (_entries.Count > MaxEntries)
            {
                _entries.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<ApplicationLogEntry> GetSnapshot()
    {
        lock (_sync)
        {
            RemoveExpired(DateTimeOffset.Now);
            return _entries.ToList();
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _entries.Clear();
        }
    }

    private void RemoveExpired(DateTimeOffset now)
    {
        var cutoff = now - Retention;
        while (_entries.First != null && _entries.First.Value.Timestamp < cutoff)
        {
            _entries.RemoveFirst();
        }
    }
}

public sealed class InMemoryLogProvider : ILoggerProvider
{
    private readonly InMemoryLogStore _store;

    public InMemoryLogProvider(InMemoryLogStore store) => _store = store;

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _store);

    public void Dispose() { }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly InMemoryLogStore _store;

        public InMemoryLogger(string categoryName, InMemoryLogStore store)
        {
            _categoryName = categoryName;
            _store = store;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (exception != null)
            {
                message = string.IsNullOrWhiteSpace(message)
                    ? exception.ToString()
                    : $"{message}{Environment.NewLine}{exception}";
            }

            _store.Add(new ApplicationLogEntry(
                DateTimeOffset.Now,
                logLevel,
                _categoryName,
                message));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
