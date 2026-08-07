---
name: project_accessibility_contract_tests
description: "18 Windows UI tests assert the a11y contract CLAUDE.md mandates: every interactive element has a Name AND exposes a UIA control pattern. Catches 'announced but unusable', which markup review cannot. Windows-only; Android is uncovered."
metadata:
  type: project
---

**Added 2026-08-07.** Windows UI suite 83 → 101. In `UITests.Windows/Views/{MainPage,OptionsPage}Tests.cs`.

## What they assert, and why both halves

Each case reads the live UIA tree via `GetUiaContract(automationId)` (Windows `BaseTest`) and
requires:

1. **A Name** — what a screen reader announces, sourced from `SemanticProperties.Description`.
2. **At least one control pattern** — Invoke, Toggle, SelectionItem, ExpandCollapse or Value.
   This is *how* assistive technology operates the element.

**Failing the second while passing the first is the whole point.** The BO3 switch carried
`Description`, `Hint` and `IsInAccessibleTree="True"`. It passed every review, was announced
correctly by a screen reader, and could not be activated by one — a `Border` with a
`TapGestureRecognizer` exposes no pattern. **Reading the markup can never catch that.** Only
reading the tree can.

## They are verified to discriminate

Pointed at `MatchFormatHeading` (a `Label`), the assertion fails with the reason: Name present,
no pattern. An assertion that has never been red proves nothing — see
[[feedback_test_the_hypothesis_first]].

## Why they live in the existing fixtures

Deliberately added to the **Windows partials of MainPageTests and OptionsPageTests**, not a new
fixture. CI's Windows matrix hardcodes five fixture names and selects by
`--filter FullyQualifiedName~<name>`; a sixth fixture would need adding to the workflow, and
`dotnet test --filter` matching nothing **exits 0 silently** — the suite would have looked green
while running none of them. See [[project_dotnet_test_filter_exits_zero]].

## Cost

Effectively free: no clicks, scrolls, waits or navigation, except one test that must open the
archetype popup because its items do not exist until then. Flake on these suites comes from
interactions, not from what a test inspects once the page is on screen.

## What they do NOT cover — do not overclaim

- **Windows only.** They read UIA. Android's `AccessibilityNodeInfo` tree is untested, so
  nothing here says anything about TalkBack.
- **Machine-readable contract only.** A `Description` of `"asdf"` passes. Whether the
  announcement is *useful* needs a human with Narrator.
- **Not keyboard reachability.** Tab order and Escape-to-close are separate from pattern
  exposure and remain unverified.
- **Known wording bug they cannot catch:** the popup item hint reads *"Double tap to select …"*,
  which is TalkBack phrasing. Narrator users press Enter or Space.

## Related

- [[feedback_invokable_controls]] — the rule these enforce, and the overlay recipe
- [[project_windows_mainpage_click_flake]] — the same defect class, found via automation instead
