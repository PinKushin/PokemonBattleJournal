using PokemonBattleJournal.Interfaces;
using Sentry;

// Sentry.ISpan is the SDK's own span. Ours is deliberately NOT called ISpan: Sentry is in
// GlobalUsings across this repo, so the obvious name collided at every call site and had to be
// aliased in three separate files before the third collision made the point. ITimedSpan is
// unique, so nothing downstream needs an alias — this file is the only place the two types meet.
using SentryApiSpan = Sentry.ISpan;

namespace PokemonBattleJournal.Logging
{
    /// <summary>
    /// <see cref="IPerformanceMonitor"/> backed by Sentry tracing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <c>SentrySdk.GetSpan()</c> to nest under whatever transaction the SDK already has in
    /// scope, and starts a standalone transaction when there is none. Either way the caller gets
    /// a real span, so instrumented code does not need to know whether it is running inside a
    /// request, a UI interaction, or a test.
    /// </para>
    /// <para>
    /// <b>Uninitialised Sentry is the normal case, not an error case.</b> Unit tests never call
    /// <c>SentrySdk.Init</c>, and neither does a build with no DSN. The SDK returns no-op spans
    /// in that state, which is what keeps instrumentation from becoming an outage — a monitor
    /// that threw or returned null would turn every instrumented path into a
    /// NullReferenceException everywhere except production.
    /// </para>
    /// </remarks>
    public sealed class SentryPerformanceMonitor : IPerformanceMonitor
    {
        public ITimedSpan StartSpan(string operation, string description)
        {
            SentryApiSpan? parent = SentrySdk.GetSpan();

            SentryApiSpan span = parent is null
                ? SentrySdk.StartTransaction(operation, operation, description)
                : parent.StartChild(operation, description);

            return new SentrySpan(span);
        }

        /// <summary>Adapts a Sentry span to the Core-side interface.</summary>
        /// <remarks>
        /// The adapter exists so Core's consumers never see a Sentry type, and so the narrow
        /// surface — numbers only — is enforced by the interface rather than by reviewer
        /// discipline. Sentry's own span can carry arbitrary string tags; this deliberately
        /// exposes no way to reach them.
        /// </remarks>
        private sealed class SentrySpan(SentryApiSpan span) : ITimedSpan
        {
            private readonly SentryApiSpan _span = span;
            private bool _finished;

            public void SetMeasurement(string name, double value)
            {
                if (!_finished)
                {
                    _span.SetMeasurement(name, value);
                }
            }

            public void SetFailed()
            {
                if (!_finished)
                {
                    _span.Status = SpanStatus.InternalError;
                }
            }

            /// <summary>
            /// Finishes the span. Safe to call twice.
            /// </summary>
            /// <remarks>
            /// Instrumented code puts the <c>using</c> inside a <c>try</c>, and an exception path
            /// can reach both the using and an explicit finish. Sentry tolerates a double finish,
            /// but the guard keeps a late SetMeasurement from silently doing nothing on a span
            /// that has already been sent.
            /// </remarks>
            public void Dispose()
            {
                if (_finished)
                {
                    return;
                }

                _finished = true;

                if (_span.Status is null)
                {
                    _span.Status = SpanStatus.Ok;
                }

                _span.Finish();
            }
        }
    }
}
