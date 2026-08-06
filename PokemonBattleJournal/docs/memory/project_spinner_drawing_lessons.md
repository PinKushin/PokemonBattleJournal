---
name: project_spinner_drawing_lessons
description: "Loading spinner drawing gotchas — translucent stroke overlap causes moving lumps (draw stacked whole arcs instead), and ClipRectangle silently drops fills on WinUI (use FillArc)."
metadata:
  type: project
---

Learned building the Pokéball loading indicator (`Controls/Loading/`), 2026-08-05. Both apply
to anything drawn with `IDrawable`/`ICanvas`, not just this control.

## Translucent strokes that overlap ADD — never build a gradient from short segments

The trail was first drawn as ~36 short line segments around the circle, each with its own
alpha, round-capped. It rendered visibly scalloped, and the lumps travelled with the rotation
so the whole thing looked like it was flickering.

The cause is not gradient resolution — adding segments cannot fix it. Every segment's round
cap overlaps its neighbour's, and two translucent strokes over the same pixels composite
additively, so **each join is a darker blob**. The artefact is inherent to the method.

**What works:** stack whole arcs. Draw N translucent arcs, all anchored at the head, each
reaching a little less far back than the last:

```csharp
for (int i = 0; i < TrailLayers; i++)
{
    float t = (float)i / (TrailLayers - 1);
    float reach = TrailSweepDegrees * (1f - (t * (1f - (1f / TrailLayers))));
    canvas.StrokeSize = stroke * (TailWidthRatio + ((1f - TailWidthRatio) * t));
    canvas.StrokeColor = ArcColor.WithAlpha(LayerAlpha);
    canvas.DrawArc(left, top, diameter, diameter, startAngle, endAngle, clockwise: false, closed: false);
}
```

Each layer is one continuous stroked path with no internal joins, so nothing composites
against itself. The gradient comes from *how many layers overlap* at each point — solid at the
head, one faint layer at the tail. The taper falls out of the same stack: layers reaching
furthest back are drawn thinnest, so visible width is set by the widest layer covering a point.

Head opacity is `1-(1-a)^n`, so pick alpha and layer count together (18 layers at 0.16 is
effectively solid at the head).

## ClipRectangle silently discards the fill on WinUI

The Pokéball's red top half was originally a full circle drawn under a clip rectangle covering
the upper half. On Windows it rendered an **all-white ball** — no error, no exception, the fill
simply did not appear. The most recognisable part of a Pokéball was missing.

**Use `FillArc` instead** — a drawing primitive rather than a canvas state operation, and
consistent across targets:

```csharp
canvas.FillColor = ArcColor;
canvas.FillArc(-radius, -radius, radius * 2, radius * 2, 0, 180, false); // 0-180 sweeps over the top
```

Angle conventions differ between helpers and are easy to get backwards: `FillArc`/`DrawArc`
measure **counter-clockwise from 3 o'clock**, while the control's own `PointOnOrbit` measures
**clockwise from 12 o'clock**. Converting between them swaps the ends as well as the origin:
`startAngle = 90 - headAngle`, `endAngle = 90 - tailAngle`.

## Residual flicker is accepted, not solved

Some shimmer remains. User's call, 2026-08-05: *"its a maui problem, wed have to build
something in dx from primitives to probably fix it then thats not cross platform."* Do not
spend more time on it, and do not introduce a platform-specific renderer for it.

One untried cheap option if it ever matters: the redraw runs at 30fps
(`LoadingIndicator.FrameInterval = 33ms`), and a rotating object at 30fps judders against a
faster display, which reads as flicker independently of any compositing artefact. Moving to
16ms is one line — but it doubles UI-thread work, and a busy UI thread is exactly what made
Android UI automation crawl before ([[project_readjournal_android_slow]]), so it needs an
Android measurement, not just a Windows glance.

## Sizing

The ball is a **fixed 28px**, not a fraction of the control, matching the Pokéball icons
elsewhere (28×28 in `ComboBoxControl`, 26×26 in its popup, 24×24 in the archetype rows). As a
ratio, enlarging the control to give the trail more room enlarged the ball by the same factor,
so the ring never gained space.
