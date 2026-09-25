using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ObsidianRAG.Tests.program;

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentBag<(string Category, LogLevel Level, string Message)> Records { get; } = new();
    public ConcurrentBag<(string Category, Exception Error)> Exceptions { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, Records, Exceptions);

    public void Dispose() { }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentBag<(string, LogLevel, string)> _records;
        private readonly ConcurrentBag<(string, Exception)> _exceptions;

        public CapturingLogger(
            string category,
            ConcurrentBag<(string, LogLevel, string)> records,
            ConcurrentBag<(string, Exception)> exceptions)
        {
            _category = category;
            _records = records;
            _exceptions = exceptions;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _records.Add((_category, logLevel, formatter(state, exception)));
            if (exception is not null) _exceptions.Add((_category, exception));
        }
    }
}
