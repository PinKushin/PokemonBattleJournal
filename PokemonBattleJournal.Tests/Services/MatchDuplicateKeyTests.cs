namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// The identity two matches are compared by when deciding whether a file re-adds something
    /// already stored.
    /// </summary>
    /// <remarks>
    /// Scored 0% on the first Core mutation run, with no test in the repo naming it — despite
    /// being what both the TrainerHill import and the backup restore rely on to avoid inserting
    /// everything a second time. Re-importing is the NORMAL case, not an edge one: TrainerHill
    /// has no "everything since last time", so consecutive exports overlap.
    ///
    /// Every test here pins a decision somebody made on purpose, including the two that look
    /// like defects and are not — a null result compares equal to a Win, and two opponents on
    /// the same deck in the same minute collide.
    /// </remarks>
    [TestFixture]
    public class MatchDuplicateKeyTests
    {
        private static readonly DateTime Noon = new(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc);

        private static MatchEntry Match(
            DateTime? startTime = null,
            uint playing = 1,
            uint against = 2,
            MatchResult? result = MatchResult.Win,
            DateTime? datePlayed = null) =>
            new()
            {
                TrainerId = 1,
                StartTime = startTime ?? Noon,
                DatePlayed = datePlayed ?? (startTime ?? Noon).Date,
                PlayingId = playing,
                AgainstId = against,
                Result = result,
            };

        [Test]
        public void From_SameMatchTwice_ProducesEqualKeys() =>
            MatchDuplicateKey.From(Match()).ShouldBe(MatchDuplicateKey.From(Match()));

        [Test]
        public void From_KeysAtTheSameMinuteOnDifferentDecks_Differ()
        {
            MatchDuplicateKey.From(Match(playing: 1))
                .ShouldNotBe(MatchDuplicateKey.From(Match(playing: 9)));
            MatchDuplicateKey.From(Match(against: 2))
                .ShouldNotBe(MatchDuplicateKey.From(Match(against: 9)));
        }

        [Test]
        public void From_IsDirectional_PlayingAndAgainstAreNotInterchangeable() =>
            // Beating Dragapult with Charizard is not the same match as the reverse, and a key
            // that treated them as one would silently drop half a mirror-heavy log.
            MatchDuplicateKey.From(Match(playing: 1, against: 2))
                .ShouldNotBe(MatchDuplicateKey.From(Match(playing: 2, against: 1)));

        [Test]
        public void From_DifferentResults_Differ() =>
            MatchDuplicateKey.From(Match(result: MatchResult.Win))
                .ShouldNotBe(MatchDuplicateKey.From(Match(result: MatchResult.Loss)));

        [Test]
        public void From_UsesStartTimeAndNotDatePlayed()
        {
            // The decision the whole type turns on. DatePlayed comes from a date picker and sits
            // at midnight, so keying on it would collapse every match of a day into one and a
            // second import would insert nothing.
            MatchDuplicateKey morning = MatchDuplicateKey.From(
                Match(startTime: Noon.Date.AddHours(9), datePlayed: Noon.Date));
            MatchDuplicateKey evening = MatchDuplicateKey.From(
                Match(startTime: Noon.Date.AddHours(19), datePlayed: Noon.Date));

            morning.ShouldNotBe(evening, "same calendar day, different matches");
        }

        [Test]
        public void From_MatchesOneMinuteApart_Differ() =>
            MatchDuplicateKey.From(Match(startTime: Noon))
                .ShouldNotBe(MatchDuplicateKey.From(Match(startTime: Noon.AddMinutes(1))));

        [Test]
        public void From_ANullResult_IsTreatedAsAWin() =>
            // Documented fallback, pinned because it is lossy: a match saved without a result
            // compares equal to one recorded as a Win, so re-importing the pair keeps only one.
            // Changing the default silently changes which duplicates are detected.
            MatchDuplicateKey.From(Match(result: null))
                .ShouldBe(MatchDuplicateKey.From(Match(result: MatchResult.Win)));

        [Test]
        public void From_TwoOpponentsOnTheSameDeckInTheSameMinute_Collide()
        {
            // NOT a defect — the documented limit of this key. AgainstId identifies a DECK and
            // the model stores no opponent identity, so these are indistinguishable here. It is
            // exactly why a key hit may only skip and report, never delete or overwrite.
            MatchDuplicateKey first = MatchDuplicateKey.From(Match());
            MatchDuplicateKey second = MatchDuplicateKey.From(Match());

            first.ShouldBe(second);
        }

        [Test]
        public void Index_KeysEveryMatch()
        {
            Dictionary<MatchDuplicateKey, MatchEntry> index = MatchDuplicateKey.Index(
            [
                Match(startTime: Noon),
                Match(startTime: Noon.AddMinutes(5)),
            ]);

            index.Count.ShouldBe(2);
            index.ShouldContainKey(MatchDuplicateKey.From(Match(startTime: Noon)));
        }

        [Test]
        public void Index_OnACollision_KeepsTheLastMatchSeen()
        {
            // Unstated in the code and worth pinning: the indexer overwrites. Whichever entry
            // the caller enumerated last is the one a duplicate check will compare against, so
            // reordering the source silently changes which match a conflict is reported for.
            MatchEntry first = Match();
            MatchEntry second = Match();

            Dictionary<MatchDuplicateKey, MatchEntry> index = MatchDuplicateKey.Index([first, second]);

            index.Count.ShouldBe(1);
            index[MatchDuplicateKey.From(first)].ShouldBeSameAs(second);
        }

        [Test]
        public void Index_OfNothing_IsEmpty() =>
            MatchDuplicateKey.Index([]).ShouldBeEmpty();
    }
}
