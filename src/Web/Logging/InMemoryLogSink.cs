using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Scrobblint.Web.Logging;

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    string? Exception);

[ProviderAlias("InMemory")]
public sealed class InMemoryLogSink : ILoggerProvider
{
    private const int Capacity = 500;
    private readonly ConcurrentQueue<LogEntry> _queue = new();

    public LogEntry[] GetEntries()
    {
        var arr = _queue.ToArray();
        Array.Reverse(arr);
        return arr;
    }

    internal void Write(LogEntry entry)
    {
        _queue.Enqueue(entry);
        while (_queue.Count > Capacity)
            _queue.TryDequeue(out _);
    }

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(this, categoryName);

    public void Dispose() { }

    private sealed class InMemoryLogger(InMemoryLogSink sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            sink.Write(new LogEntry(
                Timestamp: DateTimeOffset.UtcNow,
                Level: logLevel,
                Category: category,
                Message: formatter(state, exception),
                Exception: exception?.ToString()));
        }
    }
}
