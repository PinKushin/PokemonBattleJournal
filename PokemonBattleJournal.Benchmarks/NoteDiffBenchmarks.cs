namespace PokemonBattleJournal.Benchmarks;

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using PokemonBattleJournal.Services.Restore;

/// <summary>
/// What the LCS note diff actually costs, at and around its bound.
/// </summary>
/// <remarks>
/// <para>
/// <b>This exists to check a claim nobody measured.</b> <see cref="NoteDiff.MaxLines"/> is 1000,
/// and the doc comment justifying it says the table is "~3.8MB of int32 transiently, which is
/// fine on a phone; 2000 would be 15MB and 5000 would be 95MB, which is not". That is arithmetic
/// — cells times four bytes — written while choosing the constant. It is not a measurement, and
/// it ignores everything the arithmetic does not model: the emitted <c>NoteDiffLine</c> records,
/// the string array from splitting, and whatever the walk allocates.
/// </para>
/// <para>
/// A doc comment that states a number confidently and was never checked is the same defect as an
/// assertion that cannot fail. The numbers here either confirm the bound or move it.
/// </para>
/// <para>
/// The 2000-line case runs deliberately, past the bound, because that is the path the fallback
/// takes — and the fallback is supposed to be the CHEAP branch. If it is not measurably cheaper
/// than the diff, the bound is protecting nothing.
/// </para>
/// </remarks>
[MemoryDiagnoser]
public class NoteDiffBenchmarks
{
    /// <summary>
    /// Sizes either side of <see cref="NoteDiff.MaxLines"/>.
    /// </summary>
    /// <remarks>
    /// 300 is the bound itself and 301 is the first input that falls back, so the pair isolates
    /// the cliff. Comparing 300 against 500 alone would confound the fallback with the larger
    /// input. 1000 is kept as the OLD bound, so the change has a before number in the same run
    /// on the same machine rather than across two sessions.
    /// </remarks>
    [Params(100, 200, 300, 301, 500, 1000)]
    public int Lines { get; set; }

    private string _left = string.Empty;
    private string _right = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        // A realistic worst case rather than a pathological one: two versions of the same deck
        // list where roughly one line in ten changed. Identical inputs would let the walk run
        // straight down the diagonal and understate the cost; wholly different inputs share no
        // subsequence and understate it the other way.
        _left = string.Join("\n", Enumerable.Range(0, Lines).Select(i => $"{i % 4 + 1} Card {i}"));
        _right = string.Join("\n", Enumerable.Range(0, Lines)
            .Select(i => i % 10 == 0 ? $"{i % 4 + 1} Swapped {i}" : $"{i % 4 + 1} Card {i}"));
    }

    [Benchmark]
    public int Compute()
    {
        IReadOnlyList<NoteDiffLine> result = NoteDiff.Compute(_left, _right);
        return result.Count;
    }
}
