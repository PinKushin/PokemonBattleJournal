---
name: feedback_fact_check_the_user
description: "Verify the user's confident technical claims instead of building on them. They ask for it explicitly, and an unchecked wrong premise gets written into code and docs."
metadata:
  type: feedback
---

**Check confident assertions, including the user's.** User, 2026-08-05: *"make sure you fact
check me for things i say with certainty sometimes i am wrong."*

This is a standing instruction, not a one-off. Agreeing with a plausible-sounding premise is
the failure mode — it feels cooperative and it silently propagates into code comments, memory
files and roadmap entries, where the next session reads it as settled fact.

## The case that prompted it

The user said fixing the loading spinner's residual flicker would need DirectX, and that MAUI
cannot host DX because it is mobile-first and cross-platform-first.

Checking it:

- **Right, and decisive:** DX is Windows-only, so a DX fix is a Windows-only fix plus a
  parallel implementation per platform. Enough on its own to decline.
- **Wrong:** "MAUI is not made for it." MAUI's Windows backend is WinUI 3, which renders on
  DirectX, and `GraphicsView` draws through Win2D — a DX wrapper. The spinner is *already* on
  that stack, so a DX rewrite probably would not have fixed anything.
- **Missed entirely:** SkiaSharp is already a dependency and `.UseSkiaSharp()` is already
  called in `MauiProgram`. `SKShader.CreateSweepGradient` is the exact primitive the workaround
  imitates, cross-platform and GPU-accelerated.

The conclusion ("leave the flicker") survived. The stated reason did not, and I had already
written it into two documents as justification — including citing it as precedent against an
unrelated feature ([[feedback_platform_specific_is_fine]]).

## It cuts both ways — check your own inferences hardest

Later the same day I twice recorded conclusions about PTCG Live's battle logs that the user
had to correct, and both times the fault was the same: **inferring from a third party's
description instead of checking the artefact.**

From [replay.ptcgtools.com](https://replay.ptcgtools.com/en)'s setup text — *"Enable Windowed
Mode, the Tracker needs this to access log files"* — plus an uploaded replay named
`ptcgl_log_20260805_073547.txt`, I concluded Live writes timestamped battle-log files to disk,
and rewrote the feature design around reading them. The user said plainly it was clipboard
only. Checking the actual install settled it in one command: 21 `Game*.log` files, all Unity
exception traces, **zero** battle phrases; `Player.log` empty; nothing named `ptcgl_log_*`.
The filename was the Tracker's own naming on upload.

The lesson is not "trust the user over the evidence" — it is that a vendor's marketing and
setup copy is **not** evidence about someone else's file formats. The check cost one command
and would have cost nothing had it come first.

## Do not ABANDON a verified conclusion because someone asserts otherwise

The mirror image, and it cost more than the original error. 2026-08-10, comparing two
`tf2-core` mutation runs: one took 11m16s, one 33 minutes. Both logs on my screen said **957
mutants planned**, so I concluded the short one was incomplete. The user relayed from the other
agent that it had been a different test suite; I accepted it, stated publicly that my comparison
was invalid, and moved on.

It was not a different suite. The other agent's own accounting check settled it hours later:

```
20260810T093957Z-539389d   955 accounted, 957 planned  ->  GAP 2
20260810T092905Z-7294c4b   218 accounted, 957 planned  ->  GAP 739   (truncated)
```

Same suite, same config, and the short run had covered 218 of 957. My original read was right,
and I dropped it in favour of an assertion carrying no evidence — while the evidence that
refuted the assertion was already in front of me.

**An unevidenced correction is a claim, not a measurement.** It deserves the same check as the
claim it replaces, and the check is often free because the artefact is already open. The user's
own framing afterwards: information relayed between agents is a game of telephone, which is
exactly why the shared log requires quoting real output instead of describing it.

The asymmetry that makes this dangerous: agreeing with a correction *feels* like humility, so
nothing in the moment flags it as an error. Deference is not verification.

## How to apply

- When a claim would change what gets built or what gets recorded, verify before acting.
  Grep the repo, check the package references, read the API. Most checks cost one command.
- Distinguish the **conclusion** from the **reasoning**. Both need checking, and the
  conclusion can be right while the reason is wrong — which is the dangerous case, because
  nothing looks broken.
- Say plainly which parts hold and which do not. Do not soften a correction into agreement.
- State what was actually verified versus what is recalled API knowledge, and label the
  difference.

## Related

- [[feedback_test_the_hypothesis_first]] — the same discipline applied to diagnosis
- [[feedback_fix_stale_docs_immediately]] — why a wrong recorded reason has to be corrected
  the moment it is found
