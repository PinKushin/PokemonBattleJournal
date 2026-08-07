using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// Whether a whole conflicted match has an obvious answer, rolled up from its games.
    /// </summary>
    /// <remarks>
    /// Deliberately conservative: one game that genuinely conflicts makes the whole match a
    /// decision, even if the other two are trivially richer. Resolution applies per match rather
    /// than per game — a match's games are written in one transaction — so the weakest game
    /// governs, and suggesting Append for a match containing a real contradiction would be
    /// suggesting the user overwrite something without noticing.
    /// </remarks>
    [TestFixture]
    public class RestoreConflictSuggestionTests
    {
        private static ConflictGameDiff Richer(string label) =>
            new() { Label = label, ExistingNotes = "", IncomingNotes = "backup knows more" };

        private static ConflictGameDiff Contradicting(string label) =>
            new() { Label = label, ExistingNotes = "mine", IncomingNotes = "theirs" };

        private static RestoreConflict Conflict(params ConflictGameDiff[] games) =>
            new()
            {
                TrainerName = "Ash",
                ExistingMatchId = 1,
                StartTime = new DateTime(2026, 8, 7, 12, 0, 0, DateTimeKind.Utc),
                Description = "irrelevant here",
                Games = games,
            };

        [Test]
        public void IsRicher_WhenEveryGameIsRicher_IsTrue() =>
            Conflict(Richer("Game 1"), Richer("Game 2")).IsRicher.ShouldBeTrue();

        [Test]
        public void IsRicher_WhenAnyGameContradicts_IsFalse() =>
            Conflict(Richer("Game 1"), Contradicting("Game 2")).IsRicher.ShouldBeFalse();

        [Test]
        public void IsRicher_WithNoGames_IsFalse() =>
            // Nothing to be richer about. Suggesting a resolution for a conflict carrying no
            // visible difference would ask the user to approve something they cannot see.
            Conflict().IsRicher.ShouldBeFalse();

        [Test]
        public void SuggestedResolution_ForARicherMatch_IsAppend() =>
            Conflict(Richer("Game 1")).SuggestedResolution.ShouldBe(ConflictResolution.Append);

        [Test]
        public void SuggestedResolution_ForAGenuineConflict_IsNull() =>
            // Null means "no defensible default" — the UI leaves the row unselected so it stands
            // out against the pre-answered ones.
            Conflict(Contradicting("Game 1")).SuggestedResolution.ShouldBeNull();
    }
}
