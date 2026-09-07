# Decisions

Numbered, dated, and written in the owner's words where they gave them. A
decision made for a good reason is indistinguishable from an arbitrary one once
the conversation is gone, so the *why* lives here next to the *what*.

---

## 1. MVVM: Views and ViewModels in the head, Services and Models in Core

**Standing architecture rule.** The split is load-bearing for mutation testing.

`PokemonBattleJournal` is the MAUI head — Views (XAML), ViewModels, Controls, and
exactly four files that need platform dependencies: `ModalErrorHandler` (Shell),
`FileHelper` (FileSystem/DeviceInfo), `MainThreadHelper` (MainThread), and
`MauiSqliteConnectionFactory` (database path).

`PokemonBattleJournal.Core` is a plain `net10.0` library holding Models, Services,
Utilities, Interfaces and Logging. Namespaces are identical across both, so a
`using` never tells you which assembly a type is in.

**Put new logic in Core unless it needs MAUI.** That is not a style preference:
Stryker cannot mutation-test the app head at all, so anything added there is
unmeasured by definition. If a new service needs a platform capability, take it as
a constructor dependency or an abstract method and let the head answer it —
`SqliteConnectionFactory.GetDbPath()` is the worked example.

---

## 2. Dependency Injection: singleton persistence, transient ui-only

Data pages (MainPage, ReadJournalPage, TrainerPage and their ViewModels) are
registered as **singletons** — they hold state across navigations and sessions. So
do AppShellViewModel and the app shell itself.

Options and About pages are **transient** — created on navigation, destroyed on
leaving. Their view models do not survive the page lifecycle.

**Singleton ViewModels need a `[TearDown]` in UI tests** to reset them before each
run, because test suite isolation requires clean state that the app's own
navigation does not provide. A leftover ViewModel still alive from the previous
test, bound to stale data, looks exactly like a regression.

---

## 3. Database session pairing: acquire and release together

```csharp
using DbSession session = await _factory.BeginAsync();
```

This opens the connection **and** takes the write lock together, inside the try
block. Disposing releases both.

**Never write a bare `finally { GetLock().Release(); }`** — if opening the
connection failed, that releases a permit nothing took and throws
`SemaphoreFullException` over the real error.

See `docs/memory/project_db_session_lock_pairing.md` for the history and why this
pattern matters.

---

## 4. Error handling: try/catch + injected IErrorHandler, never silent

All errors surface through logging or a designated error handler. Silent `catch {}`
is banned.

Error handling follows the pattern: `try/catch` + injected `IErrorHandler`
(registered in `MauiProgram`, `ModalErrorHandler` in production). Never
instantiate error handlers with `new ModalErrorHandler()` — take it as a
constructor dependency.

**Every `catch` either rethrows, logs at an appropriate level, or handles the error
with a documented reason.** A catch block that swallows an exception is a defect.

---

## 5. Tracing: span names and descriptions are constants, measurements are numeric

Instrumented via `IPerformanceMonitor`/`ITimedSpan` (Core), with
`SentryPerformanceMonitor` as the adapter. Instrumented flows: restore and import.

**Span names and descriptions are NOT covered by `SentryRedactingSink`** — that
governs Serilog property values only. So the interface takes CONSTANTS and
`ITimedSpan` exposes no way to attach a string. **Varying detail must be numeric**
via `SetMeasurement`.

A reflection contract test pins that shape; do not add a string setter.

Note: `TracesSampleRate` does not CREATE transactions, it samples ones that exist.
MAUI makes none automatically, which is why tracing appeared configured for months
while the dashboard stayed empty. Debug-only `SentryDiagnosticsButton` on
OptionsPage sends one trace and one error to confirm delivery.

See `docs/memory/project_sentry_three_channels.md`.

---

## 6. Logging and Sentry: redact by type, never by position

Local sinks keep everything; the Sentry sink is wrapped by
`Logging/SentryRedactingSink` and forwards property values by **type** — numbers,
bools, enums, `DateTime`, `Guid`. Strings and `{@destructured}` objects are
withheld.

**Log ids, counts and lengths, not names or paths.** A name in a template still
reaches the local log but arrives at Sentry as `[redacted]`, which is a worse
crash report than the id would have been.

Do not widen the allowlist in `SentryRedactingSink` to get a string through — use
an enum instead.

See `docs/memory/project_sentry_privacy_audit.md`.

---

## 7. Test detection: DeviceInfo.Platform signals unit test environment

`DeviceInfo.Platform == DevicePlatform.Unknown` signals that the code is running
under unit test — no MAUI runtime is active. Use this guard when calling MAUI APIs
from testable code.

---

## 8. Service architecture: operations are typed, factories select implementations

`SqliteConnectionFactory` owns table init and exposes typed operation services:
`MatchOperations`, `TrainerOperations`, `ArchetypeOperations`, `TagOperations`.

These four depend on `ISqliteConnectionFactory`, not the concrete class — that
abstraction is what makes connection failures injectable in tests.

`MatchAnalysisService` computes all stats for `TrainerPageViewModel`.
`MatchResultCalculatorFactory` selects `BO1ResultCalculator` or
`BO3ResultCalculator` based on match format.

---

## 9. Import: validate sizes and structure before database writes

`Services/Import/TrainerHillImportService` enforces limits **before** any DB write:
size, depth, entry count and name lengths. This prevents resource exhaustion from
untrusted input.

---

## 10. Export and Restore: two formats, one backup envelope

`Services/Export/ExportService` writes two formats:
- TrainerHill's format (archetype slugs, for interop, lossy)
- Backup envelope (names verbatim, plus match timings and archetype icons, lossless)

`Services/Restore/RestoreService` reads that envelope back: it merges into an
existing trainer of the same name, restores archetypes before matches so icons
survive, and refuses a newer envelope version rather than half-applying it.

**Not wired to UI yet** — registered in DI, nothing calls it.

See `docs/memory/project_backup_restore.md`.

---

## 11. Duplicate detection: StartTime, PlayingId, AgainstId, Result

Both import and restore de-duplicate through `Services/MatchDuplicateKey`:
`(StartTime, PlayingId, AgainstId, Result)` within one trainer.

Use `StartTime`, never `DatePlayed` — a date picker leaves that at midnight.

**The key is deliberately not authoritative:** `AgainstId` identifies a *deck*,
not a person, and the model stores no opponent identity. Two opponents on the same
deck in the same minute collide. A hit skips and reports; it must never delete or
overwrite.

---

## 12. Win rate formula: canonical definition

`(wins + 0.5 * ties) / total * 100` — defined in `Utilities/Calculations.cs`.

**All stats code must align with this.** Changing it is a decision, not a
refactoring; make it in DECISIONS, not in a commit message buried in history.

---

## 13. Custom controls: constructor-injected collections, Grid items

`Controls/ComboBoxControl/` — archetype picker (icon + name popup).
`Controls/ImagePicker.cs` — icon selector.

Both use a popup with a `CollectionView`:
- **`ItemsSource` must be passed via constructor**, not set after init
- **Item templates must return `Grid` directly**, not `ViewCell`

Popup item grids have `AutomationId` bound to `"ArchetypeItem_{Name}"` — used by
Appium seed and screen readers.

---

## 14. Explicit types, never `var`

Types go at the START of a declaration. This is the user's own convention,
predating any AI work here: *"types should always be apparent as early as
possible."* Target-typed `new()` on the right is fine and preferred — the
objection is to `var`, not to brevity. Anonymous types and tuple deconstructions
have no nameable type and stay.

Enforced as a build warning: `.editorconfig` sets the three `csharp_style_var_*`
rules to `false:warning`, and `Directory.Build.props` sets
`EnforceCodeStyleInBuild` — without that, an `.editorconfig` severity is
editor-only and does nothing to the build.

**Treat a new IDE0008 exactly like any other warning: a defect, fixed before the
change lands.**

---

## 15. Accessibility: automation IDs, screen reader labels, and real controls

Every interactive and informational UI element must have:
- `AutomationId` — stable, unique identifier (used by Appium and screen readers)
- `SemanticProperties.Description` — plain-English label for screen readers
- `SemanticProperties.Hint` on tappable non-button elements — "Double tap to …"
- `SemanticProperties.HeadingLevel="Level2"` on section headers
- Images: `SemanticProperties.Description` with meaningful text; purely
  decorative images get `SemanticProperties.IsInAccessibleTree="False"`

**Tappable elements must be real controls.** A `Border`/`Grid`/`Image` with a
`TapGestureRecognizer` exposes no UIA pattern, so a screen reader cannot activate
it. Use `Button`/`ImageButton` (Invoke), `CheckBox`/`Switch` (Toggle), or overlay
a transparent `Button` in the same Grid cell when custom visuals are required.

See `docs/memory/feedback_invokable_controls.md`.

The contract is enforced on Windows via `MainPage_InteractiveElement_IsAnnouncedAndOperable`
and its OptionsPage twin, which read the live UIA tree and require both a Name and
a control pattern. Add new interactive elements to those `[TestCase]` lists. They
do not cover Android.

---

## 16. Test conventions: TDD, naming, seed isolation, retry patterns

### Test-driven development

For anything new: **write the failing test first, then write the code.**

1. Write the test. Run it. Confirm it fails for the right reason.
2. Write the minimum code to pass it.
3. Refactor. Tests stay green.

### Unit tests

`{Class}Tests`, methods `{Method}_{Scenario}_{Expected}`, NSubstitute mocks,
Shouldly assertions. Unit tests do not touch the database or the network — all DB
operations live in `IntegrationTests` as of 2026-08-05.

### Integration tests

Real SQLite, temp DB file per test.

### UI tests (Appium)

Every Shell page needs a navigation + element-visible test. Every data page needs a
data-presence assertion test (not just "element exists").

`SeedTestData()` runs in `AppiumSetup` constructor and handles:
1. First-boot trainer prompt
2. Selects "Other" for both PlayerArchetype and RivalArchetype via
   `ArchetypeItem_Other` AutomationId
3. Saves 3 Win matches
4. Clears the form on success (no navigation needed between iterations)

Seed failures throw `InvalidOperationException` — never swallowed silently.

Windows UI tests: `WipeAppData()` deletes the SQLite DB before each run so
first-boot prompt always fires on a clean slate (all state lives in the `.db3` —
the Preferences API is not used).

**Never call `.Click()` on an element.** WinAppDriver's `.Click()` is synthesized
mouse input at the element's centre in SCREEN coordinates — it carries no UIA
pattern. Always go through `ClickElement` / `TryClickIfPresent` /
`ScrollIntoViewAndClick`.

See `docs/memory/feedback_flaui_scroll_into_view.md`.

**The two platforms fail differently and the fixes do not transfer.**
- Windows clicks land in the wrong PLACE (screen coordinates)
- Android clicks land NOWHERE (dispatched, handler never runs)

Windows needs the pattern ladder; Android needs click-verify-retry.

See `docs/memory/feedback_android_flaky_tap_retry.md`.

Diagnostic logs after UI test runs:
- `%TEMP%\UITests.PerfLog.txt` (per-element FIND stage timing, rotates `.1`/`.2`)
- `%TEMP%\UITests.NavLog.txt` (navigation)
- `%TEMP%\UITests.Android.setup.log` (AppiumSetup steps)
- `%TEMP%\UITests.PopupLog.txt` (in-app ComboBox popup lifecycle, pulled via adb at teardown)

---

## 17. Platform quirks: Windows unpackaged, Android with proper lifecycle

**Windows:** unpackaged (`WindowsPackageType=None`); debug exe at
`bin\Debug\net10.0-windows10.0.19041.0\win-x64\PokemonBattleJournal.exe`.

**Android UI tests:** AVD `pixel_7_-_api_35`; `EnsureEmulatorRunning()` verifies
correct AVD by name via `adb emu avd name` and boots it if absent.

**Android local workflow:** deploy once from VS (Fast Deployment), then rerun tests
freely — never `pm clear` a VS-deployed app (wipes `.__override__/`, crashes on
launch).

**Android Release:** `RunAOTCompilation=False`, `PublishTrimmed=False`.
