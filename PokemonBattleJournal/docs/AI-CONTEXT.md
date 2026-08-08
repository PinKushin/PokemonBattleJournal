# PokemonBattleJournal — AI Context

> **Last updated:** 2026-08-03 (responsive MainPage layout + GitHub Pages site + memory/docs sync)
> **Solution file:** `PokemonBattleJournal.slnx` (not `.sln`)
> **Project website:** https://pinkushin.github.io/PokemonBattleJournal/ (deployed via `.github/workflows/static.yml` from `index.html` at repo root)
> **Read this first** when working in this repo. Update the [Session log](#session-log) whenever scope, decisions, or blockers change — especially before long multi-step work.

---

## Session log

Chronological notes for the current / recent work. **Append or edit this section** as conversations progress.

| Date | Topic | Status / notes |
|---|---|---|
| 2026-08-07 | **Restore conflicts can be seen and resolved** (feat/restore-conflict-ui, merged `09a2132`) | RestoreService could describe a conflict and nothing could act on one — the backup's copy of the data was discarded the moment the conflict was recorded. Git is the model (Keep = --ours, Replace = --theirs, Append = keep both) and the user picks per conflict because a restore has **no merge base**: two versions, no recorded ancestor, so no machinery makes it automatic. That reasoning also rules out "import a bit of git". Decisions are STAGED — selecting writes nothing, Apply writes only answered rows, per match in one transaction — because a half-applied restore "can make you think more was saved than what actually was saved". Where one side merely knows more, Append is pre-selected and can be declined. NoteDiff is ours rather than DiffPlex: Core is mutation-tested and a dependency is not, and writing it forced the Myers-vs-LCS question a package would have answered invisibly. **Found a real bug on the way**: `GetByIdAsync` showed an error modal for a row that simply does not exist, because `GetWithChildrenAsync` throws rather than returning null. Tests missed it because the shared integration factory uses NullErrorHandler, which swallows without recording, and they asserted the return value — which was already correct. The user caught it by watching a UI run. |
| 2026-08-07 | **Core mutation score: the shapes behind the number** (chore/mutation-score-core) | 53.09% -> 56%+ across eight commits, but the number is the least useful part. One bug class kept recurring in different clothes: **a branch exercised in only one direction reads as covered**. Restore guards were entered only as false; the trainer deletion cascade ran only with empty collections, so 85 mutants hid behind a green `DeleteAsync_AfterSave_RemovesTrainer`; `GetByTrainerIdAsync` was only ever asked to include, never exclude. Second shape: **single-subject blindness** — with one trainer, "delete everything" and "delete this trainer's data" are indistinguishable, and removing the TrainerId filter failed ONE test while five stayed green, including the one named "removes the trainer's matches". Three of my own new tests also failed their sabotage check, including one whose inputs could not discriminate the flag it claimed to test. Two things deliberately NOT chased: equivalent mutants from redundant code (Tag delete runs twice, so mutating either is masked), and change-detector tests over a hardcoded deck list. Killing a mutant is not the goal; noticing a real change is. Operational: never run `dotnet test` while Stryker runs — same `obj/`, the lock fails the build and produces NO output, which looks like CPU starvation and is not. |
| 2026-08-07 | **Explicit types enforced; two config files were fighting** (same branch) | `csharp_style_var_*` were already `false` in .editorconfig but carried no severity, and even with one they do nothing without `EnforceCodeStyleInBuild` — both now set. IDE0008 is a **temporarily accepted warning**, ~1000 across all TFMs, on terms written into CLAUDE.md/AGENTS.md: do not suppress, **touch a file and you clean that file**, test projects not exempt, exception ends at zero (task #21). It breaks the "hold CI annotations at zero" rule that caught a real CS8602 earlier the same day, so the docs now specify checking annotations EXCLUDING IDE0008. Separately: `.editorconfig` said `end_of_line = crlf` while `.gitattributes` says `eol=lf`, which is why every commit printed a normalization warning for files nobody had touched. |
| 2026-08-08 | **Sentry tracing wired up, and a leak I claimed that did not exist** (feat/sentry-tracing) | `TracesSampleRate` had been set for months with an empty dashboard, because the rate samples transactions that already EXIST and MAUI creates none automatically — it was sampling an empty set, and no test could catch it since tests never init the SDK. Added `IPerformanceMonitor`/`ITimedSpan` in Core with a Sentry adapter, spans on restore and import carrying counts only, plus a Debug-only `SentryDiagnosticsButton` that sends one trace and one error. **Confirmed end to end from a real envelope**: `HttpTransport: Envelope ... successfully sent`, explicit release, session envelope with `errors:1`, trace context attached. Tracing is permanently FREE at 5M spans/month so production sampling went to 1.0; the "14-day trial" banner was an upsell ad. **Span names are a channel the redactor does not cover**, so the defence is structural: `StartSpan` takes constants, `ITimedSpan` has no string setter, and a reflection test pins that — verified by adding `SetTag(string,string)` and watching only that test fail. **The correction worth keeping:** I read the envelope, saw `"Inserting match entry for trainer 2: Playing=9..."` in a breadcrumb, and raised a leak against three `LogWarning` sites. Wrong — those are ints and an enum, which the sink allows by design. The distinguishing test is whether a STRING survives, and the same file showed `"Start processing HTTP request \"[redacted]\" \"[redacted]\""`. Breadcrumbs ARE protected; the 2026-08-07 audit had already recorded that and reading it first would have prevented the claim. Measured a proxy (any value appears) rather than the variable (a string appears). What DOES bypass the sink: Sentry's own structured breadcrumb data, e.g. `"data":{"url":...}` — benign here. Also renamed `ISpan` to `ITimedSpan` after the third collision with `Sentry.ISpan`. Unit 646, integration 240, Windows 107, Android 87. See [[project_sentry_three_channels]]. |
| 2026-08-07 | **Sentry audited: the SDK was clean, our log strings were the leak** (fix/sentry-log-pii) | Every PII-adjacent Sentry option defaults to false and none was overridden — `SendDefaultPii`, `IncludeTextInBreadcrumbs` (which is what kept match notes out of breadcrumbs), `IncludeTitleInBreadcrumbs`, `AttachScreenshot`. **Nothing the SDK does on its own sent user data.** The Serilog sink did: `MinimumBreadcrumbLevel = Information` turned every `LogInformation` into a breadcrumb carrying the **rendered** message, and `MinimumEventLevel = Error` shipped the last 300 with each error — so trainer names, tag text, deck names and the full export path (which embeds the OS account name, a real name far more often than a trainer name) all left the device. Fixed in two layers, both load-bearing: call sites now log ids, counts and **lengths** (a failed save has no id yet, and the length is what explains a rejection), and `Logging/SentryRedactingSink` forwards a **copy** of each event carrying only values whose **type** cannot express user content. It fails closed — strings and `{@destructured}` objects are withheld unless allowlisted — so a log line written in two years is safe by default rather than by discipline. It builds a new `LogEvent` rather than editing the one it was handed, because Serilog passes one instance to every sink and mutating it would redact the local file log too, order-dependently. Import/restore error lists are **kept verbatim on purpose** and withheld only on the way out; that split is why one layer would not do. Two findings worth keeping: `{@MatchEntry}` did *not* carry match notes, but only because `Game1/2/3` are still null at that line — one `LogDebug("{@Game}")` would have shipped them; and **the first leak test could not fail**, because `Utf8JsonWriter` escapes an apostrophe to `'`, so "Ash's Pikachu Deck" was fully present in the payload and absent as a literal substring. The tests parse and walk the document now. Accepted residue: `LogError(ex, …)` still forwards the exception, and SQLite can quote a value in a constraint message — rewriting exception text would destroy the crash report. Unit 522, integration 204. See [[project_sentry_privacy_audit]]. |
| 2026-08-07 | **The accessibility contract is now enforced, not just documented** (test/accessibility-contract, merged `4bae2c8`) | 18 Windows UI tests; suite 83 → **101**. CLAUDE.md has mandated `SemanticProperties` on every element for months and nothing checked it — all 16 "accessibility" references in the UI tests were `AccessibilityId` used as a *locator*. Each test now reads the live UIA tree and requires **both** a Name (a screen reader announces something) and at least one control pattern (assistive tech can operate it), because passing the first while failing the second is exactly what shipped: the BO3 switch had `Description`, `Hint` and `IsInAccessibleTree="True"`, passed review, was announced correctly, and could not be activated — markup review cannot catch that, only reading the tree can. Verified to discriminate by pointing it at a `Label` (Name yes, pattern no). Added to the existing Windows partials rather than a new fixture, because CI's matrix hardcodes five fixture names and a filter matching nothing exits 0 silently. **Scope is honest:** Windows-only (reads UIA; Android's `AccessibilityNodeInfo` is uncovered), and it verifies the machine-readable contract — a `Description` of "asdf" passes. See [[project_accessibility_contract_tests]]. |
| 2026-08-07 | **Zero coordinate clicks on Windows — the picker, the rows, the text inputs** (fix/archetype-popup-invokable + fix/combobox-semantics-forwarding + fix/combobox-id-on-activator, merged `d2e450d`, `d6cf81a`, `e8577e9`) | A full Windows run now logs **no mouse clicks at all**; every interaction resolves through a UIA pattern, so no path remains that can dispatch input at a screen coordinate. Each archetype in the picker popup had been a `Grid` + `TapGestureRecognizer` — announced as "Double tap to select …" and impossible for a screen reader to select. Overlay Buttons fixed those and the ComboBoxControl opener. Added **ExpandCollapse** to the ladder (a MAUI `Picker` is a WinUI ComboBox and exposes nothing else; `Focus()` before `Expand()` is load-bearing because the helpers type the item's first letter straight after), **`Focus()` for text inputs** (an `Edit` has no activation pattern — clicking one *means* focusing it), and an **ancestor walk** up to three levels. The AutomationId now sits on the activator via a new `ActivatorAutomationId` property: MAUI throws `AutomationId may only be set one time`, so it can be neither cleared nor moved, and copying it duplicates an id that resolves to the non-invokable parent. **I first rejected that as a "consumer-facing API change" without counting the call sites — there were three, all in this repo's XAML.** Windows 83/83 at CI geometry, Android 82/82, unit 506, integration 197. |
| 2026-08-07 | **I broke Windows CI, and the repro setup was why it passed locally** (fixed in `c0c0bae`) | The off-window click guard assumed element rects are screen-space. CI reported `UserNoteInput` at `(24,311)` inside a window at `(85,78)` — x=24 left of the origin is impossible for a child, so that rect is window-relative. The guard now refuses only when the target is outside under **both** readings. **Two of my own changes hid it:** `UITEST_WINDOW_SIZE` pins the window to (0,0), where the two coordinate spaces are identical, so a reproducibility feature added to catch environment bugs made this one unreproducible; and the stacking breakpoint moved the note input from the right column (high x) to x=24. `UITEST_WINDOW_POS` now overrides the pin — 754x512 at (85,78) reproduces CI exactly. Third instance in one investigation of a conclusion drawn from a setup that could not have failed; see [[feedback_repro_setup_can_hide_the_bug]]. |
| 2026-08-07 | **Unreachable vs mis-identified** (same branches) | ReadJournal match rows looked like the same defect as the BO3 switch — a `Border` with an AutomationId, clicked by mouse — but they sit in a `SelectionMode="Single"` CollectionView, so the item container is natively selectable and announced and a screen reader could always pick a match. The id merely pointed at a `Border` two levels inside the element holding `SelectionItemPattern`. Fixed in the test helper's ancestor walk with **no app change**. Ask which it is before editing markup: no pattern anywhere means a real control is needed; a pattern on an ancestor means the lookup is wrong. See [[feedback_invokable_controls]]. |
| 2026-08-06 | **Windows click flake SOLVED — clicks were landing outside the app window** (fix/windows-click-lands-on-window, merged `6a49611`) | `WinAppDriver.Click()` is synthesized mouse input at the element's centre in **screen** coordinates, so an element laid out below the window is clicked at whatever screen position that resolves to. Locally that launched Visual Studio and the Epic Games store off the taskbar; on CI, where nothing sits behind the app, the identical click lands on empty desktop and returns silently — dispatched, ~1000ms, no handler, while find-only tests keep passing. That is the six-test signature open since 2026-08-05. Fix is a `ClickElement` seam on `TestBase` whose Windows override walks a UIA pattern ladder (ScrollIntoView, Invoke, Toggle, SelectionItem), none of which carry coordinates; every call site is routed through it, and the surviving mouse path **refuses** to click a target measured outside the window. **The geometry hypothesis had been recorded as falsified and that was wrong** — the 25/25 run at 754×512 proved elements can be *found* at CI's size and never tested whether a click *lands*. Two more of mine died on the way: window-position drift (screenshot showed the window at (0,0), taskbar at y≈1040) and the stacking breakpoint (it fixes clipping by making the page taller, and height is the binding constraint at 512px). Windows 83/83 at CI's 754×512 **and** at normal size, Android 82/82, unit 506, integration 197. See [[project_windows_mainpage_click_flake]]. |
| 2026-08-06 | **Accessibility: `SemanticProperties` on a pattern-less element is fake** (same branch) | The BO3 switch was a `Border` + `TapGestureRecognizer` carrying `SemanticProperties.Description`, `Hint` and `IsInAccessibleTree="True"` — it passed the accessibility checklist and **could not be activated by a screen reader at all**, because assistive tech invokes through UIA patterns rather than clicking pixels. Now a transparent `Button` overlays the Border and owns the AutomationId and command; appearance unchanged, every click logs `UIA Invoke`. The archetype picker has the same defect for every deck in the list (`ComboBoxPopup.cs`) and is the next branch. Rule added to CLAUDE.md/AGENTS.md: anything tappable must be a real control. Two gotchas: the implicit `Button` style's `MinimumHeightRequest=44` beats an explicit `HeightRequest` (the first overlay rendered 44px over a 32px switch), and `BorderWidth="0"` does not remove a WinUI border without `BorderColor`. See [[feedback_invokable_controls]]. |
| 2026-08-06 | **ReadJournal game 2/3 notes bound (B-08), plus UI assertions that check values** (fix/readjournal-bo3-notes + follow-ups, merged `5078370`, `b9b85fa`, `0dfebb7`, `01cfee8`) | `SelectedNote2`/`SelectedNote3` were computed from the page's first day and bound by nothing, so BO3 notes were stored, exported and restored intact while being unreadable in the app. This gates the restore **conflict UI**, not the restore itself: `RestoreService` already emits "Game 2 notes differ", which would have asked the user to adjudicate invisible text. A data-presence test then broke on panel *height* — virtualized `CollectionView` rows leave the Android accessibility tree once pushed out of view, so 400px of new editors emptied the match list; fixed with `[Order(1)]` after scroll-to-top and `NavigateTo` both failed (the latter no-ops when already on the page). Assertions upgraded from "element exists" to values: TrainerPage now pins wins/losses/ties/win-rate to the canonical formula (proved by breaking `Calculations.cs` and watching only the new test fail), and `MainPage_SaveMatch_WithResult_Saves` stopped asserting that the button it had just clicked still existed. |
| 2026-08-06 | **CI runners: self-hosted retired for good, `build/ci-local.ps1` added** (chore/ci-local-script, merged `29a8ded`) | Self-hosted runners were tried during a GitHub Actions incident and solve neither problem they were aimed at: they cannot cover an outage (the runner polls GitHub, so no workflow runs are created to poll for — the fix commit had `total_count: 0`), and they cannot reproduce the click flake (this machine under full load is still ~36x faster than CI on `Shell ready`). **The decisive objection is security**: the repo is public and both UI workflows trigger on `pull_request`, so a self-hosted runner executes stranger-authored workflow code on the dev machine as the logged-in user. `ci-local.ps1` runs the suites in CI's shape instead — per-fixture by default, sequential because an Android session created under load fails all 79 tests for its whole life even after the load stops, and persisting per-suite logs. A green verdict requires exit 0 **and** a parsed summary **and** a non-zero test count, because `dotnet test --filter` matching nothing exits 0 silently — a latent hole in the real workflows, which select tests by fixture name. |
| 2026-08-06 | **Backup export made lossless, restore service built, TrainerHill de-duplicated** (feat/backup-restore + fix/trainerhill-import-dedupe, merged `1927c96`, `dec530c`, `a7d9ed1`) | The export was lossy in **three** ways, each found by writing the test first. (1) `startTime`/`endTime` were absent entirely — `MatchEntry` stores them apart from `DatePlayed` and `CalculateAverageMatchDuration` / `CalculateWinRateByMatchLength` are computed from them, so a restore would have produced zero-length matches and silently wrong stats. (2) The single `time` field carried `DatePlayed`, the weak value a date picker leaves at midnight; it now carries `StartTime`. **The existing round-trip tests could not have caught this** — their seed set `DatePlayed` equal to `StartTime`, so the two were indistinguishable. (3) Archetype **icons** were not exported at all, and they are user data. Two claims of mine were corrected by the user mid-build: archetypes are not global (`Name` is `[Unique]` table-wide, but each row carries a `TrainerId` owner, so ownership exports as a trainer *name* since ids are renumbered on restore), and the Limitless scrape must be backed up too — resolving a deck to a sprite is real work, and the meta shifts, so an omitted archetype would return as `substitute.png` permanently once it drops out of the top 10. `RestoreService` merges trainers by name, refuses a newer envelope version rather than half-applying it, restores archetypes before matches, and **never overwrites on a key hit**. `MatchDuplicateKey` (`Services/`) is shared with the importer so the two cannot drift: `(StartTime, PlayingId, AgainstId, Result)` per trainer, explicitly *not* authoritative because `AgainstId` identifies a **deck, not a person**. Re-importing a TrainerHill log used to insert every entry again, silently — that is the normal case, since TrainerHill has no incremental export. Also fixed an inverted status message that reported the *error* count using the word "skipped". Restore is **not user-facing yet**: registered in DI, nothing calls it. Unit 478 / integration 193 / zero warnings. See [[project_backup_restore]]. |
| 2026-08-06 | **Loading indicator moved to an overlay, and the 13px that was not the overlay** (feat/loading-indicator, feat/loading-overlay, merged `d71fb75`, `5df2e39`) | Each data page's root `ScrollView` is wrapped in a `Grid` with the indicator as a sibling in the same cell, so it draws over the content and takes no layout space; `InputTransparent` passes clicks through, which matters because the indicator lingers 500ms past the busy gate. Removing the inline host accounted for 144px of shift. **The remaining 13px was not the indicator**: the 1×1 `Busy_*` sentinels are layout participants too, so un-hiding one costs its 1px plus the stack's 12px `Spacing` — exactly 13. They are automation markers, not content, and moved to the Grid layer with MainPage's `PlayerArchetypeIcon2`/`RivalArchetypeIcon2`. Found by measuring element positions in both states, not by reasoning. The test that pins this measures **both positions after a click**: WinAppDriver scrolls an off-screen element into view to click it, so measuring before the first click compared two scroll offsets and reported a phantom 132px shift in the full suite while passing in isolation. |
| 2026-08-06 | **Android CI: the three-button nav fix had never applied** (fix/android-home-gesture-cascade, merged `72a6705`) | The recurring OptionsPageTests cascade is the app being **backgrounded** — logcat shows `ActivityTaskManager` starting the launcher with a HOME intent mid-`scrollIntoView`, and the app logging `Window.Deactivated`. Every later `NoSuchElementException` is downstream and names an innocent element. Root cause of the *recurrence*: `cmd overlay enable` is **additive** — verified by hand on a booted API 35 emulator, enabling the threebutton overlay left `gestural` enabled and `navigation_mode` at 2, exit code 0 either way. So the fix recorded against finding #6 had been a no-op since it landed. Now `enable-exclusive --category`, checked against `navigation_mode` rather than the overlay's own `[x]` flag — which is precisely the flag that lies here. **But the new logging then proved this is not the CI cause**: the CI emulator is already `navigation_mode 0` with no overlay, so the fix matters for local runs only. Current hypothesis, unproven: the click centre landing on the three-button HOME button (`SaveTagButton` centre was `(540, 2329)` on 1080×2400). Added `LogForegroundApp` so a backgrounding stops presenting as a dozen unrelated lookup failures. |
| 2026-08-06 | **Windows MainPage click flake — instrumented, three hypotheses falsified** (fix/windows-mainpage-click-flake, merged `bea27d0`) | Partway through the fixture every click-driven test starts failing while every find-only test keeps passing: element present, click dispatched, handler never runs. Falsified: the leftover-popup theory (the popup closed cleanly and clicks worked for 11s after), global input death (`EnsureBO3On` succeeded after the supposed cut-off), and window geometry. The geometry one **nearly went into the record wrong** — it was first tested at 1024×768 and 800×600, both guessed from the runner's desktop resolution, and the new logging then reported CI's actual window as **754×512**, smaller than either, so the falsification had been run at sizes that could not reproduce it by construction. Re-run at 754×512: 25/25. Surviving lead is timing — CI's `Shell ready` took 8798ms against ~485ms locally, clicks ~1000ms against ~200ms, while these helpers poll on 2500–3000ms deadlines tuned on fast hardware. Not acted on: widening timeouts to chase a theory would mask a real bug. Added `LogInputBlockers` (window count, focused element, popup markers, target measured against the window) and `UITEST_WINDOW_SIZE`. Also fixed two silent guards found on the way: `WaitUntilText` returned `void` so a timeout looked like success, and `ResetBOSwitch` clicked blind without verifying the toggle flipped. |
| 2026-08-05 | **Import hardening + JSON export, and three bugs they uncovered** (feat/import-hardening-and-export, merged `38404ad`) | Import now bounds byte size, nesting depth, entry count and string lengths, all validated **before** any DB write — archetype and tag names are created on demand, so an over-long name would persist as a row after the import "failed". Two size-check paths because it is platform-dependent: Windows gives a seekable stream whose `Length` settles it without reading, Android's content-provider stream throws on `Length` so the cap is counted while copying. Export ships two formats differing in fidelity: TrainerHill's (archetype **slugs**, for interop, lossy because recovering the name needs the Limitless meta list) and a backup envelope (**names verbatim**, lossless). `NameToSlug` is built from the same normalization keys the import lookup uses, so the two are inverses by construction. **Three unrelated bugs surfaced.** (1) Phantom games: `MatchEntry` declares three `[OneToOne]` `Game` properties over three separate `[ForeignKey(typeof(Game))]` columns, SQLite-Net-Extensions cannot disambiguate and filled all three from one row, so every BO1 match rendered three tag sections. (2) The journal listed **oldest-first** — no `ORDER BY`, passed straight through; hidden because the seeder inserts in date order on a fresh DB. (3) The win-rate line chart drew segments jumping backwards in time — `GroupBy` by date with no `OrderBy`. Bugs 1 and 2 concealed each other: `ReadJournalPage_BO3Match_ShowsGame2And3TagViews` clicks the first row believing it is newest, which unsorted was the *oldest* (a BO1), and it passed only because the phantoms made the views appear. Fixing either alone breaks it; fixing both makes it pass honestly and the fixture drops 35s → 5s. Left for their own branches: backup **restore** (needs trainer creation + duplicate policy) and ReadJournal game 2/3 **notes**, which have never been bound (B-08). Local 457 unit / 180 integration / 76 Windows UI; CI green on all three workflows including Android. |
| 2026-08-05 | **Docs refresh** (docs/refresh-after-export) | Audit of every doc against reality after the above. Corrected: the unit project no longer contains six real-SQLite files (moved 2026-08-05); DB concurrency is `DbSession`/`BeginAsync`, not "acquire the static semaphore"; error handling is an **injected** `IErrorHandler`, never `new ModalErrorHandler()`; ops services depend on `ISqliteConnectionFactory`. **DI lifetimes were wrong in both instruction files** — they claimed only MainPage was a singleton, but `ReadJournalPage` and `TrainerPage` and their VMs are singletons too; only Options and About are transient. **A previous "correction" was itself wrong**: `PokemonBattleJournal.Benchmarks` was recorded as having "never existed", but `git log` shows it deleted 2026-07-26 in `b0a4ac1`. AGENTS.md still carried a `Run.ps1` invocation for it and was missing the integration-test command entirely. The stale "harden concurrency — static semaphore on transient TrainerPageViewModel" item is stale twice: no VM holds a static semaphore, and that VM is a singleton. |
| 2026-08-05 | **DB connection failures were unhandled at 20 of 22 sites** (fix/db-connection-error-handling) | `await _factory.GetDatabaseAsync()` sat *above* the `try` in every operations service, so a failure to open the database escaped every catch — no log, no `IErrorHandler`, straight to a crash. The catch only ever covered *query* failures, and opening the connection is the likeliest thing to fail on a real device (corrupt/locked `.db3`, revoked storage permission, full disk). **Moving it inside the try is not sufficient**, which `MatchOperations.SaveAsync` proved: that one site already had it inside, and injecting a connection failure produced `SemaphoreFullException` from `finally { GetLock().Release(); }` releasing a permit `WaitAsync` never took — masking the real error and replacing the return value. The naive fix would have reproduced that 20 more times. Fixed with `Services/DbSession.cs`: `using DbSession session = await _factory.BeginAsync()` pairs connection with held lock and releases on dispose (−103 lines, 22 `finally` blocks deleted). Ordering is load-bearing — open *then* lock, because `InitAsync` takes the same semaphore while creating tables. Secondary wins: the lock is now released *before* the catch body, so `ModalErrorHandler`'s dialog no longer runs while holding the DB semaphore; and operations services now depend on `ISqliteConnectionFactory`, which is both correct DI and the only reason any of this is testable (`GetDatabaseAsync` isn't virtual). 20 new unit tests in `DatabaseConnectionFailureTests`, all confirmed red first. Closes the last open item from [[project_error_handler_di]]; see [[project_db_session_lock_pairing]]. |
| 2026-08-05 | **Windows UI test latency root-caused + CI cache contention fixed** (fix/windows-uia-lookup-latency, chore/lf-line-endings, fix/ci-cache-save-contention — all merged) | Game3Tab's 20s stall was never WinAppDriver caching or the pickers: any lookup for an element that is **absent** inherited the ambient 5s `ImplicitWait` plus a ~1.8s UIA tree walk — **~6.8s per miss vs ~215ms** for the same call when the element exists. `CloseWindowsPickers` does two guaranteed misses in the Game3 `finally` (both result pickers are `IsVisible=false` once Game 3 is selected), so 13.5s of a 20.3s test was cleanup for elements that were never supposed to be there. Fixed via `TestBase.WithImplicitWait(TimeSpan.Zero, …)` on optional lookups, `TryClickIfPresent` pinning its own budget, and one shared `AmbientImplicitWait` (5s both platforms, replacing a 5s/10s split that flipped depending on which helper ran last). **Windows suite ~5min → 1m28s**; Android unchanged at 72/72 / 8m55s. Same bug explained the long-running "Windows CI flake" — a doomed lookup is charged full retry time, so on a slow runner it tripped `FindUIElement`'s 30s deadline. Also: LF line endings everywhere (renormalization touched only 2 files — the repo already stored LF); CI cache contention (five matrix jobs each compressing the same NuGet cache, four failing to reserve it, ~11 min/run wasted, one job hung until cancelled) fixed with `cache/restore` everywhere + one explicit `cache/save`; WinAppDriver escalating 5s/15s/30s backoff replacing a fixed 5s that gave up ~20s into a listener stall. Docs pass: build command in all three docs could never have worked (`-f` with the Windows TFM against the solution), wrong exe path (`win10-x64` → `win-x64`), a Benchmarks project that never existed, ComboBox Cancel "hang" closed as a transient Windows hiccup. Sentry env tagging + alert scoping to production — **done by user**. |
| 2026-08-05 | **Android CI fully green — six stacked bugs resolved** (feat/ci-matrix-per-fixture, merged) | First fully-green matrix (CI + Windows 5/5 + Android 5/5 + build job) at `b5ba64b`. The "flake" was six real bugs peeled one per run: (1) AVD name mismatch spawning double emulators; (2) our own `adb logcat` hanging on the emulator our own teardown killed — both AppiumSetup lifecycle ends now CI-gated, the action owns the emulator on CI; (3) pkill comm-truncation + `-f` self-match — `pkill -f 'crashpad_handle[r]'`; (4) transient adbd device-offline at driver creation — 3x retry; (5) launcher-ANR dialog ("Quickstep isn't responding") owning the whole a11y tree — `hide_error_dialogs 1` + in-gate `aerr_wait` auto-dismiss; (6) the note-Editor focus click landing in the gesture-nav home zone and BACKGROUNDING the app (proven by Sentry lifecycle breadcrumbs in logcat) — click now Windows-only + 3-button-nav overlay on the emulator. Structural: per-fixture matrix both platforms, build-once APK artifact job, stage-3 scroll-to-top lookup, 90s app-ready gate with PageSource dump, keyboard dismiss, nav retry, console-mirrored flushed logs. Riders merged: Sentry Serilog sink (handled errors now reach Sentry; env-tagged dev/prod), light-mode hamburger tint fix (PokeBlue-on-PokeBlue). See [[project_android_ci_gpu_flake]] final summary. |
| 2026-08-05 (session continued) | **Android CI GPU flake — real root cause found, was NOT GPU** (feat/ci-matrix-per-fixture) | The 2026-08-05 "GPU flake" entry below is superseded — investigation continued and found two separate real bugs, not runner flakiness. (1) `AppiumSetup.cs`'s hardcoded `AvdName` constant (`pixel_7_-_api_35`) didn't match the workflow's `avd-name`/`api-level` (34) — `EnsureEmulatorRunning()` launched a SECOND emulator every CI run, contending for KVM/GPU and producing the `ColorBuffer` errors. Fixed (`c3184c2`) by aligning both to API 35. (2) After that fix, `FindUIElement` latency still climbed steadily through a run and eventually every element became unfindable — but a local Windows PerfLog from earlier the same session showed the IDENTICAL shape on real GPU hardware, ruling out GPU entirely. Real cause: both `AppiumSetup.cs` files use one `[SetUpFixture]` driver session for the whole ~72-test assembly, never recycled — long-lived Appium sessions degrade over a run. Fix: `ui-tests-windows.yml`/`ui-tests-android.yml` now matrix per test-fixture class (5 jobs each, `fail-fast: false`), giving each fixture a fresh process/driver/emulator. Windows matrix confirmed 5/5 green (~9-10min each, concurrent). Android matrix result pending. Also added `timeout-minutes: 40` to the Android job (a run once hung indefinitely in emulator teardown after tests completed) and fixed `WaitUntilBusyGone` to tolerate the same WinAppDriver phantom-element race `WaitUntilRemoved` already handled. Full trail in [[project_android_ci_gpu_flake]] and [[project_ci_workflows]]. |
| 2026-08-05 (session continued from 08-04) | **IsBusyMutating gates — MainPage.SaveMatchAsync + full OptionsPage coverage** (feat/mutation-busy-gates, merged `c264d8f`) | Extended the IsBusy* gate pattern to mutating commands (Save/Delete), not just page-load reads: MainPageViewModel.SaveMatchAsync gets its own IsBusyMutating; OptionsPageViewModel.IsBusyMutating now covers all 9 mutating commands (Save/Delete Archetype+Tag, SaveTrainer, SwitchTrainer, DeleteTrainerFromList, DeleteTrainerFile, ImportFromTrainerHill — gate starts AFTER the FilePicker returns, not during user file-browsing time). TDD: 10 new unit tests. Local: unit 488/488, integration 115/115, Windows UI 73/73 (1m49s). Design for the visual loading indicator locked in via user-provided mockup: Fluent-style partial arc (not full ring) fading from solid to transparent, Pokéball riding the leading edge and spinning on its own axis independently, color red or PokeBlue (white ruled out — invisible in light mode). Documented in project_roadmap.md for the next branch — not yet implemented. |
| 2026-08-05 | **Android CI GPU flake discovered** | `gh run list` on Android UI Tests workflow shows 100% failure rate across every run on master AND this branch, even for commits verified 72/72 locally same session. Root cause: `Failed to find ColorBuffer` — GitHub-hosted ubuntu-latest runner's software GPU emulation degrading mid-run, not a real regression. See [[project_android_ci_gpu_flake]]. Do not treat Android CI red as an automatic merge blocker without checking the log for ColorBuffer errors first. |
| 2026-08-04 | **Loading gates + ReadJournal Android stall fixed** (feat/loading-gates) | Named `IsBusy*` gates on all 4 data-page VMs (`IsBusyMatchHistory`, `IsBusyChartData`, `IsBusyArchetypeList` ×2) with hidden 1×1 `Busy_*` sentinel Labels and `WaitUntilBusyGone` test sync in every page test setup. TDD: 8 TCS-gated unit tests written failing first. ReadJournal's 3 tag CollectionViews → FlexLayout+BindableLayout — root cause of ~20 s per UIA call on Android (UI thread never idle → UiAutomator waitForIdle burn). Result: SelectMatch tests 50-111 s → 92-368 ms; full Android suite 18m19s → **8m44s, 72/72**; Windows 73/73; unit 478/478. Also: type-verify-retry for UserNoteInput test (dual-platform CI flake), CI workflows now fire on all branches. |
| 2026-08-04 | **Android flaky-tap retry pattern** (fix/android-mainpage-tests, merged) | In-app popup lifecycle logging proved Appium `.Click()` sometimes never reaches MAUI gesture handlers on Android. Click-verify-retry helpers (`OpenArchetypePopup`, `SelectAndroidPickerItem`, `ResetGame1Tab`, `EnsureBO3On`) — verify a state change with viewport-visible elements, throw on final failure. MainPage 7-10 failing → 25/25. AppiumSetup defaults to VS-Fast-Deployment-safe path (no `pm clear`); CI opts into full rebuild via `ANDROID_USE_INSTALLED=0`. PerfLog/NavLog rotation. Docs/README/roadmap refresh + site legal disclaimer. |
| 2026-07-25 | **Limitless TCG scraper shipped** | `PokemonBattleJournal.Scraper` class library with SOLID/factory architecture (`IMetaDeckFetcher`, `IMetaDeckParser`, `ILimitlessMetaService`, `IMetaServiceFactory`). Upserts top-10 meta decks from limitlesstcg.com on every launch (INSERT OR IGNORE — new decks added, existing preserved). Falls back to 8 hardcoded archetypes only when offline AND table empty. Images are CDN URLs from Limitless; load natively via MAUI `Image`. `LimitlessDeckParser` fixed: guard against empty `annotationText` before `string.Replace` (threw `ArgumentException`). 11 scraper tests. |
| 2026-07-25 | **Archetype picker search** | `ComboBoxPopup` now has a `SearchBar` above the list filtering by display name in real-time (case-insensitive contains). Filter logic extracted to `internal static FilterItems(items, query, displayMemberPath)` for testability. 8 new tests in `ComboBoxPopupTests`. |
| 2026-07-25 | **BO3 tab switcher shipped** | Replaced flat BO3 VerticalStackLayout with progressive tab UI. Game 1 always visible; Game 2 tab appears when `BO3Toggle=true`; Game 3 tab appears when `ShowGame3=true` (results differ OR both Tie — per official Pokemon TCG tournament rules). No tab auto-switch on toggle. Data preserved when switching tabs (only `IsVisible`, no unloading). |
| 2026-07-25 | **Pokeball BO3 toggle shipped** | Replaced native `Switch` with tappable `ball_icon.png` `Image`. Full opacity (1.0) when BO3 on; greyed (0.3) when off via `BoolToObjectConverter`. Label shows "Best of 3" / "Best of 1" via `BoolToObjectConverter`. `ToggleBO3Command` relay command added. `AutomationId="BOSwitch"` preserved on the Image. |
| 2026-07-25 | **StartTime/EndTime fixed to TimeSpan** | `TimePicker.Time` requires `TimeSpan`; binding `DateTime` silently showed midnight. Changed both VM properties to `TimeSpan`. Defaults refreshed in `AppearingAsync` (singleton VM). Guard logic: `OnStartTimeChanged` clamps `EndTime` ≥ `StartTime`; `OnEndTimeChanged` clamps value ≥ `StartTime`. |
| 2026-07-25 | **Unit tests: 221 passing** | Up from 78. ViewModel contract + behavioral tests, scraper tests (11), ComboBoxPopup filter tests (8). |
| 2026-07-25 | **B-01/B-02/B-03 fixed** | B-01: Added `Spacing="10"` to StackLayout wrapping both archetype `ComboBoxControl`s on `MainPage.xaml`. B-02: Placeholder text changed from "Played Archetype"→"Player" and "Rival's Archetype"→"Rival". B-03: `ArchetypePicker` style `WidthRequest` reduced 210→180; `ComboBoxControl` already had `LineBreakMode.TailTruncation` on both labels so long names truncate gracefully. |
| 2026-07-25 | **docs/ reorganization + ROADMAP.md** | Moved AI files to `docs/`; README moved to `docs/README.md` (root deleted). Created `docs/ROADMAP.md` with all features (F-01→F-22) and bugs (B-01→B-05). |
| 2026-07-26 | **Page styling pass** | AboutPage, FirstStartPage, OptionsPage, ReadJournalPage all restyled: PokeYellow/PokeBlue palette, PokemonSolid/SairaRegular fonts, PokeYellow-bordered input sections. Match list cards in ReadJournalPage use PokeBlue border + result badge chips. Delete button on OptionsPage uses BostonRed. |
| 2026-07-26 | **OptionsPage icon picker → ComboBoxControl** | Replaced native `Picker` with `ComboBoxControl` (same searchable dropdown as MainPage). Added `IconItem` record (`Name`, `ImagePath`), `IconItems`/`SelectedIconItem` VM properties. `OnSelectedIconItemChanged` syncs `SelectedIcon` (image preview) and `NewDeckIcon` (save path) — also fixed pre-existing bug where `NewDeckIcon` was never set from UI. `ToDisplayName` helper strips `.png` and title-cases filename for display. 223 unit tests (2 new contract tests). |
| 2026-07-26 | **Trainer switching — shipped** | Full multi-trainer switching via `ITrainerSwitchService` (singleton event bus). `TrainerSwitchService.SwitchToAsync` sets Preferences (name + Id), fires `TrainerChanged` event. `AppShellViewModel` subscribes and syncs the flyout. `MainPageViewModel` and `TrainerPageViewModel` subscribe and reload on switch. `OptionsPageViewModel.SwitchTrainerAsync` calls the service directly. New singleton registrations: `ITrainerSwitchService`, `AppShellViewModel`, `AppShell`. `PreferencesHelper` now stores `TrainerId` (uint) as well as `TrainerName` for stable Id-based lookup. All VMs resolve trainer by Id first, fall back to name. Unsaved-data warning in `AppShellViewModel.SwitchTrainerAsync` (checks `MainPageViewModel.HasUnsavedData`). |
| 2026-07-26 | **Shell flyout — accordion trainer submenu** | Replaced broken `Shell.TitleView` Picker with `Shell.FlyoutContent` accordion. Single-column list: nav items, separator, "Switch Trainer ▶/▼" row (`ToggleTrainerMenuCommand`), indented CollectionView of trainers (`SelectTrainerCommand`). FlyoutHeader (logo) and FlyoutFooter (copyright) unchanged. |
| 2026-07-26 | **TrainerPage DateTime crash — fixed** | `BuildWinRateOverTimeChart` labeler `new DateTime((long)value)` threw `ArgumentOutOfRangeException` when LiveCharts probed with out-of-range tick values. Fixed with ticks range guard: return `string.Empty` when outside `DateTime.MinValue.Ticks..MaxValue.Ticks`. |
| 2026-07-26 | **Pokeball "Went first" toggle — shipped** | Replaced native WinUI3 `CheckBox` (shifted horizontally ~6–8px on tab switch). Replaced with tappable `ball_icon.png` `Image` + `BoolToObjectConverter` for opacity. Three relay commands: `ToggleFirstCheckCommand`, `ToggleFirstCheck2Command`, `ToggleFirstCheck3Command`. |
| 2026-07-26 | **ComboBox layout — left-aligned icon+text, right-pinned arrow** | Inner layout changed from `HorizontalStackLayout` to `Grid(*, Auto)`. `MinimumWidthRequest=130`, `MaximumWidthRequest=260`. |
| 2026-07-26 | **Checkbox shift bug — UNRESOLVED** | In BO3 mode, the CheckBox in the "Went first" row shifts ~6–8 px when switching Game tabs. Confirmed above-panel cause. Fixes tried: removed named style, removed `HorizontalOptions="Center"` from RightColumn, Tab Bar Border, game panel Grid. Leading hypothesis: FlexLayout (`JustifyContent="Center"`) re-centers `RightColumn` when natural width changes between tabs. **Next debug step:** VS Live Visual Tree to compare `ActualOffset.X` of CheckBox in Game 1 vs Game 2. |
| 2026-07-26 | **UI test coverage: all Shell pages** | Every Shell page has a navigation + element-visible Appium test. `AboutPageTests.cs` added; `AutomationId="AboutPageTitle"` added to title label. |
| 2026-07-28 | **In-app DEBUG seeding** | `App.xaml.cs` `SeedDebugDataAsync()` (compiled `#if DEBUG`) runs in App constructor via `Task.Run(...).GetAwaiter().GetResult()` — completes before MAUI visual tree starts. Seeds UITestTrainer + 3 Win matches (idempotent: if UITestTrainer exists and inactive, activates it and returns; if exists and active, returns; otherwise creates). Replaces deleted `TestSeedService`. Android AppiumSetup simplified to `adb install -r` only — no more `pm clear` or `SeedAndPushDb`. |
| 2026-07-28 | **WinUI XamlRoot crash fixed** | `MainPageViewModel.AppearingAsync()` calls `DisplayPromptAsync` (first-boot trainer-name prompt) when `_trainer == null`. On WinUI 3, `ContentDialog.ShowAsync()` requires `XamlRoot` to be set — crashes before window is composed. Root cause: `TrainerOperations.SaveAsync` inserts with `IsActive=0`; seed was not calling `SetActiveAsync`; `GetActiveAsync()` returned null; prompt fired. **Fix:** (1) Seed always calls `SetActiveAsync` after creating or finding UITestTrainer (handles crash-leftover inactive trainer). (2) `MainPageViewModel.AppearingAsync` skips prompt when `%TEMP%\PokemonBattleJournal.uitest` sentinel file present. VS `App.g.cs:71` `Debugger.Break()` is just the debug hook — not the error itself. |
| 2026-07-28 | **Sentinel file pattern** | `UITests.Windows/AppiumSetup.RunBeforeAnyTests()` writes `%TEMP%\PokemonBattleJournal.uitest` before launching; `Dispose()` deletes it. App reads `File.Exists(...)` to skip first-boot prompt under test without blocking manual debug testing. Android doesn't need it — sentinel path doesn't cross emulator boundary; in-app seed activates UITestTrainer so prompt never fires. |
| 2026-07-28 | **Serilog logs path** | Moved from `{AppDataDirectory}/log.txt` to `{AppDataDirectory}/Logs/log.txt` (rolling daily). Directory created in `MauiProgram.cs` before Serilog init. |
| 2026-07-28 | **All UI tests passing** | Windows + Android Appium tests all green in VS test runner. |
| 2026-07-29 | **Android CI build fixed** | `<MauiIcon>` path had `Resources\Appicon\` (lowercase 'i') — Linux CI case-sensitive, failed. Fixed to `Resources\AppIcon\appicon.svg`. CI now builds Android successfully. |
| 2026-07-29 | **Windows CI picker tests fixed** | MAUI Picker on Windows Server opens as child window. Added `SelectWindowsPickerItem(string)` helper to `BaseTest` — iterates all `App.WindowHandles`, switches contexts, catches only `NoSuchElementException`. `MainPageTests` updated to use it. |
| 2026-07-29 | **OptionsPageViewModel bugs fixed** | `SaveTagAsync` + `SaveArchetypeAsync` discarded return values (`_ = await SaveAsync()`). Fixed to assign. `NewDeckIcon` now pre-initialized to `"ball_icon.png"` so icon null-guard never fires silently; `finally` resets to `SelectedIcon`. UI test `OptionsPage_SaveArchetype_WithName_ClearsInput` now passes. |
| 2026-07-29 | **Integration tests added** | `TagOperationsIntegrationTests` (5 tests), `ArchetypeOperationsIntegrationTests` (6 tests), `MatchOperationsIntegrationTests` (6 tests). Pattern: `TestSqliteConnectionFactory` overrides `GetDbPath()` with unique GUID temp file; `IAsyncLifetime` for setup/teardown. `ArchetypeOperations.GetAllAsync` needs `metaService.GetTopDecksAsync` configured to return empty list — substitute returns null by default causing silent empty-list return. Tags model property is `Name` not `TagTxt`. |
| 2026-07-29 | **OptionsPageViewModel + MainPageViewModel unit tests expanded** | 8 new tests for OptionsPageVM (SwitchTrainerAsync, SaveTrainerAsync, DeleteTrainerFileAsync, AppearingAsync, SaveTagAsync/SaveArchetypeAsync zero returns). 2 new tests for MainPageVM (SaveMatchAsync success paths — BO1 and BO3). `SaveMatchAsync` uses `GetActiveAsync()` not `GetByNameAsync()`; `SetupSuccessfulSave()` helper configures both calculator and trainer mocks. Unit test count: 329 passing. |
| 2026-07-30 | **NUnit migration — all test projects** | Branch `feature/nunit-migration`. Replaced xUnit with NUnit 4.6.1 + NUnit3TestAdapter 6.2.0 across `PokemonBattleJournal.Tests`, `PokemonBattleJournal.IntegrationTests`, and all `UITests.*` projects. `[Fact]`→`[Test]`, `[Theory]`/`[InlineData]`→`[Test]`/`[TestCase]`. 13 unit test classes: constructors→`[SetUp]`, `private readonly`→`private X = null!;`, `[FixtureLifeCycle]` removed. Integration tests: `IAsyncLifetime` removed, `InitializeAsync/DisposeAsync`→`[SetUp]/[TearDown]`. UI tests: `ICollectionFixture`/`[Collection]`→NUnit `[SetUpFixture]`. `Assert.Equal(e,a)`→`Assert.That(a, Is.EqualTo(e))`. 350 unit + 22 integration passing. |
| 2026-07-31 | **Enhanced AppiumSetup performance logging** | All Appium setup steps now logged with millisecond timing:
        - Windows AppiumSetup with setup/timing/build/driver creation/shell setup
        - Android AppiumSetup with step timing (tool check, emulator, server start, build, driver, activity wait)
        - BaseTest.NavigateTo with navigation steps timing
        - Windows cleanup timing
        - Added Log/PerfLog methods for consistent timing across all setups
        - Deleted unused coverage files to reduce clutter
        
        All timing logged to `UITests.PerfLog.txt` to enable test performance analysis.
| 2026-07-31 | **Fixed Windows CI WinAppDriver connection failures** | Root cause: stale npm/Appium cache + missing explicit WinAppDriver install on `windows-latest` runners. The cached Appium had a broken Windows driver state that couldn't start WinAppDriver, causing `ECONNREFUSED 127.0.0.1:4725` for all 40 tests. Fix: removed npm cache step, added `winget install Microsoft.WinAppDriver`, added 3-attempt retry (5s delay) for WindowsDriver creation in AppiumSetup.
| 2026-08-01 | **Windows BO3 viewport reset pinned to MainPage ScrollView** | The flaky BO3 CI failures were not solved by resetting focus against a visible control. `ScrollPageToTop()` now targets `MainPageScrollView` directly, because the Windows viewport can clip the bottom section before `SaveMatchButton` or other nearby controls stay findable. BO3 tests now normalize the page at the exact Game 2/Game 3 transition points.
| 2026-07-30 | **UI test NUnit patterns — [OneTimeSetUp] + targeted cleanup** | Each shared page test class has `[OneTimeSetUp]` calling `NavigateTo("Page")` — navigates once per fixture not per test. `MainPageTests` has `[OneTimeTearDown]` calling `InvalidateCurrentPage()` (singleton VM). Per-test cleanup is targeted helpers (`ResetBOSwitch`, `ResetGame1Tab`, `CloseWindowsPickers`, `ClearUserNoteInput`, `DeleteCreatedArchetype`, `DeleteCreatedTag`) called in `try/finally` only by mutating tests. Display-only tests have zero cleanup overhead. All helpers: `ImplicitWait = TimeSpan.Zero` + raw `App.FindElement` (not `FindUIElement`) so 0ms is respected. Removed all `Task.Delay` waits — replaced with implicit-wait polling. Windows UI tests confirmed much faster. |
| 2026-07-30 | **BaseTest perf logging** | `%TEMP%\UITests.PerfLog.txt` — `[SetUp]` starts Stopwatch and logs `START {TestName}`, `[TearDown]` logs `END {TestName} [Status] {ms}ms`. `NavigateTo` logs nav duration to both NavLog and PerfLog. Enables per-test and per-navigation timing diagnostics without instrumentation in each test. |
| 2026-07-30 | **docs/ moved to PokemonBattleJournal/docs/** | VS Solution Explorer includes `PokemonBattleJournal/docs/` (project item). All CLAUDE.md path references updated. `docs/memory/` (repo-local memory) lives at `PokemonBattleJournal/docs/memory/`. `AI-CONTEXT.md` at `PokemonBattleJournal/docs/AI-CONTEXT.md`. |
| 2026-08-03 | **Responsive two-column MainPage layout** | Replaced `FlexLayout Wrap="Wrap"` (broken on Windows — `VerticalStackLayout` gives infinite width so wrap never fires) with `Grid` (2 col × 2 row). `OnSizeAllocated` in `MainPage.xaml.cs` reads actual page width and moves `RightColumn` between col 1 row 0 (wide, ≥560px) and col 0 row 1 (narrow, <560px). `SecondColDef.Width = new GridLength(0)` collapses second column in narrow mode. Note: `GridLength.Zero` does not exist in MAUI — use `new GridLength(0)`. |
| 2026-08-03 | **GitHub Pages site launched** | `index.html` at repo root is the public landing page. `.github/workflows/static.yml` deploys only `index.html` (staged into `_site/`) on push or `workflow_dispatch`. Action versions: `checkout@v7`, `configure-pages@v6`, `upload-pages-artifact@v5`, `deploy-pages@v5` (Node 24). Site live at https://pinkushin.github.io/PokemonBattleJournal/. Enable: GitHub Settings → Pages → Source → GitHub Actions. |
| 2026-08-03 | **README moved to repo root** | `PokemonBattleJournal/docs/README.md` deleted; `README.md` now at repo root so GitHub renders it. Relative links updated to match new path depth. Website link added at top. |

### User decisions

| Topic | Decision |
|---|---|
| **Release platforms** | Windows and Android (user tests both; no Mac hardware). |
| **Multi-trainer** | Full switching shipped. |
| **Android UI tests** | Tied to `pixel_7_-_api_35` — OK for now. |
| **AI onboarding** | `docs/AI-CONTEXT.md` is the canonical context doc. |
| **TrainerPage stats UI** | Not current priority. |
| **Test environment isolation** | Sentinel file pattern — not `#if DEBUG`. Manual debug sessions must still see first-boot prompt. |
| **Seeding** | In-app `#if DEBUG` in `App.xaml.cs` — no external DB manipulation, no TestSeedService. |
| **Test framework** | NUnit 4 across all test projects (unit + integration + UI). Single framework, no xUnit. |
| **UI test cleanup** | Targeted helpers in `try/finally` only for mutating tests — no blanket `[TearDown]` driver calls. |
| **UI test navigation** | `[OneTimeSetUp]` per page class — single `NavigateTo` per fixture, not per test. |

### Active work

- [x] Fix `ComboBoxPopup` empty dropdown
- [x] Fix Windows Appium path
- [x] .NET 10 migration
- [x] LiveCharts2 installed + ViewModel chart data wired
- [x] TrainerPage hang root cause diagnosed (CartesianChart WinUI3 deadlock)
- [x] BO3 tab switcher (Game 1/2/3 tabs, ShowGame3, progressive reveal)
- [x] Pokeball BO3 toggle (replace native Switch with tappable Image)
- [x] StartTime/EndTime TimeSpan fix; AppearingAsync refresh
- [x] `PokemonBattleJournal.Scraper` project — shipped; upserts top-10 on every launch; CDN images; offline fallback
- [x] Test coverage for BO3 tab features — 221+ tests passing
- [x] Archetype picker search — `ComboBoxPopup.FilterItems` extracted + tested
- [x] B-01/B-02/B-03 — dropdown spacing, placeholder text, width reduced to 180
- [x] Page styling pass — AboutPage, FirstStartPage, OptionsPage, ReadJournalPage
- [x] OptionsPage icon picker — replaced native Picker with ComboBoxControl; fixed NewDeckIcon wiring bug
- [x] UI test coverage — all 5 Shell pages have navigation + element-visible Appium tests
- [x] Trainer switching — ITrainerSwitchService, AppShellViewModel accordion flyout, Options page list
- [x] TrainerPage DateTime crash — labeler ticks range guard
- [x] Pokeball "Went first" toggle — replaces native CheckBox
- [x] ComboBox layout — left-align icon+text, right-pin arrow, auto-size 130–260px
- [x] WinUI XamlRoot crash — sentinel file + SetActiveAsync in seed
- [x] In-app DEBUG seeding — replaces TestSeedService; idempotent UITestTrainer creation
- [x] All UI tests passing (2026-07-28)
- [x] NUnit migration — all test projects (2026-07-30, branch feature/nunit-migration)
- [x] UI test [OneTimeSetUp] navigation + targeted cleanup helpers (2026-07-30)
- [x] BaseTest perf logging to UITests.PerfLog.txt (2026-07-30)
- [ ] **Add AppiumSetup timestamped logging** — cover emulator/WinAppDriver launch, Appium init, SeedTestData start/end, individual seed steps; write to PerfLog for full timeline
- [ ] **Merge feature/nunit-migration → master** once Android run confirmed passing
- [ ] **Fix TrainerPage charts** — lazy/virtualized `CartesianChart` loading to avoid WinUI3 deadlock
- [x] **Harden concurrency** — done. The claim this tracked ("static semaphore on transient `TrainerPageViewModel`") is stale twice over: no view model holds a `static` semaphore any more (`MainPageViewModel`, `OptionsPageViewModel` and `ReadJournalPageViewModel` each own a `private readonly SemaphoreSlim`, and `TrainerPageViewModel` has none), and `TrainerPageViewModel` is registered as a **singleton**, not transient. Database serialization now lives in `DbSession` — see [[project_db_session_lock_pairing]]
- [ ] Configurable Android Appium emulator (future)
- [x] **Loading indicator** — Pokéball arc spinner on all four data pages, drawn as an overlay (2026-08-06)
- [x] **Backup export made lossless** — timings, `time` = `StartTime`, archetype icons + owner (2026-08-06)
- [x] **`RestoreService`** — trainer merge-by-name, duplicate detection, conflicts reported not resolved (2026-08-06)
- [x] **TrainerHill re-import no longer duplicates** — shared `MatchDuplicateKey` (2026-08-06)
- [ ] **Wire restore into OptionsPage** — button + file picker + `RestoreResult` status. Nothing calls `IRestoreService` yet, so restore is not user-facing. Integration tests before UI tests before XAML; no modals
- [ ] **Conflict resolution UI** — own branch. `RestoreService` reports conflicts and deliberately leaves them alone; batch them in one pass rather than a dialog per match
- [ ] **Edit and delete a saved match** — neither exists. `MatchOperations` has no update path; `DeleteAsync` exists so delete is a UI-only gap. Editing invalidates the restore's "merging is TrainerHill-only" assumption — see [[project_backup_restore]]
- [ ] **Windows MainPage click flake** — instrumented, not fixed. Three hypotheses falsified; surviving lead is CI timing. Read the `BLOCKERS` lines on the next occurrence before theorising
- [ ] **Android OptionsPageTests cascade** — root cause still open. Gesture nav was a real local hazard but a no-op on CI; current unproven hypothesis is the click centre hitting the three-button HOME button

---

## Project overview

**Pokemon Battle Journal** is a .NET MAUI app for logging and analyzing **Pokemon TCG (PTCG)** battle records. Users record BO1/BO3 matches with archetypes, tags, times, and notes; browse history; and view trainer stats.

- **Author / package id:** `com.PinKushin.PokemonBattleJournal`
- **License:** The Unlicense (`LICENSE.txt`)
- **Pattern:** MVVM with CommunityToolkit.Mvvm source generators
- **Data:** Local SQLite (`PokemonBattleJournal.db3` in app data — GUID-based path on Windows unpackaged)

---

## Tech stack

| Area | Technology |
|---|---|
| Runtime | .NET 10.0 + MAUI |
| Platforms | Android 21+, iOS 15+, MacCatalyst 15+, Windows 10 19041+ |
| Database | `sqlite-net-pcl`, `SQLite.Net.Extensions.Async`, `SQLitePCLRaw.bundle_green` |
| MVVM | CommunityToolkit.Maui 15.x, CommunityToolkit.Mvvm 8.x |
| UI | Native MAUI controls + custom `ComboBoxControl`, `ImagePicker` |
| Charts | `LiveChartsCore.SkiaSharpView.Maui` 2.0.5 — 8 `CartesianChart` on TrainerPage; currently Label placeholders due to WinUI3 init deadlock |
| Logging | Serilog → debug + rolling file (`{AppDataDirectory}/Logs/log.txt`) |
| Errors | Sentry.Maui (DSN in `MauiProgram.cs`); the Serilog Sentry sink is wrapped by `Logging/SentryRedactingSink` so no user content leaves the device — [[project_sentry_privacy_audit]] |
| Tracing | `IPerformanceMonitor`/`ITimedSpan` (Core) + `SentryPerformanceMonitor`; spans on restore and import. Span names bypass the redactor, so the interface takes constants and carries only numeric measurements — [[project_sentry_three_channels]] |
| Unit tests | NUnit 4.6.1, NUnit3TestAdapter 6.2.0, Shouldly, NSubstitute |
| UI tests | Appium (Windows + Android runners, shared tests) |
| Coverage | .NET built-in "Code Coverage" collector + ReportGenerator (`./build/coverage.ps1`) |

**Syncfusion:** fully removed.

---

## Solution structure (`PokemonBattleJournal.slnx`)

```
PokemonBattleJournal.slnx
├── PokemonBattleJournal/                 # Main MAUI app (Deploy)
│   ├── Models/                           # SQLite ORM entities
│   ├── ViewModels/                       # ObservableObject + RelayCommand
│   ├── Views/                            # XAML Shell pages
│   ├── Services/                         # DB + business logic
│   ├── Interfaces/                       # Service contracts
│   ├── Utilities/                        # FileHelper, MainThreadHelper, TaskUtilities, Calculations
│   ├── Controls/                         # ComboBoxControl, ImagePicker
│   ├── Platforms/                        # Android, iOS, MacCatalyst, Windows, Tizen
│   └── Resources/                        # Fonts, sprites, styles, images
├── PokemonBattleJournal.Tests/           # Unit tests (excluded from Release solution build)
├── PokemonBattleJournal.IntegrationTests/ # Real SQLite, temp DB per test (excluded from Release solution build)
├── PokemonBattleJournal.Scraper/         # SOLID scraper library — Limitless TCG meta service
└── PokemonBattleJournal.UITests/
    ├── UITests.Shared/                   # Shared Appium tests + server helper
    ├── UITests.Windows/                  # Windows Appium runner (port 4724)
    └── UITests.Android/                  # Android Appium runner
```

Authoritative list is `PokemonBattleJournal.slnx` — the seven projects above are exactly
what it contains.

A `PokemonBattleJournal.Benchmarks/` BenchmarkDotNet project was listed here until
2026-08-05. **It did exist and was deleted on 2026-07-26 in `b0a4ac1`** ("Remove
PokemonBattleJournal.Benchmarks project entirely"). A previous cleanup pass recorded it as
having "never existed in the repo", which is wrong — `git log --all --
PokemonBattleJournal.Benchmarks` still shows its history. Corrected 2026-08-05. There are no
benchmarks in this repo today and no `Run.ps1`.

**Build notes**

- Open/build with **`PokemonBattleJournal.slnx` only**. Do **not** recreate `PokemonBattleJournal.sln`.
- Debug profile launches the Windows **`.exe`** at `bin\Debug\net10.0-windows10.0.19041.0\win-x64\PokemonBattleJournal.exe`.
- `PokemonBattleJournal.Tests` has `<Build Solution="Release|*" Project="false" />` — Release solution builds skip unit tests.
- Main app: `WindowsPackageType=None` (unpackaged Windows).
- Windows DB path is GUID-based (`%LOCALAPPDATA%\User Name\{GUID}\Data\PokemonBattleJournal.db3`) — external processes can't compute it; use in-app seeding.
- After failed Appium runs: `Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue`

---

## App navigation & lifecycle

```
App constructor
  └─ SeedDebugDataAsync()  (#if DEBUG — blocks until done)
       └─ UITestTrainer created/activated + 3 Win matches inserted
App.CreateWindow()
  └─ AppShell (flyout)
       ├─ MainPage          — create match entries
       ├─ ReadJournalPage   — browse past matches
       ├─ TrainerPage       — stats dashboard
       ├─ OptionsPage       — trainer name, archetypes, tags
       └─ AboutPage         — credits
```

- **Shell:** flyout navigation (`AppShell.xaml`).
- **DI:** `MauiProgram.cs` registers singletons for DB factory, analysis, calculators, trainer switch service, AppShellViewModel, AppShell, MainPage+VM; other pages transient.
- **First-boot prompt:** `MainPageViewModel.AppearingAsync()` shows `DisplayPromptAsync` when `_trainer == null` AND sentinel file `%TEMP%\PokemonBattleJournal.uitest` is absent. In DEBUG + test runs, UITestTrainer is active so `_trainer != null` and sentinel also suppresses it as a second guard.
- **Windows-only:** `CollectionViewHandler` mapping disables multi-select checkbox.

---

## Domain model

### Entities

| Entity | Key fields | Relationships |
|---|---|---|
| `Trainer` | `Id`, `Name` (unique), `IsActive` | → Archetypes, Tags, MatchEntries |
| `Archetype` | `Id`, `Name`, `ImagePath`, `TrainerId` | → Trainer; used in matches (Playing/Against) |
| `Tags` | `Id`, `Name`, `TrainerId` | → Trainer; M2M → `Game` via `TagGame` |
| `Game` | `Id`, `Result?`, `Turn`, `Notes?` | M2M → Tags |
| `TagGame` | `GameId`, `TagId` | Junction |
| `MatchEntry` | `Id`, trainer/archetype FKs, `Result?`, `Game1/2/3Id`, times, `DatePlayed` | → Trainer, archetypes, games |

**Important:** `TrainerOperations.SaveAsync` inserts with `IsActive=false` (default). Must always call `SetActiveAsync` immediately after to make the trainer visible to `GetActiveAsync()`.

### Enums & chart DTOs

```csharp
public enum MatchResult { Win, Loss, Tie }
public class ChartDataPoint { string? Label; double Value; }
public class TimeDataPoint { DateTime Date; double Value; }
```

---

## Architecture

```
Views (XAML) ──bind──► ViewModels ──call──► Services ──► ISqliteConnectionFactory ──► SQLite
                              │
                              └── ModalErrorHandler (alerts on errors)
```

- **Concurrency:** one `SemaphoreSlim` per `SqliteConnectionFactory` instance, taken and released together with the connection by `DbSession` — see [[project_db_session_lock_pairing]]. Never release it in a bare `finally`. `MainPageViewModel`, `OptionsPageViewModel` and `ReadJournalPageViewModel` each hold their own `private readonly SemaphoreSlim` for command re-entrancy.
  - *Corrected 2026-08-05:* this previously warned that `TrainerPageViewModel` had a `static SemaphoreSlim` while registered Transient and could deadlock. Both halves were false — that view model has no semaphore at all, and it is registered as a **singleton**. Verified against `MauiProgram.cs` and the view model source.
- **Transactions:** `RunInTransactionAsync` for multi-step saves/deletes.
- **Match results:** `MatchResultCalculatorFactory` → `BO1ResultCalculator` or `BO3ResultCalculator`.
- **Stats:** `MatchAnalysisService` (11 calculation methods) feeds `TrainerPageViewModel`.
- **Test detection:** `DeviceInfo.Platform == DevicePlatform.Unknown` ⇒ unit test environment.

### DI registration (`MauiProgram.cs`)

| Lifetime | Types |
|---|---|
| Singleton | `ISqliteConnectionFactory`, `IMatchResultsCalculatorFactory`, `IMatchAnalysisService`, `ITrainerSwitchService`, `AppShellViewModel`, `AppShell`, `MainPage`, `MainPageViewModel` |
| Transient | All other pages + ViewModels |

---

## Pages & ViewModels

| Page | VM | Purpose | Notable UI |
|---|---|---|---|
| `MainPage` | `MainPageViewModel` | Log BO1/BO3 matches | 2× `ComboBoxControl` (archetypes), native `TimePicker`/`DatePicker`/`Picker`, tag `CollectionView`, save/validate |
| `ReadJournalPage` | `ReadJournalPageViewModel` | Match history browser | `CollectionView`, game/tag detail panels |
| `TrainerPage` | `TrainerPageViewModel` | Stats dashboard | Stat labels + 8 `lvc:CartesianChart` sections (**currently Label placeholders** — charts deadlock WinUI3 on init; lazy loading needed) |
| `OptionsPage` | `OptionsPageViewModel` | Trainer, archetype, tag CRUD | `Border`+`Entry`, `ComboBoxControl` icon picker, buttons |
| `AboutPage` | `AboutPageViewModel` | Credits | Static content |

---

## Services layer

| Service | Role |
|---|---|
| `SqliteConnectionFactory` | Connection init, table creation, exposes `Trainers`/`Matches`/`Archetypes`/`Tags` ops |
| `MatchOperations` | Save/get/delete matches + games + tag links (transactional) |
| `TrainerOperations` | Trainer CRUD; `SaveAsync` inserts `IsActive=0`; must call `SetActiveAsync` separately |
| `ArchetypeOperations` | CRUD; blocks delete if used; seeds defaults |
| `TagOperations` | CRUD; cascades `TagGame`; seeds defaults |
| `MatchAnalysisService` | Win rate, archetypes, tags, opponents, streaks, duration, etc. |
| `TrainerSwitchService` | Singleton event bus. `SwitchToAsync` sets Preferences (name + Id), fires `TrainerChanged(Trainer)`. VMs subscribe in constructor, reload data on event. |
| `BO1ResultCalculator` / `BO3ResultCalculator` | Aggregate game results into match result |
| `ModalErrorHandler` | Shows error alerts (`IErrorHandler`) — **injected**, never constructed at a call site |
| `TrainerHillImportService` | Reads TrainerHill JSON. Size/depth/count/length caps enforced **before** any DB write. De-duplicates on re-import |
| `ExportService` | Two formats: TrainerHill's (archetype slugs, lossy interop) and the backup envelope (names verbatim + timings + archetype icons, lossless) |
| `RestoreService` | Reads the backup envelope back. Merges trainers by name, restores archetypes before matches, refuses a newer envelope version. **Registered in DI but not wired to any UI yet** |
| `MatchDuplicateKey` | Shared by import and restore: `(StartTime, PlayingId, AgainstId, Result)` per trainer. Not authoritative — `AgainstId` is a deck, not a person — so a hit skips and reports, never deletes |

**Win rate formula (canonical):** `(wins + 0.5 * ties) / total * 100` in `Calculations.CalculateWinRate`.

---

## Custom controls

| Control | Location | Purpose |
|---|---|---|
| `ComboBoxControl` | `Controls/ComboBoxControl/` | MainPage + OptionsPage archetype/icon picker (icon + name popup, searchable) |
| `ImagePicker` | `Controls/ImagePicker.cs` | Options page icon selection |

Text inputs use **Border + Label + Entry** (not a separate `HintedEntry` control).

---

## Test coverage

### ViewModel binding contracts

Each page ViewModel has a `{VM}ContractTests.cs` in `PokemonBattleJournal.Tests/ViewModels/` that uses reflection to assert every XAML-bound property and command still exists. **Do not rename or remove any of these members without updating the contract tests.**

XAML bindings by page:

| Page | ViewModel | Bound properties | Bound commands |
|---|---|---|---|
| MainPage | `MainPageViewModel` | WelcomeMsg, Archetypes, PlayerSelected, RivalSelected, BO3Toggle, StartTime, EndTime, DatePlayed, CurrentDateTimeDisplay, TagCollection, TagsSelected, UserNoteInput, FirstCheck, PossibleResults, Result, SavedFileDisplay, Match2TagsSelected, UserNoteInput2, FirstCheck2, Result2, Match3TagsSelected, UserNoteInput3, FirstCheck3, Result3, ShowGame3, IsGame1Selected, IsGame2Selected, IsGame3Selected, HasValidationErrors, ValidationMessage | AppearingCommand, DisappearingCommand, SaveMatchCommand, SelectGame1Command, SelectGame2Command, SelectGame3Command, ToggleBO3Command, ToggleFirstCheckCommand, ToggleFirstCheck2Command, ToggleFirstCheck3Command |
| OptionsPage | `OptionsPageViewModel` | Title, NameInput, NewDeckName, SelectedIcon, IconCollection, TagInput, AllTrainers | AppearingCommand, SaveTrainerCommand, SaveArchetypeCommand, SaveTagCommand, SaveAllCommand, DeleteTrainerFileCommand, SwitchTrainerCommand, DeleteTrainerFromListCommand |
| ReadJournalPage | `ReadJournalPageViewModel` | WelcomeMsg, MatchHistory, SelectedMatch, SelectedNote, PlayingName, PlayingIconSource, AgainstName, AgainstIconSource, DatePlayed, Game1TagsInfo, Game2TagsInfo, Game3TagsInfo, HasGame1Tags, HasGame2Tags, HasGame3Tags, TagsSelectedGame1, TagsSelectedGame2, TagsSelectedGame3, Result | AppearingCommand, LoadMatchCommand |
| TrainerPage | `TrainerPageViewModel` | WelcomeMsg, WinAverage, Wins, Losses, Ties, AverageMatchDuration, FirstTurnAdvantage, StreakInfo, MostPlayedArchetypes, ArchetypeWinRates, OpponentPerformance, TagUsage, WinRateOverTime, WinRateByMatchLength | AppearingCommand |
| FirstStartPage | `FirstStartPageViewModel` | TrainerNameInput | SaveTrainerNameCommand |

### Unit tests

350 passing + 22 integration tests (NUnit 4.6.1, NSubstitute, Shouldly).

**Still lightly covered:**
- `SqliteConnectionFactory` init (integration-style)
- `ModalErrorHandler`, `FileHelper`, `PreferencesHelper`, `MainThreadHelper`
- End-to-end UI flows beyond basic Appium smoke tests

### UI tests (Appium)

| Runner | Status |
|---|---|
| `UITests.Windows` | Passing. Port 4724. Sentinel file written before launch. `CleanupTestTrainer()` in Dispose deletes UITestTrainer + cascade. SQLite packages in csproj for teardown. |
| `UITests.Android` | Passing. `adb install -r` only; in-app seed handles data idempotently. AVD `pixel_7_-_api_35`. |
| `UITests.Shared` | `AppWindowTests`, `MainPageTests`, `AboutPageTests`, `OptionsPageTests`, `ReadJournalPageTests`, `TrainerPageTests` |

**UI test NUnit patterns (established 2026-07-30):**
- `[OneTimeSetUp]` calls `NavigateTo("Page")` — once per fixture class, not per test
- `[OneTimeTearDown]` calls `InvalidateCurrentPage()` on MainPage (singleton VM — state doesn't reset on navigate-away)
- Cleanup helpers use `ImplicitWait = TimeSpan.Zero` + raw `App.FindElement` (not `FindUIElement` which ignores ImplicitWait) — called in `try/finally` only by tests that mutate state
- `BaseTest.[SetUp]` starts per-test Stopwatch; `[TearDown]` writes `END {test} [status] {ms}ms` to `%TEMP%\UITests.PerfLog.txt`
- No `Task.Delay` anywhere — all waits via implicit-wait polling

**Seeding flow:**
1. Windows `AppiumSetup.RunBeforeAnyTests()` writes sentinel file, starts Appium (port 4724), launches exe.
2. App constructor runs `SeedDebugDataAsync()` — creates/activates UITestTrainer + 3 Win matches.
3. `MainPageViewModel.AppearingAsync()` finds active UITestTrainer, skips first-boot prompt (sentinel also guards).
4. Tests run against seeded state.
5. `Dispose()`: kills app, calls `CleanupTestTrainer()` (deletes only UITestTrainer data), deletes sentinel.

**Android seeding:** same in-app seed runs on install. No sentinel needed — `DisplayPromptAsync` uses native Android dialogs (no XamlRoot requirement); UITestTrainer active means prompt doesn't fire anyway.

### Coverage

- `./build/coverage.ps1 -IncludeUI` — see [[project_coverage_tooling]].
- Uses .NET's built-in `"Code Coverage"` collector, **not** coverlet (`"XPlat Code Coverage"`).
  Only the built-in one instruments the app process the Appium tests drive, so only it can
  report coverage of Views, Controls, `App` and `MauiProgram`.
- Block coverage exists only in the `.coverage` binary and its converted XML; cobertura carries
  line and branch alone.

---

## Platform notes

| Platform | Notes |
|---|---|
| Windows | Unpackaged; exe at `bin\Debug\net10.0-windows10.0.19041.0\win-x64\PokemonBattleJournal.exe`; DB at `%LOCALAPPDATA%\User Name\{GUID}\Data\PokemonBattleJournal.db3` |
| Android | `RunAOTCompilation=False`, `PublishTrimmed=False` in Release; AVD `pixel_7_-_api_35` |
| iOS / MacCatalyst | Min OS 15.0 |

---

## Code conventions

- `[ObservableProperty]` / `[RelayCommand]` — CommunityToolkit source generators
- Async DB access: `using DbSession session = await _factory.BeginAsync();` **inside** the `try`. It opens the connection and takes the write lock together and releases on dispose. Never a bare `finally { GetLock().Release(); }` — if opening failed that releases a permit nothing took and throws `SemaphoreFullException` over the real error. See [[project_db_session_lock_pairing]]
- Errors: `try/catch` + an **injected** `IErrorHandler`. Never `new ModalErrorHandler()` — it is registered in `MauiProgram` and injected at every site ([[project_error_handler_di]])
- Logging: log **ids, counts and lengths — never names, free text or file paths**. Local sinks keep everything; `SentryRedactingSink` forwards only values whose type cannot express user content, so a name in a log template arrives at Sentry as `[redacted]` ([[project_sentry_privacy_audit]])
- Logging: `_logger.LogInformation/Debug/Warning/Error` throughout services/VMs; logs at `{AppDataDirectory}/Logs/log.txt`
- Tests: `{Class}Tests`, methods `{Method}_{Scenario}_{Expected}`

---

## Roadmap

| Item | Status |
|---|---|
| Remove Syncfusion | ✅ Done |
| Expand unit tests | ✅ 221+ tests passing |
| Fix MainPage archetype ComboBoxControl | ✅ Done |
| Fix Windows Appium path | ✅ Done |
| Multi-trainer switcher UI | ✅ Shipped |
| In-app DEBUG seeding | ✅ Shipped (2026-07-28) |
| WinUI XamlRoot crash fix | ✅ Shipped (2026-07-28) |
| TrainerPage charts (LiveCharts2) | 🔲 In progress — VM ready, XAML has placeholders; lazy loading needed |
| Configurable Android Appium AVD | 🔲 Deferred |
| .NET 10 upgrade | ✅ Done |
| JSON import/export (TrainerHill format) | ✅ Shipped (2026-08-05), hardened + de-duplicated (2026-08-06) |
| Loading indicator (Pokéball spinner, overlay) | ✅ Shipped (2026-08-06) |
| Backup export — lossless envelope | ✅ Shipped (2026-08-06) — timings + archetype icons + owner |
| Backup restore — service | ✅ Shipped (2026-08-06) — **not user-facing; nothing calls it yet** |
| Backup restore — OptionsPage wiring | 🔲 Next |
| Restore conflict resolution UI | 🔲 Planned — own branch; service reports conflicts and leaves them alone |
| Edit / delete a saved match | 🔲 Planned — no update path exists; changes the restore's merge assumptions |
| PTCG Live battle log parsing | 🔲 Planned — clipboard only, there is no log file; sample + format notes in `docs/samples/` |
| Deck maker (deck lists tied to archetypes) | 🔲 Planned |
| Deck comparer (side-by-side diff) | 🔲 Planned |

---

## Commands cheat sheet

```powershell
# Build main app (Windows). -f must target the app project, NOT the solution — the
# test and scraper projects do not target the Windows TFM, so passing -f to
# PokemonBattleJournal.slnx fails with NETSDK1005 on every one of them.
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-windows10.0.19041.0

# Build everything (all projects, all their own TFMs — no -f)
dotnet build PokemonBattleJournal.slnx

# Unit tests — genuinely unit tests now (~1s). The six real-SQLite files that
# used to live here moved to the IntegrationTests project on 2026-08-05.
dotnet test PokemonBattleJournal.Tests/PokemonBattleJournal.Tests.csproj

# Integration tests (real SQLite, temp DB file per test)
dotnet test PokemonBattleJournal.IntegrationTests/PokemonBattleJournal.IntegrationTests.csproj

# Windows UI tests
dotnet test PokemonBattleJournal.UITests/UITests.Windows/UITests.Windows.csproj

# Android UI tests (needs pixel_7_-_api_35 AVD). Deploy first if APP code changed,
# or the suite tests the previously installed build and passes:
#   dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-android -t:Install
dotnet test PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj

# Coverage (built-in collector; -IncludeUI is the only way to cover Views/Controls)
./build/coverage.ps1 -IncludeUI

# Kill orphaned app after failed Appium run
Stop-Process -Name PokemonBattleJournal -Force -ErrorAction SilentlyContinue
```

---

## For AI assistants — maintenance rules

1. **Read `AI-CONTEXT.md`** (this file) at the start of a session.
2. **Read `docs/memory/`** — persistent memory files for user preferences, feedback, and project decisions.
3. **Update [Session log](#session-log)** when: user states a new goal, you discover a bug/blocker, you finish significant work, before a long multi-file refactor.
4. **Keep facts accurate:** prefer reading code over trusting stale sections.
5. **Commit freely** — no need to ask first. Every commit must build (optimally zero warnings) before it lands. **Push sparingly** — only when testing something against CI, or once work is stable; don't push on every commit (hits CI + user's bandwidth).
6. **Minimize scope** — match existing patterns; don't reintroduce Syncfusion or heavy dependencies without explicit approval.
7. **TrainerOperations.SaveAsync** always creates `IsActive=false` — always call `SetActiveAsync` after programmatic trainer creation.
8. **Sentinel file** (`%TEMP%\PokemonBattleJournal.uitest`) suppresses first-boot prompt under test — never use `#if DEBUG` for this guard.
