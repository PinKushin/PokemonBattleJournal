using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// What the conflict UI has to render: both versions of one game, and what changed between
    /// them.
    /// </summary>
    /// <remarks>
    /// The old <c>RestoreConflict</c> carried only a <c>Description</c> string — "game 2 notes
    /// differ" — which is enough to warn someone and not enough to let them choose. Keep, Append
    /// and Replace are only a real decision if the user can see what each one would do, so the
    /// conflict has to carry both sides verbatim.
    ///
    /// The added/removed tag sets are derived rather than stored, so they cannot drift from the
    /// lists they describe.
    /// </remarks>
    [TestFixture]
    public class ConflictGameDiffTests
    {
        private static ConflictGameDiff Diff(
            string? existingNotes, string? incomingNotes,
            string[]? existingTags = null, string[]? incomingTags = null) =>
            new()
            {
                Label = "Game 1",
                ExistingNotes = existingNotes,
                IncomingNotes = incomingNotes,
                ExistingTags = existingTags ?? [],
                IncomingTags = incomingTags ?? [],
            };

        [Test]
        public void NotesDiffer_WhenTextIsDifferent_IsTrue() =>
            Diff("mine", "theirs").NotesDiffer.ShouldBeTrue();

        [Test]
        public void NotesDiffer_WhenTextMatches_IsFalse() =>
            Diff("same", "same").NotesDiffer.ShouldBeFalse();

        [Test]
        public void NotesDiffer_TreatsNullAndEmptyAsTheSameThing()
        {
            // A game with no note round-trips through JSON as null on one side and "" on the
            // other depending on how it was written. Reporting that as a difference would send
            // the user a conflict with nothing in it to decide.
            Diff(null, "").NotesDiffer.ShouldBeFalse();
            Diff("", null).NotesDiffer.ShouldBeFalse();
        }

        [Test]
        public void NotesDiffer_IsCaseSensitive() =>
            Diff("Bricked", "bricked").NotesDiffer.ShouldBeTrue();

        [Test]
        public void AddedTags_AreTheOnesOnlyTheBackupHas() =>
            Diff(null, null, ["a", "b"], ["b", "c"]).AddedTags.ShouldBe(["c"]);

        [Test]
        public void RemovedTags_AreTheOnesOnlyTheStoredMatchHas() =>
            Diff(null, null, ["a", "b"], ["b", "c"]).RemovedTags.ShouldBe(["a"]);

        [Test]
        public void AddedAndRemoved_AreEmptyWhenTagSetsMatch()
        {
            ConflictGameDiff diff = Diff(null, null, ["a", "b"], ["b", "a"]);
            diff.AddedTags.ShouldBeEmpty();
            diff.RemovedTags.ShouldBeEmpty();
            diff.TagsDiffer.ShouldBeFalse();
        }

        [Test]
        public void TagsDiffer_IsTrueWhenEitherSideHasSomethingTheOtherLacks()
        {
            Diff(null, null, ["a"], []).TagsDiffer.ShouldBeTrue();
            Diff(null, null, [], ["a"]).TagsDiffer.ShouldBeTrue();
        }

        // ---------------- richer vs genuine conflict ----------------
        //
        // Richer means one side simply knows more, with nothing contradicting: Append produces
        // the superset and there is no judgement to make. That is the case the UI pre-selects
        // Append for. A genuine conflict is where both sides hold different non-empty values,
        // and no automatic answer is defensible.

        [Test]
        public void IsRicher_WhenOnlyTheBackupHasANote_IsTrue() =>
            Diff("", "misplayed turn 3").IsRicher.ShouldBeTrue();

        [Test]
        public void IsRicher_WhenOnlyTheStoredMatchHasANote_IsTrue() =>
            Diff("dead draw", null).IsRicher.ShouldBeTrue();

        [Test]
        public void IsRicher_WhenBothNotesAreNonEmptyAndDiffer_IsFalse() =>
            Diff("dead draw", "misplayed turn 3").IsRicher.ShouldBeFalse();

        [Test]
        public void IsRicher_WhenTagsAreOnlyAdded_IsTrue() =>
            Diff(null, null, ["a"], ["a", "b"]).IsRicher.ShouldBeTrue();

        [Test]
        public void IsRicher_WhenTagsAreOnlyRemoved_IsTrue() =>
            Diff(null, null, ["a", "b"], ["a"]).IsRicher.ShouldBeTrue();

        [Test]
        public void IsRicher_WhenTagsAreBothAddedAndRemoved_IsFalse() =>
            // Neither side is a superset, so "just take both" is a decision rather than a fact.
            Diff(null, null, ["a", "b"], ["b", "c"]).IsRicher.ShouldBeFalse();

        [Test]
        public void IsRicher_WhenTheGameExistsOnOneSideOnly_IsFalse()
        {
            // A BO1 against a BO3 backup. Appending a whole game changes the match FORMAT, which
            // is a bigger claim than filling in a blank note, so it stays a human decision.
            ConflictGameDiff missing = new()
            {
                Label = "Game 2",
                ExistingPresent = false,
                IncomingNotes = "they conceded",
            };
            missing.IsRicher.ShouldBeFalse();
        }

        [Test]
        public void HasAnyDifference_IsFalseOnlyWhenNotesAndTagsBothAgree()
        {
            Diff("same", "same", ["a"], ["a"]).HasAnyDifference.ShouldBeFalse();
            Diff("mine", "theirs", ["a"], ["a"]).HasAnyDifference.ShouldBeTrue();
            Diff("same", "same", ["a"], ["b"]).HasAnyDifference.ShouldBeTrue();
        }
    }
}
