namespace PokemonBattleJournal.Services.Restore
{
    /// <summary>
    /// What the user chose to do with a restore conflict.
    /// </summary>
    /// <remarks>
    /// Three answers rather than two because the interesting case has no safe automatic
    /// resolution. A conflict means both sides hold different non-empty values, so Keep loses the
    /// backup's version and Replace loses the user's; Append is the only one that cannot destroy
    /// anything, and it exists so that "I want both" does not require hand-editing afterwards.
    /// </remarks>
    public enum ConflictResolution
    {
        /// <summary>Leave the stored match exactly as it is. The safe default.</summary>
        Keep,

        /// <summary>Union both sides — notes joined, tags merged. Loses nothing.</summary>
        Append,

        /// <summary>Take the backup's version wholesale, including its blanks.</summary>
        Replace,
    }

    /// <summary>
    /// Applies a <see cref="ConflictResolution"/> to one field pair. Pure, and deliberately so.
    /// </summary>
    /// <remarks>
    /// Deciding what a resolution means needs no database. Keeping it out of
    /// <see cref="RestoreService"/> is what makes the combinations cheap to enumerate — proving
    /// each one through a real SQLite round trip is how the interesting ones end up untested.
    /// </remarks>
    public static class ConflictResolver
    {
        /// <summary>
        /// Placed between two notes that are both non-empty and differ.
        /// </summary>
        /// <remarks>
        /// Visible on purpose. A silent join would leave the user unable to tell which half was
        /// theirs, which defeats the point of choosing Append over Replace. Uses "\n" rather than
        /// <see cref="System.Environment.NewLine"/> so the stored text does not depend on which
        /// platform performed the restore — a backup taken on Windows and restored on Android
        /// must produce the same note.
        /// </remarks>
        public const string NoteSeparator = "\n\n--- from backup ---\n\n";

        /// <summary>
        /// Resolves two versions of a game's notes.
        /// </summary>
        public static string? ResolveNotes(string? existing, string? incoming, ConflictResolution resolution)
        {
            switch (resolution)
            {
                case ConflictResolution.Keep:
                    return existing;

                case ConflictResolution.Replace:
                    // Including when the backup has no note. Replace that declined to clear one
                    // would silently behave like Keep, and nothing else can empty the field.
                    return incoming;

                case ConflictResolution.Append:
                    if (string.IsNullOrEmpty(existing))
                    {
                        return incoming;
                    }

                    if (string.IsNullOrEmpty(incoming))
                    {
                        return existing;
                    }

                    // Reachable when the notes agree and it was the TAGS that differed. Two
                    // copies of the same sentence is not what "keep both" means.
                    return string.Equals(existing, incoming, StringComparison.Ordinal)
                        ? existing
                        : existing + NoteSeparator + incoming;

                default:
                    return existing;
            }
        }

        /// <summary>
        /// Resolves two versions of a game's tags.
        /// </summary>
        /// <remarks>
        /// Append preserves order: the user's own tags stay where they were and anything new
        /// arrives after them. Comparison is ordinal, matching the <c>HashSet&lt;string&gt;</c>
        /// that <c>RestoreService.CompareGame</c> uses to decide a conflict exists at all — if
        /// the two disagreed, a pair reported as different would silently merge as identical.
        /// </remarks>
        public static IReadOnlyList<string> ResolveTags(
            IReadOnlyList<string> existing,
            IReadOnlyList<string> incoming,
            ConflictResolution resolution)
        {
            switch (resolution)
            {
                case ConflictResolution.Keep:
                    return existing;

                case ConflictResolution.Replace:
                    return incoming;

                case ConflictResolution.Append:
                    List<string> merged = [.. existing];
                    HashSet<string> seen = [.. existing];
                    foreach (string tag in incoming)
                    {
                        if (seen.Add(tag))
                        {
                            merged.Add(tag);
                        }
                    }

                    return merged;

                default:
                    return existing;
            }
        }
    }
}
