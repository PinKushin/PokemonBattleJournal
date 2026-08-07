using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;

namespace PokemonBattleJournal.Logging
{
    /// <summary>
    /// Wraps the Sentry sink and forwards a copy of each log event whose property values have
    /// been reduced to the ones that carry no user content.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This app logs a lot of user content on purpose — trainer names, deck names, tags, chosen
    /// file paths — because that is what makes a local log worth reading. The Sentry sink is
    /// configured with <c>MinimumBreadcrumbLevel = Information</c>, so every one of those lines
    /// became a breadcrumb carrying the RENDERED message, and every <c>LogError</c> shipped the
    /// last few hundred of them off the device. Sentry's own defaults were never the problem:
    /// <c>SendDefaultPii</c>, <c>IncludeTextInBreadcrumbs</c> and <c>AttachScreenshot</c> are all
    /// false and none is overridden. The leak was entirely in what this app chose to write.
    /// </para>
    /// <para>
    /// The call sites were rewritten so the content is not in the string in the first place —
    /// that is the real fix, and it is why the local log now names ids, counts and lengths. This
    /// sink exists because that fix depends on discipline and a log line written in two years
    /// will not have read this comment. It fails CLOSED: a value is forwarded only if its type
    /// is on a small list that cannot express user content.
    /// </para>
    /// <para>
    /// It builds a NEW <see cref="LogEvent"/> rather than editing the one it was given. Serilog
    /// hands the same instance to every sink, so mutating it here would silently redact the
    /// local file log too, with the outcome depending on sink order.
    /// </para>
    /// <para>
    /// Two things it deliberately does not do. The message TEMPLATE is forwarded untouched — it
    /// is written by this app, and without it a breadcrumb would say nothing at all. And an
    /// exception attached with <c>LogError(ex, …)</c> is forwarded as-is: SQLite can quote an
    /// offending value in a constraint message, but rewriting exception text would destroy the
    /// one thing a crash report is for. That residue is accepted and recorded.
    /// </para>
    /// </remarks>
    public sealed class SentryRedactingSink : ILogEventSink
    {
        /// <summary>Stands in for any value that is not provably free of user content.</summary>
        public const string Redacted = "[redacted]";

        /// <summary>
        /// Property names whose string values are written by this app, not by a person.
        /// </summary>
        /// <remarks>
        /// Every entry is a hole in a fail-closed rule, so each one has to earn its place by
        /// being demonstrably app-authored at every site that uses the name. Adding one because
        /// a value "looks harmless" is how this control stops working.
        /// </remarks>
        private static readonly HashSet<string> AppAuthoredStringProperties = new(StringComparer.Ordinal)
        {
            // MainPageViewModel: fixed strings from match validation, e.g. "Select a deck".
            "ValidationMessage",
            // TrainerHillImportService: this app's reason for skipping an entry, never the entry.
            "Problem",
        };

        private readonly ILogEventSink _inner;

        public SentryRedactingSink(ILogEventSink inner) => _inner = inner;

        public void Emit(LogEvent logEvent) => _inner.Emit(Redact(logEvent));

        /// <summary>
        /// Returns a copy of <paramref name="logEvent"/> carrying only diagnostic property values.
        /// </summary>
        internal static LogEvent Redact(LogEvent logEvent)
        {
            List<LogEventProperty> forwarded = new(logEvent.Properties.Count);
            foreach (KeyValuePair<string, LogEventPropertyValue> property in logEvent.Properties)
            {
                forwarded.Add(new LogEventProperty(property.Key, Sanitize(property.Key, property.Value)));
            }

            return new LogEvent(
                logEvent.Timestamp,
                logEvent.Level,
                logEvent.Exception,
                logEvent.MessageTemplate,
                forwarded,
                logEvent.TraceId ?? default,
                logEvent.SpanId ?? default);
        }

        private static LogEventPropertyValue Sanitize(string name, LogEventPropertyValue value)
        {
            // Anything that is not a single scalar came from destructuring ({@Match}) or from
            // logging a collection. Its contents are whatever the model holds today plus whatever
            // is added to the model later, which is precisely what cannot be reasoned about here.
            if (value is not ScalarValue scalar)
            {
                return new ScalarValue(Redacted);
            }

            return IsDiagnostic(name, scalar.Value) ? scalar : new ScalarValue(Redacted);
        }

        /// <summary>
        /// True when the value's TYPE cannot express user content.
        /// </summary>
        /// <remarks>
        /// An allowlist of types rather than a denylist of names, so a property nobody thought
        /// about is withheld instead of forwarded. Note what is absent and why:
        /// <c>string</c> is the shape all user content in this app takes; <c>Uri</c> and
        /// <c>char[]</c>/<c>byte[]</c> are scalars to Serilog and can hold anything; and an
        /// unrecognised object is a scalar too, rendered through <c>ToString()</c>, which is how
        /// a whole model would arrive here without a <c>@</c> in the template.
        /// </remarks>
        private static bool IsDiagnostic(string name, object? value) =>
            value switch
            {
                null => true,
                bool or byte or sbyte or short or ushort or int or uint or long or ulong => true,
                float or double or decimal => true,
                DateTime or DateTimeOffset or TimeSpan or Guid => true,
                Enum => true,
                string => AppAuthoredStringProperties.Contains(name),
                _ => false,
            };
    }

    /// <summary>
    /// Registers the Sentry sink behind <see cref="SentryRedactingSink"/>.
    /// </summary>
    public static class SentryRedactingSinkExtensions
    {
        /// <summary>
        /// Writes to Sentry with every non-diagnostic property value redacted.
        /// </summary>
        /// <remarks>
        /// The one place the Sentry sink is configured. Tests call this rather than rebuilding
        /// the configuration, so a change to the levels here cannot leave a test asserting
        /// against wiring the app no longer uses.
        /// </remarks>
        public static LoggerConfiguration RedactedSentry(this LoggerSinkConfiguration sinkConfiguration)
        {
            // LoggerSinkConfiguration.Wrap is a static method, not an extension — the class is not
            // static, so it cannot host one. Calling it as sinkConfiguration.Wrap(…) therefore
            // does not mean what it looks like, and types the configuration lambda's parameter as
            // a sink. Naming the type is the only unambiguous form.
            ILogEventSink redactedSentry = LoggerSinkConfiguration.Wrap(
                inner => new SentryRedactingSink(inner),
                wrapped => wrapped.Sentry(o =>
                {
                    // UseSentry in MauiProgram owns the SDK lifecycle; this sink forwards to the
                    // hub it already created.
                    o.InitializeSdk = false;

                    // This app's error policy catches everything and logs it (silent catch is
                    // banned), so without an Error-level event only truly unhandled crashes would
                    // ever reach Sentry — handled-and-logged errors, i.e. nearly all of them,
                    // would be invisible.
                    o.MinimumEventLevel = LogEventLevel.Error;
                    o.MinimumBreadcrumbLevel = LogEventLevel.Information;
                }));

            return sinkConfiguration.Sink(redactedSentry);
        }
    }
}
