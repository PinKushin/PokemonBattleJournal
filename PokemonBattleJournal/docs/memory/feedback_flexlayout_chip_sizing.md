---
name: feedback_flexlayout_chip_sizing
description: "FlexLayout tag chip sizing — MinimumWidthRequest forces even row wrapping without truncating text"
metadata:
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-04T17:18:22.360Z
---

Use `MinimumWidthRequest` on FlexLayout child items (e.g. Border chips) to control wrapping row density without capping or truncating text.

**Why:** `FlexLayout.Basis` sets a fixed width floor — items won't grow beyond it unless `Grow="1"` is added. With Grow, items stretch to fill the row but text still truncates if the label doesn't expand. `MinimumWidthRequest` lets each chip be content-sized (as wide as its text needs) but guarantees a floor that forces wrapping at the desired column count. For 8 tags at ~140dp minimum in a ~560dp container: 4 per row, even 4+4 distribution, no truncation.

**How to apply:** For any FlexLayout chip/badge/tag row:
- Set `Wrap="Wrap"` on FlexLayout
- Set `AlignItems="Start"` so rows don't stretch vertically
- Set `MinimumWidthRequest="140"` (or appropriate floor) on each child Border/Frame
- Do NOT use `FlexLayout.Basis` — it caps width and truncates text
- Do NOT use `FlexLayout.Grow="1"` alone — causes text truncation at narrow windows

FlexLayout default is `Nowrap` — must always set `Wrap="Wrap"` explicitly or items squish instead of wrapping. This is not called out clearly in MAUI docs.
