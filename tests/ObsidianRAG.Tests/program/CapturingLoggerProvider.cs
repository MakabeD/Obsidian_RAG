using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ObsidianRAG.Tests.program;

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentBag<(string Category, LogLevel Level, string Message)> Records { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Records);

    public void Dispose() { }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentBag<(string, LogLevel, string)> _records;

        public CapturingLogger(string category, ConcurrentBag<(string, LogLevel, string)> records)
        {
            _category = category;
            _records = records;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _records.Add((_category, logLevel, formatter(state, exception)));
        }
    }
}
