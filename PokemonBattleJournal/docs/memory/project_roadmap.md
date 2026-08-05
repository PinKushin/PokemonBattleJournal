---
name: project_roadmap
description: "Planned features and product goals for PokemonBattleJournal — import/export, deck tools, and other roadmap items."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T20:15:18.066Z
---

Planned features confirmed by the user. Implement via TDD — write failing tests first.

**Why:** User stated these goals explicitly during sessions. They should drive future feature work and architecture decisions.

**How to apply:** When starting any new feature work, check this list. Prefer designs that leave room for these features even if not implementing them yet.

---

## Import / Export (JSON)

Format reverse-engineered from `trainerhill-battle-log-2026-07-27.json`:

```json
[
  {
    "playing": "archetype-slug",
    "against": "archetype-slug",
    "time": "2026-07-27 19:45:24.403684",
    "result": "Win|Loss|Tie",
    "game1": { "result": "Win|Loss|Tie", "turn": 1, "tags": ["..."], "notes": "..." },
    "game2": { ... },  // BO3 only
    "game3": { ... }   // BO3 only, split result
  }
]
```

Key mapping notes:
- `playing` / `against` are archetype name slugs — resolve to `Archetype` DB rows by name (case-insensitive slug match), create on import if absent
- `turn` is int OR string in the wild — coerce to `uint` (1 = went first, 2 = went second)
- `result` at match level is the overall result; game-level results drive BO3 calculation
- `tags` are tag names — resolve to `Tags` DB rows, create on import if absent
- `time` maps to `DatePlayed` + `StartTime`

Implementation plan (TDD):
1. `ImportService` — parses JSON array, resolves archetypes/tags, calls `MatchOperations.SaveAsync`
2. `ExportService` — queries `MatchOperations`, serializes to same JSON shape
3. Unit tests for both services with mock DB operations
4. OptionsPage: "Import" button (`FilePicker.PickAsync` → JSON file) + "Export" button (`FileSaver.SaveAsync`)
5. Both services injected via DI; no SQL in the services directly

### Export — two modes

**TrainerHill export (per-trainer):**
- Output same JSON shape as the import format above (single trainer's matches only)
- TrainerHill has no multi-profile support — it stores everything in browser cookies per account — so export is always scoped to one trainer
- OptionsPage: "Export to TrainerHill format" exports the *active* trainer's matches
- Filename suggestion: `trainerhill-battle-log-{TrainerName}-{date}.json`

**Full backup export:**
- User chooses: all trainers or a single trainer
- JSON envelope wraps multiple trainer exports: `{ "trainers": [ { "name": "...", "matches": [...] } ] }`
- OptionsPage: "Export backup" with a picker or radio for "All trainers" vs "Active trainer only"
- Filename suggestion: `pbj-backup-{date}.json` or `pbj-backup-{TrainerName}-{date}.json`
- Backup format should be importable back in as a restore (import service reads both flat array and backup envelope)

## Deck Maker

Build and store deck lists tied to archetypes. Goals:
- Associate a deck list (card name + count) with an `Archetype`
- View/edit deck list from OptionsPage or a dedicated DeckPage
- Export deck list to a standard format (e.g., PTCG Live import format)

Architecture notes: new `DeckEntry` model + `DeckOperations` service; new Shell page if complex enough.

## Pokeball Archetype Picker Animation

When the archetype ComboBox is tapped, animate the pokeball icon as if it's opening to "release" the archetype list. Goal: reinforce the "tap to pick a Pokémon (deck)" metaphor.

**Trigger:** User idea from 2026-08-03 session — ball_icon.png is the unselected placeholder; opening the picker should feel like throwing a ball.

### Rough implementation plan

**Physics note:** Pokeball hinges at the back — only the top half rotates away from the viewer. Bottom stays still.

1. **Asset:** Split `ball_icon.png` into two separate images: `ball_icon_top.png` (top red half) and `ball_icon_bottom.png` (bottom white half). Stack them in a Grid.

2. **Trigger point:** `ComboBoxControl.OnTapped` / `TapGestureRecognizer` command before `PopupNavigation.Instance.PushAsync(popup)`.

3. **Animation (MAUI `Animation` API):**
   ```csharp
   // Top half rotates backward around its bottom edge (the hinge line).
   // AnchorY = 1.0 pins the pivot at the bottom of the top image.
   _ballTop.AnchorY = 1.0;
   var open = new Animation();
   open.Add(0, 0.6, new Animation(v => _ballTop.RotationX = v, 0, -110,
       easing: Easing.CubicIn));   // rotate lid back ~110° (past vertical so it's clearly open)
   open.Add(0.5, 1.0, new Animation(v => _ballContainer.Opacity = v, 1, 0,
       easing: Easing.Linear));    // fade out as it opens
   open.Commit(this, "BallOpen", length: 280,
       finished: (_, _) => { /* show popup; reset RotationX = 0, Opacity = 1 */ });
   ```

4. **Close animation:** Reverse — `RotationX` from -110 back to 0, opacity 0 → 1, triggered on popup dismiss callback.

5. **Platform notes:** `RotationX` is 3D perspective rotation; verify it doesn't render flat on Android API < 28. MAUI animations run on UI thread — keep length ≤ 300ms so it doesn't feel laggy before the picker appears.

6. **Accessibility:** Check `AccessibilitySettings.IsReduceMotionEnabled` — skip animation and open immediately if true.

## Known Bugs (fix before next release)

### ComboBox Cancel Button Hangs App (MainPage)
Tapping Cancel on either archetype picker popup on MainPage (Journal Entry) freezes the app — requires force-close. Root cause unknown. Likely async deadlock in popup dismiss path. TDD approach: write failing UI test that taps Cancel and asserts popup dismisses within 3 seconds, then fix.

See [[project_combobox_cancel_hang]] for investigation notes.

---

## Website Refresh (feat/site-refresh — separate branch, later)

`index.html` at repo root (GitHub Pages via static.yml). Current AI-built lander is solid; refinements in priority order:

1. ~~**Legal disclaimer**~~ — **Done 2026-08-04.** Footer disclaimer added to index.html: unofficial fan-made tool, not affiliated with Nintendo/The Pokémon Company/Game Freak/Creatures Inc., trademarks acknowledged.
2. **App screenshots section** — feature tour with real UI captures (charts, journal, main page). Biggest visual impact.
3. **Auto-updating stats** — replace hardcoded "505 COMMITS" / "530+ TESTS" / fake ticker meta shares with shields.io badges or drop numbers.
4. **Ticker honesty** — remove "UTC // LIVE" claim or make decorative-obvious; data is static.
5. **Download section** — add Releases download buttons once the installer ships (pairs with Real Installer roadmap item).
6. **Verify hero-bg asset** — `.hero-bg` image must resolve on Pages; broken bg fails silently at 40% opacity.
7. **Accessibility pass** — skip-link, focus states on nav, contrast check on 9px `--muted` mono text (likely fails WCAG).

---

## Deck Comparer

Compare two deck lists side-by-side:
- Show cards in common, cards unique to each
- Highlight counts that differ
- Useful for tracking meta evolution between tournament seasons

Likely a sub-view of DeckPage rather than its own Shell page.

---

## AOT Compatibility (long-term)

Make the whole app AOT-compatible so Release builds run through NativeAOT / full Mono AOT — faster startup, smaller runtime footprint, and (on Android) `pm clear` becomes safe because assemblies live in the APK instead of `.__override__/`, unblocking cleaner test isolation.

**Current state:** Android Release explicitly sets `RunAOTCompilation=False` + `PublishTrimmed=False` (see CLAUDE.md) because deps aren't ready. Fast Deployment (Debug) uses Mono JIT + external assemblies.

**Blockers per dep:**
- **SQLite-net-pcl** — heavy runtime reflection on table mapping. Swap for source-generator variant, or migrate to EF Core 9 with compiled model.
- **CommunityToolkit.Mvvm** — already AOT-safe via source generators. ✓
- **CommunityToolkit.Maui popups** — reflection in `ShowPopupAsync<T>`; audit for trim warnings.
- **LiveCharts2** — reflection-heavy property binding; check trim/AOT support.
- **MAUI XAML bindings** — every `Binding` needs `x:DataType` (compiled bindings). Runtime bindings crash under AOT. Do an audit pass and fill in `x:DataType` everywhere.

**Enablement steps:**
1. Set `<IsAotCompatible>true</IsAotCompatible>` + `<TrimMode>full</TrimMode>` in csproj (Release).
2. `dotnet publish -c Release -f net10.0-android /p:PublishAot=true` (later: iOS too).
3. Fix every IL2026 / IL3050 warning by adding `[DynamicallyAccessedMembers]` where reflection is unavoidable, or refactor to source generators.
4. Verify all UI tests still pass on the AOT build.

Once AOT is on for Android, delete the "pm clear vs Fast Deployment" workaround memory — the whole class of bug disappears.

---

## Real Installer (Windows/Android)

Right now Windows deploys as an unpackaged .exe (`WindowsPackageType=None`) and Android deploys through VS Fast Deployment for dev. Ship a real installer for released builds:

- **Windows** — MSIX package with Start Menu entry, uninstaller, auto-update; or WiX MSI. Improves startup because the CLR loads from a fixed install path (no per-user reprovisioning) and Windows can prefetch. Also gives file associations for `.trainerhill.json` imports.
- **Android** — signed release APK/AAB via Play Store or F-Droid. AAB with dynamic delivery is smaller and installs faster on device than a monolithic APK.
- **macOS/iOS** — future, once MAUI targets are enabled again.

Bundling with AOT + an installer is the combo: no Fast Deployment paths on user machines, no assembly resolution overhead, clean uninstall, real update channel.

---

## Loading Gates + Optional Loading Indicator

**GATES SHIPPED 2026-08-04** (feat/loading-gates): IsBusyMatchHistory / IsBusyChartData /
IsBusyArchetypeList ×2 live on all four data pages with Busy_* sentinels and
WaitUntilBusyGone test sync — see [[project_loading_gates]]. Together with the
ReadJournal FlexLayout swap: SelectMatch tests 50-111 s → sub-second, Android suite
18 m → 8 m 44 s, 72/72. **Remaining from this entry:** only the optional visual
indicator (spinner/PokéBall animation) — user polish, unscheduled.

**The backend gate matters more than any visual.** Confirmed 2026-08-04: Android UIA
server waits ~20 s per element lookup when the UI thread is busy on async render —
regardless of how much data is on screen (dropping ReadJournal seed from 14 → 4
matches did nothing). A named `IsBusy_*` flag that flips fast is what unblocks UI
tests; the animated indicator is user polish on top.

### Named busy tokens (primary design)

Every async load declares a scoped `IsBusy_*` bool property on its VM, not a single
page-wide flag. Multiple concurrent loads each own their own gate so tests can wait
for the specific data they care about:

```csharp
public partial bool IsBusy_ChartData { get; set; }
public partial bool IsBusy_MatchHistory { get; set; }
public partial bool IsBusy_ArchetypeList { get; set; }
```

Each async op wraps in try/finally so the flag always clears:

```csharp
try { IsBusy_ChartData = true; await LoadChartsAsync(); }
finally { IsBusy_ChartData = false; }
```

Each bool binds to a hidden **1×1 Label** in XAML with a stable AutomationId:

```xml
<Label WidthRequest="1" HeightRequest="1" Opacity="0"
       AutomationId="Busy_ChartData"
       IsVisible="{Binding IsBusy_ChartData}" />
```

Tests then `WaitUntilGone("Busy_ChartData")` before element lookups. No arbitrary
sleeps. UIA server sees the flag flip to hidden the moment the load completes.

Global `IsAnyBusy` computed from the set (any bool true) drives the optional
visible spinner. Registry / dict of tokens is overkill until dozens of concurrent
loads coexist — start with per-property.

### Where the gates go

- **TrainerPage** — `IsBusy_ChartData` around chart calc pipeline
- **ReadJournalPage** — `IsBusy_MatchHistory` around match list + detail load
- **MainPage** — `IsBusy_ArchetypeList` around Limitless fetch on first popup open
- **OptionsPage** — `IsBusy_ArchetypeList` shared with MainPage

### Optional visual indicator

Once the gates ship, layer on a user-facing indicator:
- ActivityIndicator or Lottie animated icon (PokéBall spinning fits theme)
- Bind IsVisible to `IsAnyBusy` for full-page overlay, or specific `IsBusy_*` for inline
- Respect `AccessibilitySettings.IsReduceMotionEnabled` — swap animation for static "Loading…" label
- Overlay uses semi-transparent scrim over content; inline uses a small spinner in the section

### TDD

- Write a failing test that opens TrainerPage, asserts `Busy_ChartData` is visible, then waits for it to disappear within 5 s and asserts chart elements are present. Then wire the gate to make it pass.
- Repeat per gate.
