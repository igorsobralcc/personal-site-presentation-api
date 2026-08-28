using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PersonalSite.Presentation.Api.Tests;

internal sealed record TestLogEntry(string Category, LogLevel Level, string Message, Exception? Exception);

internal sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<TestLogEntry> _entries = new();
    public IReadOnlyCollection<TestLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, _entries);
    public void Dispose() { }

    private sealed class TestLogger(string category, ConcurrentQueue<TestLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new(category, logLevel, formatter(state, exception), exception));
        }
    }
}
