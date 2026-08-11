---
name: project_pbj_is_the_dogfood_target
description: "PBJ app code is quiet on purpose — it is the dogfood consumer for the user's own WindowsDriverCore and TcgDex.CSharpSdk. Do not read the lack of feature commits as the project stalling."
metadata:
  type: project
---

**Stated by the user 2026-08-11:** *"the main reason i havent been working in the pbj codebase, is
because i kinda want to 'eat my own dogfood' with this project, so im trying to get winappdriver
done now since i have tcgdex ready to use here."*

PBJ is deliberately the **consumer** of two of the user's own projects, both siblings in
`C:\Users\pinku\source\repos\PinKushin\`:

| Project | Role in PBJ |
|---|---|
| `WindowsDriverCore` | replacement for WinAppDriver, to drive PBJ's Windows UI suite |
| `TcgDex.CSharpSdk` | the deck builder on the roadmap ([[project_roadmap]]) |

So a stretch of PBJ commits that are all docs, tests and infrastructure is **the plan working**,
not the project stalling. On 2026-08-10 the staleness check read "0 code" commits behind for PBJ
across a whole day of work; that is why.

## What this means when WindowsDriverCore arrives here

**PBJ's Windows suite is the acceptance test, and it is a good one.** 83 tests, reliable at CI's
754x512 geometry, hard-won over the click-flake investigation ([[project_windows_mainpage_click_flake]]).
A suite that stable turns a post-swap difference into evidence about the DRIVER rather than noise
about the tests. The 18 accessibility contract tests are the sharpest part: they read the live UIA
tree and demand both a Name and a control pattern, so a driver that surfaces elements differently
fails them loudly ([[project_accessibility_contract_tests]]).

**The risk is a shared helper quietly changing MEANING, and it has already happened once.** A
presence check swapped from one UI backend to another passed its sabotage check correctly while
silently changing the question from "is it visible" to "is it in the tree", flipping a real test for
a reason no targeted check could see ([[feedback_flaui_scroll_into_view]]).

So when the driver is swapped: **the whole suite is the experiment, not the one test in mind.** Run
the full Windows suite at CI geometry before and after and compare the SET of passing tests, not the
count. A swap that still reports 83/83 while changing *which* 83 is the failure this misses.

## Related

- [[project_roadmap]] — deck builder is the TcgDex consumer
- [[feedback_dont_churn_stable_ci]] — the UI suites are hard-won; change them for measured reasons
