using PokemonBattleJournal.Interfaces;
using PokemonBattleJournal.Logging;

// Sentry.ISpan is in scope via GlobalUsings and collides with ours. The collision is itself
// worth knowing about: it is why the adapter exists.
using CoreSpan = PokemonBattleJournal.Interfaces.ISpan;

namespace PokemonBattleJournal.Tests.Logging
{
    /// <summary>
    /// The performance monitor's behaviour, and the privacy property that matters most about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Span names and descriptions reach Sentry as free text. <c>SentryRedactingSink</c> does not
    /// see them — it governs Serilog property values only — so tracing opens a second channel out
    /// of the device that the 2026-08-07 audit did not cover.
    /// </para>
    /// <para>
    /// The defence is structural rather than a filter: the interface takes constants and offers
    /// no way to attach a string, so varying detail can only be numeric. These tests pin that
    /// shape, because the shape IS the guarantee. A future overload accepting interpolated text
    /// would silently reopen the channel and nothing else would notice.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class SentryPerformanceMonitorTests
    {
        [Test]
        public void StartSpan_WithSentryUninitialised_StillReturnsAUsableSpan()
        {
            // Tests never initialise the SDK, and neither does a user who has not consented.
            // A monitor that returned null or threw would make every instrumented path a
            // NullReferenceException outside production — the instrumentation would become the
            // outage. Sentry's own no-op span is what makes this safe; assert we surface it.
            SentryPerformanceMonitor monitor = new();

            using CoreSpan span = monitor.StartSpan("test", "no sentry configured");

            span.ShouldNotBeNull();
            Should.NotThrow(() => span.SetMeasurement("count", 3));
            Should.NotThrow(span.SetFailed);
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            // A span finished twice must not throw: instrumented code puts the using inside a
            // try, and an exception path can reach both the using and an explicit finish.
            SentryPerformanceMonitor monitor = new();
            CoreSpan span = monitor.StartSpan("test", "double dispose");

            span.Dispose();

            Should.NotThrow(span.Dispose);
        }

        [Test]
        public void TheInterface_OffersNoWayToAttachAStringToASpan()
        {
            // The privacy guarantee, asserted against the SHAPE of the type rather than against
            // any one call site. Span names and descriptions are free text on the wire and no
            // sink filters them, so the protection is that varying data cannot be a string at
            // all. If someone adds SetTag(string, string) or a description overload taking
            // interpolated text, this fails and they have to justify it deliberately.
            System.Reflection.MethodInfo[] methods = typeof(CoreSpan).GetMethods();

            foreach (System.Reflection.MethodInfo method in methods)
            {
                foreach (System.Reflection.ParameterInfo parameter in method.GetParameters())
                {
                    if (parameter.ParameterType == typeof(string))
                    {
                        // Only a constant NAME may be a string; the VALUE must be numeric.
                        parameter.Name.ShouldBe("name",
                            $"ISpan.{method.Name} takes a string '{parameter.Name}' — span data "
                            + "reaches Sentry unfiltered, so only constant measurement names may "
                            + "be strings. Varying detail must be numeric.");
                    }
                }
            }
        }

        [Test]
        public void StartSpan_TakesExactlyTwoConstantStrings_AndNothingVarying()
        {
            // Same argument one level up: the operation and description are the only free text
            // that leaves, so there must be no third parameter through which a caller could pass
            // a trainer name or a note.
            System.Reflection.MethodInfo start = typeof(IPerformanceMonitor)
                .GetMethod(nameof(IPerformanceMonitor.StartSpan))!;

            System.Reflection.ParameterInfo[] parameters = start.GetParameters();

            parameters.Length.ShouldBe(2);
            parameters[0].Name.ShouldBe("operation");
            parameters[1].Name.ShouldBe("description");
            parameters.ShouldAllBe(p => p.ParameterType == typeof(string));
        }
    }
}
