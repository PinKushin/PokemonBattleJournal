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
something in dx from primitives to probably fix it."* Do not spend more time on it.

**Do not record DX as the reason — that was fact-checked and is wrong in an important way.**

The original note said a fix would need DX primitives and that MAUI cannot host DX. The
conclusion (leave it) is right; the reasoning does not hold:

- DX is Windows-only and MAUI is cross-platform-first, so a DX fix would be a **Windows-only
  fix plus a parallel implementation per platform**. That alone is a good reason to decline.
- But "MAUI is not made for it" is too strong. MAUI's Windows backend *is* WinUI 3, which
  renders on DirectX, and `GraphicsView` draws there through Win2D — a DX wrapper. **The
  spinner already runs on that stack.** A DX rewrite would probably not fix the flicker,
  because we are not missing DX.

**If this is ever revisited, the route is SkiaSharp, not DX.** Verified 2026-08-05: SkiaSharp
is already a dependency (via `LiveChartsCore.SkiaSharpView.Maui`) and `.UseSkiaSharp()` is
already called in `MauiProgram`. `SKCanvasView` is cross-platform and GPU-accelerated, and
`SKShader.CreateSweepGradient` is an angular gradient about a centre point — exactly the
primitive `ICanvas` lacks and precisely what the layered-arc stack is imitating. It would very
likely remove the banding and the compositing artefacts together, everywhere, with no
per-platform code.

Not tested, and not worth doing for a spinner that already looks good. Recorded so nobody
concludes "impossible" from a reason that does not survive checking.

**The frame rate was already raised, and it worked — this is done, not pending.** The redraw
ran at 30fps (`FrameInterval = 33ms`); a rotating object at 30fps judders badly against a
244Hz display, which reads as flicker independently of any compositing artefact. Moved to
16ms, the user confirmed the ball spin was much smoother, and it was kept. `FrameInterval` is
16ms today.

The remaining shimmer is what is left *after* that change, which is why the Skia route above
is the only untried option, not the frame rate.

Still outstanding: this doubled UI-thread work and has **not** been measured on Android, where
a busy UI thread is what made UI automation crawl before
([[project_readjournal_android_slow]]). If Android UI tests slow down, back off `TrailLayers`
first (44 → 24 costs gradient smoothness), then `FrameInterval`.

## Tuned values, and why (settled with the user 2026-08-05, on screenshots of both platforms)

| Constant | Value | Reason |
|---|---|---|
| `TrailLayers` | 44 | 18 gave 17° steps and visible banding along the tail; 44 gives 7° and reads continuous |
| `LayerAlpha` | 0.075 | paired with the layer count to hold head opacity at ~0.97; raising layers without lowering this saturates the trail solid |
| `TailWidthRatio` | **0.45** | 0.60 was tried and rejected — the taper stopped registering and it read as a solid donut with a gradient rather than a trail with a leading edge |
| `TrailSweepDegrees` | 310 | near-closed loop, "almost an ouroboros" |
| head thickness | = ball diameter | derived, not an independent ratio, so the proportion cannot drift if the ball size changes |
| `FrameInterval` | 16ms | 33ms visibly stepped on the user's 244Hz monitor; the angle maths was already time-based, so only the frame count needed changing |

Perceived taper is stronger than `TailWidthRatio` suggests, because width and alpha fall off
together — the eye sees the product. That is why 0.45 looks like a definite taper and 0.60
looks like almost none.

## The tail's fade depends on the background

The faint end holds up on dark and washes out on white — visible comparing the Windows app
(dark) against the Android emulator (light). Any alpha floor tuned on one will be wrong on the
other. Check both when theming lands ([[project_theme_switcher]]); do not re-tune on a single
background.

## Comparing themes side by side (idea from the user, 2026-08-05)

The Windows app is unpackaged, so two instances can run at once — but both follow the OS
theme, giving two identical windows. `Application.Current.UserAppTheme` overrides the theme
per process, so **once the in-app theme switcher exists, two instances set differently give
genuine side-by-side light/dark on one screen**, with no emulator needed. Worth building into
the switcher work: it is also the tooling for reviewing that work.

## Sizing

The ball is a **fixed 28px**, not a fraction of the control, matching the Pokéball icons
elsewhere (28×28 in `ComboBoxControl`, 26×26 in its popup, 24×24 in the archetype rows). As a
ratio, enlarging the control to give the trail more room enlarged the ball by the same factor,
so the ring never gained space.
