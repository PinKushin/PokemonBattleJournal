---
name: feedback-integration-tests-project
description: PokemonBattleJournal.IntegrationTests is a separate project that must be updated alongside PokemonBattleJournal.Tests
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T13:26:37.220Z
---

There are TWO test projects that both need updating when production APIs change:

- `PokemonBattleJournal.Tests/` — unit + integration tests (NUnit, NSubstitute mocks)
- `PokemonBattleJournal.IntegrationTests/` — real DB integration tests (NUnit, no mocks)

**Why:** CI runs both. Missed `PokemonBattleJournal.IntegrationTests` caused CI failure even though local unit tests passed.

**How to apply:** After any API signature change (model properties, service method signatures), grep BOTH projects. Run `dotnet test PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj --filter "Category!=LiveWeb"` locally before pushing. The `LiveWeb` category hits real Limitless CDN and is excluded from normal CI runs.
