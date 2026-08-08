namespace PokemonBattleJournal.Interfaces
{
    /// <summary>
    /// Starts a timed span around an operation, for whatever performance backend is wired up.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An abstraction rather than calling <c>SentrySdk</c> directly, for the same reason
    /// <see cref="IErrorHandler"/> is one: 54 direct <c>new ModalErrorHandler()</c> calls made
    /// every catch path untestable, and direct SDK calls here would do the same to every
    /// instrumented path. A test substitutes this; nothing has to initialise Sentry.
    /// </para>
    /// <para>
    /// <b>The naming contract is a privacy contract.</b> Span names and operations reach Sentry
    /// as free text — they are not filtered by
    /// <c>SentryRedactingSink</c>, which only governs Serilog property values. So a span
    /// description is exactly as dangerous as a log message, and the same rule applies: ids,
    /// counts and lengths, never names, notes or paths. "restore.match" with an id is fine;
    /// "Ash vs Charizard ex" is a leak that no sink will catch.
    /// </para>
    /// <para>
    /// Callers pass a constant operation and a constant description. There is deliberately no
    /// overload taking interpolated text, because the easiest thing to write should be the safe
    /// thing. Attach varying detail with <see cref="ITimedSpan.SetMeasurement"/> instead, which is
    /// numeric by construction.
    /// </para>
    /// </remarks>
    public interface IPerformanceMonitor
    {
        /// <summary>
        /// Begins a span. Dispose to finish it.
        /// </summary>
        /// <param name="operation">
        /// Constant category, e.g. <c>"restore"</c> or <c>"db.query"</c>. Never user content.
        /// </param>
        /// <param name="description">
        /// Constant description of the step, e.g. <c>"apply conflict resolution"</c>. Never user
        /// content.
        /// </param>
        ITimedSpan StartSpan(string operation, string description);
    }

    /// <summary>
    /// A running span. Disposing finishes it and records its duration.
    /// </summary>
    /// <remarks>
    /// Numbers only. There is no method to attach a string, on purpose — see the privacy note on
    /// <see cref="IPerformanceMonitor"/>. If a span needs to carry "which one", carry the id.
    /// </remarks>
    public interface ITimedSpan : IDisposable
    {
        /// <summary>
        /// Attaches a numeric measurement, e.g. how many matches a restore touched.
        /// </summary>
        /// <param name="name">Constant measurement name, e.g. <c>"matches"</c>.</param>
        /// <param name="value">The number.</param>
        void SetMeasurement(string name, double value);

        /// <summary>
        /// Marks the span failed. Called from a catch, before the exception is rethrown or
        /// handed to <see cref="IErrorHandler"/>.
        /// </summary>
        void SetFailed();
    }
}
