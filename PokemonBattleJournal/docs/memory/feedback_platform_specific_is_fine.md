---
name: feedback_platform_specific_is_fine
description: "Platform-specific code is acceptable when it's an easy split using ordinary platform APIs. Only exotic interop (e.g. hosting DX inside MAUI) is ruled out. Don't treat 'not cross-platform' as an automatic no."
metadata:
  type: feedback
---

**Do not treat "that would be platform-specific" as a reason to stop.** User, 2026-08-05:
*"basically if its an easy split and not something that is hugely out of the ordinary like
using dx, its possible ill use them."*

The line is about **difficulty and ordinariness**, not about crossing a platform boundary:

- **Fine** — per-platform handlers, platform views, effects, and built-in native renderers.
  `AcrylicBrush` on WinUI or `RenderEffect` on Android 12+ for a blurred overlay is a normal
  MAUI customisation, and MAUI is designed to accommodate it.
- **Not fine** — bypassing MAUI's rendering entirely. Building the loading spinner from DX
  primitives to eliminate its residual flicker was rejected because hosting DX inside MAUI is
  essentially impractical: *"Maui is not made for it."*

## Why this needs writing down

I made exactly this mistake and had to be corrected. Having abandoned the spinner flicker fix,
I then cited it as precedent against a blurred scrim — filing "custom DX renderer" and "use
the platform's own blur API" under the same heading of "not cross-platform, therefore no."
They are not remotely the same amount of work, and treating them alike would have ruled out a
whole category of perfectly reasonable options.

## How to apply

When a feature wants something MAUI has no cross-platform primitive for, cost it out per
platform *before* proposing a compromise. If each platform has a built-in API and the split is
a handler or an effect, offer it. Say plainly which platforms would be covered and what the
others fall back to. Only rule it out when the mechanism itself is exotic — embedding a
foreign renderer, reimplementing layout, fighting the framework's own pipeline.

Also relevant: a cross-platform compromise that is one line and works everywhere is still
usually the right *first* increment. Ship the simple version, then enhance per platform if the
simple one proves inadequate — that ordering was agreed for the loading scrim (dim first,
acrylic later if the dim is too flat).

## Related

- [[project_spinner_drawing_lessons]] — the flicker that was abandoned, and the precise reason
- [[project_roadmap]] — the scrim design this came up in
- [[user_no_signing_budget]] — a genuinely hard constraint, unlike this one
