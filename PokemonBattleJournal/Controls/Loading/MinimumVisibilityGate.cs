namespace PokemonBattleJournal.Controls.Loading
{
    /// <summary>
    /// Turns a raw <c>IsBusy</c> flag into a visibility signal that stays on screen long enough
    /// to be seen.
    /// </summary>
    /// <remarks>
    /// Local database work finishes in tens of milliseconds. Bound directly, a spinner would
    /// flash on and off faster than the eye resolves, which reads as a rendering glitch rather
    /// than feedback — and it makes the indicator impossible for a UI test to catch, because the
    /// test races the operation and usually loses.
    ///
    /// The floor deliberately does not become a ceiling: work that outlasts the minimum keeps
    /// the spinner up and it disappears the moment that work finishes.
    ///
    /// Time is a parameter rather than something this reads from the clock, so the behaviour is
    /// testable without sleeping — which the project's own rules ban in tests.
    /// </remarks>
    internal sealed class MinimumVisibilityGate
    {
        private readonly TimeSpan _minimumVisible;
        private DateTime? _shownAt;
        private bool _wasBusy;

        public MinimumVisibilityGate(TimeSpan minimumVisible)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(minimumVisible, TimeSpan.Zero);
            _minimumVisible = minimumVisible;
        }

        public bool IsVisible { get; private set; }

        /// <summary>
        /// Recomputes visibility from the current busy state and the current time. Safe to call
        /// on every animation tick as well as on every change of the busy flag.
        /// </summary>
        public void Update(bool isBusy, DateTime now)
        {
            bool startedWorking = isBusy && !_wasBusy;
            _wasBusy = isBusy;

            if (isBusy)
            {
                // Restart on the rising edge of *busy*, not on becoming visible. A second
                // operation that begins while the spinner is still showing must get its own
                // full window — otherwise it inherits the first operation's expiry and can
                // vanish while work is still running. Keying off the rising edge also stops a
                // long-running operation from continuously pushing its own deadline back.
                if (startedWorking)
                {
                    _shownAt = now;
                }

                IsVisible = true;
                return;
            }

            if (!IsVisible)
            {
                return;
            }

            if (_shownAt is null || now - _shownAt.Value >= _minimumVisible)
            {
                IsVisible = false;
                _shownAt = null;
            }
        }
    }
}
