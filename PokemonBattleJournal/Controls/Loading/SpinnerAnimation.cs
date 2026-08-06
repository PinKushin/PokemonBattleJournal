namespace PokemonBattleJournal.Controls.Loading
{
    /// <summary>
    /// Angle state for the loading spinner: where the arc's leading edge points, and how far the
    /// Pokéball has turned on its own axis.
    /// </summary>
    /// <remarks>
    /// Deliberately a plain class with no MAUI dependency, so the timing behaviour is unit
    /// testable — drawing cannot be meaningfully asserted, but everything deciding *what* to
    /// draw can be.
    ///
    /// Advances by elapsed time rather than per tick. A frame-counting spinner runs at whatever
    /// rate the device happens to tick at, so the same animation is visibly faster on a fast
    /// device and stutters when the UI thread is busy.
    /// </remarks>
    internal sealed class SpinnerAnimation
    {
        private const double FullTurn = 360.0;

        /// <summary>
        /// How much faster the ball turns on its own axis than it travels around the ring.
        /// </summary>
        /// <remarks>
        /// The independent spin is the point of the design, not a flourish: a ball that only
        /// orbits reads as a dot sliding along a track. Anything close to 1 looks accidental,
        /// so the two rates are kept clearly apart.
        /// </remarks>
        private const double BallSpinMultiplier = 2.5;

        private readonly double _degreesPerSecond;

        public SpinnerAnimation(double degreesPerSecond)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(degreesPerSecond, 0);
            _degreesPerSecond = degreesPerSecond;
        }

        /// <summary>Where the arc's leading edge points, 0–360.</summary>
        public double AngleDegrees { get; private set; }

        /// <summary>How far the Pokéball has rotated about its own centre, 0–360.</summary>
        public double BallSpinDegrees { get; private set; }

        /// <summary>
        /// Moves the animation on by the time actually elapsed since the last frame.
        /// </summary>
        public void Advance(TimeSpan elapsed)
        {
            // A backwards clock adjustment mid-animation would otherwise spin the arc in
            // reverse, which reads as a glitch rather than a pause.
            if (elapsed <= TimeSpan.Zero)
            {
                return;
            }

            double delta = _degreesPerSecond * elapsed.TotalSeconds;

            // Wrapped rather than accumulated: left running, an unbounded angle loses floating
            // point precision and the motion visibly stutters.
            AngleDegrees = Wrap(AngleDegrees + delta);
            BallSpinDegrees = Wrap(BallSpinDegrees + (delta * BallSpinMultiplier));
        }

        public void Reset()
        {
            AngleDegrees = 0;
            BallSpinDegrees = 0;
        }

        private static double Wrap(double degrees)
        {
            double wrapped = degrees % FullTurn;
            return wrapped < 0 ? wrapped + FullTurn : wrapped;
        }
    }
}
