---
name: project_release_xaml_broken
description: "Debug does not fully compile XAML, so Release-only XamlC errors hide indefinitely. Release Android was uncompilable from the Core extraction until 2026-08-11, and a static property bound with {Binding} silently left three debug-only buttons visible."
metadata:
  type: project
---

**Found 2026-08-11 while testing whether `RunAOTCompilation` works.** It does not, for an unrelated
reason — `XA1030`: it requires `PublishTrimmed`, which is why both sit `False` together in the
csproj and is almost certainly the error the user hit years ago. But the build that proved it
surfaced two real bugs that had nothing to do with AOT.

## Build Release before believing anything about XAML

**Debug does not fully compile XAML. Release does.** So a XamlC error can sit in the repo forever
while every day-to-day build and every CI job passes. Both bugs below lived behind that gap.

```powershell
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -c Release -f net10.0-android
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -c Release -f net10.0-windows10.0.19041.0
```

## 1. Model namespaces need `;assembly=PokemonBattleJournal.Core`

Four XAML files declared `clr-namespace:PokemonBattleJournal.Models` with no assembly qualifier, so
the resolver looked in the app head. `Trainer`, `Archetype` and `Tags` moved to Core during the
extraction ([[project_core_library_extraction_plan]]) and had been unresolvable ever since:

```
XamlC error XC0000: Cannot resolve type "clr-namespace:PokemonBattleJournal.Models:Trainer"
```

Confirmed pre-existing by building Release with no AOT and no trimming — identical errors. **Any
XAML naming a Core type needs the assembly qualifier**; the head's own types do not.

## 2. `{Binding}` cannot bind a STATIC property — it failed open

Fixing the namespace let XamlC resolve those types for the first time, which let it validate
bindings it had been skipping. It immediately reported `IsDebugBuild` as not found on
`OptionsPageViewModel`, six times.

The property exists. It is `public static bool`, and `{Binding}` resolves against the instance.
**A failed binding leaves the target at its default, and `IsVisible` defaults to `true`** — so all
three "Debug only" buttons would have appeared in Release: the loading-gate toggle, the
sample-conflict seeder, and *"Send Sentry test event"*, which fires a trace and an error at Sentry
in a project whose hard constraint is no telemetry ([[user_no_server_no_user_data]]).

Correct form for a static, and it needs no change notification since the value is fixed per
configuration:

```xml
IsVisible="{x:Static viewmodel:OptionsPageViewModel.IsDebugBuild}"
```

Debug behaviour is identical before and after — the buttons were visible because the binding
failed, and are visible because the static is true.

## The shape worth remembering

**Two failures covering for each other.** The guard that hid the debug buttons was broken, and the
compiler pass that would have reported it was disabled by an unrelated bug in the same file set.
Neither was visible while the other stood. That is why "it builds and the tests pass" was true and
meaningless here.

## Known-remaining warnings, deliberately not fixed

Three pairs of `XC0045` on `BindingContext` against `Trainer`, `Tags` and `Archetype`. Those
bindings carry an explicit `Source={x:Reference BasePage}` so they resolve against the page at
runtime, while XamlC checks the path against the ambient `x:DataType`. Making them compile-time
clean needs a typed accessor on the page rather than a suppression. Also 22 `XC0022` (bindings
without `x:DataType`) — performance, not correctness.
