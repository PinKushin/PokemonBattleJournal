using PokemonBattleJournal.Controls.Loading;

namespace PokemonBattleJournal.Tests.Controls
{
    /// <summary>
    /// The spinner's angle maths, extracted from the control so it can be tested without a
    /// MAUI runtime.
    /// </summary>
    /// <remarks>
    /// Same approach as <c>ComboBoxPopup.FilterItems</c>: the drawing itself cannot be
    /// meaningfully asserted in a unit test, but everything that decides *what* gets drawn can
    /// be, so it lives in a plain class the control merely renders.
    ///
    /// Advancing by elapsed time rather than per tick matters — a frame-count spinner runs at
    /// whatever speed the device happens to tick at, so the same animation is visibly faster on
    /// a fast device. These tests pin the rate to wall-clock.
    /// </remarks>
    public class SpinnerAnimationTests
    {
        [Test]
        public void Advance_QuarterOfTheRotationPeriod_TurnsNinetyDegrees()
        {
            SpinnerAnimation animation = new(degreesPerSecond: 360);

            animation.Advance(TimeSpan.FromMilliseconds(250));

            animation.AngleDegrees.ShouldBe(90, tolerance: 0.001);
        }

        [Test]
        public void Advance_RateIsIndependentOfTickFrequency()
        {
            // One second of animation must land in the same place whether it arrived as four
            // coarse ticks or forty fine ones.
            SpinnerAnimation coarse = new(degreesPerSecond: 180);
            SpinnerAnimation fine = new(degreesPerSecond: 180);

            for (int i = 0; i < 4; i++) coarse.Advance(TimeSpan.FromMilliseconds(250));
            for (int i = 0; i < 40; i++) fine.Advance(TimeSpan.FromMilliseconds(25));

            coarse.AngleDegrees.ShouldBe(fine.AngleDegrees, tolerance: 0.001);
            coarse.AngleDegrees.ShouldBe(180, tolerance: 0.001);
        }

        [Test]
        public void Advance_PastAFullTurn_WrapsWithoutGrowingUnbounded()
        {
            // Left running for hours, an unwrapped angle loses floating point precision and
            // eventually stutters.
            SpinnerAnimation animation = new(degreesPerSecond: 360);

            animation.Advance(TimeSpan.FromSeconds(10.25));

            animation.AngleDegrees.ShouldBeGreaterThanOrEqualTo(0);
            animation.AngleDegrees.ShouldBeLessThan(360);
            animation.AngleDegrees.ShouldBe(90, tolerance: 0.001);
        }

        [Test]
        public void Advance_ZeroElapsed_DoesNotMove()
        {
            SpinnerAnimation animation = new(degreesPerSecond: 360);
            animation.Advance(TimeSpan.FromMilliseconds(100));
            double before = animation.AngleDegrees;

            animation.Advance(TimeSpan.Zero);

            animation.AngleDegrees.ShouldBe(before);
        }

        [Test]
        public void Advance_NegativeElapsed_Ignored()
        {
            // A system clock adjustment mid-animation must not spin the arc backwards.
            SpinnerAnimation animation = new(degreesPerSecond: 360);
            animation.Advance(TimeSpan.FromMilliseconds(100));
            double before = animation.AngleDegrees;

            animation.Advance(TimeSpan.FromMilliseconds(-500));

            animation.AngleDegrees.ShouldBe(before);
        }

        [Test]
        public void Reset_ReturnsToTheStartingAngle()
        {
            SpinnerAnimation animation = new(degreesPerSecond: 360);
            animation.Advance(TimeSpan.FromMilliseconds(400));

            animation.Reset();

            animation.AngleDegrees.ShouldBe(0);
        }

        /// <summary>
        /// The Pokéball spins on its own axis independently of its orbit, so the two rates must
        /// be separable — that is the whole visual idea, not a detail.
        /// </summary>
        [Test]
        public void BallSpinAngle_TurnsFasterThanTheOrbit()
        {
            // Measured over a span short enough that neither value wraps. Comparing wrapped
            // angles would prove nothing: at 180 deg/s for a second the ball has turned 450
            // degrees, which wraps to 90 and reads as *slower* than the 180-degree orbit.
            SpinnerAnimation animation = new(degreesPerSecond: 100);

            animation.Advance(TimeSpan.FromSeconds(1));

            animation.AngleDegrees.ShouldBe(100, tolerance: 0.001);
            animation.BallSpinDegrees.ShouldBeGreaterThan(animation.AngleDegrees,
                "the ball must visibly rotate on its own axis, not just ride the arc");
            animation.BallSpinDegrees.ShouldBeLessThan(360,
                "guard the premise — this assertion is only meaningful before the ball wraps");
        }

        [Test]
        public void BallSpinAngle_AlsoWraps()
        {
            SpinnerAnimation animation = new(degreesPerSecond: 360);

            animation.Advance(TimeSpan.FromSeconds(30));

            animation.BallSpinDegrees.ShouldBeGreaterThanOrEqualTo(0);
            animation.BallSpinDegrees.ShouldBeLessThan(360);
        }

        [Test]
        public void Constructor_NonPositiveRate_Throws()
        {
            Should.Throw<ArgumentOutOfRangeException>(() => new SpinnerAnimation(degreesPerSecond: 0));
            Should.Throw<ArgumentOutOfRangeException>(() => new SpinnerAnimation(degreesPerSecond: -90));
        }
    }
}
