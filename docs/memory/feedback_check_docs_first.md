---
name: feedback-check-docs-first
description: Always check official package docs for required setup before debugging mysterious hangs or failures with third-party libraries
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-25T19:17:39.087Z
---

Check the official docs for any new library's setup requirements BEFORE spending time debugging mysterious failures.

**Why:** Spent hours investigating threading, concurrency, IsVisible workarounds, and staggered initialization for a LiveCharts2 hang on TrainerPage. Root cause was a single missing line — `.UseSkiaSharp()` in `MauiProgram.cs` — that the docs clearly state is required. All that debugging was wasted because we assumed the library was set up correctly.

**How to apply:** When a third-party control or library behaves in an unexpected or broken way (hangs, crashes, doesn't render), read the official installation/setup docs first before assuming it's a threading, concurrency, or app code issue. Pay special attention to `MauiProgram.cs` / app builder registration steps — libraries often require explicit registration that isn't obvious from the package name alone.

**Concrete example:** `LiveChartsCore.SkiaSharpView.Maui` requires BOTH `.UseSkiaSharp()` AND `.UseLiveCharts()` in `MauiProgram.cs`. Missing `.UseSkiaSharp()` causes all CartesianChart controls to hang silently.
