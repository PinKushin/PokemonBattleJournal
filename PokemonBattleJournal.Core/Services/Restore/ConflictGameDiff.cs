namespace PokemonBattleJournal.Services.Restore
{
    /// <summary>
    /// Both versions of one game in a conflicted match, and what differs between them.
    /// </summary>
    /// <remarks>
    /// A conflict used to carry only a <c>Description</c> string — "game 2 notes differ" — which
    /// is enough to warn someone and not enough to let them choose. Keep, Append and Replace are
    /// only a real decision if the user can see what each would do, so both sides travel verbatim.
    ///
    /// The difference sets are DERIVED, never stored. Storing them alongside the lists they
    /// describe is how a display ends up disagreeing with the data it was built from.
    /// </remarks>
    public sealed record ConflictGameDiff
    {
        /// <summary>Which game this is, for display: "Game 1", "Game 2", "Game 3".</summary>
        public required string Label { get; init; }

        /// <summary>The note currently stored in the app.</summary>
        public string? ExistingNotes { get; init; }

        /// <summary>The note the backup carries.</summary>
        public string? IncomingNotes { get; init; }

        /// <summary>Tags currently stored in the app.</summary>
        public IReadOnlyList<string> ExistingTags { get; init; } = [];

        /// <summary>Tags the backup carries.</summary>
        public IReadOnlyList<string> IncomingTags { get; init; } = [];

        /// <summary>
        /// True when the two notes are not the same text.
        /// </summary>
        /// <remarks>
        /// Null and empty are the same thing here. A game with no note round-trips through JSON
        /// as null on one side and an empty string on the other depending on how it was written,
        /// and reporting that as a difference would hand the user a conflict with nothing in it
        /// to decide. Comparison is otherwise ordinal, matching
        /// <c>RestoreService.CompareGame</c> — if these disagreed, a pair reported as conflicting
        /// would render as identical.
        /// </remarks>
        public bool NotesDiffer =>
            !string.Equals(ExistingNotes ?? string.Empty, IncomingNotes ?? string.Empty, StringComparison.Ordinal);

        /// <summary>Tags the backup has that the stored match does not — the "+" side.</summary>
        public IReadOnlyList<string> AddedTags =>
            [.. IncomingTags.Where(t => !ExistingTags.Contains(t, StringComparer.Ordinal))];

        /// <summary>Tags the stored match has that the backup does not — the "−" side.</summary>
        public IReadOnlyList<string> RemovedTags =>
            [.. ExistingTags.Where(t => !IncomingTags.Contains(t, StringComparer.Ordinal))];

        /// <summary>True when either side carries a tag the other lacks. Order is not a difference.</summary>
        public bool TagsDiffer => AddedTags.Count > 0 || RemovedTags.Count > 0;

        /// <summary>False when the stored match has no such game at all.</summary>
        /// <remarks>
        /// A BO1 stored against a BO3 backup differs by the EXISTENCE of game 2, not by its
        /// contents. Without this the UI would render an empty panel for a real difference,
        /// because the diff carries notes and tags and both are blank on the missing side.
        /// </remarks>
        public bool ExistingPresent { get; init; } = true;

        /// <summary>False when the backup has no such game at all.</summary>
        public bool IncomingPresent { get; init; } = true;

        /// <summary>True when one side has this game and the other does not.</summary>
        public bool PresenceDiffers => ExistingPresent != IncomingPresent;

        /// <summary>True when there is anything here for the user to decide about.</summary>
        public bool HasAnyDifference => PresenceDiffers || NotesDiffer || TagsDiffer;

        /// <summary>
        /// True when one side simply knows more and nothing contradicts — Append produces the
        /// superset and there is no judgement to make.
        /// </summary>
        /// <remarks>
        /// This is the distinction a <c>MatchMatchKind</c> enum used to describe and no code ever
        /// made; the enum was deleted rather than kept as documentation, since unreachable code
        /// depresses coverage and mutation numbers for nothing. The
        /// UI pre-selects Append for these so the rows still showing no choice are the ones that
        /// actually need thought. Nothing is written without an explicit Apply either way, so a
        /// pre-selection is a suggestion rather than an action.
        ///
        /// Three things deliberately are NOT richer:
        /// <list type="bullet">
        /// <item>Both notes non-empty and different — Append would concatenate two real notes,
        /// which is a choice, not a fact.</item>
        /// <item>Tags added AND removed — neither side is a superset, so "take both" is a
        /// decision.</item>
        /// <item>A game on one side only — appending a whole game changes the match FORMAT, a
        /// bigger claim than filling in a blank note.</item>
        /// </list>
        /// </remarks>
        public bool IsRicher =>
            !PresenceDiffers
            && !(NotesDiffer
                 && !string.IsNullOrEmpty(ExistingNotes)
                 && !string.IsNullOrEmpty(IncomingNotes))
            && !(AddedTags.Count > 0 && RemovedTags.Count > 0);
    }
}
