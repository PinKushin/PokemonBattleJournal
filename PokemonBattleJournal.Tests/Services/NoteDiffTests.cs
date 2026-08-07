using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// Line-level diff between two versions of a note, for the conflict view.
    /// </summary>
    /// <remarks>
    /// Ours rather than a package. It is a hundred lines, it is the kind of logic Stryker can
    /// measure once it lives in Core, and a dependency would put it back outside measurement —
    /// which is the opposite of what the Core extraction was for.
    ///
    /// LCS rather than Myers. Myers wins on long inputs; these are match notes, a few lines at
    /// most, and the quadratic table is both smaller to write and obviously correct at that size.
    /// The pathological case — someone pastes a wall of text — is handled by a bound rather than
    /// by a cleverer algorithm.
    /// </remarks>
    [TestFixture]
    public class NoteDiffTests
    {
        private static string[] Rendered(IReadOnlyList<NoteDiffLine> lines) =>
            [.. lines.Select(l => l.Kind switch
            {
                NoteDiffKind.Added => "+" + l.Text,
                NoteDiffKind.Removed => "-" + l.Text,
                _ => " " + l.Text,
            })];

        [Test]
        public void Compute_IdenticalText_IsAllUnchanged() =>
            Rendered(NoteDiff.Compute("a\nb", "a\nb")).ShouldBe([" a", " b"]);

        [Test]
        public void Compute_BothEmpty_ReturnsNothing()
        {
            NoteDiff.Compute(null, null).ShouldBeEmpty();
            NoteDiff.Compute("", "").ShouldBeEmpty();
        }

        [Test]
        public void Compute_OnlyTheBackupHasText_IsAllAdded() =>
            Rendered(NoteDiff.Compute("", "a\nb")).ShouldBe(["+a", "+b"]);

        [Test]
        public void Compute_OnlyTheStoredMatchHasText_IsAllRemoved() =>
            Rendered(NoteDiff.Compute("a\nb", null)).ShouldBe(["-a", "-b"]);

        [Test]
        public void Compute_ALineInserted_MarksOnlyThatLine() =>
            Rendered(NoteDiff.Compute("a\nc", "a\nb\nc")).ShouldBe([" a", "+b", " c"]);

        [Test]
        public void Compute_ALineDeleted_MarksOnlyThatLine() =>
            Rendered(NoteDiff.Compute("a\nb\nc", "a\nc")).ShouldBe([" a", "-b", " c"]);

        [Test]
        public void Compute_ALineChanged_ShowsRemovedThenAdded() =>
            // The removed line comes first so a reader sees "was X, now Y" in reading order.
            Rendered(NoteDiff.Compute("a\nold\nc", "a\nnew\nc")).ShouldBe([" a", "-old", "+new", " c"]);

        [Test]
        public void Compute_NormalisesLineEndings()
        {
            // A backup taken on Windows and restored on Android, or the reverse. Without this
            // every single line would read as changed because "a\r" != "a".
            Rendered(NoteDiff.Compute("a\r\nb", "a\nb")).ShouldBe([" a", " b"]);
            Rendered(NoteDiff.Compute("a\rb", "a\nb")).ShouldBe([" a", " b"]);
        }

        [Test]
        public void Compute_IsCaseSensitive() =>
            Rendered(NoteDiff.Compute("Bricked", "bricked")).ShouldBe(["-Bricked", "+bricked"]);

        [Test]
        public void Compute_BeyondTheLineBound_FallsBackToWholeBlock()
        {
            // The quadratic table is fine for a note and not fine for a pasted deck list. Past
            // the bound it stops diffing and just shows both versions, which is still correct —
            // only less granular.
            string big = string.Join("\n", Enumerable.Range(0, NoteDiff.MaxLines + 1).Select(i => $"line {i}"));
            string alsoBig = string.Join("\n", Enumerable.Range(0, NoteDiff.MaxLines + 1).Select(i => $"line {i}"));

            IReadOnlyList<NoteDiffLine> result = NoteDiff.Compute(big, alsoBig);

            result.ShouldAllBe(l => l.Kind != NoteDiffKind.Unchanged);
            result.Count(l => l.Kind == NoteDiffKind.Removed).ShouldBe(NoteDiff.MaxLines + 1);
            result.Count(l => l.Kind == NoteDiffKind.Added).ShouldBe(NoteDiff.MaxLines + 1);
        }

        [Test]
        public void Compute_TrailingBlankLines_DoNotBecomePhantomChanges() =>
            // "a\n" and "a" are the same note to a reader; only one of them survived whatever
            // editor produced it.
            Rendered(NoteDiff.Compute("a\n", "a")).ShouldBe([" a"]);
    }
}
