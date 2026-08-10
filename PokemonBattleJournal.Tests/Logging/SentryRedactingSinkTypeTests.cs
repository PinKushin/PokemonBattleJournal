using System.Diagnostics;
using PokemonBattleJournal.Logging;
using Serilog.Events;
using Serilog.Parsing;

namespace PokemonBattleJournal.Tests.Logging
{
    /// <summary>
    /// Which property TYPES the redactor lets through, one type at a time.
    /// </summary>
    /// <remarks>
    /// The sink's whole guarantee is "a value is forwarded only if its type cannot express user
    /// content", and the allowlist names a dozen types while
    /// <see cref="SentryPayloadPiiTests"/> only ever exercises int, double and string. Mutation
    /// testing found the rest unguarded: deleting `byte or sbyte or short or ...` from the
    /// pattern, or `DateTime or DateTimeOffset or TimeSpan or Guid`, changed nothing any test
    /// could see.
    ///
    /// A type dropped from that list is not a crash — it silently becomes `[redacted]`, so a
    /// crash report quietly loses a duration or an id and still looks fine. Tested against
    /// <c>Redact</c> directly rather than through the Sentry SDK, because the question is about
    /// one function and a harness would only make it slower to enumerate.
    /// </remarks>
    [TestFixture]
    public class SentryRedactingSinkTypeTests
    {
        private static LogEvent EventWith(string name, object? value)
        {
            MessageTemplate template = new MessageTemplateParser().Parse("value is {" + name + "}");
            return new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                exception: null,
                template,
                [new LogEventProperty(name, new ScalarValue(value))]);
        }

        private static object? ForwardedValue(string name, object? value)
        {
            LogEvent redacted = SentryRedactingSink.Redact(EventWith(name, value));
            return ((ScalarValue)redacted.Properties[name]).Value;
        }

        private static void ShouldSurvive(object value) =>
            ForwardedValue("Diagnostic", value)
                .ShouldBe(value, $"{value.GetType().Name} cannot express user content and must reach Sentry");

        [Test] public void Bool_Survives() => ShouldSurvive(true);
        [Test] public void Byte_Survives() => ShouldSurvive((byte)7);
        [Test] public void SByte_Survives() => ShouldSurvive((sbyte)-7);
        [Test] public void Short_Survives() => ShouldSurvive((short)-300);
        [Test] public void UShort_Survives() => ShouldSurvive((ushort)300);
        [Test] public void Int_Survives() => ShouldSurvive(42);
        [Test] public void UInt_Survives() => ShouldSurvive(42u);
        [Test] public void Long_Survives() => ShouldSurvive(42L);
        [Test] public void ULong_Survives() => ShouldSurvive(42ul);
        [Test] public void Float_Survives() => ShouldSurvive(1.5f);
        [Test] public void Double_Survives() => ShouldSurvive(1.5d);
        [Test] public void Decimal_Survives() => ShouldSurvive(1.5m);
        [Test] public void DateTime_Survives() => ShouldSurvive(new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc));
        [Test] public void DateTimeOffset_Survives() => ShouldSurvive(DateTimeOffset.UnixEpoch);
        [Test] public void TimeSpan_Survives() => ShouldSurvive(TimeSpan.FromMinutes(20));
        [Test] public void Guid_Survives() => ShouldSurvive(Guid.Empty);
        [Test] public void Enum_Survives() => ShouldSurvive(MatchResult.Win);

        [Test]
        public void Null_Survives() =>
            // Nothing to leak, and redacting it would turn "no value" into the word "[redacted]".
            ForwardedValue("Diagnostic", null).ShouldBeNull();

        // ---------------- withheld ----------------

        [Test]
        public void AnArbitraryString_IsWithheld() =>
            ForwardedValue("TrainerName", "Ash Ketchum").ShouldBe(SentryRedactingSink.Redacted);

        [Test]
        public void Char_IsWithheld() =>
            // Absent from the allowlist on purpose: a char is a fragment of user text.
            ForwardedValue("Initial", 'A').ShouldBe(SentryRedactingSink.Redacted);

        [Test]
        public void AUri_IsWithheld() =>
            // Serilog treats Uri as a scalar, and a url carries paths, hosts and query strings.
            ForwardedValue("Endpoint", new Uri("https://example.invalid/ash/matches"))
                .ShouldBe(SentryRedactingSink.Redacted);

        [Test]
        public void AByteArray_IsWithheld() =>
            ForwardedValue("Blob", new byte[] { 1, 2, 3 }).ShouldBe(SentryRedactingSink.Redacted);

        [Test]
        public void AnUnrecognisedObject_IsWithheld() =>
            // The shape that arrives when a whole model is logged without a @ in the template:
            // Serilog stores it as a scalar and renders it via ToString().
            ForwardedValue("Entry", new MatchEntry { TrainerId = 1 })
                .ShouldBe(SentryRedactingSink.Redacted);

        [Test]
        public void AnAllowlistedName_LetsItsStringThrough() =>
            // ValidationMessage and Problem are written by this app, never by a person.
            ForwardedValue("ValidationMessage", "Select a deck").ShouldBe("Select a deck");

        [Test]
        public void TheAllowlistIsCaseSensitive() =>
            // "problem" is not "Problem". A near-miss must fail closed rather than leak.
            ForwardedValue("problem", "some text").ShouldBe(SentryRedactingSink.Redacted);

        [Test]
        public void TheMessageTemplateIsAlwaysForwarded()
        {
            // Withholding the template too would leave a breadcrumb saying nothing at all.
            LogEvent redacted = SentryRedactingSink.Redact(EventWith("TrainerName", "Ash"));

            redacted.MessageTemplate.Text.ShouldContain("value is");
        }

        // Trace and span ids must survive redaction, because they are what ties a log line to
        // the transaction it belongs to. Losing them does not break anything visibly — it just
        // produces crash reports that cannot be correlated with a trace.
        //
        // Both `?? default` expressions survived mutation to a bare `default`, and EventWith is
        // why: it builds events with no ids at all, so `null ?? default` and `default` are the
        // same value. Correct and broken agree on that input. The fix is an event that actually
        // carries ids, not a stronger assertion.
        [Test]
        public void TheTraceAndSpanIdsAreForwarded()
        {
            ActivityTraceId traceId = ActivityTraceId.CreateRandom();
            ActivitySpanId spanId = ActivitySpanId.CreateRandom();
            MessageTemplate template = new MessageTemplateParser().Parse("value is {Diagnostic}");
            LogEvent original = new(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                exception: null,
                template,
                [new LogEventProperty("Diagnostic", new ScalarValue(1))],
                traceId,
                spanId);

            LogEvent redacted = SentryRedactingSink.Redact(original);

            redacted.TraceId.ShouldBe(traceId);
            redacted.SpanId.ShouldBe(spanId);
        }

        [Test]
        public void TheOriginalEventIsNotMutated()
        {
            // Serilog hands the same instance to every sink, so editing in place would redact the
            // local file log too, with the outcome depending on sink order.
            LogEvent original = EventWith("TrainerName", "Ash");

            _ = SentryRedactingSink.Redact(original);

            ((ScalarValue)original.Properties["TrainerName"]).Value.ShouldBe("Ash");
        }
    }
}
