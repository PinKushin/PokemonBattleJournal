---
name: sonar-warnings-fixed
description: All SonarAnalyzer and Roslynator warnings resolved in commit dc45c19; suppression patterns and reasoning
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T00:59:57.081Z
---

Packages added: `SonarAnalyzer.CSharp` + `Roslynator.Analyzers` (analyzer-only, no runtime impact). All warnings resolved in commit `dc45c19`.

## Fixes applied

| Rule | Fix |
|------|-----|
| S112 | `throw new Exception` → `throw new InvalidOperationException` in all services |
| S3267 | foreach loops with `await` bodies or mutable accumulators: `#pragma warning disable S3267` with justification comment |
| S8969 | Null-forgiving `!` removed: `.Where(x => x != null).Select(x => x!)` → `.OfType<T>()`, `?? string.Empty` |
| S6562 | `new DateTime(ticks)` → `new DateTime(ticks, DateTimeKind.Utc)` |
| S6444 | `Regex.Replace` calls: added `TimeSpan.FromSeconds(1)` timeout parameter (prevents ReDos) |
| S6608 | `.First()` on indexable collection → `[0]` |
| S3168 | `async void` fire-and-forget in `TaskUtilities`: `[SuppressMessage]` with justification |
| S2068 | FluentUI icon font constants with "password" in name: `#pragma warning disable S2068` — icon identifiers, not credentials |
| S1450 | MAUI UI element private fields (ComboBoxControl, ImagePicker): `#pragma warning disable S1450` — must be fields, not locals, to survive GC when added to visual tree |
| S125 | Commented-out code: low-value noise, acceptable to ignore during development |

## Unit test fix included
`AppShellViewModel` constructor changed from 5 args to 3 (removed `ReadJournalPageViewModel` and `TrainerPageViewModel` params). `AppShellViewModelTests` and `OptionsPageViewModelTests` both updated.
