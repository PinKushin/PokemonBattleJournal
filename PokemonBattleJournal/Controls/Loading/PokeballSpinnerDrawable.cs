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
        /// <summary>
        /// Translucent arcs stacked to build the trail. More layers give a finer gradient and a
        /// smoother taper; each one is a single stroked arc, so the cost is modest.
        /// </summary>
        private const int TrailLayers = 18;

        /// <summary>
        /// Alpha of a single layer. The head sits under every layer, so opacity there is
        /// 1-(1-a)^n — at 18 layers this reaches effectively solid while the lone tail layer
        /// stays faint.
        /// </summary>
        private const float LayerAlpha = 0.16f;

        /// <summary>
        /// Width at the tail as a fraction of the head's, so the trail thins as it recedes.
        /// </summary>
        /// <remarks>
        /// Held well above zero on purpose. A hard taper reads as a wisp that has run out rather
        /// than a trail still travelling, and it fights the near-closed sweep — the tail has to
        /// still look like part of the same ring when it comes back round to the ball.
        /// </remarks>
        private const float TailWidthRatio = 0.45f;

        /// <summary>
        /// How much of the circle the trail covers. Close to a closed ring on purpose — the tail
        /// nearly reaches the ball, so it reads as chasing itself rather than as a short comet.
        /// </summary>
        private const float TrailSweepDegrees = 310f;

        private const float StrokeThicknessRatio = 0.17f;

        /// <summary>
        /// Ball diameter in device-independent pixels — a fixed size, not a fraction of the
        /// control.
        /// </summary>
        /// <remarks>
        /// 28 matches the Pokéball icons elsewhere in the UI (28×28 in <c>ComboBoxControl</c>,
        /// 26×26 in its popup, 24×24 in the archetype rows), so it reads as the same object the
        /// rest of the app uses rather than a different one.
        ///
        /// Fixed rather than proportional on purpose: as a ratio, making the control bigger to
        /// give the trail more room grew the ball by the same factor, so the ring never actually
        /// gained space. Pinning the ball lets the ring breathe as the control grows.
        /// </remarks>
        private const float BallDiameter = 28f;

        /// <summary>Ceiling for the ball on very small controls, so it cannot swallow the ring.</summary>
        private const float MaxBallRadiusRatio = 0.22f;


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
            float ballRadius = Math.Min(BallDiameter / 2f, size * MaxBallRadiusRatio);
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

        /// <summary>
        /// Draws the trail as a stack of translucent arcs, each anchored at the head and each
        /// reaching a little less far back than the last.
        /// </summary>
        /// <remarks>
        /// Built this way to solve two problems at once.
        ///
        /// The gradient is an accumulation rather than a per-piece alpha. Near the head every
        /// layer overlaps, so the colour builds to solid; near the tail only the longest layer
        /// reaches, so it stays faint. Because each layer is a single continuous stroked arc,
        /// there are no joins inside it to composite against.
        ///
        /// That is what the previous approach got wrong. Drawing the trail as many short
        /// segments meant every segment's round cap overlapped its neighbour's, and two
        /// translucent strokes over the same pixels add — so each join rendered as a darker lump
        /// and the whole ring looked scalloped. Rotating, those lumps travelled with it and read
        /// as flicker. More segments could not fix it; the overlap was inherent to the method.
        ///
        /// The taper falls out of the same stack. Layers that reach furthest back are drawn
        /// thinnest, so the visible width is set by the widest layer covering each point, which
        /// grows towards the head.
        /// </remarks>
        private void DrawTrail(ICanvas canvas, PointF centre, float orbitRadius, float stroke)
        {
            canvas.StrokeLineCap = LineCap.Round;

            float diameter = orbitRadius * 2f;
            float left = centre.X - orbitRadius;
            float top = centre.Y - orbitRadius;

            for (int i = 0; i < TrailLayers; i++)
            {
                float t = TrailLayers == 1 ? 1f : (float)i / (TrailLayers - 1);

                // Layer 0 spans the whole sweep and is thinnest; the last layer is a short,
                // full-width cap right behind the ball.
                float reach = TrailSweepDegrees * (1f - (t * (1f - (1f / TrailLayers))));
                canvas.StrokeSize = stroke * (TailWidthRatio + ((1f - TailWidthRatio) * t));
                canvas.StrokeColor = ArcColor.WithAlpha(LayerAlpha);

                // PointOnOrbit measures clockwise from 12 o'clock; DrawArc measures
                // counter-clockwise from 3 o'clock, so the ends swap as well as convert.
                float startAngle = (float)(90 - AngleDegrees);
                float endAngle = (float)(90 - (AngleDegrees - reach));

                canvas.DrawArc(left, top, diameter, diameter, startAngle, endAngle,
                    clockwise: false, closed: false);
            }
        }

        private void DrawBall(ICanvas canvas, PointF at, float radius)
        {
            canvas.SaveState();
            // Rotate about the ball's own centre so it spins on its axis while it orbits.
            canvas.Translate(at.X, at.Y);
            canvas.Rotate((float)BallSpinDegrees);

            RectF bounds = new(-radius, -radius, radius * 2, radius * 2);

            // Bottom half white, top half red, split by a band with a centre button.
            //
            // The red half is a filled arc, NOT a circle drawn under a clip rectangle. The clip
            // approach rendered an all-white ball on WinUI — the clip silently discarded the
            // fill rather than constraining it — so the most recognisable part of a Pokéball
            // was missing. FillArc is a drawing primitive rather than a state operation and
            // behaves the same everywhere.
            //
            // Angles are measured from 3 o'clock, increasing counter-clockwise, so 0 to 180
            // sweeps up over the top.
            canvas.FillColor = Colors.White;
            canvas.FillEllipse(bounds);

            canvas.FillColor = ArcColor;
            canvas.FillArc(-radius, -radius, radius * 2, radius * 2, 0, 180, false);

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
