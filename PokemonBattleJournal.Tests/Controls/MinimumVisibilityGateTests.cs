using PokemonBattleJournal.Controls.Loading;

namespace PokemonBattleJournal.Tests.Controls
{
    /// <summary>
    /// Keeps the spinner on screen long enough to be seen, and long enough to be tested.
    /// </summary>
    /// <remarks>
    /// Local database work finishes in tens of milliseconds, so a spinner bound straight to an
    /// <c>IsBusy</c> flag flashes on and off faster than the eye resolves. That reads as a
    /// glitch rather than feedback, and it makes the indicator impossible for a UI test to
    /// catch — the test races the operation and usually loses.
    ///
    /// Time is passed in rather than read from the clock so these tests are deterministic and
    /// need no sleeping, which the project bans in tests anyway.
    /// </remarks>
    public class MinimumVisibilityGateTests
    {
        private static readonly DateTime T0 = new(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);
        private static MinimumVisibilityGate Gate() => new(TimeSpan.FromMilliseconds(500));

        [Test]
        public void SetBusy_True_ShowsImmediately()
        {
            MinimumVisibilityGate gate = Gate();

            gate.Update(isBusy: true, now: T0);

            gate.IsVisible.ShouldBeTrue("delaying the appearance would make quick work show nothing at all");
        }

        [Test]
        public void WorkFinishesInstantly_StaysVisibleForTheMinimum()
        {
            MinimumVisibilityGate gate = Gate();
            gate.Update(isBusy: true, now: T0);

            gate.Update(isBusy: false, now: T0.AddMilliseconds(20));

            gate.IsVisible.ShouldBeTrue("20ms of work must not produce a 20ms flash");
        }

        [Test]
        public void AfterTheMinimumElapses_Hides()
        {
            MinimumVisibilityGate gate = Gate();
            gate.Update(isBusy: true, now: T0);
            gate.Update(isBusy: false, now: T0.AddMilliseconds(20));

            gate.Update(isBusy: false, now: T0.AddMilliseconds(500));

            gate.IsVisible.ShouldBeFalse();
        }

        [Test]
        public void WorkOutlastingTheMinimum_HidesAsSoonAsItFinishes()
        {
            // The floor must not become a ceiling: slow work keeps the spinner up, and it
            // disappears the moment the work is done rather than lingering.
            MinimumVisibilityGate gate = Gate();
            gate.Update(isBusy: true, now: T0);

            gate.Update(isBusy: true, now: T0.AddSeconds(3));
            gate.IsVisible.ShouldBeTrue();

            gate.Update(isBusy: false, now: T0.AddSeconds(3).AddMilliseconds(1));
            gate.IsVisible.ShouldBeFalse();
        }

        [Test]
        public void RestartingWhileStillVisible_ExtendsFromTheNewStart()
        {
            // Two quick operations back to back should read as one continuous spinner, not a
            // blink between them.
            MinimumVisibilityGate gate = Gate();
            gate.Update(isBusy: true, now: T0);
            gate.Update(isBusy: false, now: T0.AddMilliseconds(50));

            gate.Update(isBusy: true, now: T0.AddMilliseconds(100));
            gate.Update(isBusy: false, now: T0.AddMilliseconds(120));

            gate.IsVisible.ShouldBeTrue("the second operation restarts the minimum window");
            gate.Update(isBusy: false, now: T0.AddMilliseconds(599));
            gate.IsVisible.ShouldBeTrue();
            gate.Update(isBusy: false, now: T0.AddMilliseconds(600));
            gate.IsVisible.ShouldBeFalse();
        }

        [Test]
        public void NeverBusy_NeverVisible()
        {
            MinimumVisibilityGate gate = Gate();

            gate.Update(isBusy: false, now: T0);
            gate.Update(isBusy: false, now: T0.AddSeconds(10));

            gate.IsVisible.ShouldBeFalse();
        }

        [Test]
        public void ZeroMinimum_TracksBusyExactly()
        {
            // Opting out must be possible — a caller that wants raw behaviour should get it.
            MinimumVisibilityGate gate = new(TimeSpan.Zero);

            gate.Update(isBusy: true, now: T0);
            gate.IsVisible.ShouldBeTrue();

            gate.Update(isBusy: false, now: T0);
            gate.IsVisible.ShouldBeFalse();
        }

        [Test]
        public void NegativeMinimum_Throws()
        {
            Should.Throw<ArgumentOutOfRangeException>(
                () => new MinimumVisibilityGate(TimeSpan.FromMilliseconds(-1)));
        }
    }
}
