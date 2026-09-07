# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Living context doc

**Read `PokemonBattleJournal/docs/AI-CONTEXT.md` at the start of every session.** It is the canonical context file — architecture, domain model, session log, known bugs, and user decisions. Update its Session log before starting any long multi-file task, and again when finishing significant work.

## Long-term AI memory

**Read all files in `PokemonBattleJournal/docs/memory/` at the start of every session.** These are the persistent memory files for Claude Code — user preferences, feedback on past approaches, and project decisions that must carry across conversations. Apply them throughout the session exactly as you would local memory.

- `PokemonBattleJournal/docs/memory/MEMORY.md` — index of all memory entries
- `PokemonBattleJournal/docs/memory/feedback_*.md` — how the user wants work approached
- `PokemonBattleJournal/docs/memory/project_*.md` — project decisions and context
- `PokemonBattleJournal/docs/memory/user_*.md` — user background and preferences

**After making a memory-worthy observation** (the user corrects an approach, confirms a decision, states a preference), write both to the local memory system AND copy the updated files to `PokemonBattleJournal/docs/memory/` and commit them so the repo stays in sync.

## Commands

Lookup table first; the annotated block below carries the reasoning, and the comments in
it are the point — read them before changing a command.

| I want to | Command |
|---|---|
| Build for Windows | `dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0` |
| Build as CI does | set `PbjWindowsHeadOnly=true`, then the above with `-c Release -warnaserror` |
| Build everything | `dotnet build PokemonBattleJournal.slnx` |
| Run the app | `dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0` |
| Unit tests (~3s) | `dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj` |
| One test | add `--filter "FullyQualifiedName~MethodName"` |
| Integration tests | `dotnet test PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj` |
| Windows UI tests | `pwsh ../run-exclusive.ps1 dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj` |
| Android UI tests | `pwsh ../run-exclusive.ps1 dotnet test PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj` — **deploy first if app code changed** |
| All CI suites locally | `./build/ci-local.ps1` (fast) or `-All` (everything) |
| Mutation testing | `pwsh ../run-exclusive.ps1 -TimeoutMinutes 90 dotnet stryker --config-file stryker-core.json` |
| Fuzzing | `bash build/run-measurements.sh fuzz 300` |

**UI suites, Stryker and fuzzing take the machine-wide lock** — several agents share this
desktop, and they fail QUIETLY when they overlap. Unit and integration tests do not.

### The commands in full, with the reasons

```powershell
# Build the app for Windows. -f must target the app project, NOT the solution —
# the test and scraper projects do not target the Windows TFM, so passing -f to
# PokemonBattleJournal.slnx fails with NETSDK1005 on every one of them.
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Same build in CI's shape. PbjWindowsHeadOnly trims TargetFrameworks to the Windows
# head; -f alone does NOT, because dotnet build runs an implicit restore and restore
# walks EVERY declared TFM regardless of -f — so it demands the android workload even
# for a Windows-only build. That is what broke Windows UI and CodeQL for a week from
# 2026-08-11, when the windows-latest image stopped shipping MAUI workloads preinstalled.
#
# Release, not Debug: Debug does not fully compile XAML, so XamlC errors AND warnings
# are invisible until Release. -warnaserror is what CI uses; keep it at zero.
$env:PbjWindowsHeadOnly = 'true'
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0 -c Release -warnaserror

# Build everything (all projects, all their own TFMs — no -f)
dotnet build PokemonBattleJournal.slnx

# Run (Windows)
dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Unit tests — genuinely unit tests now, and fast (~3s for 704). The six
# real-SQLite files that used to live here moved to the IntegrationTests
# project on 2026-08-05; nothing in here touches a database or the network.
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj

# Single unit test
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj --filter "FullyQualifiedName~MethodName"

# Integration tests (real SQLite, temp DB file per test)
dotnet test PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj

# Windows UI tests (WinAppDriver + Appium — app auto-built and launched)
dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj

# Android UI tests (requires pixel_7_-_api_35 AVD; emulator booted automatically).
# Default assumes the app is already deployed (VS Fast Deployment) — safe path:
# force-stop + delete .db3 only. Set ANDROID_USE_INSTALLED=0 (CI does) to force
# full EmbedAssembliesIntoApk build + adb install + pm clear.
#
# IF APP CODE CHANGED, DEPLOY FIRST — otherwise you test the previous build. That
# is wrong in both directions: an existing test passes against stale code (silent,
# ships regressions), and a NEW test fails on an element the old APK lacks (looks
# like an Android binding bug). Android alone failing a test you just wrote is
# usually a stale APK, not a platform quirk. ~40s, needs a booted emulator:
#   dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-android -t:Install
dotnet test PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj

# All the CI suites locally, in CI's shape — the stand-in when GitHub Actions is
# down (self-hosted runners do NOT help there; the runner polls GitHub for work).
# UI suites run one fixture per invocation to match the per-fixture matrix; the
# script is sequential on purpose and refuses to start Android while the Windows
# suite is alive. See docs/memory/project_android_session_poisoning.md.
./build/ci-local.ps1              # unit + integration, seconds
./build/ci-local.ps1 -All         # everything; leave the machine alone
./build/ci-local.ps1 -Suites WindowsUI -Combined   # one fast pass, less faithful

# Launch the app at CI's window geometry to inspect layout the way CI sees it.
# UI tests honour UITEST_WINDOW_SIZE; this is the app's own equivalent.
PBJ_WINDOW_SIZE=754x512 dotnet run --project PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Reproduce CI's Windows geometry in the UI tests. Set BOTH: the size alone pins the window to
# (0,0), where screen-space and window-relative coordinates coincide — which once hid a real
# coordinate-space bug until CI, whose window sits at (85,78), failed on it.
UITEST_WINDOW_SIZE=754x512 UITEST_WINDOW_POS=85,78 dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj

# A leftover PokemonBattleJournal.exe makes WinAppDriver attach to the wrong window and fails
# the whole fixture in OneTimeSetUp — it looks exactly like a regression. Kill it first.
Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue


# ALL of the below take the machine-wide lock: several agents share this desktop and
# these projects, and both UI suites and Stryker fail QUIETLY when they overlap.
# The lock lives beside the repos, at PinKushin/run-exclusive.ps1, forward slashes so
# the path survives being pasted through a shell:
#   pwsh ../run-exclusive.ps1 -TimeoutMinutes 90 dotnet stryker --config-file stryker-core.json
#   pwsh ../run-exclusive.ps1 dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj
# See the global CLAUDE.md section "Exclusive workloads take the machine-wide lock".

# Mutation testing — grades the assertions, not the code. Stryker still cannot
# touch the MAUI head (its internal recompile does not reproduce XAML codegen or
# the MVVM source generators, and surfaces no CS error when it fails), which is
# why the logic lives in PokemonBattleJournal.Core. Two configs, two targets.
dotnet tool restore
dotnet stryker                                  # PokemonBattleJournal.Scraper (~5 min, ratcheted at 100%)
dotnet stryker --config-file stryker-core.json  # Core (~17 min on the box; uses both test projects)

# Fuzzing — the other half of Stryker. Stryker mutates the CODE and asks whether the
# tests notice; a fuzzer mutates the INPUT and asks whether the code survives.
# PokemonBattleJournal.Fuzz has TWO suites, selected by env var, run as separate
# processes so the slow one cannot starve the fast one:
#
#   (default)                  four pure string functions — ~15k exec/s, SATURATED (ft ~446)
#   PBJFUZZ_SUITE=importrestore  the import and restore FILE PARSERS — ~430 exec/s,
#                              nowhere near saturated (ft ~1150 and climbing). This is the
#                              real attack surface: files a stranger can hand the app.
#
# PBJFUZZ_CRASH_DIR is what actually preserves a reproducer. libFuzzer's own
# -artifact_prefix writes NOTHING through libfuzzer-dotnet — a managed exception kills
# the bridge before its crash handler runs — so a finding is reported and the input lost.
# PBJFUZZ_SELFTEST=1 makes every input throw, which is how the runner proves the
# preservation path before trusting a clean run. Both box slots do this automatically.
# These run the box's own runner, so they need bash (WSL or Git Bash), not PowerShell —
# and the toolchain (libfuzzer-dotnet, sharpfuzz) exists in WSL and on fuzz-box, not on Windows.
#   bash build/run-measurements.sh fuzz 300                 # fast suite
#   bash build/run-measurements.sh fuzz-importrestore 300   # parsers (SQLite per iteration)
```

**Solution file:** always use `PokemonBattleJournal.slnx`. Do not recreate `PokemonBattleJournal.sln`.

There is a second one, `DO-NOT-OPEN-IN-VS.LinuxMeasurementBox.slnx`, and the filename is the
whole warning. It exists because Stryker builds the containing solution before mutating and the
real solution cannot build on Linux: `UITests.Windows` needs `Microsoft.WindowsDesktop.App`
(NETSDK1073) and the app head's `net10.0-android` TFM needs the Android SDK (XA5300). It lists
Core, Scraper, Tests, IntegrationTests and Fuzz — **the app head is deliberately absent**, so it
builds as a ProjectReference at net10.0 alone rather than every TFM it declares. Add a new
`net10.0` project to both files. Note an `.slnx` is XML, so its comments cannot contain a doubled
hyphen; that is MSB4025 and the error names only a line and column.

## Architecture

MVVM app: `Views (XAML) → ViewModels → Services → ISqliteConnectionFactory → SQLite`

**See `docs/DECISIONS.md` entries 1–14 for the full architectural rules:** MVVM split (Core vs head), DI (singletons vs transient), DB session pairing, error handling, tracing, logging/Sentry redaction, services, import/export/restore, duplicate detection, win rate formula, custom controls, and explicit types.

**Quick summary:** Logic goes in Core unless it needs MAUI (Stryker cannot test the head). Singleton VMs hold state across navigations (need `[TearDown]` in tests). DB sessions acquire and release together inside try. Errors surface through injected `IErrorHandler`. Spans are named by constants; measurements are numeric. Sentry sees only types (numbers, enums, dates, guids) not strings.

## Accessibility standards

See `docs/DECISIONS.md` entry 15 for the full contract: automation IDs, screen reader labels, hint text, heading semantics, image descriptions, and the requirement that tappable elements are real controls (not Gestures on Borders/Grids). 

Windows enforces this via `MainPage_InteractiveElement_IsAnnouncedAndOperable` and its OptionsPage twin (live UIA tree checks). Android is not covered.


## TDD workflow

See `docs/DECISIONS.md` entry 16 for the full test conventions, including TDD order (failing test first), naming patterns, unit vs integration vs UI test scopes, and seed isolation. 

Quick rule: **write the failing test first, then write the code.** Unit tests live in `PokemonBattleJournal.Tests` (no database), integration tests in `IntegrationTests` (real SQLite), UI tests drive Appium on Windows and Android with element-visible and data-presence assertions.

## Explicit types, never `var`

See `docs/DECISIONS.md` entry 14. Types at the START of declarations. Target-typed `new()` on the right is fine. The objection is `var`, not brevity. Build warning enforced via `.editorconfig` and `EnforceCodeStyleInBuild`. IDE0008 is at zero; treat any new instance like any other warning.

## Test conventions

See `docs/DECISIONS.md` entry 16 for the full contract: TDD order, naming patterns, seed handling, retry patterns per platform, and diagnostic log locations. 

Key rules: Never `.Click()` on an element — use `ClickElement`/`TryClickIfPresent`/`ScrollIntoViewAndClick` which walk a UIA pattern ladder. Windows clicks fail by landing in the wrong place; Android by not dispatching. Patterns differ; fixes do not transfer. Seed failures throw, never silent. Windows tests `WipeAppData()` for a cold start. Diagnostic logs go to `%TEMP%\UITests.*.txt`.

## Platform notes

See `docs/DECISIONS.md` entry 17 for full details.

- **Windows:** unpackaged; debug exe at `bin\Debug\net10.0-windows10.0.19041.0\win-x64\PokemonBattleJournal.exe`
- **Android UI tests:** AVD `pixel_7_-_api_35`; `EnsureEmulatorRunning()` boots if absent
- **Android local:** deploy once from VS, rerun tests freely. Never `pm clear` a VS deployment (crashes on next launch)
- **Android Release:** `RunAOTCompilation=False`, `PublishTrimmed=False`
