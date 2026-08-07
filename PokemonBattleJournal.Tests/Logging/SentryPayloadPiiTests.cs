using PokemonBattleJournal.Logging;
using Sentry;
using Serilog;
using System.Text;
using System.Text.Json;

namespace PokemonBattleJournal.Tests.Logging
{
    /// <summary>
    /// Pins the boundary between what this app writes to a log and what leaves the device.
    /// </summary>
    /// <remarks>
    /// Sentry's own defaults are not the problem — <c>SendDefaultPii</c>,
    /// <c>IncludeTextInBreadcrumbs</c>, <c>IncludeTitleInBreadcrumbs</c> and
    /// <c>AttachScreenshot</c> are all false and none is overridden in
    /// <c>MauiProgram</c>. What ships user content is the Serilog Sentry sink: it is
    /// configured with <c>MinimumBreadcrumbLevel = Information</c>, so every
    /// <c>LogInformation</c> becomes a breadcrumb carrying the RENDERED message, and
    /// <c>MinimumEventLevel = Error</c>, so every <c>LogError</c> becomes an event that
    /// carries those breadcrumbs plus the log properties as extras.
    ///
    /// Each test replays log statements in the shape the app actually writes them and
    /// asserts the value never appears anywhere in the serialized payload. Serializing the
    /// whole event rather than inspecting named fields is deliberate: a leak that lands in
    /// a field this test forgot to check is exactly the failure mode being guarded against.
    ///
    /// These are contract tests for the sink filter, not for any one call site. They must
    /// keep passing when someone adds a log line years from now — that is the point.
    /// </remarks>
    [TestFixture]
    [NonParallelizable]
    public class SentryPayloadPiiTests
    {
        /// <summary>A trainer name is whatever the person typed; assume it identifies them.</summary>
        private const string TrainerName = "Ash Ketchum";

        /// <summary>A save-dialog path carries the OS account name, which is very often a real name.</summary>
        private const string ExportPath = @"C:\Users\ashketchum\Documents\pbj-backup-2026-08-07.json";

        /// <summary>Tags are free text, so their contents are unbounded.</summary>
        private const string TagText = "lost to the guy from the shop on 4th";

        private readonly List<SentryEvent> _captured = [];
        private IDisposable? _sdk;
        private Serilog.Core.Logger? _log;

        [SetUp]
        public void SetUp()
        {
            _captured.Clear();

            _sdk = SentrySdk.Init(o =>
            {
                // Well-formed but unroutable. Nothing is transmitted regardless: BeforeSend
                // returns null, which drops the event after it has been fully built — so what
                // the test inspects is exactly what the transport would have sent.
                o.Dsn = "https://0123456789abcdef0123456789abcdef@o0.ingest.sentry.io/0";
                o.AutoSessionTracking = false;
                o.IsGlobalModeEnabled = true;
                o.Debug = false;
                o.SetBeforeSend((evt, _) =>
                {
                    _captured.Add(evt);
                    return null;
                });
            });

            // The production wiring itself, not a copy of it. If MauiProgram's Sentry sink is
            // reconfigured, these tests move with it instead of quietly guarding a dead shape.
            _log = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.RedactedSentry()
                .CreateLogger();
        }

        [TearDown]
        public void TearDown()
        {
            _log?.Dispose();
            _sdk?.Dispose();
        }

        [Test]
        public void ExportPath_LoggedAtInformation_DoesNotReachSentry()
        {
            string payload = CapturePayload(log =>
                log.Information("Exported to {Path}", ExportPath));

            payload.ShouldNotContain("ashketchum");
            payload.ShouldNotContain(ExportPath);

            // Deliberately not asserting on the "C:\Users" prefix alone. Stack frames carry the
            // absolute source paths baked into the PDB at build time, so that substring is in
            // every payload — but those are the DEVELOPER's paths on the build machine, not
            // anything read from the device the app is running on.
        }

        [Test]
        public void ExportPath_LoggedOnAnErrorEvent_DoesNotReachSentry()
        {
            string payload = CapturePayload(log =>
                log.Error(new InvalidOperationException("disk full"),
                    "Error during export of {File}", $"pbj-backup-{TrainerName}.json"));

            payload.ShouldNotContain(TrainerName);
        }

        [Test]
        public void TrainerName_LoggedAtInformation_DoesNotReachSentry()
        {
            string payload = CapturePayload(log =>
                log.Information("Trainer saved: {TrainerName}", TrainerName));

            payload.ShouldNotContain(TrainerName);
        }

        [Test]
        public void TrainerName_LoggedOnAnErrorEvent_DoesNotReachSentry()
        {
            string payload = CapturePayload(log =>
                log.Error(new InvalidOperationException("constraint failed"),
                    "Error saving Trainer: {TrainerName}", TrainerName));

            payload.ShouldNotContain(TrainerName);
        }

        [Test]
        public void TagText_LoggedAtWarning_DoesNotReachSentry()
        {
            string payload = CapturePayload(log =>
                log.Warning("Tag not saved: no active trainer (tag was {TagInput})", TagText));

            payload.ShouldNotContain(TagText);
        }

        [Test]
        public void DestructuredMatchEntry_DoesNotReachSentry()
        {
            MatchEntry entry = new()
            {
                TrainerId = 1,
                PlayingId = 2,
                Playing = new Archetype { Id = 2, Name = "Ash's Pikachu Deck" },
                AgainstId = 3,
                Against = new Archetype { Id = 3, Name = "Gary's Eevee Deck" },
            };

            string payload = CapturePayload(log =>
                log.Information("Inserting match entry: {@MatchEntry}", entry));

            payload.ShouldNotContain("Ash's Pikachu Deck");
            payload.ShouldNotContain("Gary's Eevee Deck");
        }

        [Test]
        public void CountsAndIdentifiers_StillReachSentry()
        {
            // The fix must not work by making the sink useless. A crash report with no counts,
            // ids or timings diagnoses nothing, and a filter that strips everything would pass
            // every other test in this fixture.
            string payload = CapturePayload(log =>
                log.Information("Loading matches for trainer: {TrainerId} ({Count} matches, {ElapsedMs}ms)",
                    4815u, 162, 342.5));

            payload.ShouldContain("4815");
            payload.ShouldContain("162");
            payload.ShouldContain("342.5");
        }

        [Test]
        public void ImportSkip_ForwardsThisAppsReason_ButNotTheEntry()
        {
            // The exact shape TrainerHillImportService logs, and the one case where a string is
            // allowlisted. Both halves are asserted together: if the allowlist name were ever
            // misspelled the reason would silently vanish from every crash report, and a test
            // that only checked the redacted half would still pass.
            string payload = CapturePayload(log =>
                log.Warning("Skipped entry {Playing} vs {Against}: {Problem}",
                    "Ash's Pikachu Deck", "Gary's Eevee Deck", "result is not one of win/loss/tie"));

            payload.ShouldContain("result is not one of win/loss/tie");
            payload.ShouldNotContain("Ash's Pikachu Deck");
            payload.ShouldNotContain("Gary's Eevee Deck");
        }

        [Test]
        public void RedactedBreadcrumb_StillSaysWhatHappened()
        {
            // The other half of the contract. Withholding the value is only correct if the
            // breadcrumb still tells you which code path ran — a crash report full of
            // "[redacted]" and nothing else would satisfy every test above and be worthless.
            string payload = CapturePayload(log =>
                log.Information("Trainer saved: {TrainerName}", TrainerName));

            payload.ShouldContain("Trainer saved:");
            payload.ShouldContain(SentryRedactingSink.Redacted);
        }

        /// <summary>
        /// Runs the given log calls, then forces an error so the breadcrumbs they produced are
        /// actually sent, and returns the serialized event.
        /// </summary>
        private string CapturePayload(Action<Serilog.ILogger> logCalls)
        {
            logCalls(_log!);

            // Breadcrumbs only leave the device attached to an event. The forcing message is
            // deliberately free of user content, so anything found in the payload came from
            // the calls above.
            _log!.Error(new InvalidOperationException("forced"), "Operation failed");

            _captured.ShouldNotBeEmpty("the Serilog sink did not produce a Sentry event");
            return Flatten(_captured[^1]);
        }

        /// <summary>
        /// Serializes the event the way the transport would, then returns every property name
        /// and every leaf value as DECODED text.
        /// </summary>
        /// <remarks>
        /// Searching the raw JSON bytes does not work, and failed silently when this fixture
        /// was first written: <see cref="Utf8JsonWriter"/> defaults to
        /// <c>JavaScriptEncoder.Default</c>, which escapes an apostrophe to <c>'</c>. A
        /// deck called "Ash's Pikachu Deck" was therefore absent from the bytes as a literal
        /// substring while being fully present in the payload, and the test passed on a leak
        /// it was written to catch. Parsing and walking the document asserts on content rather
        /// than on encoding, so no escaping scheme can hide a value from these tests.
        /// </remarks>
        private static string Flatten(SentryEvent evt)
        {
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                evt.WriteTo(writer, null);
            }

            using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
            StringBuilder text = new();
            Walk(document.RootElement, text);
            return text.ToString();
        }

        private static void Walk(JsonElement element, StringBuilder text)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        text.AppendLine(property.Name);
                        Walk(property.Value, text);
                    }

                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement item in element.EnumerateArray())
                    {
                        Walk(item, text);
                    }

                    break;

                default:
                    text.AppendLine(element.ToString());
                    break;
            }
        }
    }
}
