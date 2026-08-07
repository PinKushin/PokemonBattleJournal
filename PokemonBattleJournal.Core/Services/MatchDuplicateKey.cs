namespace PokemonBattleJournal.Services
{
    /// <summary>
    /// Identity of a match for duplicate detection, within one trainer.
    /// </summary>
    /// <remarks>
    /// Shared by the backup restore and the TrainerHill import because they have the same
    /// problem: both take a file that may overlap what is already stored. TrainerHill's site
    /// has no "everything since last time", so a re-export overlaps the previous one and
    /// re-importing is the normal case rather than an edge one.
    ///
    /// <c>StartTime</c>, never <c>DatePlayed</c>. DatePlayed comes from a date picker and sits
    /// at midnight, so it collides constantly. StartTime carries a real time of day from every
    /// source — sub-second for TrainerHill entries, minute precision from the app's own time
    /// picker — so two matches colliding needs the same matchup, the same day and the same
    /// minute.
    ///
    /// **It is deliberately not authoritative.** <c>AgainstId</c> identifies a *deck*, not a
    /// person: the model records no opponent identity at all, so two different opponents on the
    /// same deck in the same minute produce an identical key, and mirror matches make that
    /// likelier. A key hit is therefore grounds for skipping and reporting, never for deleting
    /// or overwriting — the app cannot tell a re-import from two genuinely identical matches,
    /// so the user gets the final say.
    /// </remarks>
    internal readonly record struct MatchDuplicateKey(
        DateTime StartTime,
        uint PlayingId,
        uint AgainstId,
        MatchResult Result)
    {
        public static MatchDuplicateKey From(MatchEntry match) =>
            new(match.StartTime, match.PlayingId, match.AgainstId, match.Result ?? MatchResult.Win);

        /// <summary>Builds the set of keys already stored for a trainer.</summary>
        public static Dictionary<MatchDuplicateKey, MatchEntry> Index(IEnumerable<MatchEntry> matches)
        {
            Dictionary<MatchDuplicateKey, MatchEntry> index = [];
            foreach (MatchEntry match in matches)
            {
                index[From(match)] = match;
            }

            return index;
        }
    }
}
