using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// The three answers a user can give to a restore conflict, as pure functions.
    /// </summary>
    /// <remarks>
    /// Split out of <c>RestoreService</c> deliberately. Deciding what a resolution MEANS needs no
    /// database, and keeping it separate is what lets these cases be enumerated cheaply — the
    /// alternative is proving each one through a real SQLite round trip, which is how the
    /// interesting combinations end up untested.
    ///
    /// A conflict means both sides hold different non-empty values, so there is no safe automatic
    /// answer; the user picks. Append exists because it is the only choice that cannot lose
    /// anything, and Keep is the default precisely because doing nothing is safe.
    /// </remarks>
    [TestFixture]
    public class ConflictResolverTests
    {
        // ---------------- notes ----------------

        [Test]
        public void ResolveNotes_Keep_ReturnsExistingUnchanged()
        {
            ConflictResolver.ResolveNotes("mine", "theirs", ConflictResolution.Keep)
                .ShouldBe("mine");
        }

        [Test]
        public void ResolveNotes_Replace_ReturnsIncoming()
        {
            ConflictResolver.ResolveNotes("mine", "theirs", ConflictResolution.Replace)
                .ShouldBe("theirs");
        }

        [Test]
        public void ResolveNotes_Append_JoinsBothWithTheSeparator()
        {
            ConflictResolver.ResolveNotes("mine", "theirs", ConflictResolution.Append)
                .ShouldBe("mine" + ConflictResolver.NoteSeparator + "theirs");
        }

        [Test]
        public void ResolveNotes_AppendWhenExistingEmpty_ReturnsIncomingWithNoSeparator()
        {
            // The "backup has a note this match lacks" case. Appending a separator to nothing
            // would leave a stray marker at the top of an otherwise clean note.
            ConflictResolver.ResolveNotes("", "theirs", ConflictResolution.Append)
                .ShouldBe("theirs");
            ConflictResolver.ResolveNotes(null, "theirs", ConflictResolution.Append)
                .ShouldBe("theirs");
        }

        [Test]
        public void ResolveNotes_AppendWhenIncomingEmpty_ReturnsExistingWithNoSeparator()
        {
            ConflictResolver.ResolveNotes("mine", "", ConflictResolution.Append)
                .ShouldBe("mine");
            ConflictResolver.ResolveNotes("mine", null, ConflictResolution.Append)
                .ShouldBe("mine");
        }

        [Test]
        public void ResolveNotes_AppendWhenIdentical_DoesNotDuplicate()
        {
            // Same text on both sides is not a conflict worth two copies of. This can be reached
            // when the notes agree but the TAGS are what differ.
            ConflictResolver.ResolveNotes("same", "same", ConflictResolution.Append)
                .ShouldBe("same");
        }

        [Test]
        public void ResolveNotes_ReplaceWithEmptyIncoming_ClearsTheNote()
        {
            // Replace means replace. If the backup genuinely has no note, saying otherwise would
            // make Replace quietly behave like Keep, and the user would have no way to clear one.
            ConflictResolver.ResolveNotes("mine", null, ConflictResolution.Replace)
                .ShouldBeNullOrEmpty();
        }

        // ---------------- tags ----------------

        [Test]
        public void ResolveTags_Keep_ReturnsExistingUnchanged()
        {
            ConflictResolver.ResolveTags(["a", "b"], ["c"], ConflictResolution.Keep)
                .ShouldBe(["a", "b"]);
        }

        [Test]
        public void ResolveTags_Replace_ReturnsIncoming()
        {
            ConflictResolver.ResolveTags(["a", "b"], ["c"], ConflictResolution.Replace)
                .ShouldBe(["c"]);
        }

        [Test]
        public void ResolveTags_Append_UnionsExistingFirstThenNewOnes()
        {
            // Order is part of the contract: the user's own tags stay where they were, and
            // anything the backup adds arrives after them.
            ConflictResolver.ResolveTags(["a", "b"], ["b", "c"], ConflictResolution.Append)
                .ShouldBe(["a", "b", "c"]);
        }

        [Test]
        public void ResolveTags_Append_DoesNotDuplicateAnExistingTag()
        {
            ConflictResolver.ResolveTags(["a"], ["a"], ConflictResolution.Append)
                .ShouldBe(["a"]);
        }

        [Test]
        public void ResolveTags_AppendIsCaseSensitive()
        {
            // Matches CompareGame, which sets tags into a default HashSet<string> (ordinal). If
            // these two disagreed, a pair that compared as different would merge as identical.
            ConflictResolver.ResolveTags(["Bricked"], ["bricked"], ConflictResolution.Append)
                .ShouldBe(["Bricked", "bricked"]);
        }

        [Test]
        public void ResolveTags_ReplaceWithEmptyIncoming_ClearsTheTags()
        {
            ConflictResolver.ResolveTags(["a"], [], ConflictResolution.Replace)
                .ShouldBeEmpty();
        }
    }
}
