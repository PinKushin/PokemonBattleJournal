---
name: feedback_engineering_principles
description: "User's core engineering standards — DRY, SOLID, design patterns, extensibility, composition, accessibility, and best practices. Apply to all code produced in this project."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T19:42:47.257Z
---

Apply every principle below to all code written in this project. These are not optional polish — they are baseline expectations.

**Why:** User explicitly requested these as permanent standards, not one-off guidance. They want code that is maintainable, extensible, accessible, and architected for the long term.

**How to apply:** Evaluate every change against each principle before considering it done. If a shortcut would violate one of these, don't take it — fix the design instead.

---

## DRY (Don't Repeat Yourself)

Extract shared logic the moment it appears a second time. No copy-pasted blocks, no duplicated validation, no parallel switch statements that must be kept in sync. Shared behavior lives in one place — a base class, a helper, a service method, or a utility function.

Examples already in this codebase: `Calculations.cs` for win-rate formula; `ModalErrorHandler` for error handling; `BaseTest` for Appium helpers.

## SOLID

- **Single Responsibility:** Each class/method does one thing. `SqliteConnectionFactory` owns DB init; operation classes (`MatchOperations`, `TrainerOperations`, etc.) own their entity's CRUD — don't blur those boundaries.
- **Open/Closed:** Extend behavior by adding new types/implementations, not by editing existing ones. `IMatchResultCalculator` / factory pattern already demonstrates this — adding BO5 means a new class, not a modified switch.
- **Liskov Substitution:** Derived types and interface implementations must be substitutable without the caller knowing. Don't implement interfaces partially or throw `NotImplementedException`.
- **Interface Segregation:** Keep interfaces narrow. A service that reads should not be forced to implement write methods.
- **Dependency Inversion:** Depend on abstractions, not concretions. All services injected via DI through interfaces. New dependencies go through `MauiProgram.cs`, not `new SomeService()`.

## Design Patterns

Use the right pattern when it eliminates a class of future pain:

- **Factory:** `MatchResultCalculatorFactory` already exists — follow this model for any "select an implementation based on a condition" scenario.
- **Strategy:** Swap algorithms at runtime via an interface. Use when behavior varies by context (e.g., different stat calculators per format).
- **Repository:** `MatchOperations`, `TrainerOperations` etc. are repositories — keep them that way. Don't put SQL directly in ViewModels.
- **Observer/Event:** Use `[ObservableProperty]` + `INotifyPropertyChanged` (CommunityToolkit MVVM) for all ViewModel→View data flow. Don't poll state.
- **Command:** All user actions go through `[RelayCommand]`-decorated methods. No code-behind event handlers that call ViewModel methods directly.
- **Template Method:** Put invariant steps in a base method, let subclasses fill in the variant parts — good for test base classes and service base classes.

## Extensibility

Design so new features are additions, not rewrites:
- New archetype types: no code change needed, just a DB row.
- New match formats (BO5): new `IMatchResultCalculator` implementation + factory registration.
- New chart: new ViewModel property + new XAML section — existing charts untouched.
- New Shell page: register in `MauiProgram.cs` as transient, add `FlyoutItem` in `AppShell.xaml`, add UI test.

Flag when a proposed design closes off future extensibility and offer an open alternative.

## Composition over Inheritance

Prefer injecting collaborators over subclassing them. Inherit only for true is-a relationships (e.g., `BaseTest` for shared test infrastructure). Services compose smaller services; ViewModels compose services.

## Accessibility

Every interactive and informational UI element must have:
- `AutomationId` — stable, unique, kebab-case or PascalCase identifier (used by Appium and screen readers)
- `SemanticProperties.Description` — plain-English label read by TalkBack/Narrator
- `SemanticProperties.Hint` on tappable non-button elements — "Double tap to …"
- `SemanticProperties.HeadingLevel` on section headers
- Images: meaningful `SemanticProperties.Description` (e.g., `"{Name} deck icon"`); purely decorative images get `SemanticProperties.IsInAccessibleTree="False"`
- Popup/overlay items (e.g., `ComboBoxPopup`) need AutomationIds on their item containers so automation AND screen readers can reach them
- Errors must be surfaced — no silent `catch {}` that hides failures from logging

## Error Handling

- All errors surface through `ModalErrorHandler.HandleError` in services and ViewModels.
- `catch {}` and `catch (Exception) { }` with no logging are banned. Every catch either rethrows, logs to `Console.Error` (test infra), or calls `ModalErrorHandler`.
- Test setup exceptions (seed failures, wipe failures) must throw or log — never swallow silently.
- `WaitForExit(int)` return value must always be checked; reading `ExitCode` on a still-running process throws.

## TDD (Test-Driven Development)

Write failing tests first for anything new — always.

Order:
1. Write the test. Run it. Confirm it fails for the right reason (not a compile error — the actual missing behaviour).
2. Write the minimum code to make it pass.
3. Refactor. Tests stay green.

In this project:
- New service method → unit test in `PokemonBattleJournal.Tests` first.
- New ViewModel command → unit test asserting expected state change first.
- New Shell page → Appium navigation + element-visible test before the page exists.
- New seed assertion → data-presence test before the seed logic.
- Bug fix → regression test reproducing the bug, confirmed failing, before the fix.

**Why:** User explicitly requires TDD. A test that was never red proves nothing.

## AI Memory Upkeep

Keep `docs/memory/` and the auto-memory directory in sync throughout every session — not just at the end:
- When a user corrects an approach, confirms a decision, or states a preference: write a memory file immediately.
- When a bug's root cause reveals a non-obvious constraint or project quirk: capture it.
- When architecture decisions are made (new service, new pattern, new page): add a project memory entry.
- Always update `MEMORY.md` index when adding or changing a memory file.
- Copy changed memory files to both `docs/memory/` (for the repo) and the auto-memory dir (`C:\Users\pinku\.claude\projects\...\memory\`) so both locations stay in sync.
- Memory is medium-term context for future sessions — write it as if briefing a new AI instance who has read the code but not this conversation.

**Why:** Without current memory, each session re-derives the same context from scratch, wastes time, and risks repeating past mistakes.

## Best Practices

- No `new ConcreteService()` inside classes — use DI.
- No hardcoded strings that appear more than once — extract to constants or resources.
- No `Thread.Sleep` in production code; in test seeds, keep sleeps minimal and document why.
- DB operations always acquire the `SemaphoreSlim` in `SqliteConnectionFactory`.
- Unit tests: `{Class}Tests` / `{Method}_{Scenario}_{Expected}` naming, NSubstitute mocks, Shouldly assertions.
- UI tests: every Shell page needs navigation + element-visible Appium test; seed data must be verified by a data-presence assertion test (not just "button exists").
- No `Task.Delay` / `Thread.Sleep` in tests unless waiting for an async render — and document it with why.
