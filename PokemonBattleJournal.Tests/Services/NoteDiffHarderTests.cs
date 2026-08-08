using PokemonBattleJournal.Services.Restore;

namespace PokemonBattleJournal.Tests.Services
{
    /// <summary>
    /// Diffs big enough that the LCS table has to make a choice.
    /// </summary>
    /// <remarks>
    /// NoteDiff scored 87.8% with ten survivors, and the reason was input size rather than
    /// missing assertions: every existing case is two or three lines, where the table degenerates
    /// and mutating a loop bound, a comparison or Math.Max produces the same answer anyway. A
    /// three-line note cannot tell a correct LCS from a broken one.
    ///
    /// These use interleaved edits over enough lines that a wrong table shows up in the output.
    /// </remarks>
    [TestFixture]
    public class NoteDiffHarderTests
    {
        private static string Render(IReadOnlyList<NoteDiffLine> lines) =>
            string.Join("|", lines.Select(l => l.Kind switch
            {
                NoteDiffKind.Added => "+" + l.Text,
                NoteDiffKind.Removed => "-" + l.Text,
                _ => " " + l.Text,
            }));

        [Test]
        public void Compute_InterleavedEditsAcrossManyLines_KeepsEveryCommonLine()
        {
            // Eight lines with changes at both ends and in the middle. Mutating a loop bound
            // drops a row or column of the table and the shared lines stop being recognised.
            string before = "a\nb\nc\nd\ne\nf\ng\nh";
            string after = "a\nB\nc\nd\nX\ne\nf\nh";

            string result = Render(NoteDiff.Compute(before, after));

            result.ShouldBe(" a|-b|+B| c| d|+X| e| f|-g| h");
        }

        [Test]
        public void Compute_AWholeBlockReplaced_StillAnchorsOnTheSurvivingLines()
        {
            // The head and tail match while everything between differs — the case where a wrong
            // Math.Max collapses the table and turns the whole note into remove-then-add.
            string before = "header\none\ntwo\nthree\nfooter";
            string after = "header\nuno\ndos\ntres\nfooter";

            string result = Render(NoteDiff.Compute(before, after));

            result.ShouldStartWith(" header|");
            result.ShouldEndWith("| footer");
            result.ShouldContain("-one");
            result.ShouldContain("+uno");
        }

        [Test]
        public void Compute_RepeatedLines_AreMatchedRatherThanRewritten()
        {
            // Duplicate content is where a greedy walk diverges from a real LCS: the naive answer
            // rewrites everything, the correct one keeps three of the four "x" lines.
            string before = "x\nx\nx\nx\nend";
            string after = "x\nx\nx\nend";

            IReadOnlyList<NoteDiffLine> result = NoteDiff.Compute(before, after);

            result.Count(l => l.Kind == NoteDiffKind.Unchanged).ShouldBe(4, "three x lines plus end");
            result.Count(l => l.Kind == NoteDiffKind.Removed).ShouldBe(1);
            result.Count(l => l.Kind == NoteDiffKind.Added).ShouldBe(0);
        }

        [Test]
        public void Compute_OnlyOneSideOverTheBound_StillFallsBack()
        {
            // The existing bound test puts BOTH sides over the limit, so `||` mutated to `&&`
            // survives it. One long side and one short one is the case that tells them apart.
            string huge = string.Join("\n", Enumerable.Range(0, NoteDiff.MaxLines + 1).Select(i => $"line {i}"));

            // The short side SHARES a line with the long one. Without that, a real diff and the
            // whole-block fallback produce the same shape — nothing in common either way — and
            // an earlier version of this test stayed green with `||` mutated to `&&`.
            IReadOnlyList<NoteDiffLine> result = NoteDiff.Compute(huge, "line 0");

            result.ShouldAllBe(l => l.Kind != NoteDiffKind.Unchanged,
                "past the bound nothing is diffed, so even the shared line is shown removed and added");
            result.Count(l => l.Kind == NoteDiffKind.Added).ShouldBe(1);
        }

        [Test]
        public void Compute_ANoteOfNothingButBlankLines_IsTreatedAsEmpty()
        {
            // Trailing-blank trimming walks backwards to index 0. Loosening `end > 0` to
            // `end >= 0` reads lines[-1] and throws, and only an all-blank note reaches it.
            NoteDiff.Compute("\n\n\n", "\n\n").ShouldBeEmpty();
            NoteDiff.Compute("\n\n\n", "kept").ShouldBe(
                [new NoteDiffLine(NoteDiffKind.Added, "kept")]);
        }

        [Test]
        public void Compute_ABlankLineInTheMiddle_IsKept()
        {
            // Only TRAILING blanks are dropped. A paragraph break inside a note is content.
            string result = Render(NoteDiff.Compute("a\n\nb", "a\n\nb"));

            result.ShouldBe(" a| | b");
        }
    }
}
