namespace PokemonBattleJournal.Tests.Fixtures
{
    /// <summary>
    /// An <see cref="ILogger{T}"/> that records what was logged so tests can assert on it.
    /// </summary>
    /// <remarks>
    /// A recording fake rather than an NSubstitute mock because <c>ILogger.Log</c> is generic
    /// over <c>TState</c>, and the concrete <c>TState</c> the logging extension methods pass
    /// is an internal framework type. That makes <c>Arg.Is&lt;object&gt;(…)</c> fail to match
    /// and forces <c>Arg.AnyType</c>, which can assert that *something* was logged but not
    /// what it said. Message content is exactly what matters when the thing under test is a
    /// guard that used to fail silently.
    /// </remarks>
    public sealed class RecordingLogger<T> : ILogger<T>
    {
        public sealed record Entry(LogLevel Level, string Message, Exception? Exception);

        private readonly List<Entry> _entries = [];

        public IReadOnlyList<Entry> Entries => _entries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _entries.Add(new Entry(logLevel, formatter(state, exception), exception));
        }

        /// <summary>Entries at <paramref name="level"/> whose message contains <paramref name="substring"/> (case-insensitive).</summary>
        public IReadOnlyList<Entry> EntriesMatching(LogLevel level, string substring) =>
            [.. _entries.Where(e => e.Level == level
                && e.Message.Contains(substring, StringComparison.OrdinalIgnoreCase))];

        /// <summary>All recorded messages, newline-joined — used to build readable assertion failures.</summary>
        public string Dump() => _entries.Count == 0
            ? "(nothing logged)"
            : string.Join(Environment.NewLine, _entries.Select(e => $"[{e.Level}] {e.Message}"));
    }
}
