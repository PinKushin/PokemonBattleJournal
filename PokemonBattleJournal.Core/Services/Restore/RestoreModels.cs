namespace PokemonBattleJournal.Services.Restore
{
    /// <summary>
    /// How an incoming entry relates to a match already in the database.
    /// </summary>
    /// <remarks>
    /// Deliberately four outcomes rather than a bool. The app cannot always tell a re-import
    /// from two genuinely distinct matches: the dedupe key is
    /// <c>(TrainerId, StartTime, PlayingId, AgainstId, Result)</c>, and <c>AgainstId</c>
    /// identifies a *deck*, not a person — the model records no opponent identity at all. Two
    /// different opponents on the same deck at the same minute produce an identical key.
    ///
    /// So nothing here may silently drop or overwrite. Ambiguity is reported and the user
    /// decides.
    /// </remarks>
    public enum MatchMatchKind
    {
        /// <summary>No existing match shares the key. Insert it.</summary>
        New,

        /// <summary>Every compared field agrees. Skip, but count and report it.</summary>
        Identical,

        /// <summary>
        /// One side has data the other lacks — a note or tags present on one and absent on the
        /// other, never differing values. Safe to union.
        /// </summary>
        Richer,

        /// <summary>
        /// Both sides carry different non-empty values for the same field. Needs a human;
        /// never resolved automatically.
        /// </summary>
        Conflict,
    }

    /// <summary>
    /// One incoming entry that could not be applied without a decision.
    /// </summary>
    public sealed record RestoreConflict
    {
        public required string TrainerName { get; init; }
        public required uint ExistingMatchId { get; init; }
        public required DateTime StartTime { get; init; }

        /// <summary>Plain-English account of what differs, for display without re-deriving it.</summary>
        public required string Description { get; init; }

        /// <summary>
        /// Both versions of each game that differs, so the user can see what they are choosing
        /// between.
        /// </summary>
        /// <remarks>
        /// Without this a conflict could only be reported, never resolved: the backup's version
        /// of the data was discarded the moment the conflict was recorded, leaving nothing for a
        /// Replace or an Append to apply. Games that agree are omitted rather than rendered as
        /// three identical panels.
        /// </remarks>
        public IReadOnlyList<ConflictGameDiff> Games { get; init; } = [];

        /// <summary>
        /// True when every differing game simply knows more, with nothing contradicting.
        /// </summary>
        /// <remarks>
        /// Conservative on purpose: one genuinely conflicting game makes the whole match a
        /// decision even if the others are trivially richer. Resolution applies per MATCH — its
        /// games are written in one transaction — so the weakest game governs, and suggesting
        /// Append for a match containing a real contradiction would be suggesting the user
        /// overwrite something without noticing.
        /// </remarks>
        public bool IsRicher => Games.Count > 0 && Games.All(g => g.IsRicher);

        /// <summary>
        /// The resolution to pre-select, or null when no default is defensible.
        /// </summary>
        /// <remarks>
        /// Null is not "do nothing" — it means the row stays unselected so it stands out against
        /// the pre-answered ones, and the user has to look at it. Nothing is written without an
        /// explicit Apply either way, so a suggestion is never an action.
        /// </remarks>
        public ConflictResolution? SuggestedResolution =>
            IsRicher ? ConflictResolution.Append : null;
    }

    /// <summary>
    /// What a restore did. Returned rather than logged so the UI can report it and the caller
    /// can tell "nothing to do" from "everything was ambiguous".
    /// </summary>
    public sealed record RestoreResult
    {
        public int TrainersCreated { get; init; }
        public int TrainersMerged { get; init; }
        public int MatchesInserted { get; init; }

        /// <summary>
        /// Entries already present and identical. Counted rather than ignored: the user is
        /// entitled to know a re-import did nothing, and to override it.
        /// </summary>
        public int MatchesSkippedIdentical { get; init; }

        public int MatchesMerged { get; init; }

        /// <summary>Left untouched, awaiting a decision. Never auto-resolved.</summary>
        public IReadOnlyList<RestoreConflict> Conflicts { get; init; } = [];

        /// <summary>
        /// Per-entry problems that did not stop the restore — an unparsable result, a missing
        /// game1. Collected rather than thrown so one bad entry cannot abort the rest.
        /// </summary>
        public IReadOnlyList<string> Errors { get; init; } = [];
    }
}
