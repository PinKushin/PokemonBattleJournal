using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.ViewModels
{
    /// <summary>
    /// One conflicted match awaiting a decision.
    /// </summary>
    /// <remarks>
    /// Deliberately thin. Everything that decides anything — what Keep/Append/Replace mean,
    /// whether a difference is a genuine conflict or one side merely knowing more, how two notes
    /// diff — lives in <c>PokemonBattleJournal.Core</c> where Stryker can measure it. What is
    /// left here is observable state and three commands that set a field, which is the part a
    /// mutation score would have nothing useful to say about anyway.
    ///
    /// Selecting a resolution writes NOTHING. Choices are staged until the user applies them,
    /// so closing the app mid-review leaves the database untouched and the backup file still on
    /// disk — the safe failure. Applying immediately would leave a database partly from the
    /// backup with no record of where it stopped.
    /// </remarks>
    public partial class ConflictRowViewModel : ObservableObject
    {
        public ConflictRowViewModel(RestoreConflict conflict)
        {
            Conflict = conflict;
            SelectedResolution = conflict.SuggestedResolution;
            Games = [.. conflict.Games.Select(g => new ConflictGameRowViewModel(g))];
        }

        public RestoreConflict Conflict { get; }

        /// <summary>The games that differ, each with its own diff.</summary>
        public IReadOnlyList<ConflictGameRowViewModel> Games { get; }

        /// <summary>
        /// What the user chose, or null when they have not chosen yet.
        /// </summary>
        /// <remarks>
        /// Pre-set from <see cref="RestoreConflict.SuggestedResolution"/>, which is Append when
        /// one side merely knows more and null when the two genuinely contradict. Null rows are
        /// the ones that still need reading, so leaving them unselected is what makes them stand
        /// out — the user scans for blanks rather than re-reading everything.
        /// </remarks>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsResolved))]
        [NotifyPropertyChangedFor(nameof(ChoiceSummary))]
        public partial ConflictResolution? SelectedResolution { get; set; }

        public bool IsResolved => SelectedResolution is not null;

        /// <summary>Trainer and match time, enough to find the match in the journal.</summary>
        public string Title => $"{Conflict.TrainerName} — {Conflict.StartTime:g}";

        public string Description => Conflict.Description;

        /// <summary>
        /// True when the app suggested an answer, so the UI can say why a row arrived pre-filled.
        /// </summary>
        public bool WasSuggested => Conflict.SuggestedResolution is not null;

        public string ChoiceSummary => SelectedResolution switch
        {
            ConflictResolution.Keep => "Keeping what is in the app",
            ConflictResolution.Append => "Keeping both",
            ConflictResolution.Replace => "Taking the backup's version",
            _ => "No choice made yet",
        };

        [RelayCommand]
        public void ChooseKeep() => SelectedResolution = ConflictResolution.Keep;

        [RelayCommand]
        public void ChooseAppend() => SelectedResolution = ConflictResolution.Append;

        [RelayCommand]
        public void ChooseReplace() => SelectedResolution = ConflictResolution.Replace;
    }

    /// <summary>
    /// One differing game inside a conflicted match, with its note diff already computed.
    /// </summary>
    public partial class ConflictGameRowViewModel : ObservableObject
    {
        public ConflictGameRowViewModel(ConflictGameDiff diff)
        {
            Diff = diff;
            NoteDiffLines = NoteDiff.Compute(diff.ExistingNotes, diff.IncomingNotes);
        }

        public ConflictGameDiff Diff { get; }

        /// <summary>Line-by-line diff of the two notes, for a git-style display.</summary>
        public IReadOnlyList<NoteDiffLine> NoteDiffLines { get; }

        public string Label => Diff.Label;

        public bool HasNoteDifference => Diff.NotesDiffer;

        public bool HasTagDifference => Diff.TagsDiffer;

        /// <summary>Tags only the backup has, rendered the way a diff renders them.</summary>
        public string AddedTagSummary => Diff.AddedTags.Count == 0
            ? string.Empty
            : "+ " + string.Join(", ", Diff.AddedTags);

        /// <summary>Tags only the stored match has.</summary>
        public string RemovedTagSummary => Diff.RemovedTags.Count == 0
            ? string.Empty
            : "- " + string.Join(", ", Diff.RemovedTags);

        /// <summary>
        /// Set when this game exists on only one side, which is a difference the note and tag
        /// displays cannot show because both are blank on the missing side.
        /// </summary>
        public string PresenceSummary
        {
            get
            {
                if (!Diff.PresenceDiffers)
                {
                    return string.Empty;
                }

                return Diff.ExistingPresent
                    ? $"{Diff.Label} is not in the backup"
                    : $"{Diff.Label} exists only in the backup";
            }
        }

        public bool HasPresenceDifference => Diff.PresenceDiffers;
    }
}
