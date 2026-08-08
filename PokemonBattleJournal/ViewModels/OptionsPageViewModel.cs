using System.Text;
using CommunityToolkit.Maui.Storage;
using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.ViewModels
{
    public partial class OptionsPageViewModel : ObservableObject
    {
        private readonly ISqliteConnectionFactory _connection;
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private Trainer? _trainer;
        private readonly ILogger<OptionsPageViewModel> _logger;
        private readonly IErrorHandler _errorHandler;
        private readonly ITrainerSwitchService _switchService;
        private readonly AppShellViewModel _shellVm;
        private readonly ITrainerHillImportService _importService;
        private readonly IExportService _exportService;
        private readonly IRestoreService _restoreService;
        private readonly IPerformanceMonitor _monitor;

        public OptionsPageViewModel(ILogger<OptionsPageViewModel> logger, ISqliteConnectionFactory connection, ITrainerSwitchService switchService, AppShellViewModel shellVm, ITrainerHillImportService importService, IExportService exportService, IRestoreService restoreService, IErrorHandler errorHandler, IPerformanceMonitor monitor)
        {
            _connection = connection;
            _logger = logger;
            _errorHandler = errorHandler;
            _switchService = switchService;
            _shellVm = shellVm;
            _importService = importService;
            _exportService = exportService;
            _restoreService = restoreService;
            _monitor = monitor;
        }

        [ObservableProperty]
        public partial List<Trainer> AllTrainers { get; set; } = [];

        [ObservableProperty]
        public partial Trainer? SelectedSwitchTrainer { get; set; }

        partial void OnSelectedSwitchTrainerChanged(Trainer? value)
        {
            if (value is null || value.Id == (_trainer?.Id ?? 0))
                return;
            SwitchTrainerCommand.Execute(value);
        }

        [ObservableProperty]
        public partial string Title { get; set; } = "Options";

        [ObservableProperty]
        public partial string TrainerName { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string? NameInput { get; set; }

        [ObservableProperty]
        public partial string? TagInput { get; set; }

        [ObservableProperty]
        public partial string? NewDeckName { get; set; }

        [ObservableProperty]
        public partial string? NewDeckIcon { get; set; } = "ball_icon.png";

        [ObservableProperty]
        public partial List<string> IconCollection { get; set; } = new List<string>();

        [ObservableProperty]
        public partial string SelectedIcon { get; set; } = "ball_icon.png";

        [ObservableProperty]
        public partial List<IconItem> IconItems { get; set; } = [];

        [ObservableProperty]
        public partial IconItem? SelectedIconItem { get; set; }

        partial void OnSelectedIconItemChanged(IconItem? value)
        {
            SelectedIcon = value?.ImagePath ?? "ball_icon.png";
            NewDeckIcon = value?.ImagePath;
        }

        [ObservableProperty]
        public partial List<Archetype> AllArchetypes { get; set; } = [];

        [ObservableProperty]
        public partial List<Tags> AllTags { get; set; } = [];

        [ObservableProperty]
        public partial string FileConfirmMessage { get; set; } = "Delete Trainer File?";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasImportStatus))]
        public partial string ImportStatusMessage { get; set; } = string.Empty;

        public bool HasImportStatus => !string.IsNullOrEmpty(ImportStatusMessage);

        [RelayCommand]
        public async Task ImportFromTrainerHillAsync()
        {
            if (_trainer is null)
            {
                _logger.LogWarning("Import not started: no active trainer");
                return;
            }

            try
            {
                FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select TrainerHill battle log",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".json"] },
                        { DevicePlatform.Android, ["application/json"] },
                        { DevicePlatform.iOS, ["public.json"] },
                        { DevicePlatform.MacCatalyst, ["public.json"] },
                    })
                });

                if (file is null)
                    return;

                // Gate starts AFTER the file picker returns — the picker is user browsing
                // time, not app processing time, and would otherwise leave Busy_Mutating
                // visible for however long the user takes to pick a file.
                IsBusyMutating = true;
                try
                {
                    await using Stream stream = await file.OpenReadAsync();
                    (int imported, int skippedDuplicates, List<string> errors) =
                        await _importService.ImportAsync(stream, _trainer.Id);

                    // Duplicates and errors are reported separately. The old message called
                    // errors "skipped", which read as "these entries were already here" when it
                    // actually meant "these entries failed" — opposite meanings, and the
                    // reassuring one was shown for the alarming case.
                    List<string> parts = [$"Imported {imported} matches"];
                    if (skippedDuplicates > 0)
                        parts.Add($"{skippedDuplicates} already present");
                    if (errors.Count > 0)
                        parts.Add($"{errors.Count} failed");
                    ImportStatusMessage = string.Join(", ", parts);

                    _logger.LogInformation(
                        "TrainerHill import: {Imported} imported, {Skipped} already present, {Errors} errors",
                        imported, skippedDuplicates, errors.Count);

                    if (errors.Count > 0)
                    {
                        // Kept verbatim ON PURPOSE. These name entries from the imported file, so
                        // they are user content — but a count alone cannot diagnose "2 failed",
                        // and this log is where the answer has to be. It stays complete on the
                        // device and is withheld on the way to Sentry by SentryRedactingSink.
                        // That split is the whole point of having both layers.
                        _logger.LogWarning("TrainerHill import errors: {Errors}", string.Join("; ", errors));
                    }
                }
                finally
                {
                    IsBusyMutating = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TrainerHill import");
                ImportStatusMessage = "Import failed";
                _errorHandler.HandleError(ex);
            }
        }

        /// <summary>
        /// True in Debug builds only. Bound to the visibility of the loading-indicator toggle
        /// so the affordance simply is not there in Release.
        /// </summary>
        public static bool IsDebugBuild =>
#if DEBUG
            true;
#else
            false;
#endif

        /// <summary>
        /// Holds <see cref="IsBusyMutating"/> open so the loading indicator can be seen.
        /// </summary>
        /// <remarks>
        /// A toggle rather than a timed "simulate a slow operation", deliberately. A timed
        /// version would need a <c>Task.Delay</c>, which this project bans outside of tests, and
        /// it would give UI tests a window to race. Toggling holds the gate open until it is
        /// switched off, so the test is deterministic and the animation can be watched for as
        /// long as it takes to judge it.
        ///
        /// Debug-only in effect: the button that invokes it is bound to <see cref="IsDebugBuild"/>.
        /// </remarks>
        [RelayCommand]
        public void ToggleSimulatedLoading()
        {
            IsBusyMutating = !IsBusyMutating;
            _logger.LogInformation("Simulated loading toggled {State}", IsBusyMutating ? "on" : "off");
        }

        /// <summary>
        /// Sends one trace and one error to Sentry so the pipeline can be confirmed end to end.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Exists because "tracing is configured" and "traces arrive" turned out to be different
        /// claims. TracesSampleRate was set for months while the dashboard stayed empty: the rate
        /// samples transactions that already EXIST, and MAUI creates none automatically, so it was
        /// sampling an empty set. Nothing in the test suite could have caught that — tests never
        /// initialise the SDK, so they exercise the no-op path by construction.
        /// </para>
        /// <para>
        /// Covers BOTH channels deliberately, because they fail independently: a span (tracing)
        /// and a logged exception (errors, via the RedactedSentry sink). A run that shows one and
        /// not the other localises the problem immediately.
        /// </para>
        /// <para>
        /// The work inside the span is a real <c>NoteDiff</c> at the bound rather than a delay.
        /// <c>Task.Delay</c> is banned in production code here, and a zero-duration span is
        /// indistinguishable from a timer artefact — 300 lines measures ~757us, which is a
        /// duration worth looking at.
        /// </para>
        /// <para>
        /// Debug-only in effect: the button that invokes it is bound to <see cref="IsDebugBuild"/>.
        /// Nothing here carries user content; every string is a constant written in this file.
        /// </para>
        /// </remarks>
        [RelayCommand]
        public void SendSentryDiagnostics()
        {
            string left = string.Join("\n", Enumerable.Range(0, NoteDiff.MaxLines).Select(i => $"{i % 4 + 1} Card {i}"));
            string right = string.Join("\n", Enumerable.Range(0, NoteDiff.MaxLines)
                .Select(i => i % 10 == 0 ? $"{i % 4 + 1} Swapped {i}" : $"{i % 4 + 1} Card {i}"));

            using (ITimedSpan span = _monitor.StartSpan("diagnostics", "sentry smoke test"))
            {
                IReadOnlyList<NoteDiffLine> diff = NoteDiff.Compute(left, right);
                span.SetMeasurement("lines.in", NoteDiff.MaxLines);
                span.SetMeasurement("lines.out", diff.Count);
            }

            // A second span marked failed, so the dashboard shows both statuses and a broken
            // SetFailed cannot hide behind an all-green trace.
            using (ITimedSpan failing = _monitor.StartSpan("diagnostics", "sentry smoke test - failed span"))
            {
                failing.SetFailed();
            }

            try
            {
                throw new InvalidOperationException(
                    "Sentry diagnostics: deliberate test error, not a real failure.");
            }
            catch (InvalidOperationException ex)
            {
                // Logged rather than handed to IErrorHandler on purpose: the handler shows a
                // modal, and this is about confirming delivery, not exercising the dialog.
                _logger.LogError(ex, "Sentry diagnostics event sent with {Spans} spans", 2);
            }

            ExportStatusMessage = "Sent a test trace and error to Sentry";
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasExportStatus))]
        public partial string ExportStatusMessage { get; set; } = string.Empty;

        public bool HasExportStatus => !string.IsNullOrEmpty(ExportStatusMessage);

        /// <summary>
        /// Exports the active trainer's matches in TrainerHill's format.
        /// </summary>
        [RelayCommand]
        public async Task ExportTrainerHillAsync()
        {
            if (_trainer is null)
            {
                _logger.LogWarning("Export not started: no active trainer");
                ExportStatusMessage = "No active trainer to export";
                return;
            }

            await SaveExportAsync(
                () => _exportService.ExportTrainerHillAsync(_trainer.Id),
                $"trainerhill-battle-log-{SanitizeForFileName(_trainer.Name)}-{DateTime.Now:yyyy-MM-dd}.json",
                ExportFormat.TrainerHill);
        }

        /// <summary>
        /// Exports every trainer in the app's own backup envelope.
        /// </summary>
        [RelayCommand]
        public async Task ExportBackupAsync() =>
            await SaveExportAsync(
                () => _exportService.ExportBackupAsync(),
                $"pbj-backup-{DateTime.Now:yyyy-MM-dd}.json",
                ExportFormat.Backup);

        /// <summary>
        /// Which serializer an export ran, for logging.
        /// </summary>
        /// <remarks>
        /// An enum rather than a string because the redacting Sentry sink forwards values by
        /// TYPE: an enum is diagnostic by construction, whereas a string — even a literal one
        /// written here — would be withheld, and widening the sink's allowlist to let it through
        /// would weaken the rule for every other string in the app.
        /// </remarks>
        private enum ExportFormat
        {
            TrainerHill,
            Backup,
        }

        /// <summary>
        /// Serializes, then writes the result wherever the user chooses.
        /// </summary>
        /// <remarks>
        /// Shared by both export commands because the only difference between them is which
        /// serializer runs and what the file is called.
        ///
        /// The busy gate deliberately covers only the serialize step, not the save dialog:
        /// the dialog is user browsing time, and gating it would leave Busy_Mutating visible
        /// for as long as the user takes to choose a folder — the same reasoning as the import
        /// picker.
        /// </remarks>
        private async Task SaveExportAsync(Func<Task<string>> serialize, string suggestedFileName, ExportFormat format)
        {
            try
            {
                string json;
                IsBusyMutating = true;
                try
                {
                    json = await serialize();
                }
                finally
                {
                    IsBusyMutating = false;
                }

                using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
                FileSaverResult result = await FileSaver.Default.SaveAsync(suggestedFileName, stream);

                if (!result.IsSuccessful)
                {
                    // Cancelling the dialog surfaces here as an unsuccessful result, so this is
                    // the normal "changed my mind" path as well as the genuine failure path.
                    // Log at Information and say nothing alarming.
                    _logger.LogInformation(result.Exception, "Export not saved: {Format}", format);
                    ExportStatusMessage = string.Empty;
                    return;
                }

                // Never the path. The suggested name embeds the trainer's name, and the chosen
                // path embeds the OS account name — which is a real name far more often than a
                // trainer name is. Neither answers a question the format and size do not.
                _logger.LogInformation("Exported {Format}: {Bytes} bytes", format, json.Length);
                ExportStatusMessage = $"Exported to {Path.GetFileName(result.FilePath)}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during export of {Format}", format);
                ExportStatusMessage = "Export failed";
                _errorHandler.HandleError(ex);
            }
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRestoreStatus))]
        public partial string RestoreStatusMessage { get; set; } = string.Empty;

        public bool HasRestoreStatus => !string.IsNullOrEmpty(RestoreStatusMessage);

        /// <summary>
        /// Restores a backup envelope the user picks off disk.
        /// </summary>
        /// <remarks>
        /// Deliberately not gated on an active trainer, unlike the TrainerHill import: a backup
        /// carries its own trainers, and the case where this matters most — a fresh install with
        /// no trainer at all — is exactly the case a trainer guard would block.
        /// </remarks>
        [RelayCommand]
        public async Task RestoreBackupAsync()
        {
            try
            {
                FileResult? file = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a Pokémon Battle Journal backup",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, [".json"] },
                        { DevicePlatform.Android, ["application/json"] },
                        { DevicePlatform.iOS, ["public.json"] },
                        { DevicePlatform.MacCatalyst, ["public.json"] },
                    })
                });

                if (file is null)
                    return;

                string json;

                // Gate starts AFTER the picker returns — the picker is user browsing time, not
                // app processing time, and gating it would leave Busy_Mutating up for as long as
                // the user takes to choose a file. Same reasoning as the import and export paths.
                IsBusyMutating = true;
                try
                {
                    await using Stream stream = await file.OpenReadAsync();

                    // The service enforces the same ceiling, but only once the file is already a
                    // string in memory. Checking the length first means a hostile or corrupt
                    // multi-gigabyte file is refused instead of being materialised to be measured.
                    if (stream.CanSeek && stream.Length > IRestoreService.MaxBackupBytes)
                    {
                        _logger.LogWarning("Restore refused: {Bytes} bytes exceeds the limit", stream.Length);
                        RestoreStatusMessage = $"Backup refused: the file is larger than {IRestoreService.MaxBackupBytes / (1024 * 1024)}MB.";
                        return;
                    }

                    using StreamReader reader = new(stream);
                    json = await reader.ReadToEndAsync();
                }
                finally
                {
                    IsBusyMutating = false;
                }

                await ApplyRestoreAsync(json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading backup file for restore");
                RestoreStatusMessage = "Restore failed";
                _errorHandler.HandleError(ex);
            }
        }

        /// <summary>
        /// Runs a restore over already-read JSON and reports what it did.
        /// </summary>
        /// <remarks>
        /// Split out from the command so the reporting is testable: <c>FilePicker</c> is a MAUI
        /// static with no seam, so anything downstream of it in the command body can only be
        /// exercised on a device.
        /// </remarks>
        internal async Task ApplyRestoreAsync(string json)
        {
            IsBusyMutating = true;
            try
            {
                RestoreResult result = await _restoreService.RestoreBackupAsync(json);
                RestoreStatusMessage = DescribeRestore(result);

                _logger.LogInformation(
                    "Restore: {Created} trainers created, {Merged} trainers merged, {Inserted} matches inserted, {Skipped} already present, {Conflicts} conflicts, {Errors} errors",
                    result.TrainersCreated, result.TrainersMerged, result.MatchesInserted,
                    result.MatchesSkippedIdentical, result.Conflicts.Count, result.Errors.Count);

                // The status line can only carry counts. Whatever a count stands for has to reach
                // the log, or a user reporting "2 failed" leaves nothing to diagnose it with.
                if (result.Errors.Count > 0)
                    _logger.LogWarning("Restore errors: {Errors}", string.Join("; ", result.Errors));

                // Replaces rather than appends: restoring a second file must not leave rows from
                // the first lingering with no way to tell them apart.
                Conflicts.Clear();
                foreach (RestoreConflict conflict in result.Conflicts)
                {
                    Conflicts.Add(new ConflictRowViewModel(conflict));
                }

                CurrentConflictIndex = 0;
                RefreshConflictWalk();

                if (result.Conflicts.Count > 0)
                {
                    _logger.LogWarning("Restore conflicts left unapplied: {Conflicts}", string.Join("; ",
                        result.Conflicts.Select(c => $"{c.TrainerName} @ {c.StartTime:o}: {c.Description}")));
                }

                if (result.TrainersCreated + result.MatchesInserted > 0)
                    await ReloadAfterRestoreAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during backup restore");
                RestoreStatusMessage = "Restore failed";
                _errorHandler.HandleError(ex);
            }
            finally
            {
                IsBusyMutating = false;
            }
        }

        /// <summary>
        /// Conflicted matches from the last restore, awaiting a decision.
        /// </summary>
        /// <remarks>
        /// Populated by the restore and emptied only by applying. Nothing here has been written
        /// to the database — that is the point of staging. A user who closes the app mid-review
        /// loses their selections and nothing else, and the backup file is still on disk.
        /// </remarks>
        public ObservableCollection<ConflictRowViewModel> Conflicts { get; } = [];

        /// <summary>
        /// Fills the review list with two sample conflicts so the section can be seen and driven.
        /// </summary>
        /// <remarks>
        /// Debug-only in effect: the button that invokes it is bound to <see cref="IsDebugBuild"/>,
        /// the same arrangement as the simulated-loading toggle.
        ///
        /// It exists because the conflict section is invisible until a conflict exists, and
        /// producing a real one takes an export, an edit and a restore — a round trip through a
        /// file picker that Appium cannot drive. The service path is covered by integration
        /// tests; what needs a UI test is the rendering, the three buttons and the apply gate.
        ///
        /// Both samples point at match ids no row can hold, so applying them reports nothing done
        /// rather than rewriting a real match. One arrives pre-selected and one blank, which is
        /// the distinction the UI is supposed to make visible.
        /// </remarks>
        [RelayCommand]
        public void SeedSampleConflicts()
        {
            Conflicts.Clear();

            Conflicts.Add(new ConflictRowViewModel(new RestoreConflict
            {
                TrainerName = TrainerName.Length > 0 ? TrainerName : "Trainer",
                ExistingMatchId = uint.MaxValue,
                StartTime = DateTime.Now,
                Description = "game 1 notes differ",
                Games =
                [
                    new ConflictGameDiff
                    {
                        Label = "Game 1",
                        ExistingNotes = "dead draw",
                        IncomingNotes = "misplayed turn 3",
                        ExistingTags = ["bricked"],
                        IncomingTags = ["donked"],
                    },
                ],
            }));

            Conflicts.Add(new ConflictRowViewModel(new RestoreConflict
            {
                TrainerName = TrainerName.Length > 0 ? TrainerName : "Trainer",
                ExistingMatchId = uint.MaxValue - 1,
                StartTime = DateTime.Now.AddHours(-2),
                Description = "game 1 notes differ",
                Games =
                [
                    new ConflictGameDiff
                    {
                        Label = "Game 1",
                        ExistingNotes = string.Empty,
                        IncomingNotes = "they conceded",
                    },
                ],
            }));

            CurrentConflictIndex = 0;
            RefreshConflictWalk();
            _logger.LogInformation("Seeded {Count} sample conflicts for review", Conflicts.Count);
        }


        public bool HasConflicts => Conflicts.Count > 0;

        /// <summary>
        /// Which conflict the review is sitting on.
        /// </summary>
        /// <remarks>
        /// The review shows ONE match at a time rather than listing them all. Two reasons, and
        /// the second is not cosmetic. A restore can carry any number of conflicted matches, so a
        /// list grows without bound inside a page that already scrolls. And a list means the
        /// choice buttons live in a DataTemplate, where they are virtualised — realised, recycled
        /// and re-realised as rows move — which is what produced the CI race where
        /// ApplyConflictsButton sat in the UIA tree advertising no invokable pattern. One match
        /// at a time means one set of controls, built once and re-bound, which is the structural
        /// fix rather than a retry around the symptom.
        ///
        /// A match's own differing games still stack inside the single card. That is bounded at
        /// three by BO3; the match count is not.
        /// </remarks>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CurrentConflict))]
        [NotifyPropertyChangedFor(nameof(ConflictPositionLabel))]
        [NotifyPropertyChangedFor(nameof(HasNextConflict))]
        [NotifyPropertyChangedFor(nameof(HasPreviousConflict))]
        public partial int CurrentConflictIndex { get; set; }

        /// <summary>The conflict being reviewed, or null when none are outstanding.</summary>
        public ConflictRowViewModel? CurrentConflict =>
            CurrentConflictIndex >= 0 && CurrentConflictIndex < Conflicts.Count
                ? Conflicts[CurrentConflictIndex]
                : null;

        /// <summary>
        /// Position in the walk, e.g. "Match 2 of 7".
        /// </summary>
        /// <remarks>
        /// The affordance a rebase gives you: how much is left before you start answering. Empty
        /// when there is nothing to review, so the label does not read "Match 1 of 0".
        /// </remarks>
        public string ConflictPositionLabel => Conflicts.Count == 0
            ? string.Empty
            : $"Match {CurrentConflictIndex + 1} of {Conflicts.Count}";

        public bool HasNextConflict => CurrentConflictIndex < Conflicts.Count - 1;

        public bool HasPreviousConflict => CurrentConflictIndex > 0;

        /// <summary>Moves to the next conflict, stopping at the last.</summary>
        /// <remarks>
        /// Clamps rather than wraps. Wrapping would drop the user back on a match they have
        /// already answered, which reads as the button having done nothing.
        /// </remarks>
        [RelayCommand]
        public void NextConflict()
        {
            if (HasNextConflict)
                CurrentConflictIndex++;
        }

        /// <summary>Moves back one conflict so a staged choice can be revised before Apply.</summary>
        [RelayCommand]
        public void PreviousConflict()
        {
            if (HasPreviousConflict)
                CurrentConflictIndex--;
        }

        /// <summary>
        /// Re-raises everything derived from the conflict collection after it changes.
        /// </summary>
        /// <remarks>
        /// The collection is observable but the derived members are not computed from it by the
        /// generator, and the index may now point past the end — applying removes rows, so a walk
        /// sitting on the last one would leave CurrentConflict null while HasConflicts is still
        /// true, rendering an empty panel with no way out.
        /// </remarks>
        private void RefreshConflictWalk()
        {
            int clamped = Conflicts.Count == 0 ? 0 : Math.Min(CurrentConflictIndex, Conflicts.Count - 1);

            if (clamped != CurrentConflictIndex)
            {
                // Setting the property raises the four dependents on its own.
                CurrentConflictIndex = clamped;
            }
            else
            {
                OnPropertyChanged(nameof(CurrentConflict));
                OnPropertyChanged(nameof(ConflictPositionLabel));
                OnPropertyChanged(nameof(HasNextConflict));
                OnPropertyChanged(nameof(HasPreviousConflict));
            }

            OnPropertyChanged(nameof(HasConflicts));
        }

        /// <summary>
        /// Writes every decision the user has actually made, and leaves the rest listed.
        /// </summary>
        /// <remarks>
        /// Per match and all-or-nothing, because that is how the service applies it. The status
        /// afterwards names both halves — what went in and what is still outstanding — since the
        /// failure this whole flow is designed against is a user believing more was saved than
        /// was.
        /// </remarks>
        [RelayCommand]
        public async Task ApplyConflictsAsync()
        {
            List<ConflictRowViewModel> answered = [.. Conflicts.Where(c => c.IsResolved)];
            if (answered.Count == 0)
            {
                _logger.LogWarning("Apply conflicts declined: no decisions have been made");
                return;
            }

            IsBusyMutating = true;
            try
            {
                int applied = 0;
                foreach (ConflictRowViewModel row in answered)
                {
                    // SelectedResolution cannot be null here — IsResolved is exactly that check —
                    // but reading it once keeps the call site honest about what is being applied.
                    ConflictResolution resolution = row.SelectedResolution!.Value;
                    _ = await _restoreService.ApplyResolutionAsync(row.Conflict, resolution);
                    _ = Conflicts.Remove(row);
                    applied++;
                }

                RefreshConflictWalk();

                List<string> parts = [$"{applied} applied"];
                if (Conflicts.Count > 0)
                {
                    parts.Add($"{Conflicts.Count} still {(Conflicts.Count == 1 ? "needs" : "need")} review");
                }

                RestoreStatusMessage = string.Join(", ", parts);
                _logger.LogInformation(
                    "Conflicts applied: {Applied} written, {Outstanding} outstanding",
                    applied, Conflicts.Count);

                await ReloadAfterRestoreAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying conflict resolutions");
                RestoreStatusMessage = "Applying your choices failed";
                _errorHandler.HandleError(ex);
            }
            finally
            {
                IsBusyMutating = false;
            }
        }

        /// <summary>
        /// Re-reads the collections this page is displaying after a restore has written to them.
        /// </summary>
        /// <remarks>
        /// A restore inserts trainers, archetypes and tags underneath a page that already loaded
        /// them in AppearingAsync. Without this the fresh-install case — no trainer, restore a
        /// backup — leaves the trainer picker empty on the very page the user is standing on,
        /// which is indistinguishable from a restore that silently did nothing.
        ///
        /// The shell reload is what puts a restored trainer in the title bar and switcher. It is
        /// last because it is the only part that touches another ViewModel.
        /// </remarks>
        private async Task ReloadAfterRestoreAsync()
        {
            AllTrainers = await _connection.Trainers.GetAllAsync();
            AllArchetypes = await _connection.Archetypes.GetAllAsync();
            AllTags = await _connection.Tags.GetAllAsync();
            SelectedSwitchTrainer = AllTrainers.FirstOrDefault(t => t.Id == (_trainer?.Id ?? 0));
            await _shellVm.LoadAsync();
        }

        /// <summary>
        /// Turns a <see cref="RestoreResult"/> into the one sentence the user gets.
        /// </summary>
        /// <remarks>
        /// Every outcome is named separately and none of them share a word. The import status
        /// message used to report the *error* count as "skipped", which reads as "already
        /// present" — opposite meanings, with the reassuring phrasing on the alarming case.
        ///
        /// A result with nothing at all in it and an error is a whole-file rejection (wrong
        /// version, too large, unparsable). That reason is shown verbatim: "0 matches, 1 failed"
        /// would hide the only sentence saying whether to pick a different file or update the app.
        /// </remarks>
        internal static string DescribeRestore(RestoreResult result)
        {
            int touched = result.TrainersCreated + result.TrainersMerged + result.MatchesInserted
                + result.MatchesSkippedIdentical + result.Conflicts.Count;

            if (touched == 0)
                return result.Errors.Count > 0 ? result.Errors[0] : "Backup contained no matches";

            List<string> parts = [$"Restored {Pluralize(result.MatchesInserted, "match", "matches")}"];

            if (result.TrainersCreated > 0)
                parts.Add($"{Pluralize(result.TrainersCreated, "trainer", "trainers")} added");

            if (result.MatchesSkippedIdentical > 0)
                parts.Add($"{result.MatchesSkippedIdentical} already present");

            // "not applied" is spelled out because the conflict resolution UI does not exist yet:
            // until it does, this count is the only sign that anything is still outstanding.
            if (result.Conflicts.Count > 0)
                parts.Add($"{result.Conflicts.Count} {(result.Conflicts.Count == 1 ? "needs" : "need")} review (not applied)");

            if (result.Errors.Count > 0)
                parts.Add($"{result.Errors.Count} failed");

            return string.Join(", ", parts);
        }

        private static string Pluralize(int count, string singular, string plural) =>
            $"{count} {(count == 1 ? singular : plural)}";

        /// <summary>Strips characters that are illegal in a file name on any target platform.</summary>
        private static string SanitizeForFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "trainer";

            // Path.GetInvalidFileNameChars() is platform-specific and Android's set is smaller
            // than Windows', so an export named on one platform could be unopenable on another.
            // An explicit allowlist keeps the suggested name portable.
            char[] cleaned = [.. name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-')];
            return new string(cleaned).Trim('-');
        }

        /// <summary>
        /// Loading gate: true while trainers + archetypes + tags load. Bound to the
        /// hidden Busy_ArchetypeList sentinel Label for UI test sync.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyBusy))]
        public partial bool IsBusyArchetypeList { get; set; }

        /// <summary>
        /// Loading gate: true while any Save/Delete/Import/Switch command is mutating the
        /// DB or reloading trainer-scoped state. Bound to the hidden Busy_Mutating
        /// sentinel Label. Without this, UI tests race the CollectionView rebind that
        /// follows a delete — WinAppDriver can return a phantom element whose ID is
        /// null/empty mid-rebind, surfacing as InvalidOperationException. Covers:
        /// SaveTrainerAsync, SwitchTrainerAsync, DeleteTrainerFromListAsync,
        /// DeleteTrainerFileAsync, SaveTagAsync, DeleteTagAsync, SaveArchetypeAsync,
        /// DeleteArchetypeAsync, ImportFromTrainerHillAsync.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsAnyBusy))]
        public partial bool IsBusyMutating { get; set; }

        /// <summary>
        /// True while EITHER gate is up. This is what the loading indicator binds to.
        /// </summary>
        /// <remarks>
        /// The page has a load gate and a mutate gate, and binding the spinner to one would
        /// leave the other operation with no feedback. The [NotifyPropertyChangedFor] on both
        /// inputs is load-bearing: without it the binding never updates and the spinner simply
        /// never appears, which no amount of correct XAML would reveal.
        /// </remarks>
        public bool IsAnyBusy => IsBusyArchetypeList || IsBusyMutating;

        [RelayCommand]
        public async Task AppearingAsync()
        {
            _logger.LogInformation("OptionsPageViewModel appearing");
            IsBusyArchetypeList = true;
            try
            {
                IconCollection = await PopulateIconCollectionAsync();
                _trainer = _switchService.ActiveTrainer ?? await _connection.Trainers.GetActiveAsync();
                TrainerName = _trainer?.Name ?? string.Empty;
                Title = $"{TrainerName}'s Options";
                _logger.LogInformation("Active trainer resolved: {TrainerId}", _trainer?.Id ?? 0);
                AllTrainers = await _connection.Trainers.GetAllAsync();
                SelectedSwitchTrainer = AllTrainers.FirstOrDefault(t => t.Id == (_trainer?.Id ?? 0));
                AllArchetypes = await _connection.Archetypes.GetAllAsync();
                AllTags = await _connection.Tags.GetAllAsync();
                _logger.LogInformation(
                    "Options loaded for trainer {TrainerId}: {TrainerCount} trainers, {ArchetypeCount} archetypes, {TagCount} tags",
                    _trainer?.Id ?? 0, AllTrainers.Count, AllArchetypes.Count, AllTags.Count);
            }
            catch (Exception ex)
            {
                // Log only — no dialog from AppearingAsync. ContentDialog requires XamlRoot
                // which isn't set until the page is fully composed; calling it here crashes WinUI (0xc000027b).
                // Ids and counts, not names: this line reaches Sentry as an error event. The
                // icon collection used to be destructured here, which said nothing a count does
                // not — the question it ever answered was "did the icons load at all".
                _logger.LogError(ex, "Error loading Options page for trainer {TrainerId} ({IconCount} icons)",
                    _trainer?.Id ?? 0, IconCollection?.Count ?? 0);
            }
            finally
            {
                IsBusyArchetypeList = false;
            }
        }

        [RelayCommand]
        public async Task SwitchTrainerAsync(Trainer trainer)
        {
            if (trainer.Id == (_trainer?.Id ?? 0))
                return;

            IsBusyMutating = true;
            try
            {
                await _switchService.SwitchToAsync(trainer);
                _trainer = trainer;
                TrainerName = trainer.Name ?? string.Empty;
                Title = $"{TrainerName}'s Options";
                FileConfirmMessage = $"Delete {TrainerName}'s Trainer File?";
                await _shellVm.LoadAsync();
            }
            finally
            {
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task DeleteTrainerFromListAsync(Trainer trainer)
        {
            bool confirmed = await Shell.Current.DisplayAlertAsync(
                "Delete Trainer",
                $"Delete '{trainer.Name}' and all their match data?",
                "Delete", "Cancel");
            if (!confirmed)
                return;

            bool deletedActive = trainer.Id == (_trainer?.Id ?? 0);
            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                _ = await _connection.Trainers.DeleteAsync(trainer);
                AllTrainers = await _connection.Trainers.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting trainer {TrainerId}", trainer.Id);
                _errorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }

            if (deletedActive)
            {
                _trainer = null;
                TrainerName = string.Empty;
                await HandleNoActiveTrainerAsync();
            }

            AllTrainers = await _connection.Trainers.GetAllAsync();
            await _shellVm.LoadAsync();
        }

        [RelayCommand]
        public async Task SaveTrainerAsync()
        {
            if (NameInput is null)
            {
                _logger.LogWarning("Trainer not saved: name is empty");
                return;
            }

            TrainerName = NameInput;
            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Trainers.SaveAsync(NameInput);
                if (affected == 0)
                {
                    // No id exists yet on a failed save, so the length stands in for the name.
                    // It is the property that actually explains a rejection — empty, or long
                    // enough to hit a column limit — and it identifies nobody.
                    _logger.LogInformation("Trainer not saved: name is {NameLength} chars", NameInput.Length);
                    return;
                }
                _logger.LogInformation("Trainer saved: name is {NameLength} chars", NameInput.Length);
                _trainer = await _connection.Trainers.GetByNameAsync(NameInput);
                if (_trainer is null)
                {
                    _logger.LogInformation("Trainer not found immediately after save: name is {NameLength} chars",
                        NameInput.Length);
                    return;
                }
                _logger.LogInformation("Trainer loaded: {TrainerId}", _trainer.Id);
                await _switchService.SwitchToAsync(_trainer);
                AllTrainers = await _connection.Trainers.GetAllAsync();
                _shellVm.OnTrainerCreated(_trainer);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Trainer: name is {NameLength} chars", NameInput?.Length ?? 0);
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                NameInput = null;
                Title = $"{TrainerName}'s Options";
                IsBusyMutating = false;
            }
        }
        [RelayCommand]
        public async Task SaveTagAsync()
        {
            // Named separately so a log reader can tell which input was missing. An empty
            // TagInput after a UI interaction means the text never reached the field; a null
            // trainer means the page was used before AppearingAsync resolved one.
            if (TagInput is null)
            {
                _logger.LogWarning("Tag not saved: tag name is empty");
                return;
            }

            if (_trainer is null)
            {
                // The length, not the text. What this line has to establish is that TagInput WAS
                // populated, so the reader knows the trainer was the missing piece — the tag's
                // wording never mattered, and tags are free text a person typed.
                _logger.LogWarning("Tag not saved: no active trainer (tag was {TagLength} chars)", TagInput.Length);
                return;
            }

            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Tags.SaveAsync(TagInput, _trainer.Id);
                if (affected == 0)
                {
                    _logger.LogInformation("Tag not saved: tag is {TagLength} chars", TagInput.Length);
                    return;
                }
                _logger.LogInformation("Tag saved for trainer {TrainerId} ({TagLength} chars)",
                    _trainer.Id, TagInput.Length);
                AllTags = await _connection.Tags.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Tag: tag is {TagLength} chars", TagInput.Length);
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                TagInput = null;
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task SaveArchetypeAsync()
        {
            if (NewDeckName is null)
            {
                _logger.LogWarning("Archetype not saved: deck name is empty");
                return;
            }

            if (NewDeckIcon is null)
            {
                _logger.LogWarning("Archetype not saved: no deck icon selected (deck name was {DeckNameLength} chars)",
                    NewDeckName.Length);
                return;
            }

            if (_trainer is null)
            {
                _logger.LogWarning("Archetype not saved: no active trainer (deck name was {DeckNameLength} chars)",
                    NewDeckName.Length);
                return;
            }

            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Archetypes.SaveAsync(NewDeckName, NewDeckIcon, _trainer.Id);
                if (affected == 0)
                {
                    _logger.LogInformation("Archetype not saved: deck name is {DeckNameLength} chars, icon selected {HasIcon}",
                        NewDeckName.Length, NewDeckIcon is not null);
                    return;
                }
                _logger.LogInformation("Archetype saved for trainer {TrainerId} ({DeckNameLength} chars, icon selected {HasIcon})",
                    _trainer.Id, NewDeckName.Length, NewDeckIcon is not null);
                AllArchetypes = await _connection.Archetypes.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving Archetype: deck name is {DeckNameLength} chars, icon selected {HasIcon}",
                    NewDeckName.Length, NewDeckIcon is not null);
                _errorHandler.HandleError(ex);
            }
            finally
            {
                NewDeckName = null;
                NewDeckIcon = SelectedIcon; // reset to current icon selection (default: ball_icon.png)
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task DeleteArchetypeAsync(Archetype archetype)
        {
            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Archetypes.DeleteAsync(archetype);
                if (affected == 0)
                {
                    _logger.LogInformation("Archetype not deleted: {ArchetypeId}", archetype.Id);
                    return;
                }
                _logger.LogInformation("Archetype deleted: {ArchetypeId}", archetype.Id);
                AllArchetypes = await _connection.Archetypes.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Archetype: {ArchetypeId}", archetype.Id);
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task DeleteTagAsync(Tags tag)
        {
            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                int affected = await _connection.Tags.DeleteAsync(tag);
                if (affected == 0)
                {
                    _logger.LogInformation("Tag not deleted: {TagId}", tag.Id);
                    return;
                }
                _logger.LogInformation("Tag deleted: {TagId}", tag.Id);
                AllTags = await _connection.Tags.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Tag: {TagId}", tag.Id);
                _errorHandler.HandleError(ex);
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }
        }

        [RelayCommand]
        public async Task SaveAllAsync()
        {
            try
            {
                await SaveTrainerAsync();
                await SaveTagAsync();
                await SaveArchetypeAsync();
                _logger.LogInformation("All saved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving all");
                _errorHandler.HandleError(ex);
            }
        }

        [RelayCommand]
        public async Task DeleteTrainerFileAsync()
        {
            if (_trainer is null)
            {
                _logger.LogWarning("Trainer file not deleted: no active trainer");
                return;
            }

            // Captured before the try, because the try clears _trainer on the way through and
            // the catch would otherwise have nothing left to name.
            uint trainerId = _trainer.Id;
            IsBusyMutating = true;
            try
            {
                await _semaphore.WaitAsync();
                _ = await _connection.Trainers.DeleteAsync(_trainer);
                _trainer = null;
                TrainerName = string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting Trainer: {TrainerId}", trainerId);
                _errorHandler.HandleError(ex);
                return;
            }
            finally
            {
                _ = _semaphore.Release();
                IsBusyMutating = false;
            }

            await HandleNoActiveTrainerAsync();
            AllTrainers = await _connection.Trainers.GetAllAsync();
            Title = _trainer is not null ? $"{TrainerName}'s Options" : "Options";
            await _shellVm.LoadAsync();
        }

        // Called after the active trainer is deleted. Offers the user a choice:
        // switch to an existing account, create a new one, or continue as guest
        // (guest = null active trainer; MainPage will re-prompt on next visit).
        private async Task HandleNoActiveTrainerAsync()
        {
            if (Shell.Current is null) return; // unit test environment

            List<Trainer> remaining = await _connection.Trainers.GetAllAsync();

            string[] options = remaining.Count > 0
                ? [.. remaining.Select(t => t.Name ?? "Unknown"), "Create New Account"]
                : ["Create New Account"];

            string? choice = await Shell.Current.DisplayActionSheetAsync(
                "Choose an account", "Continue as Guest", null, options);

            if (string.IsNullOrEmpty(choice) || choice == "Continue as Guest")
                return; // guest — MainPage prompt will fire next time

            if (choice == "Create New Account")
            {
                await PromptAndCreateTrainerAsync();
                return;
            }

            Trainer? picked = remaining.FirstOrDefault(t => t.Name == choice);
            if (picked is not null)
            {
                await _switchService.SwitchToAsync(picked);
                _trainer = picked;
                TrainerName = picked.Name ?? string.Empty;
            }
        }

        private async Task PromptAndCreateTrainerAsync()
        {
            string? name = await Shell.Current.DisplayPromptAsync(
                "New Account", "Enter your trainer name",
                accept: "Save", cancel: "Skip",
                placeholder: "Trainer name", maxLength: 50);

            if (string.IsNullOrWhiteSpace(name)) return;

            await _connection.Trainers.SaveAsync(name);
            Trainer? created = await _connection.Trainers.GetByNameAsync(name);
            if (created is null) return;

            await _switchService.SwitchToAsync(created);
            _trainer = created;
            TrainerName = created.Name ?? string.Empty;
            _shellVm.OnTrainerCreated(created);
        }

        //Icon name collection file reader
        private async Task<List<string>> PopulateIconCollectionAsync()
        {
            string? imageName;
            List<string> iconCollection = [];
            try
            {
                await _semaphore.WaitAsync();
                await using Stream fileStream = await FileSystem.Current.OpenAppPackageFileAsync("icon_file_names.txt");
                using StreamReader reader = new(fileStream);
                while ((imageName = await reader.ReadLineAsync()) is not null)
                {
                    iconCollection.Add(imageName);
                }
                IconItems = iconCollection
                    .Select(f => new IconItem(ToDisplayName(f), f))
                    .ToList();
                return iconCollection;
            }
            catch (Exception ex)
            {
                // Log only — called from AppearingAsync before XamlRoot is composed; dialog would crash WinUI.
                _logger.LogError(ex, "Error loading icon collection from app package");
                return iconCollection;
            }
            finally
            {
                _ = _semaphore.Release();
            }
        }

        internal static string ToDisplayName(string filename)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(filename);
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(name.Replace('_', ' '));
        }
    }
}