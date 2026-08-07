namespace PokemonBattleJournal.Services.Restore
{
    /// <summary>How one line of a note relates to the other version.</summary>
    public enum NoteDiffKind
    {
        /// <summary>Present in both, unchanged.</summary>
        Unchanged,

        /// <summary>Only in the backup.</summary>
        Added,

        /// <summary>Only in the stored match.</summary>
        Removed,
    }

    /// <summary>One line of a rendered note diff.</summary>
    public sealed record NoteDiffLine(NoteDiffKind Kind, string Text);

    /// <summary>
    /// Line-level diff between two versions of a note.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written here rather than taken as a package. It is a hundred lines; a library wrapping it
    /// would carry a public surface and options this app does not need, and — the argument that
    /// actually decides it — code in Core is mutation-tested, while a dependency is invisible to
    /// Stryker. Taking a package for logic would move logic back outside measurement, which is
    /// the opposite of why Core exists.
    /// </para>
    /// <para>
    /// Longest-common-subsequence rather than Myers. Myers wins on large inputs by exploring
    /// only the edit path; these are match notes, a handful of lines, and at that size the
    /// quadratic table is smaller to write and easier to be sure of. The case Myers would have
    /// covered — someone pastes a deck list into a note — is handled by
    /// <see cref="MaxLines"/> instead, which is a bound rather than a cleverer algorithm.
    /// </para>
    /// </remarks>
    public static class NoteDiff
    {
        /// <summary>
        /// Past this many lines on either side, stop diffing and show both versions whole.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Sized for a note holding a DECK LIST, which is a first-class use of this field rather
        /// than an abuse of it: pasting an opponent's list is how you later work out which
        /// variant you actually played against. A list is roughly 25-35 lines grouped by count,
        /// two of them plus commentary is under a hundred, and a full match log is a few hundred.
        /// An earlier bound of 200 was picked assuming a note was a sentence, and would have
        /// degraded on exactly the case where a line diff is most useful — seeing "+2 Iono,
        /// -2 Judge" between two versions of the same list.
        /// </para>
        /// <para>
        /// The real constraint is the LCS table, which is O(n×m) cells in both time and memory.
        /// At 1000 that is ~3.8MB of int32 transiently, which is fine on a phone; 2000 would be
        /// 15MB and 5000 would be 95MB, which is not. Falling back past the bound is still
        /// correct — the user sees both versions whole — just not line-by-line.
        /// </para>
        /// </remarks>
        public const int MaxLines = 1000;

        /// <summary>
        /// Diffs two notes by line.
        /// </summary>
        public static IReadOnlyList<NoteDiffLine> Compute(string? existing, string? incoming)
        {
            string[] left = SplitLines(existing);
            string[] right = SplitLines(incoming);

            if (left.Length == 0 && right.Length == 0)
            {
                return [];
            }

            if (left.Length > MaxLines || right.Length > MaxLines)
            {
                return
                [
                    .. left.Select(l => new NoteDiffLine(NoteDiffKind.Removed, l)),
                    .. right.Select(l => new NoteDiffLine(NoteDiffKind.Added, l)),
                ];
            }

            return Walk(left, right, BuildLcsTable(left, right));
        }

        /// <summary>
        /// Splits into lines, normalising line endings first.
        /// </summary>
        /// <remarks>
        /// A backup taken on Windows and restored on Android carries CRLF into a comparison
        /// against LF. Without normalising, every line of an otherwise identical note reads as
        /// changed because "a\r" is not "a". Trailing blank lines are dropped for the same
        /// reason: "a\n" and "a" are the same note to a reader, and only one of them survived
        /// whichever editor produced it.
        /// </remarks>
        private static string[] SplitLines(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return [];
            }

            string[] lines = value
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');

            int end = lines.Length;
            while (end > 0 && lines[end - 1].Length == 0)
            {
                end--;
            }

            return end == lines.Length ? lines : lines[..end];
        }

        /// <summary>Standard LCS length table; cell [i,j] is the match length of the suffixes.</summary>
        private static int[,] BuildLcsTable(string[] left, string[] right)
        {
            int[,] table = new int[left.Length + 1, right.Length + 1];
            for (int i = left.Length - 1; i >= 0; i--)
            {
                for (int j = right.Length - 1; j >= 0; j--)
                {
                    table[i, j] = string.Equals(left[i], right[j], StringComparison.Ordinal)
                        ? table[i + 1, j + 1] + 1
                        : Math.Max(table[i + 1, j], table[i, j + 1]);
                }
            }

            return table;
        }

        /// <summary>
        /// Walks the table front to back, emitting removals before additions at each divergence.
        /// </summary>
        /// <remarks>
        /// Removed first is deliberate: a reader scanning top to bottom sees "was X, now Y" in
        /// that order, which is the order the two words appear in the sentence describing it.
        /// </remarks>
        private static List<NoteDiffLine> Walk(string[] left, string[] right, int[,] table)
        {
            List<NoteDiffLine> result = [];
            int i = 0;
            int j = 0;

            while (i < left.Length && j < right.Length)
            {
                if (string.Equals(left[i], right[j], StringComparison.Ordinal))
                {
                    result.Add(new NoteDiffLine(NoteDiffKind.Unchanged, left[i]));
                    i++;
                    j++;
                }
                else if (table[i + 1, j] >= table[i, j + 1])
                {
                    result.Add(new NoteDiffLine(NoteDiffKind.Removed, left[i]));
                    i++;
                }
                else
                {
                    result.Add(new NoteDiffLine(NoteDiffKind.Added, right[j]));
                    j++;
                }
            }

            while (i < left.Length)
            {
                result.Add(new NoteDiffLine(NoteDiffKind.Removed, left[i]));
                i++;
            }

            while (j < right.Length)
            {
                result.Add(new NoteDiffLine(NoteDiffKind.Added, right[j]));
                j++;
            }

            return result;
        }
    }
}
