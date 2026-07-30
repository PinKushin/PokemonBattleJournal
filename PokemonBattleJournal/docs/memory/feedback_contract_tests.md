---
name: feedback_contract_tests
description: "User wants ViewModel contract tests (reflection-based) that pin XAML binding names to ViewModel properties/commands, constraining AI from silently breaking bindings."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-25T13:13:52.386Z
---

Use reflection-based contract tests in the unit test project to verify every XAML-bound property and command exists on the ViewModel. This acts as a specification that breaks when an AI renames or removes a bound member.

**Why:** XAML bindings without `x:DataType` are runtime-only and won't fail at compile time. Even compiled bindings don't protect against renaming in unit tests. The user explicitly wants these as AI guardrails — "tests that constrain an AI that reads them."

**How to apply:** For every page ViewModel, add a `{ViewModel}ContractTests.cs` file in `PokemonBattleJournal.Tests/ViewModels/` with `[Theory][InlineData("PropertyName")]` tests using reflection (`typeof(VM).GetProperty(name).ShouldNotBeNull()`). Cover both properties and commands. Derive the list from XAML `{Binding X}` and `Command={Binding X}` attributes.
