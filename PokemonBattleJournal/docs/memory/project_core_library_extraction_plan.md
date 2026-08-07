---
name: project_core_library_extraction_plan
description: "DONE 2026-08-07. 46 files moved out of the MAUI head into PokemonBattleJournal.Core (plain net10.0) so Stryker can mutation-test them. Only 3 files were genuinely MAUI-coupled. Put new logic in Core unless it needs a platform."
metadata:
  type: project
---

**Done 2026-08-07**, in one commit with no behaviour change, exactly as planned.

## Why it existed

Stryker cannot mutation-test the MAUI head at all — its internal Roslyn recompile does not
reproduce XAML codegen or the MVVM source generators, and surfaces **no CS error** when it
fails ([[project_stryker_mutation_testing]]). Every branch in Services and Utilities was
therefore unmeasured, and the one project that *could* be measured turned up 14 tests that
could not fail on its first run ([[feedback_tests_that_cannot_fail]]).

**It was never for reuse.** Nothing consumes it as a package, and that was fine.

## What the split actually is

`PokemonBattleJournal.Core`, plain `net10.0`: Models, Services, Utilities, Interfaces,
Logging, and `Constants`. **Namespaces are unchanged**, so no `using` anywhere had to move and
a `using` never tells you which assembly a type is in.

Three files stayed in the head because they genuinely need a platform:

| File | What blocks it |
|---|---|
| `Services/ModalErrorHandler.cs` | `Shell`, `Application` |
| `Utilities/FileHelper.cs` | `FileSystem`, `DeviceInfo` |
| `Utilities/MainThreadHelper.cs` | `MainThread`, `DeviceInfo` |

An earlier estimate lumped SQLite-net in with MAUI and made this look far worse than it was.
SQLite-net is an ordinary NuGet package that works in a class library.

## The one thing that did not move cleanly — read this before adding a service

`Constants.DatabasePath` was built from `FileHelper.GetAppDataPath()`, so it reached MAUI.

Injecting a path was **not** workable: the six integration-test subclasses set their temp path
in *field initialisers*, which in C# run **before** any base-constructor call, so the value
cannot be passed up. Instead `SqliteConnectionFactory` is now **abstract** with an abstract
`GetDbPath()`, and the head answers it in the new `MauiSqliteConnectionFactory`. Those six
subclasses already overrode `GetDbPath()`, so they needed **no change at all**, and the
boundary became compiler-enforced rather than conventional. `Constants` kept
`DatabaseFilename` and `Flags`, which are pure.

**That is the pattern for anything similar.** New logic goes in Core; if it needs a platform
capability, take it as a constructor dependency or an abstract method and let the head answer
it. Adding it to the head instead means it is unmeasured by definition.

## Deliberate choices worth not re-litigating

- **Core mirrors the head's compilation settings, `ImplicitUsings` included.** Any error this
  build surfaced then came from the move, not from a settings difference. Aligning both to
  `ImplicitUsings=disable` per the global standard is a **separate commit**.
- **The SQLitePCLRaw security overrides are duplicated into Core on purpose.** Restore resolves
  per project, so the head's copies do not cover it and NU1903 reappears without them. They
  look like removable cruft and are not — [[project_sqlite_security_pins]].
- **No separate domain entities plus mapping.** The models carry SQLite attributes, so a purist
  would split persistence from domain. That solves swapping the database, a schema that
  disagrees with the object shape, or several consumers with different storage — **none of
  which exist here**. Agreed with the user 2026-08-07; do not revisit unopposed.

## Verification

Core clean, head clean, full solution clean apart from four pre-existing warnings in
`Platforms/iOS` and `Platforms/MacCatalyst` that the branch never touched and that a
`-f net10.0` build never compiles. Unit 523, integration 204.

Stryker now runs against Core via `dotnet stryker --config-file stryker-core.json` (both test
projects, since the operations services are covered by the integration suite and excluding it
would report false survivors).
