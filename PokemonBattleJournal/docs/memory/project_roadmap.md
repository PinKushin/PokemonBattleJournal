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

## Known Bugs (fix before first release)

**There has never been a release.** Nothing has shipped, so every bug listed here is a
first-release blocker by definition, and the release vehicle itself is still an open roadmap
item (see *Real Installer (Windows/Android)* below).

*None currently open.*

### ~~ComboBox Cancel Button Hangs App (MainPage)~~ — CLOSED 2026-08-05, not a bug
Was a transient Windows OS hiccup, not application behavior — user confirmed 2026-08-05,
never reproduced. Regression UI tests for Cancel dismissal were added anyway (merged
`5c9b7da`) and are kept. Do not hunt for an async deadlock in the popup dismiss path.
See [[project_combobox_cancel_hang]].

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

### Code signing — hard budget constraint (stated 2026-08-05)

**The user has no budget for code-signing certificates.** Commercial OV/EV Windows certs run
several hundred USD per year (and since 2023 require hardware-token/HSM storage, which pushes
the cost up further). Do not plan around buying one. This constrains the release design, so
the free paths below are the real options:

**Android — genuinely free.** Android signing involves no CA at all: a self-generated
`keytool` keystore *is* the standard mechanism, not a workaround. A signed release APK on
GitHub Releases costs nothing. **The keystore must be backed up permanently — losing it means
never being able to update the app.** Google Play is a one-time developer registration fee
(~$25, verify current), F-Droid is free; neither is required for sideloading.

**Windows — unsigned is worse than it sounds on Windows 11.** Two separate mechanisms:

1. **Mark of the Web.** Downloaded files carry a `Zone.Identifier` ADS; Properties → *Unblock*
   clears it. An `.exe` can usually be run via *More info* → *Run anyway*, but MotW blocks
   `.ps1`, `.chm`, and app-loaded DLLs harder — and Explorer propagates MotW to every file
   extracted from a downloaded ZIP, which matters because a self-contained build is an exe
   surrounded by many DLLs. User has been burned by this repeatedly.
2. **Smart App Control (Win11 22H2+).** Can block unsigned apps outright **with no "Run
   anyway" option**. Re-enabling SAC after disabling it requires an OS reinstall. Only active
   on clean installs (starts in evaluation mode), so not universal — but for affected users an
   unsigned app simply does not run.

**Do not conflate those two.** The user's own machine is the MotW case (#1) — every exe
downloaded from the internet has to be unblocked via Properties — not SAC. #2 is a risk to
*other* users on affected Win11 installs, not an observed behavior here. Worth knowing: if
"Run anyway" is never offered and Properties is the only route, Windows Security →
Reputation-based protection → "Check apps and files" is likely set to **Block** rather than
Warn, which is stricter than the default.

Free routes that actually clear this, in preference order:

- **SignPath.io** — free Authenticode signing for open-source projects, integrates with GitHub
  Actions. Real signature, $0. **Best fit for this project.**
- **Microsoft Store** — Microsoft signs the MSIX; no warning at all. Individual developer
  registration was historically a small one-time fee (~$19) and may since have been
  reduced/waived — verify. Costs Store packaging + review instead of money.
- **Certum Open Source Code Signing** — OSS-specific cert, historically ~€30/yr. Cheap but not
  free.
- **Ship unsigned on GitHub Releases** — $0, works for a developer audience who will click
  through, hostile for normal Win11 users. Acceptable for v0.1 only.

**Never self-sign for public Windows distribution.** It is worse than unsigned: MSIX sideload
then requires users to install your certificate into Trusted Root — scarier and more work.

### Self-contained deployment — decided

Ship Windows **self-contained** (`SelfContained=true` + `WindowsAppSDKSelfContained=true`).
This eliminates the entire "user didn't install .NET / the Windows App SDK and the app
crashes" failure class — nothing to document, nothing for users to get wrong. Required
anyway for an unpackaged app without the WindowsAppSDK runtime present.

Correcting a misconception recorded here deliberately: self-contained is **not** a runtime
performance hit, and it does **not** affect SmartScreen either way. Runtime speed is
essentially unchanged (ReadyToRun can make startup *faster*). What it actually costs is
**download size** (well over 100 MB vs a small fraction of that) and **servicing** — you own
the bundled runtime, so a .NET security patch means cutting a new release rather than users
getting it from Windows Update. Signing and deployment mode are independent concerns.

**Realistic free-tier first release:** signed Android APK (real signing, $0) + self-contained
Windows build signed via SignPath, both on GitHub Releases.

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

### Optional visual indicator — DESIGN LOCKED (2026-08-04, mockup provided by user)

Fluent-style ring spinner, NOT a full solid ring and NOT a simple spinning Pokéball alone:

- **Partial arc**, not a closed circle. Solid/opaque red near the leading edge, fading to
  transparent trailing behind it — matches the "chasing itself" Windows modern spinner look.
- **Pokéball rides the leading edge** of the arc, positioned at the arc's head like a comet.
- **Pokéball spins on its own axis** independently while it also orbits around the circle path.
- Arc color: red (primary choice) or PokeBlue — both hold up in light and dark mode. White ruled out (invisible/low-contrast on light backgrounds).
- Reference mockup: user-provided image — red arc, gray/white Pokéball dot at the 12 o'clock
  leading point, trail fading counter-clockwise from the ball.
- Bind IsVisible to `IsAnyBusy` for full-page overlay, or specific `IsBusy_*`/`IsBusyMutating`
  for inline per-action indicators.
- Respect `AccessibilitySettings.IsReduceMotionEnabled` — swap animation for static "Loading…" label.
- Overlay uses semi-transparent scrim over content; inline uses a small version in the section/button.
- Implementation approach: likely custom `GraphicsView`/`SKCanvasView` (SkiaSharp is already a
  dependency via LiveCharts2) drawing an arc + rotating Pokéball sprite, animated via a
  `Microsoft.Maui.Animations` ticker or simple `Dispatcher.StartTimer` angle increment. Lottie
  is an alternative if a matching animation is easier to source/build externally.

### TDD

- Write a failing test that opens TrainerPage, asserts `Busy_ChartData` is visible, then waits for it to disappear within 5 s and asserts chart elements are present. Then wire the gate to make it pass.
- Repeat per gate.

---

## Android test execution strategy (added 2026-08-05, not started)

Android UI jobs now finish faster on CI than the Windows ones, while the same 72 tests run
serially in **8m55s** locally (Windows: 73 tests, **1m28s**). The gap is the execution
environment — the emulator on Windows — not the hardware; the user's machine outclasses a
GitHub Ubuntu runner.

Planned, in rough priority order:

1. **Default Android UI testing to CI.** Keep local runs for targeted `--filter` debugging
   rather than full sweeps.
2. **Auto-target a real phone.** If a physical device is attached, run there; otherwise boot
   the AVD. Automatic detection via `adb devices`, with an env-var override in the shape of
   the existing `ANDROID_USE_INSTALLED`.
3. **Local parallelism** to match what the CI matrix gets. Needs distinct AVDs, adb/Appium
   ports, and app data per instance — `AppiumSetup` currently owns a single driver, port and
   emulator, so concurrent fixtures would fight over one `.db3`.
4. **Evaluate WSL2** (Ubuntu already installed) as an emulator host — requires nested
   virtualization + KVM, and a real phone would need `usbipd-win` forwarding. Spike before
   committing; the emulator would contend with Hyper-V for the same hardware, so it is not
   obviously faster than more native AVDs.

Full constraint list in `docs/memory/project_android_test_execution_strategy.md`.

---

## Inline validation feedback (added 2026-08-05, not started)

Guards that decline a save now log a warning naming the missing input
([[feedback_no_silent_guards]]), which fixes *diagnosis*. It does not fix the user
experience: someone who leaves a field empty still sees nothing happen at all.

**User's decision (2026-08-05):** a **text label with red text** explaining which step failed
validation. **Not a modal** — and this is a hard constraint with reasons behind it, not a
style preference: *"modals can cause bad mojo in automation especially on ci thats why i want
to stay away from them."*

This repo has been bitten by modal/dialog behavior repeatedly:

- [[project_winui_xamlroot_crash]] — `DisplayPromptAsync` before the window was composed
  crashed WinUI with "no XamlRoot".
- [[project_optionspage_crash_fresh_db]] — `ModalErrorHandler` firing during
  `AppearingAsync` on a fresh DB crashed with `0xc000027b`; fixed by making it log-only.
- [[project_android_ci_gpu_flake]] — a system ANR dialog owned the **entire accessibility
  tree**, so no element of ours was reachable until it was dismissed.
- [[feedback_combobox_popup_platforms]] — popup Cancel buttons frequently are not in
  Android's UIA tree, which is why `DismissPopupPlatform` exists.

A modal is a separate window: it steals focus, may be absent from the UIA tree, and can
appear when no test is waiting for it. An inline label is a bound property on a page the
tests already hold a handle to. **Do not introduce a modal for validation feedback.**

Design notes for whoever picks this up:

- Inline label near the offending input, not a page-level banner — the point is to say
  *which* field is wrong, matching the split guards already in `OptionsPageViewModel`.
- Bind to an observable `…ValidationMessage` string per form (empty = hidden). Use an
  explicit `bool` VM property for `IsVisible`, never a null-check converter
  ([[feedback_no_isnot_null_converter_in_xaml]]).
- Red must come from a theme resource, not a literal, so the theming pass can retint it
  ([[project_theme_switcher]]). Check contrast in light mode.
- Accessibility: the label needs `AutomationId` + `SemanticProperties.Description`, and
  should ideally be announced when it appears.
- The warning log and the label should share one source of truth so they cannot disagree.
- MainPage's `SaveMatchAsync` already builds a multi-line validation message string via
  `ValidateEntryAsync` — reuse that shape rather than inventing a second mechanism.
