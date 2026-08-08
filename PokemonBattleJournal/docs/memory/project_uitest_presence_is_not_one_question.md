---
name: project_uitest_presence_is_not_one_question
description: "\"Is the element present\" has at least three different answers and every UI backend gives a different one. All 106 Windows tests encode WinAppDriver's answer, so swapping the backend silently rewrites 106 contracts. Contain WinAppDriver's bugs; do not replace its semantics."
metadata: 
  node_type: memory
  type: project
  originSessionId: 4938edd8-4dd8-4200-98f4-755f11ee9fd8
  modified: 2026-08-08T18:46:51.664Z
---

Learned 2026-08-08 by breaking the suite twice.

## The trap

WinAppDriver is unmaintained (last release 2023) and every Windows UI fix that has stuck moved
toward FlaUI/UIA — see [[project_game3tab_ci_flake_recurring]]. That makes "replace the
WinAppDriver call with FlaUI" look obviously right. It is not, for presence checks.

**"Present" is at least three questions:**

| Backend / predicate | Answers | Result when used for `IsElementPresentCore` |
|---|---|---|
| FlaUI `FindFirstDescendant` | is it in the UIA tree | **1 failure** |
| FlaUI + `!IsOffscreen` | in the tree AND on screen | **5 failures** |
| WinAppDriver `FindElements` | its own filtered notion | **106/106** |

- **Tree existence** fails because MAUI keeps `CollectionView` items *realised* after a search
  filters them out. `ArchetypeItem_Other` still EXISTS after typing a non-matching query, so
  `MainPage_ArchetypePicker_Search_FiltersResults` flipped to failing. The test was right.
- **`!IsOffscreen`** fails the other way: the BO3 panels report `IsOffscreen` while
  WinAppDriver considers them present, so the BO3 state helpers stopped believing a panel had
  appeared. Broke `BO3GameTabs`, `BOSwitch`, both `Game3Tab` tests.

**Every test encodes the backend's answer.** Changing `IsElementPresentCore` is not a
refactor; it rewrites the contract of every test that calls it.

## What to do instead

The CI failure that started this was never a wrong answer — it was an **exception**:

```
System.InvalidOperationException : The specified element ID is either null or the empty string.
  at OpenQA.Selenium.WebElementFactory.GetElementId(...)
```

WinAppDriver returned an element with no id. So: keep asking WinAppDriver, retry once, and
fall back to the UIA tree ONLY in the error path (`87da523`). Containment, not replacement.

`IsVisibleViaUIA` answers *existence* and is correctly used by `FindUIElement`'s fallback,
where WinAppDriver simply has not caught up yet. Do not point presence assertions at it.

## The real fix is a driver, not a rewrite

The user is building **WindowsDriverCore** — a WinAppDriver replacement that implements the
WinAppDriver API (so it can pass the WinAppDriver test suite) while converting calls into UIA,
raw COM and HWND underneath, with some FlaUI surface exposed too.

That is the correct shape, and this episode is the evidence: a driver answering the SAME
questions correctly needs zero test changes, while a driver answering DIFFERENT questions
correctly needs all of them re-decided. Prefer driver-agnostic helpers (`IsElementPresent`)
over raw `App.FindElement` in test bodies so the eventual swap is one method.

## Related

- [[project_game3tab_ci_flake_recurring]] — where moving TOWARD FlaUI was right
- [[feedback_tests_that_cannot_fail]] — sensitivity is not validity
