namespace PokemonBattleJournal.Controls.Loading
{
    /// <summary>
    /// Draws the loading spinner: a partial arc fading from solid at its leading edge to
    /// transparent behind it, with a Pokéball riding the head like a comet.
    /// </summary>
    /// <remarks>
    /// The Pokéball is drawn as vector geometry rather than by loading <c>ball_icon.png</c>.
    /// Two reasons: the ball has to rotate about its own centre, which means transforming it
    /// independently of the arc, and loading an image inside a drawable needs platform-specific
    /// <c>IImage</c> handling that would have to be repeated per target. Circles and arcs are
    /// identical everywhere and stay sharp at any size.
    ///
    /// The trail is drawn as a series of short segments with decreasing alpha rather than a
    /// gradient stroke, because <see cref="ICanvas"/> has no cross-platform gradient-along-a-path
    /// primitive.
    /// </remarks>
    internal sealed class PokeballSpinnerDrawable : IDrawable
    {
        /// <summary>Segments in the fading trail. Enough to read as smooth, few enough to stay cheap.</summary>
        private const int TrailSegments = 24;

        /// <summary>How much of the circle the trail covers. Deliberately not a closed ring.</summary>
        private const float TrailSweepDegrees = 240f;

        private const float StrokeThicknessRatio = 0.09f;
        private const float BallRadiusRatio = 0.16f;

        public double AngleDegrees { get; set; }
        public double BallSpinDegrees { get; set; }
        public Color ArcColor { get; set; } = Colors.Red;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            float size = Math.Min(dirtyRect.Width, dirtyRect.Height);
            if (size <= 0)
            {
                return;
            }

            PointF centre = new(dirtyRect.Center.X, dirtyRect.Center.Y);
            float stroke = size * StrokeThicknessRatio;
            float ballRadius = size * BallRadiusRatio;
            // Keep the whole ball inside the bounds — it straddles the arc's path, so the radius
            // has to come off the orbit, not just the stroke.
            float orbitRadius = (size / 2f) - Math.Max(stroke / 2f, ballRadius);
            if (orbitRadius <= 0)
            {
                return;
            }

            DrawTrail(canvas, centre, orbitRadius, stroke);
            DrawBall(canvas, PointOnOrbit(centre, orbitRadius, AngleDegrees), ballRadius);
        }

        private void DrawTrail(ICanvas canvas, PointF centre, float orbitRadius, float stroke)
        {
            canvas.StrokeSize = stroke;
            canvas.StrokeLineCap = LineCap.Round;

            float step = TrailSweepDegrees / TrailSegments;

            for (int i = 0; i < TrailSegments; i++)
            {
                // Segment 0 sits at the head, immediately behind the ball, and is fully opaque;
                // each one further back is fainter. Squared so the tail fades out early and the
                // head stays crisp, which is what gives the "chasing itself" look.
                float fade = 1f - ((float)i / TrailSegments);
                canvas.StrokeColor = ArcColor.WithAlpha(fade * fade);

                double from = AngleDegrees - (i * step);
                double to = from - step;

                PointF a = PointOnOrbit(centre, orbitRadius, from);
                PointF b = PointOnOrbit(centre, orbitRadius, to);
                canvas.DrawLine(a, b);
            }
        }

        private void DrawBall(ICanvas canvas, PointF at, float radius)
        {
            canvas.SaveState();
            // Rotate about the ball's own centre so it spins on its axis while it orbits.
            canvas.Translate(at.X, at.Y);
            canvas.Rotate((float)BallSpinDegrees);

            RectF bounds = new(-radius, -radius, radius * 2, radius * 2);

            // Bottom half white, top half red, split by a band with a centre button — the
            // Pokéball read at a glance. Halves are drawn as clipped circles so the outline
            // stays a true circle at any size.
            canvas.FillColor = Colors.White;
            canvas.FillEllipse(bounds);

            canvas.SaveState();
            canvas.ClipRectangle(-radius, -radius, radius * 2, radius);
            canvas.FillColor = ArcColor;
            canvas.FillEllipse(bounds);
            canvas.RestoreState();

            float band = Math.Max(1f, radius * 0.22f);
            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = band;
            canvas.DrawLine(-radius, 0, radius, 0);

            canvas.StrokeSize = Math.Max(1f, radius * 0.14f);
            canvas.DrawEllipse(bounds);

            float button = radius * 0.34f;
            canvas.FillColor = Colors.White;
            canvas.FillEllipse(-button, -button, button * 2, button * 2);
            canvas.StrokeSize = Math.Max(1f, radius * 0.12f);
            canvas.DrawEllipse(-button, -button, button * 2, button * 2);

            canvas.RestoreState();
        }

        /// <summary>
        /// Position on the orbit for an angle in degrees, measured clockwise from 12 o'clock so
        /// the motion matches how a clock hand reads.
        /// </summary>
        private static PointF PointOnOrbit(PointF centre, float radius, double angleDegrees)
        {
            double radians = (angleDegrees - 90) * Math.PI / 180.0;
            return new PointF(
                centre.X + (float)(radius * Math.Cos(radians)),
                centre.Y + (float)(radius * Math.Sin(radians)));
        }
    }
}
