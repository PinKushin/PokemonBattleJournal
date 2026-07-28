# Memory Index

- [ViewModel contract tests as AI guardrails](feedback_contract_tests.md) — Reflection-based tests pinning XAML binding names to ViewModel members; user's explicit strategy for constraining AI changes.
- [Check docs first before debugging library issues](feedback_check_docs_first.md) — Always verify official setup/installation docs before investigating mysterious third-party library failures.
- [UI test coverage for every Shell page](project_ui_test_coverage.md) — Every Shell page must have a navigation + element-visible Appium test; FindUIElement timeout catches page hangs.
- [App styling palette and font conventions](project_styling_palette.md) — PokeYellow headings (PokemonSolid), PokeBlue accents, SairaRegular body, PokeYellow input borders; all Shell pages now styled.
- [Engineering principles](feedback_engineering_principles.md) — DRY, SOLID, design patterns (factory/strategy/repository/command), extensibility, composition over inheritance, full accessibility (AutomationId + SemanticProperties on all elements), error surfacing (no silent catch), and test best practices.
- [Project roadmap](project_roadmap.md) — Planned features: JSON import/export (TrainerHill format with archetype slug resolution), deck maker (deck lists tied to archetypes), deck comparer (side-by-side diff).
- [Theme switcher goal](project_theme_switcher.md) — Long-term: in-app theme switcher; Android emulator defaults light; never hardcode colors.
- [Security standards](feedback_security.md) — Never introduce SQL injection, XSS, command injection, path traversal, or insecure deserialization. Verify before marking any task done that touches SQL, file I/O, HTTP clients, or user-supplied data.
- [ComboBox fix plan + test seeding](project_combobox_fix_plan.md) — Button overlay fix for Android clickability, XAML popup rewrite, CloseAsync(result) pattern, TestDbSeeder SQL approach, WinAppDriver NoSuchWindowException workaround, archetype click test plan.
