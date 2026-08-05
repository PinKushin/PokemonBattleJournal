---
name: user_no_signing_budget
description: User has no budget for paid code-signing certificates or paid developer services — plan releases around free options only
metadata:
  type: user
---

**The user has no money for code-signing certificates or paid developer services.** Stated
directly 2026-08-05 when the installer roadmap item came up: "i dont have money to get
signitures."

**Why it matters:** it is a hard constraint on release architecture, not a preference. Any
plan that assumes a commercial Authenticode certificate (several hundred USD/year, plus
hardware-token storage since 2023) is a non-starter and wastes the user's time.

**How to apply:** when release, distribution, or signing comes up, propose only free or
near-free paths, and say plainly which parts cost money so the user can decide rather than
discovering it late:

- Android signing is free by design (self-generated keystore) — no caveat needed.
- Windows: SignPath.io (free Authenticode for open source) is the best fit; Microsoft Store
  is the other no-warning route; unsigned-on-GitHub works but is hostile on Windows 11.
- Flag one-time fees explicitly (Google Play registration, Microsoft Store registration) —
  small is not the same as free, and the user should choose.

Same reasoning applies beyond signing: the user is on GitHub free tier with usage limits and
has no credit card attached, so CI minutes and any paid SaaS are also constrained. See
[[project_ci_workflows]] for how the CI budget shaped the workflow design.

Full analysis of the signing options, Mark of the Web, and Smart App Control lives in
[[project_roadmap]] under *Real Installer*.
