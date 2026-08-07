---
name: project_roadmap
description: "Planned features and product goals for PokemonBattleJournal — import/export, deck tools, and other roadmap items."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T20:15:18.066Z
---

Planned features confirmed by the user. Implement via TDD — write failing tests first.

**Why:** User stated these goals explicitly during sessions. They should drive future feature work and architecture decisions.

**How to apply:** When starting any new feature work, check this list. Prefer designs that leave room for these features even if not implementing them yet.

---

## Extract a Core class library — VERY STRONG CANDIDATE, next up (added 2026-08-07)

Move `Models/`, `Services/` and `Utilities/` into a plain `net10.0` library the MAUI app
references. **The user is leaning towards doing this next.**

**The point is mutation testing.** Stryker cannot analyse the MAUI project at all, so
"tests that pass but cannot fail" are invisible in exactly the code where a wrong answer would
damage a user's data — `MatchAnalysisService`, `RestoreService`, the import limits. The one part
of the repo Stryker *could* measure turned up 14 such tests on the first run, and every one had
been passing.

**Measured, not estimated:** of 36 candidate files, **only 3** actually depend on MAUI
(`ModalErrorHandler`, `FileHelper`, `MainThreadHelper`). Everything else depends on SQLite-net,
which is fine in a class library.

Full step-by-step, risks and verification order: [[project_core_library_extraction_plan]].

**Not for reuse** — nobody will consume it as a package, and that is not the reason. Rejected as
part of this: splitting into separate domain entities with mapping. The models carry SQLite
attributes and a purist would separate them, but that solves problems this app does not have and
doubles the type count.

---

## Import / Export (JSON)

Format reverse-engineered from `trainerhill-battle-log-2026-07-27.json`:

```json
[
  {
    "playing": "archetype-slug",
    "against": "archetype-slug",
    "time": "2026-07-27 19:45:24.403684",
    "result": "Win|Loss|Tie",
    "game1": { "result": "Win|Loss|Tie", "turn": 1, "tags": ["..."], "notes": "..." },
    "game2": { ... },  // BO3 only
    "game3": { ... }   // BO3 only, split result
  }
]
```

Key mapping notes:
- `playing` / `against` are archetype name slugs — resolve to `Archetype` DB rows by name (case-insensitive slug match), create on import if absent
- `turn` is int OR string in the wild — coerce to `uint` (1 = went first, 2 = went second)
- `result` at match level is the overall result; game-level results drive BO3 calculation
- `tags` are tag names — resolve to `Tags` DB rows, create on import if absent
- `time` maps to `DatePlayed` + `StartTime`

Implementation plan (TDD):
1. `ImportService` — parses JSON array, resolves archetypes/tags, calls `MatchOperations.SaveAsync`
2. `ExportService` — queries `MatchOperations`, serializes to same JSON shape
3. Unit tests for both services with mock DB operations
4. OptionsPage: "Import" button (`FilePicker.PickAsync` → JSON file) + "Export" button (`FileSaver.SaveAsync`)
5. Both services injected via DI; no SQL in the services directly

### Import hardening — size / depth / count limits (added 2026-08-05, NOT started)

**The import currently has no limits of any kind.** `TrainerHillImportService.ImportAsync`
calls `JsonSerializer.DeserializeAsync<List<TrainerHillEntry>>` straight onto a user-picked
stream — no byte cap, no `MaxDepth`, no entry-count cap, no string-length cap. Found during a
security pass 2026-08-05.

The threat model is mild (the user picks their own file), but the realistic case is someone
sharing a "TrainerHill export" that is hostile or simply enormous: the whole file is
materialised into a `List<TrainerHillEntry>` before a single entry is validated, so a large
or deeply nested document can OOM the app or wedge it mid-import. It also contradicts the
project's own standard in `CLAUDE.md`: *"Validate and constrain all imported data (JSON, XML)
before it touches the DB. Reject unknown fields; coerce types explicitly."*

What to add, cheapest first:

1. **Byte cap before parsing** — reject the stream above some ceiling (a few MB is generous;
   the reference export of a full battle log is tiny). Check `Stream.Length` when seekable,
   otherwise read through a counting wrapper.
2. **`JsonSerializerOptions.MaxDepth`** — the shape is 3 levels deep (array ▸ entry ▸ game),
   so a small limit costs nothing and kills nesting attacks outright.
3. **Entry-count cap**, surfaced through the existing per-entry `errors` list rather than an
   exception, so partial imports still report usefully.
4. **String-length guards** on `notes`, `playing`, `against` and tag names before they reach
   `ResolveArchetypeAsync` / `ResolveTagAsync` — these create DB rows, so an unbounded name
   is a persistent junk row, not just a transient parse cost.
5. Consider `JsonSerializer.DeserializeAsyncEnumerable` to stream entries instead of
   materialising the whole array — this makes the byte cap much less load-bearing and is the
   real fix if imports are ever expected to be large.

TDD: the failing tests are easy to write first here — an oversized stream, a deeply nested
document, and an over-long note each get a test asserting the import is refused (or truncated
with a collected error) and that **nothing was written to the DB**.

Do this with, or before, the export work — the two share the format and it is the natural
moment to pin down what the parser will and will not accept.

### Backup restore + duplicate handling — SERVICE DONE 2026-08-06, UI NOT STARTED

**Status lives in [[project_backup_restore]]** — read that first. Summary: the export fidelity
fixes, `RestoreService`, and TrainerHill de-duplication are all built and merged. What remains
is the OptionsPage wiring and, separately, the conflict-resolution UI.

The design below is what was built, and is kept because the *reasoning* still governs the
remaining work — particularly why a key hit may never delete or overwrite.

**One correction to it:** the "fix the export first" section called out a single missing
`startTime`/`endTime`. There were three fidelity defects, not one — the `time` field also
carried the wrong source value, and archetype icons were not exported at all. See
[[project_backup_restore]].

#### Original design (agreed with the user 2026-08-05)

**Trainer targeting**

- **Full backup restore:** trainers come from the file. **Merge into an existing trainer of the
  same name** rather than creating a second one.
- **TrainerHill import:** entries go to whoever is importing. Verified 2026-08-05 — already the
  behaviour, `ImportFromTrainerHillAsync` passes `_trainer.Id` (the active trainer).

**There is no duplicate detection today, at all.** Verified: importing the same log twice
inserts every match twice. So this is not only a restore problem — it already affects
TrainerHill import, and fixing it once fixes both.

**Key on `StartTime`, not `DatePlayed`.** Corrected 2026-08-05 — an earlier version of this
note claimed app-created matches only carry date precision, which is wrong. `MainPageViewModel`
records both a start and an end time and stores them as full timestamps:

```csharp
StartTime = DatePlayed.Date + StartTime,   // date + time-of-day from the picker
EndTime   = DatePlayed.Date + EndTime,
```

So every match has a time of day regardless of source:

- **TrainerHill entries** — sub-second (`"2026-07-27 19:45:24.403684"`), and the importer sets
  `StartTime`/`EndTime` from it. Collisions between genuinely different matches are effectively
  impossible.
- **App-created** — minute precision from the time picker, defaulting to the current time, so
  consecutive entries naturally differ. Two matches colliding needs the same matchup, the same
  day *and* the same start minute.

`DatePlayed` is the weak field (a date picker leaves it at midnight), so it is the wrong thing
to key on. Use:

`(TrainerId, StartTime, PlayingId, AgainstId, Result)`

with `EndTime` available as a further discriminator, since duration differs between matches
that somehow share a start minute. That is strong enough to act on for both sources, rather
than only for imported rows.

**Fix the export first — the backup format is not as lossless as claimed.** Found 2026-08-05
while designing this, in the export shipped the same day:

- `ExportEntry` has a single `time` field and **no `startTime`/`endTime`**, so a restore loses
  `EndTime` outright. Not cosmetic: `EndTime` feeds `CalculateAverageMatchDuration` and
  `CalculateWinRateByMatchLength`, so restoring a backup would silently corrupt two stats.
- `ExportService` writes `Time = match.DatePlayed` — the *weak* field, midnight whenever the
  date came from a picker — instead of `StartTime`, which carries the actual time of day.

TrainerHill's schema genuinely only has one `time` field, so that export stays single-valued;
write `StartTime` into it rather than `DatePlayed`, since it holds the same date plus real
precision. **The backup envelope has no such constraint and should carry `startTime` and
`endTime` explicitly** — being lossless is its entire reason for existing
([[project_db_session_lock_pairing]] is unrelated; see the export section above).

Do this before the restore, not after: a restore built against the current format would bake
in the data loss and every backup taken meanwhile would already be missing durations.

**Overlapping matches: warn, do not block** (discussed 2026-08-05, no code yet)

The question raised was whether two matches should be allowed to overlap in time, since a
player cannot legally play two tournament games at once. Two reasons not to make it a hard
rule, one of them checked:

- **Overlap is legitimately possible.** Locals, or PTCG Live on a phone and a PC at once, or
  in-person plus phone. A hard rule means someone with a real overlap cannot log a match at
  all, and the only workaround is falsifying the time. Refusing real data is the worse failure.
- **Overlap does not currently harm the stats page** — verified against
  `MatchAnalysisService`. Nothing plots individual matches on a time axis; every chart groups
  by `DatePlayed.Date` or uses per-match duration (`EndTime - StartTime`). Two matches at the
  same instant simply both count toward that day. This would only become a problem if a future
  chart laid matches out on a timeline.

**But it DOES threaten duplicate detection, and the key cannot fully solve it.** An earlier
version of this note claimed simultaneous matches would be against different opponents and so
could not collide. That is wrong (user, 2026-08-05): **`AgainstId` identifies a deck, not a
person.** The model records no opponent identity at all. Two different people both playing
Dragapult produce an identical key, and mirror matches make it likelier still.

So `(TrainerId, StartTime, PlayingId, AgainstId, Result)` can match two legitimately distinct
matches. And if two real matches also share notes and tags, **nothing in the stored data
distinguishes them from a duplicate** — the model simply does not capture enough. Any silent
auto-merge would delete a real match.

Resolution: **never silently drop an exact-identical candidate.** Skip it by default, but
report the count ("12 entries already present, skipped") and offer an "import anyway" path, so
the user decides and a genuine identical pair stays recoverable. This keeps the common case
(re-importing an overlapping log) quiet without ever destroying data the app cannot tell apart.

**Recording an opponent name would NOT reliably fix this** — considered and rejected (user,
2026-08-05). Online you do not pay attention to screen names, so the field would usually be
blank or wrong; it might have some value for in-person play, but it cannot be relied on, and
it is not even clear the username appears in PTCG Live battle logs. A dedupe rule resting on a
field that is usually empty is worse than no rule, because it looks authoritative.

So the ambiguity is inherent: **accept it and let the user decide**, per the reporting rule
above. Do not add a field to chase it.

So: a soft, inline warning when a new match overlaps an existing one — a natural first consumer
of the **Inline validation feedback** item below, and specifically not a modal
([[feedback_no_silent_guards]] for the logging rule, and the standing objection to dialogs in
automation).

**What to do with a candidate (user's preference, best first)**

1. **Identical in every compared field** — skip, but **count and report it**, and offer an
   "import anyway" path. Not silent: the app cannot tell a re-import from two genuinely
   identical matches (see the opponent-identity gap above), so the user gets the final say
   rather than losing a real match to a guess.
2. **One side strictly richer** — one has tags and the other does not, one has a note and the
   other does not. **Merge**: union the tags, take the non-empty note. Strictly better than
   asking, and the user explicitly preferred merging where possible.
3. **Genuine conflict** — both sides have different non-empty values for the same field. **Ask
   which to keep.** Do not silently pick.

Note the modal constraint: the user has a standing objection to modals in automation
([[project_error_handler_di]]). A conflict prompt during a bulk restore would also be
miserable — batch the conflicts and resolve them in one pass rather than one dialog per match.

**Reuses the existing import hardening.** Size, depth, entry-count and name-length caps already
run before any DB write; the envelope path goes through the same parser, so it inherits them.

### Export — two modes

**TrainerHill export (per-trainer):**
- Output same JSON shape as the import format above (single trainer's matches only)
- TrainerHill has no multi-profile support — it stores everything in browser cookies per account — so export is always scoped to one trainer
- OptionsPage: "Export to TrainerHill format" exports the *active* trainer's matches
- Filename suggestion: `trainerhill-battle-log-{TrainerName}-{date}.json`

**Full backup export:**
- User chooses: all trainers or a single trainer
- JSON envelope wraps multiple trainer exports: `{ "trainers": [ { "name": "...", "matches": [...] } ] }`
- OptionsPage: "Export backup" with a picker or radio for "All trainers" vs "Active trainer only"
- Filename suggestion: `pbj-backup-{date}.json` or `pbj-backup-{TrainerName}-{date}.json`
- Backup format should be importable back in as a restore (import service reads both flat array and backup envelope)

## PTCG Live battle log parsing (wanted, user 2026-08-05, not started)

Parse Pokémon TCG Live's own battle logs so matches from Live upload trivially, instead of
being typed in by hand. Stated as a want rather than a scheduled item.

Why it fits well: the app already has an import pipeline with the hard parts solved —
size/depth/count/length limits enforced before any DB write, get-or-create for archetypes and
tags, per-entry error collection. A Live parser is another front end onto
`MatchOperations.SaveAsync`, most likely alongside `TrainerHillImportService` under
`Services/Import/`.

### CURRENT format, captured from a live match 2026-08-06

**Real log from the current client saved at
`PokemonBattleJournal/docs/samples/ptcgl-battle-log-2026-08-06.txt`.** Card IDs enabled. This
is the authority; everything in the older section below is superseded where they disagree.

**The format HAS changed since the 2025 samples — exactly as the user warned.**

| | 2025 samples | 2026-08-06 capture |
|---|---|---|
| Turn header | `Turn # 1 - Shinwrld's Turn` | `AradJohn's Turn` — **no turn number at all** |
| Cards | `Dreepy` | `(sv6_128) Dreepy` — id prefixed, with card IDs enabled |
| End line | `All Prize cards taken. gklinsing wins.` | `Opponent conceded. AradJohn wins.` |

So a parser must **count turn headers** rather than read a number from them, and the end-of-game
prefix varies by win condition. The stable marker is the `<name> wins.` suffix; the sentence
before it gives the reason (prizes taken, concession, presumably deck-out).

**Card ID shape:** `(setcode_number)`, where the set code may contain digits and hyphens and the
number may carry a variant suffix — observed `(sv6_128)`, `(mee_5)`, `(svbsp_129)`,
`(me2-5_34_ph)`, `(sv8-5_80_mph)`, `(sv5_156_ph)`. A regex must not assume `[a-z]+_[0-9]+`.

**The log owner is identifiable from the log itself — no username setting needed.** This
removes a requirement recorded earlier. The logging player's information is revealed and the
opponent's is not:

- Opening hand: the owner's is itemised in a bullet list; the opponent gets only
  `- 7 drawn cards.`
- Draws: the owner's are named (`AradJohn drew (me3_71) Crushing Hammer.`); the opponent's are
  anonymous (`KiokiYuudoku drew a card.`).

The named-draw test is the more robust of the two, since it recurs throughout the match rather
than appearing once at setup.

**Archetype inference looks tractable from this sample.** The owner's deck resolves from cards
played — `(sv6_130) Dragapult ex` with Drakloak, Dreepy and Munkidori — and the opponent's from
what they revealed, here Salazzle ex, Pecharunt and Meowth ex. Note the asymmetry: the
opponent's archetype is only as good as what they happened to play, so a concession on turn one
may leave it unidentifiable. Plan for "unknown" as a legitimate outcome rather than forcing a
guess.

**Still no timestamp**, confirmed on the current format.

**A "Pokémon Checkup" block appears between turns** for between-turn effects (poison damage,
knockouts from status). It is not a player turn and must not be counted as one.

### The 2025 samples — SUPERSEDED, kept for contrast

**Do not treat what follows as a specification.** Live's log format has been changed before,
sometimes silently (user, 2026-08-05), and the samples below come from a repository last
pushed **2025-04-15** — well over a year old. They are a starting point for shape, not a
contract.

**First task of this feature is to play a couple of matches on the current client and capture
fresh logs**, then diff them against this. Anything below that no longer matches is wrong, and
building the parser against stale samples wastes the work twice: once writing it, once
debugging why real logs do not parse.

Two things already known to differ from the old samples, from
[replay.ptcgtools.com](https://replay.ptcgtools.com/en), a live tool tracking the current
format:

- **The modern client has a "HIDE CARD IDS FROM EXPORT" setting**, which that tool requires you
  to disable. So current logs can carry **card IDs**, which the 2025 samples do not show at
  all. That matters a lot: matching card IDs is far more reliable than matching printed card
  names for inferring an archetype, and it may turn the "confirm the deck" step below into
  something closer to a lookup.
- **English logs only.** That tool supports no other language, which implies the sentence
  patterns are localised. Whatever we build inherits the same constraint — worth stating in the
  UI rather than failing mysteriously on a non-English log.

Logs are copyable straight from the client on **both PC and mobile**, so no file export path is
needed — paste is enough, which suits a MAUI app on either target.

With those caveats, the shape observed in the (old) samples from
[kagd/pokemon-tcg-battle-replay](https://github.com/kagd/pokemon-tcg-battle-replay):

**It is plain prose text with rigidly consistent sentences**, not JSON:

```
Setup
Shinwrld chose heads for the opening coin flip.
Shinwrld won the coin toss.
Shinwrld decided to go first.
gklinsing drew 7 cards for the opening hand.
   • Raikou V, Ultra Ball, Prime Catcher, Basic Lightning Energy, ...

Turn # 1 - Shinwrld's Turn
Shinwrld attached Basic Psychic Energy to Drifloon in the Active Spot.
Shinwrld ended their turn.
...
All Prize cards taken. gklinsing wins.
```

What that gives us, mapped to `MatchEntry`:

| Field | Available? | From |
|---|---|---|
| Result | **yes** | `"All Prize cards taken. <name> wins."` |
| Turn (went first) | **yes** | `"<name> decided to go first."` |
| Playing / Against | derivable | cards played — deck NAMES are never stated |
| Notes / Tags | no | nothing corresponds |
| DatePlayed / StartTime / EndTime | **NO** | not in the log at all |

**Two of the earlier assumptions were wrong:**

- **Usernames ARE present**, both players', on nearly every line. The doubt about this was
  unfounded. But it creates a requirement: the app must know **your own Live username** to tell
  which side is you, otherwise Playing and Against cannot be assigned. That is a new setting.
- **No timestamps in the log CONTENT** — grepped the full sample for dates, clock times and
  meridiems, nothing. **But the log FILE name carries one**, which changes the conclusion
  entirely; see the next section.

**Archetype has to be inferred from cards played**, since deck names never appear. Overlaps the
existing meta-deck resolution and could reuse `ILimitlessMetaService`, but mapping a card list
to an archetype is real work and probably wants a "confirm the deck" step rather than silent
guessing.

**Do not copy the existing parsers.** [kagd/pokemon-tcg-battle-replay](https://github.com/kagd/pokemon-tcg-battle-replay)
is TypeScript with **no licence file at all** — that means all rights reserved, not free to
reuse, regardless of the language mismatch.
[AugustDailey/Ptcgo-Log-Parser](https://github.com/AugustDailey/Ptcgo-Log-Parser) targets PTCG
*Online*, the predecessor, so the format is likely stale. Their value is confirming the format
is stable and parseable, which the sample above already does. The parser itself is a
line-matching exercise well within reach in C#, and it belongs beside
`TrainerHillImportService` under `Services/Import/`.

Prior art worth a look for scope, not code:
[jlgrimes/training-court](https://github.com/jlgrimes/training-court) is a battle-log and
tournament tracker for the same audience.

**Live tools tracking the CURRENT format** — the useful reference, since they must keep working
as Live changes:

- [replay.ptcgtools.com](https://replay.ptcgtools.com/en) — the informative one. Where the
  card-ID export setting and English-only constraint above came from.
- [deaddraw.app](https://deaddraw.app/) — the most useful source found. Read with a
  JS-executing browser; a plain `WebFetch` returns only the title because the page is
  client-rendered, which is a tooling limit, not a property of the site.

Two things from Dead Draw that change how this feature should be planned:

**The card-ID setting, exactly:** *"In TCG Live, go to **Settings > Battle Log** and disable
'Hide card IDs from export'. **Per-device setting.**"* Per-device is the trap — it must be set
on every device the user plays on, and forgetting it silently produces a degraded log rather
than an error. Whatever we build should detect a log with no card IDs and say so plainly,
pointing at that exact path, rather than failing to identify decks and looking broken.

**Live's own logs are unreliable:** *"Battle logs from TCG Live contain inaccuracies — we
compensate where we can and are always improving."* That is a tool specialising in current logs
saying the source data is imperfect. Plan for it: a Live import should be treated as a draft the
user confirms, not as authoritative data written straight to the database. It also sets the
expectation for this feature — "trivial upload" should mean less typing, not zero review.

Their stated flow also confirms the mechanics for the manual path: play a game, open the battle
log, copy the full text, paste.

### There is NO battle log file — clipboard only. VERIFIED on this machine 2026-08-05

I briefly recorded the opposite, from [replay.ptcgtools.com](https://replay.ptcgtools.com/en)
whose Tracker setup says *"Enable Windowed Mode — the Tracker needs this to access log files"*,
plus a public replay named `ptcgl_log_20260805_073547.txt`. I inferred Live persists battle
logs to disk with timestamped filenames. **That was wrong** — inferred from a third party's
marketing copy instead of checking. The user said from the start it was clipboard-only.

Checked the actual install at `%LOCALAPPDATA%Low\pokemon\Pokemon TCG Live\`:

- `Game<yyyy.MM.dd_HH.mm.ss>.log` — timestamped filenames, tempting, but **Unity exception
  traces only**. Grepped every one of 21 files for battle phrases (`drew N cards`, `Turn # `,
  `ended their turn`, `Prize cards taken`): **zero matches**. Sampled contents are stack traces
  from `EventBuyInButton`.
- `Player.log` / `Player-prev.log` — Unity's standard player logs, also zero battle matches.
  `Player.log` was 0 bytes.
- Nothing anywhere named `ptcgl_log_*`.

So `ptcgl_log_20260805_073547.txt` is **the Tracker's own naming on upload**, not Live's. And
"needs windowed mode" is almost certainly about driving or watching the game window — a
plausible way to auto-upload is to click Live's own copy button and take the clipboard — not
about reading a battle log file that does not exist.

**Consequences, now settled:**

- **The clipboard is the only source, on every platform.** MAUI's
  `Clipboard.Default.GetTextAsync()` covers Windows and Android alike, so a single
  "Paste from clipboard" button removes the notepad step the user complained about, with no
  platform split needed.
- **No timestamp is available from anywhere.** Not the log text, not a filename. `DatePlayed`,
  `StartTime` and `EndTime` must come from the user or from import time, and Live-imported
  matches therefore need a different duplicate story than TrainerHill ones — as originally
  flagged.
- **Genuine auto-import is out of scope.** It would mean automating the Live client, which is
  what the Tracker does and why it demands windowed mode. Fragile, and far more than this
  feature is worth.

**Their required settings are still useful and confirmed:** Windowed Mode, English only, and
disable HIDE CARD IDS FROM EXPORT. They also do "automatic archetype detection", so inferring a
deck from card IDs is demonstrably achievable rather than speculative.

## Deck Maker

Build and store deck lists tied to archetypes. Goals:
- Associate a deck list (card name + count) with an `Archetype`
- View/edit deck list from OptionsPage or a dedicated DeckPage
- Export deck list to a standard format (e.g., PTCG Live import format)

Architecture notes: new `DeckEntry` model + `DeckOperations` service; new Shell page if complex enough.

## Pokeball Archetype Picker Animation

When the archetype ComboBox is tapped, animate the pokeball icon as if it's opening to "release" the archetype list. Goal: reinforce the "tap to pick a Pokémon (deck)" metaphor.

**Trigger:** User idea from 2026-08-03 session — ball_icon.png is the unselected placeholder; opening the picker should feel like throwing a ball.

### Rough implementation plan

**Physics note:** Pokeball hinges at the back — only the top half rotates away from the viewer. Bottom stays still.

1. **Asset:** Split `ball_icon.png` into two separate images: `ball_icon_top.png` (top red half) and `ball_icon_bottom.png` (bottom white half). Stack them in a Grid.

2. **Trigger point:** `ComboBoxControl.OnTapped` / `TapGestureRecognizer` command before `PopupNavigation.Instance.PushAsync(popup)`.

3. **Animation (MAUI `Animation` API):**
   ```csharp
   // Top half rotates backward around its bottom edge (the hinge line).
   // AnchorY = 1.0 pins the pivot at the bottom of the top image.
   _ballTop.AnchorY = 1.0;
   var open = new Animation();
   open.Add(0, 0.6, new Animation(v => _ballTop.RotationX = v, 0, -110,
       easing: Easing.CubicIn));   // rotate lid back ~110° (past vertical so it's clearly open)
   open.Add(0.5, 1.0, new Animation(v => _ballContainer.Opacity = v, 1, 0,
       easing: Easing.Linear));    // fade out as it opens
   open.Commit(this, "BallOpen", length: 280,
       finished: (_, _) => { /* show popup; reset RotationX = 0, Opacity = 1 */ });
   ```

4. **Close animation:** Reverse — `RotationX` from -110 back to 0, opacity 0 → 1, triggered on popup dismiss callback.

5. **Platform notes:** `RotationX` is 3D perspective rotation; verify it doesn't render flat on Android API < 28. MAUI animations run on UI thread — keep length ≤ 300ms so it doesn't feel laggy before the picker appears.

6. **Accessibility:** Check `AccessibilitySettings.IsReduceMotionEnabled` — skip animation and open immediately if true.

## Edit and delete a saved match (user, 2026-08-06, NOT started)

Neither exists today. **Verified 2026-08-06:** `MatchOperations` exposes only `SaveAsync` —
there is no update path at all — and no ViewModel has an edit or delete-match command. Matches
are insert-only once submitted.

The user wants both: *"currently there is not way of editing after you submit the match,
actually you are right there probably should be though, just like i should allow individual
ones to be deleted, also not a feature yet written."*

**Edit a match**
- Needs `MatchOperations.UpdateAsync`. Note the `[OneToOne(CascadeOperations = CascadeOperation.All)]`
  game relationships: updating a match has to update or replace its `Game` rows, and the
  existing `SaveAsync` inserts them, so this is not a one-line addition.
- Natural entry point is ReadJournalPage, which already selects and displays a match.
- Do NOT forget `EndTime`/`StartTime`: they feed `CalculateAverageMatchDuration` and
  `CalculateWinRateByMatchLength`.

**Delete a single match**
- **CORRECTED 2026-08-06:** `IMatchOperations.DeleteAsync(MatchEntry)` already exists and is
  documented as deleting "all related records". So this is a **UI-only** gap, not a service
  one — do not write a second delete. Check what it actually cascades before trusting the
  summary, but start from it.
- Per [[project_error_handler_di]] the confirmation must not be a modal — the user has a
  standing objection to dialogs, especially under automation.

### This changes the restore's duplicate handling — read before implementing conflicts

The restore design ([[project_roadmap]] backup section) says merging is only really needed for
TrainerHill imports, and **that is correct only while matches are insert-only**. Reasoning
confirmed 2026-08-06: with no edit path, a backup restore can only ever meet a match that is
byte-identical to the file, or one that does not exist yet. Cases 2 (one side richer) and 3
(genuine conflict) are structurally impossible for backups today.

**Adding edit changes that.** Once a match can be modified after a backup is taken, restoring
that backup can legitimately produce both cases — an entry whose note was added later (case 2),
or whose note was changed (case 3). Whoever builds edit should revisit the restore's assumption
rather than trusting this note's "TrainerHill only" framing.

## ReadJournal: games 2 and 3 are only half-displayed (found 2026-08-05, NOT started)

**Game 2 and game 3 notes have never been shown.** `ReadJournalPageViewModel` computes
`SelectedNote2` and `SelectedNote3` and keeps them up to date, but `ReadJournalPage.xaml` binds
only `SelectedNote` — the other two are calculated and discarded. Game 1's notes are the only
notes the user has ever seen.

The tag views for games 2 and 3 *are* bound (`Game2TagsView`, `Game3TagsView`), but until
2026-08-05 they were visible on **every** match, including best-of-one, because
`MatchOperations.LoadRelatedDataAsync` left phantom `Game2`/`Game3` objects in place — copies
of game 1 — and the `IsVisible` binding tests those for null. That is fixed
([[project_db_session_lock_pairing]] is unrelated; the phantom fix lives in
`MatchOperations.LoadRelatedDataAsync` with a regression test in
`MatchOperationsIntegrationTests`). So the section now correctly appears only for BO2/BO3
matches — but with no notes.

What to do:

1. Bind `SelectedNote2` / `SelectedNote3` alongside the existing game 2 and 3 tag views, with
   the same visibility rule so they appear only when the game exists.
2. Give the notes editors `AutomationId`s and `SemanticProperties`, as `SelectedMatchNotes`
   already has, and a UI test per the "every data page needs a data-presence assertion" rule.
3. While in there, consider replacing the `IsNotNullConverter` visibility bindings with
   explicit bool view-model properties — the project already ruled that converter out after it
   crashed OptionsPage ([[feedback_no_isnot_null_converter_in_xaml]]), and ReadJournal is the
   last place still using it.

**Do not "fix" this by reverting the phantom-game change.** The phantoms made a BO1 match
render three tag sections, which is the bug the user reported as *"the tags for each game are
all shown at once"*.

## Known Bugs (fix before first release)

**There has never been a release.** Nothing has shipped, so every bug listed here is a
first-release blocker by definition, and the release vehicle itself is still an open roadmap
item (see *Real Installer (Windows/Android)* below).

*None currently open.*

### ~~ComboBox Cancel Button Hangs App (MainPage)~~ — CLOSED 2026-08-05, not a bug
Was a transient Windows OS hiccup, not application behavior — user confirmed 2026-08-05,
never reproduced. Regression UI tests for Cancel dismissal were added anyway (merged
`5c9b7da`) and are kept. Do not hunt for an async deadlock in the popup dismiss path.
See [[project_combobox_cancel_hang]].

---

## Website Refresh (feat/site-refresh — separate branch, later)

`index.html` at repo root (GitHub Pages via static.yml). Current AI-built lander is solid; refinements in priority order:

1. ~~**Legal disclaimer**~~ — **Done 2026-08-04.** Footer disclaimer added to index.html: unofficial fan-made tool, not affiliated with Nintendo/The Pokémon Company/Game Freak/Creatures Inc., trademarks acknowledged.
2. **App screenshots section** — feature tour with real UI captures (charts, journal, main page). Biggest visual impact.
3. **Auto-updating stats** — replace hardcoded "505 COMMITS" / "530+ TESTS" / fake ticker meta shares with shields.io badges or drop numbers.
4. **Ticker honesty** — remove "UTC // LIVE" claim or make decorative-obvious; data is static.
5. **Download section** — add Releases download buttons once the installer ships (pairs with Real Installer roadmap item).
6. **Verify hero-bg asset** — `.hero-bg` image must resolve on Pages; broken bg fails silently at 40% opacity.
7. **Accessibility pass** — skip-link, focus states on nav, contrast check on 9px `--muted` mono text (likely fails WCAG).

---

## Deck Comparer

Compare two deck lists side-by-side:
- Show cards in common, cards unique to each
- Highlight counts that differ
- Useful for tracking meta evolution between tournament seasons

Likely a sub-view of DeckPage rather than its own Shell page.

---

## AOT Compatibility (long-term)

Make the whole app AOT-compatible so Release builds run through NativeAOT / full Mono AOT — faster startup, smaller runtime footprint, and (on Android) `pm clear` becomes safe because assemblies live in the APK instead of `.__override__/`, unblocking cleaner test isolation.

**Current state:** Android Release explicitly sets `RunAOTCompilation=False` + `PublishTrimmed=False` (see CLAUDE.md) because deps aren't ready. Fast Deployment (Debug) uses Mono JIT + external assemblies.

**Blockers per dep:**
- **SQLite-net-pcl** — heavy runtime reflection on table mapping. Swap for source-generator variant, or migrate to EF Core 9 with compiled model.
- **CommunityToolkit.Mvvm** — already AOT-safe via source generators. ✓
- **CommunityToolkit.Maui popups** — reflection in `ShowPopupAsync<T>`; audit for trim warnings.
- **LiveCharts2** — reflection-heavy property binding; check trim/AOT support.
- **MAUI XAML bindings** — every `Binding` needs `x:DataType` (compiled bindings). Runtime bindings crash under AOT. Do an audit pass and fill in `x:DataType` everywhere.

**Enablement steps:**
1. Set `<IsAotCompatible>true</IsAotCompatible>` + `<TrimMode>full</TrimMode>` in csproj (Release).
2. `dotnet publish -c Release -f net10.0-android /p:PublishAot=true` (later: iOS too).
3. Fix every IL2026 / IL3050 warning by adding `[DynamicallyAccessedMembers]` where reflection is unavoidable, or refactor to source generators.
4. Verify all UI tests still pass on the AOT build.

Once AOT is on for Android, delete the "pm clear vs Fast Deployment" workaround memory — the whole class of bug disappears.

---

## Real Installer (Windows/Android)

Right now Windows deploys as an unpackaged .exe (`WindowsPackageType=None`) and Android deploys through VS Fast Deployment for dev. Ship a real installer for released builds:

- **Windows** — MSIX package with Start Menu entry, uninstaller, auto-update; or WiX MSI. Improves startup because the CLR loads from a fixed install path (no per-user reprovisioning) and Windows can prefetch. Also gives file associations for `.trainerhill.json` imports.
- **Android** — signed release APK/AAB via Play Store or F-Droid. AAB with dynamic delivery is smaller and installs faster on device than a monolithic APK.
- **macOS/iOS** — future, once MAUI targets are enabled again.

Bundling with AOT + an installer is the combo: no Fast Deployment paths on user machines, no assembly resolution overhead, clean uninstall, real update channel.

### Code signing — hard budget constraint (stated 2026-08-05)

**The user has no budget for code-signing certificates.** Commercial OV/EV Windows certs run
several hundred USD per year (and since 2023 require hardware-token/HSM storage, which pushes
the cost up further). Do not plan around buying one. This constrains the release design, so
the free paths below are the real options:

**Android — genuinely free.** Android signing involves no CA at all: a self-generated
`keytool` keystore *is* the standard mechanism, not a workaround. A signed release APK on
GitHub Releases costs nothing. **The keystore must be backed up permanently — losing it means
never being able to update the app.** Google Play is a one-time developer registration fee
(~$25, verify current), F-Droid is free; neither is required for sideloading.

**Windows — unsigned is worse than it sounds on Windows 11.** Two separate mechanisms:

1. **Mark of the Web.** Downloaded files carry a `Zone.Identifier` ADS; Properties → *Unblock*
   clears it. An `.exe` can usually be run via *More info* → *Run anyway*, but MotW blocks
   `.ps1`, `.chm`, and app-loaded DLLs harder — and Explorer propagates MotW to every file
   extracted from a downloaded ZIP, which matters because a self-contained build is an exe
   surrounded by many DLLs. User has been burned by this repeatedly.
2. **Smart App Control (Win11 22H2+).** Can block unsigned apps outright **with no "Run
   anyway" option**. Re-enabling SAC after disabling it requires an OS reinstall. Only active
   on clean installs (starts in evaluation mode), so not universal — but for affected users an
   unsigned app simply does not run.

**Do not conflate those two.** The user's own machine is the MotW case (#1) — every exe
downloaded from the internet has to be unblocked via Properties — not SAC. #2 is a risk to
*other* users on affected Win11 installs, not an observed behavior here. Worth knowing: if
"Run anyway" is never offered and Properties is the only route, Windows Security →
Reputation-based protection → "Check apps and files" is likely set to **Block** rather than
Warn, which is stricter than the default.

Free routes that actually clear this, in preference order:

- **SignPath.io** — free Authenticode signing for open-source projects, integrates with GitHub
  Actions. Real signature, $0. **Best fit for this project.**
- **Microsoft Store** — Microsoft signs the MSIX; no warning at all. Individual developer
  registration was historically a small one-time fee (~$19) and may since have been
  reduced/waived — verify. Costs Store packaging + review instead of money.
- **Certum Open Source Code Signing** — OSS-specific cert, historically ~€30/yr. Cheap but not
  free.
- **Ship unsigned on GitHub Releases** — $0, works for a developer audience who will click
  through, hostile for normal Win11 users. Acceptable for v0.1 only.

**Never self-sign for public Windows distribution.** It is worse than unsigned: MSIX sideload
then requires users to install your certificate into Trusted Root — scarier and more work.

### Self-contained deployment — decided

Ship Windows **self-contained** (`SelfContained=true` + `WindowsAppSDKSelfContained=true`).
This eliminates the entire "user didn't install .NET / the Windows App SDK and the app
crashes" failure class — nothing to document, nothing for users to get wrong. Required
anyway for an unpackaged app without the WindowsAppSDK runtime present.

Correcting a misconception recorded here deliberately: self-contained is **not** a runtime
performance hit, and it does **not** affect SmartScreen either way. Runtime speed is
essentially unchanged (ReadyToRun can make startup *faster*). What it actually costs is
**download size** (well over 100 MB vs a small fraction of that) and **servicing** — you own
the bundled runtime, so a .NET security patch means cutting a new release rather than users
getting it from Windows Update. Signing and deployment mode are independent concerns.

**Realistic free-tier first release:** signed Android APK (real signing, $0) + self-contained
Windows build signed via SignPath, both on GitHub Releases.

---

## Loading Gates + Optional Loading Indicator

**GATES SHIPPED 2026-08-04** (feat/loading-gates): IsBusyMatchHistory / IsBusyChartData /
IsBusyArchetypeList ×2 live on all four data pages with Busy_* sentinels and
WaitUntilBusyGone test sync — see [[project_loading_gates]]. Together with the
ReadJournal FlexLayout swap: SelectMatch tests 50-111 s → sub-second, Android suite
18 m → 8 m 44 s, 72/72. **Remaining from this entry:** only the optional visual
indicator (spinner/PokéBall animation) — user polish, unscheduled.

**The backend gate matters more than any visual.** Confirmed 2026-08-04: Android UIA
server waits ~20 s per element lookup when the UI thread is busy on async render —
regardless of how much data is on screen (dropping ReadJournal seed from 14 → 4
matches did nothing). A named `IsBusy_*` flag that flips fast is what unblocks UI
tests; the animated indicator is user polish on top.

### Named busy tokens (primary design)

Every async load declares a scoped `IsBusy_*` bool property on its VM, not a single
page-wide flag. Multiple concurrent loads each own their own gate so tests can wait
for the specific data they care about:

```csharp
public partial bool IsBusy_ChartData { get; set; }
public partial bool IsBusy_MatchHistory { get; set; }
public partial bool IsBusy_ArchetypeList { get; set; }
```

Each async op wraps in try/finally so the flag always clears:

```csharp
try { IsBusy_ChartData = true; await LoadChartsAsync(); }
finally { IsBusy_ChartData = false; }
```

Each bool binds to a hidden **1×1 Label** in XAML with a stable AutomationId:

```xml
<Label WidthRequest="1" HeightRequest="1" Opacity="0"
       AutomationId="Busy_ChartData"
       IsVisible="{Binding IsBusy_ChartData}" />
```

Tests then `WaitUntilGone("Busy_ChartData")` before element lookups. No arbitrary
sleeps. UIA server sees the flag flip to hidden the moment the load completes.

Global `IsAnyBusy` computed from the set (any bool true) drives the optional
visible spinner. Registry / dict of tokens is overkill until dozens of concurrent
loads coexist — start with per-property.

### Where the gates go

- **TrainerPage** — `IsBusy_ChartData` around chart calc pipeline
- **ReadJournalPage** — `IsBusy_MatchHistory` around match list + detail load
- **MainPage** — `IsBusy_ArchetypeList` around Limitless fetch on first popup open
- **OptionsPage** — `IsBusy_ArchetypeList` shared with MainPage

### Optional visual indicator — DESIGN LOCKED (2026-08-04, mockup provided by user)

Fluent-style ring spinner, NOT a full solid ring and NOT a simple spinning Pokéball alone:

- **Partial arc**, not a closed circle. Solid/opaque red near the leading edge, fading to
  transparent trailing behind it — matches the "chasing itself" Windows modern spinner look.
- **Pokéball rides the leading edge** of the arc, positioned at the arc's head like a comet.
- **Pokéball spins on its own axis** independently while it also orbits around the circle path.
- Arc color: red (primary choice) or PokeBlue — both hold up in light and dark mode. White ruled out (invisible/low-contrast on light backgrounds).
- Reference mockup: user-provided image — red arc, gray/white Pokéball dot at the 12 o'clock
  leading point, trail fading counter-clockwise from the ball.
- Bind IsVisible to `IsAnyBusy` for full-page overlay, or specific `IsBusy_*`/`IsBusyMutating`
  for inline per-action indicators.
- Respect `AccessibilitySettings.IsReduceMotionEnabled` — swap animation for static "Loading…" label.
- Overlay uses semi-transparent scrim over content; inline uses a small version in the section/button.
- Implementation approach: likely custom `GraphicsView`/`SKCanvasView` (SkiaSharp is already a
  dependency via LiveCharts2) drawing an arc + rotating Pokéball sprite, animated via a
  `Microsoft.Maui.Animations` ticker or simple `Dispatcher.StartTimer` angle increment. Lottie
  is an alternative if a matching animation is easier to source/build externally.

### Overlay + region-scoped indicators — page-level overlay DONE 2026-08-06, region scoping NOT started

**Done (branch `feat/loading-overlay`):** each page's root `ScrollView` is wrapped in a `Grid`
and the indicator is a sibling in the same cell, so it draws over the content with zero layout
impact. `InputTransparent="True"` on the host, which matters because the indicator lingers
500ms past the gate and a test resumes clicking while it is still up.

Two things that were only found by measuring, and are worth not re-learning:

- **The indicator was not the only layout participant.** After the overlay move a 13px shift
  remained. It was the `Busy_*` sentinels: 1×1 `Label`s whose `IsVisible` is bound to a gate,
  sitting in a `Spacing="12"` stack, so un-hiding one costs 1 + 12 = 13px. They are automation
  markers, not content — they now live in the Grid layer too, pinned top-left.
  MainPage's `PlayerArchetypeIcon2` / `RivalArchetypeIcon2` are the same pattern and moved with
  them. **Any future 1×1 sentinel goes in the Grid layer, never in the content stack.**
- **Do not set `InputTransparent` on the sentinels.** A 1×1 Label is not interactive so it buys
  nothing, and it is unverified whether it perturbs the Android accessibility tree the tests
  depend on. It stays on the indicator host only.

**Loading message — deliberately left extensible, NOT implemented (user, 2026-08-06.)** The
user liked that each page carries its own wording ("Working…", "Loading journal…", "Crunching
stats…") and wants the option to pick randomly from a set later, but explicitly scoped that
out: *"no dont include it in the current scope about the random labels, just keep the
labeling/message agnostic so we can extend it later."* The shape already supports it — one
`Label AutomationId="LoadingIndicatorLabel"` per page whose `Text` is a literal, so a random or
per-context provider is a one-attribute swap per page (`Text="{Binding LoadingMessage}"`) with
no restructuring. Keep it that way: do not inline the message into the control or hard-code a
single shared string.

**Still to do: region scoping.** The overlay is page-level everywhere. The section below is the
design for scoping it to whichever region is actually busy.

**Historical context — why inline was abandoned:**

The indicator currently sits in the layout flow (a `VerticalStackLayout` child next to the
busy sentinel). That means it *displaces page content* when it appears and lets it snap back
when it clears. The user's verdict on seeing it: *"the visuals actually suck because the
spinners are not showing up above the page content they are showing up in the layout and
disappearing after like half a second."*

`MinimumVisibleDuration` makes inline worse rather than better — content jumps down, sits
displaced for the full 500ms, then jumps back. More distracting than showing nothing.

Moving to an overlay inverts both problems: same Grid cell as the content means **zero layout
impact**, and the minimum duration stops being a liability and becomes what it was meant to be
— long enough to register. So the overlay is what makes the minimum-duration design pay off.

**Priority: required before release, not blocking the next feature** (user, 2026-08-05 —
*"its not optional, but its not priority either"*). The mechanism works and is tested; what is
wrong is where it is drawn. Slot it in after the higher-value feature work rather than ahead of
it, but do not ship a release with the inline version.

The scrim should not be a blanket page-covering overlay either — see the region scoping below.

The model the user wants:

1. **Page load** — indicator covers the page while the page itself is loading, then goes away.
2. **Then region-scoped** — if a particular CollectionView (or any one section) is still
   loading, the animation renders only in *that* region, taking up the minimum space that
   region wants, and clears when that region finishes.

So the scrim is scoped to whatever is actually busy, not to the window. The existing gates
already line up with this: `IsBusyMatchHistory` is the ReadJournal list, `IsBusyChartData` is
the TrainerPage charts, `IsBusyArchetypeList` is the archetype collection. Each is a region,
not a page.

**Implementation shape.** MAUI Grid children in the same cell stack, and the last sibling
renders on top, so a region overlay is the region's own container plus an overlay child:

```xml
<Grid>
    <CollectionView ... />
    <Grid IsVisible="{Binding Source={x:Reference RegionSpinner}, Path=IsShowing}"
          InputTransparent="True"
          BackgroundColor="{AppThemeBinding Light=#B0FFFFFF, Dark=#B0000000}">
        <loading:LoadingIndicator x:Name="RegionSpinner" IsBusy="{Binding IsBusyMatchHistory}" />
    </Grid>
</Grid>
```

Three things carry the weight:

- **`InputTransparent="True"`** — non-negotiable. The indicator stays up for
  `MinimumVisibleDuration` (500ms) *after* the gate clears, but `WaitUntilBusyGone` waits on
  the raw gate, so a UI test resumes while the overlay is still on screen. Without input
  transparency those clicks land on the scrim and produce exactly the intermittent
  tap failures that took this session to eliminate ([[feedback_android_flaky_tap_retry]]).
  The alternative is a sentinel bound to the indicator's `IsShowing` for tests to wait on —
  more machinery for no extra benefit.
- **`AppThemeBinding` on the scrim colour** — a dark scrim over light content reads as a bug.
- **Ordering, not z-index** — last child wins.

**Start with a dim, not a blur** — but a real blur is not off the table, and it is NOT the same
problem as the spinner's flicker.

MAUI has no *cross-platform* blur primitive. Getting one means reaching for each platform's
built-in native renderer — `AcrylicBrush` on WinUI, `RenderEffect` on Android 12+ — through a
handler or platform view. That is ordinary platform-specific customisation, which MAUI is
designed to accommodate.

Do not cite the spinner's residual flicker as precedent against this. That was left alone as a
priority call, and the DX reasoning first recorded for it did not survive checking: MAUI's
Windows backend already renders through DirectX via Win2D, and the real escape hatch would be
SkiaSharp — already a dependency, cross-platform, with `SKShader.CreateSweepGradient` as the
exact primitive `ICanvas` lacks. See [[project_spinner_drawing_lessons]] and
[[feedback_fact_check_the_user]].

So: ship the dim first because it is one line of XAML and works everywhere, and treat acrylic /
RenderEffect as a per-platform enhancement if the dim proves too flat.

### TDD

- Write a failing test that opens TrainerPage, asserts `Busy_ChartData` is visible, then waits for it to disappear within 5 s and asserts chart elements are present. Then wire the gate to make it pass.
- Repeat per gate.

---

## Android test execution strategy (added 2026-08-05, not started)

Android UI jobs now finish faster on CI than the Windows ones, while the same 72 tests run
serially in **8m55s** locally (Windows: 73 tests, **1m28s**). The gap is the execution
environment — the emulator on Windows — not the hardware; the user's machine outclasses a
GitHub Ubuntu runner.

Planned, in rough priority order:

1. **Default Android UI testing to CI.** Keep local runs for targeted `--filter` debugging
   rather than full sweeps.
2. **Auto-target a real phone.** If a physical device is attached, run there; otherwise boot
   the AVD. Automatic detection via `adb devices`, with an env-var override in the shape of
   the existing `ANDROID_USE_INSTALLED`.
3. **Local parallelism** to match what the CI matrix gets. Needs distinct AVDs, adb/Appium
   ports, and app data per instance — `AppiumSetup` currently owns a single driver, port and
   emulator, so concurrent fixtures would fight over one `.db3`.
4. **Evaluate WSL2** (Ubuntu already installed) as an emulator host — requires nested
   virtualization + KVM, and a real phone would need `usbipd-win` forwarding. Spike before
   committing; the emulator would contend with Hyper-V for the same hardware, so it is not
   obviously faster than more native AVDs.

Full constraint list in `docs/memory/project_android_test_execution_strategy.md`.

---

## Inline validation feedback (added 2026-08-05, not started)

Guards that decline a save now log a warning naming the missing input
([[feedback_no_silent_guards]]), which fixes *diagnosis*. It does not fix the user
experience: someone who leaves a field empty still sees nothing happen at all.

**User's decision (2026-08-05):** a **text label with red text** explaining which step failed
validation. **Not a modal** — and this is a hard constraint with reasons behind it, not a
style preference: *"modals can cause bad mojo in automation especially on ci thats why i want
to stay away from them."*

This repo has been bitten by modal/dialog behavior repeatedly:

- [[project_winui_xamlroot_crash]] — `DisplayPromptAsync` before the window was composed
  crashed WinUI with "no XamlRoot".
- [[project_optionspage_crash_fresh_db]] — `ModalErrorHandler` firing during
  `AppearingAsync` on a fresh DB crashed with `0xc000027b`; fixed by making it log-only.
- [[project_android_ci_gpu_flake]] — a system ANR dialog owned the **entire accessibility
  tree**, so no element of ours was reachable until it was dismissed.
- [[feedback_combobox_popup_platforms]] — popup Cancel buttons frequently are not in
  Android's UIA tree, which is why `DismissPopupPlatform` exists.

A modal is a separate window: it steals focus, may be absent from the UIA tree, and can
appear when no test is waiting for it. An inline label is a bound property on a page the
tests already hold a handle to. **Do not introduce a modal for validation feedback.**

Design notes for whoever picks this up:

- Inline label near the offending input, not a page-level banner — the point is to say
  *which* field is wrong, matching the split guards already in `OptionsPageViewModel`.
- Bind to an observable `…ValidationMessage` string per form (empty = hidden). Use an
  explicit `bool` VM property for `IsVisible`, never a null-check converter
  ([[feedback_no_isnot_null_converter_in_xaml]]).
- Red must come from a theme resource, not a literal, so the theming pass can retint it
  ([[project_theme_switcher]]). Check contrast in light mode.
- Accessibility: the label needs `AutomationId` + `SemanticProperties.Description`, and
  should ideally be announced when it appears.
- The warning log and the label should share one source of truth so they cannot disagree.
- MainPage's `SaveMatchAsync` already builds a multi-line validation message string via
  `ValidateEntryAsync` — reuse that shape rather than inventing a second mechanism.
