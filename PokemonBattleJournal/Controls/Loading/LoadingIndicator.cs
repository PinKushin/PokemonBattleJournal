namespace PokemonBattleJournal.Controls.Loading
{
    /// <summary>
    /// Pokéball loading spinner. Bind <see cref="IsBusy"/> to any of the view models'
    /// <c>IsBusy*</c> gates.
    /// </summary>
    /// <remarks>
    /// Ticks on the dispatcher rather than using MAUI's animation system so the redraw rate is
    /// explicit and the control can stop cleanly when it is not visible — an animation left
    /// running behind a hidden view is invisible battery drain, and on Android it also keeps the
    /// UI thread busy, which is exactly what made UI automation crawl before
    /// (see project_readjournal_android_slow).
    ///
    /// The control does not hide itself with <c>IsVisible</c>; it stops drawing and reports
    /// <see cref="IsShowing"/>. Callers bind their own container's visibility, so a UI test can
    /// look for the container by AutomationId.
    /// </remarks>
    public class LoadingIndicator : GraphicsView
    {
        /// <summary>Redraw interval. ~30fps is smooth for a spinner and half the work of 60.</summary>
        private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

        private static readonly TimeSpan DefaultMinimumVisible = TimeSpan.FromMilliseconds(500);

        private readonly PokeballSpinnerDrawable _spinner = new();
        private readonly SpinnerAnimation _animation = new(degreesPerSecond: 200);
        private MinimumVisibilityGate _gate = new(DefaultMinimumVisible);
        private IDispatcherTimer? _timer;
        private DateTime _lastTick;

        public LoadingIndicator()
        {
            Drawable = _spinner;
            HeightRequest = 48;
            WidthRequest = 48;
        }

        public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
            nameof(IsBusy), typeof(bool), typeof(LoadingIndicator), false,
            propertyChanged: (b, _, _) => ((LoadingIndicator)b).OnBusyChanged());

        /// <summary>The work signal. Bind this to a view model <c>IsBusy*</c> gate.</summary>
        public bool IsBusy
        {
            get => (bool)GetValue(IsBusyProperty);
            set => SetValue(IsBusyProperty, value);
        }

        public static readonly BindableProperty IsShowingProperty = BindableProperty.Create(
            nameof(IsShowing), typeof(bool), typeof(LoadingIndicator), false,
            defaultBindingMode: BindingMode.OneWayToSource);

        /// <summary>
        /// True while the spinner should be on screen. Tracks <see cref="IsBusy"/> but stays
        /// true for at least <see cref="MinimumVisibleDuration"/>, so quick work does not
        /// produce a flash the eye cannot resolve and a UI test cannot catch.
        /// </summary>
        public bool IsShowing
        {
            get => (bool)GetValue(IsShowingProperty);
            private set => SetValue(IsShowingProperty, value);
        }

        public static readonly BindableProperty MinimumVisibleDurationProperty = BindableProperty.Create(
            nameof(MinimumVisibleDuration), typeof(TimeSpan), typeof(LoadingIndicator), DefaultMinimumVisible,
            propertyChanged: (b, _, n) => ((LoadingIndicator)b)._gate = new MinimumVisibilityGate((TimeSpan)n));

        /// <summary>How long the spinner stays up once shown. <see cref="TimeSpan.Zero"/> tracks <see cref="IsBusy"/> exactly.</summary>
        public TimeSpan MinimumVisibleDuration
        {
            get => (TimeSpan)GetValue(MinimumVisibleDurationProperty);
            set => SetValue(MinimumVisibleDurationProperty, value);
        }

        public static readonly BindableProperty ArcColorProperty = BindableProperty.Create(
            nameof(ArcColor), typeof(Color), typeof(LoadingIndicator), Colors.Red,
            propertyChanged: (b, _, n) => ((LoadingIndicator)b)._spinner.ArcColor = (Color)n);

        /// <summary>Arc and Pokéball-top colour. Red or PokeBlue — both hold up in light and dark mode.</summary>
        public Color ArcColor
        {
            get => (Color)GetValue(ArcColorProperty);
            set => SetValue(ArcColorProperty, value);
        }

        public static readonly BindableProperty ReduceMotionProperty = BindableProperty.Create(
            nameof(ReduceMotion), typeof(bool), typeof(LoadingIndicator), false,
            propertyChanged: (b, _, _) => ((LoadingIndicator)b).OnBusyChanged());

        /// <summary>
        /// When true the arc is drawn in a fixed position instead of animating. Callers pair this
        /// with a static "Loading…" label for users who have asked the system to reduce motion.
        /// </summary>
        public bool ReduceMotion
        {
            get => (bool)GetValue(ReduceMotionProperty);
            set => SetValue(ReduceMotionProperty, value);
        }

        private void OnBusyChanged()
        {
            Tick();

            if (IsShowing && !ReduceMotion)
            {
                StartTimer();
            }
            else if (!IsShowing)
            {
                StopTimer();
            }
        }

        private void StartTimer()
        {
            if (_timer is not null)
            {
                return;
            }

            _lastTick = DateTime.UtcNow;
            _timer = Dispatcher.CreateTimer();
            _timer.Interval = FrameInterval;
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
        }

        private void StopTimer()
        {
            _timer?.Stop();
            _timer = null;
            _animation.Reset();
        }

        private void Tick()
        {
            DateTime now = DateTime.UtcNow;

            _gate.Update(IsBusy, now);
            IsShowing = _gate.IsVisible;

            if (!IsShowing)
            {
                StopTimer();
                return;
            }

            if (!ReduceMotion)
            {
                _animation.Advance(now - _lastTick);
                _spinner.AngleDegrees = _animation.AngleDegrees;
                _spinner.BallSpinDegrees = _animation.BallSpinDegrees;
            }

            _lastTick = now;
            Invalidate();
        }
    }
}
