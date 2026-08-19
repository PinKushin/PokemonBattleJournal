namespace PokemonBattleJournal.Fuzz;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PokemonBattleJournal.Logging;
using PokemonBattleJournal.Services.Restore;
using Serilog.Events;
using Serilog.Parsing;
using SharpFuzz;

/// <summary>
/// Drives the pure logic that consumes input this app did not produce.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why one process and not four.</b> libFuzzer prefers a narrow target, and
/// four executables would be the textbook answer. It would also mean four
/// projects, four corpora and a workflow that divides a fixed budget by four.
/// Multiplexing on the first byte is the standard alternative: the fuzzer sees
/// the selector as just another input byte, and coverage feedback teaches it to
/// exercise every branch.
/// </para>
/// <para>
/// <b>What is deliberately absent.</b> <c>RestoreService</c> and
/// <c>TrainerHillImportService</c> are the two biggest untrusted-input surfaces
/// here and they are not yet modes. Both need a live SQLite connection per
/// iteration, which drops throughput from tens of thousands of executions per
/// second to hundreds, and both need stub implementations of
/// <c>ILimitlessMetaService</c> and <c>IErrorHandler</c>. They belong in a
/// second, slower target rather than sharing a budget with these.
/// </para>
/// </remarks>
internal static class Program
{
    /// <summary>How many modes the first byte selects between.</summary>
    private const int ModeCount = 4;

    /// <summary>
    /// Splits one payload into two sides for the diff and merge modes.
    /// </summary>
    /// <remarks>
    /// A single NUL is used rather than a longer marker because the fuzzer has
    /// to discover it by mutation: a one-byte separator appears by chance in
    /// almost any input, while a multi-byte one would have to be assembled and
    /// most of the budget would go on producing two non-empty sides at all.
    /// </remarks>
    private const byte SideSeparator = 0;

    /// <summary>
    /// Where to write a crashing input, or <c>null</c> to write none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>libFuzzer's own <c>-artifact_prefix</c> writes nothing here, so without this a
    /// finding is lost at the moment it is found.</b> On a managed exception SharpFuzz aborts
    /// the .NET child and the <c>libfuzzer-dotnet</c> bridge dies with it, so libFuzzer's crash
    /// handler never runs: no <c>crash-*</c> file and no "Test unit written to" line, while the
    /// exception itself prints in full. That split is the worst possible one — the run REPORTS
    /// the defect and silently loses the input needed to reproduce it. Measured by
    /// Tf2DemoSalvage on 2026-08-11 against a real crash; PokemonBattleJournal shared the defect
    /// and had simply never crashed, so ~148M executions a night were running with no way to
    /// keep a reproducer.
    /// </para>
    /// <para>
    /// <b>The corpus is not a fallback.</b> libFuzzer only adds inputs that increase coverage,
    /// and a crashing input is never added — replaying the whole corpus afterwards isolates
    /// nothing.
    /// </para>
    /// </remarks>
    private static readonly string? CrashDir =
        Environment.GetEnvironmentVariable("PBJFUZZ_CRASH_DIR");

    /// <summary>
    /// When set, every input throws. Makes the preservation path provable on demand instead of
    /// only when something genuinely breaks — which is what let this defect hide.
    /// </summary>
    private static readonly bool SelfTest =
        Environment.GetEnvironmentVariable("PBJFUZZ_SELFTEST") == "1";

    /// <summary>
    /// Which family of targets to drive. The fast suite (four pure string functions) and the slow
    /// suite (the import and restore file parsers, each needing a SQLite connection per iteration)
    /// run as SEPARATE processes with separate budgets — mixing them would let the slow parsers,
    /// at hundreds of executions a second, starve the pure functions that manage tens of thousands.
    /// `PBJFUZZ_SUITE=importrestore` selects the slow one; anything else is the fast default.
    /// </summary>
    private static readonly Action<byte[]> Target =
        Environment.GetEnvironmentVariable("PBJFUZZ_SUITE") == "importrestore"
            ? ImportRestoreFuzz.Consume
            : Consume;

    private static void Main() => Fuzzer.LibFuzzer.Run(Run);

    /// <summary>
    /// Copies the input, then runs the real target so a crash can be preserved.
    /// </summary>
    /// <remarks>
    /// Two details carry the whole fix.
    /// <list type="bullet">
    ///   <item><b>Copy before the call.</b> libFuzzer reuses its buffer, so the span is not
    ///   valid by the time anything downstream of the throw looks at it.</item>
    ///   <item><b>Write from an exception FILTER, not a catch.</b> A filter runs while the
    ///   exception is still propagating and its result decides nothing here — it always returns
    ///   false — so the exception continues to SharpFuzz exactly as before. Catching and
    ///   rethrowing would work too, but it would rewrite the stack trace, and the trace is the
    ///   other half of the finding.</item>
    /// </list>
    /// </remarks>
    private static void Run(ReadOnlySpan<byte> bytes)
    {
        byte[] input = bytes.ToArray();

        try
        {
            // Selftest is checked here, not inside a suite, so BOTH suites prove the
            // crash-preservation path on demand — an empty findings directory is
            // indistinguishable from a clean run, so the only proof is a deliberate throw.
            if (SelfTest)
            {
                throw new InvalidOperationException(
                    $"PBJFUZZ_SELFTEST: deliberate throw on {input.Length} bytes");
            }

            Target(input);
        }
        catch (Exception) when (Preserve(input))
        {
            // Unreachable: Preserve always returns false, so this handler is never selected.
            throw;
        }
    }

    /// <summary>
    /// Writes the crashing input beside the run's findings. Always returns <c>false</c>.
    /// </summary>
    private static bool Preserve(byte[] input)
    {
        if (CrashDir is null)
        {
            return false;
        }

        try
        {
            _ = Directory.CreateDirectory(CrashDir);

            // Content-addressed, so the same input twice is one file rather than two, and the
            // name is enough to tell two findings apart in a log.
            string digest = Convert.ToHexString(SHA256.HashData(input))[..16].ToLowerInvariant();
            string path = Path.Combine(CrashDir, $"crash-{digest}.bin");

            File.WriteAllBytes(path, input);
            Console.Error.WriteLine(
                $"crash input preserved: crash-{digest}.bin ({input.Length} bytes)");
        }
        catch (Exception ex)
        {
            // Never let preservation change the outcome of the run, but never swallow it
            // either: a silent failure here recreates the exact defect being fixed.
            Console.Error.WriteLine(
                $"could not preserve crash input: {ex.GetType().Name}: {ex.Message}");
        }

        return false;
    }

    private static void Consume(byte[] bytes)
    {
        // An empty input carries no mode and no payload. Returning rather than
        // picking a default keeps the corpus honest: a zero-length file should
        // not be credited with exercising anything.
        if (bytes.Length == 0)
        {
            return;
        }

        int mode = bytes[0] % ModeCount;
        byte[] payload = bytes[1..];

        switch (mode)
        {
            case 0: Diff(payload); break;
            case 1: ResolveNotes(payload); break;
            case 2: ResolveTags(payload); break;
            default: Redact(payload); break;
        }

        // Nothing is caught. These are pure functions over strings with no
        // documented failure mode, so ANY exception is a finding — catching
        // broadly here would make this a program that proves nothing.
    }

    /// <summary>
    /// The LCS line diff, whose cost is quadratic and whose bound is the only
    /// thing standing between a pasted deck list and an allocation the phone
    /// cannot serve.
    /// </summary>
    /// <param name="payload">Two note versions, NUL-separated.</param>
    /// <remarks>
    /// <para>
    /// Three invariants are checked rather than just "it did not throw", because
    /// a diff that returns confident nonsense is worse than one that crashes:
    /// </para>
    /// <list type="bullet">
    ///   <item>Every non-empty side contributes at least one line, so a note
    ///   cannot silently vanish from the review the user is shown.</item>
    ///   <item>Kept and removed lines reconstruct the left side in order, and
    ///   kept and added lines reconstruct the right — the property that makes
    ///   it a diff rather than a list of guesses.</item>
    ///   <item>Output is bounded by the two inputs together. An LCS walk that
    ///   fails to advance would loop, and a hang is the failure libFuzzer is
    ///   worst at attributing unless the bound is asserted.</item>
    /// </list>
    /// </remarks>
    private static void Diff(byte[] payload)
    {
        (string left, string right) = TwoSides(payload);

        IReadOnlyList<NoteDiffLine> result = NoteDiff.Compute(left, right);

        if (result.Count > CountLines(left) + CountLines(right))
        {
            throw new InvalidOperationException(
                $"diff produced {result.Count} lines from {CountLines(left)}+{CountLines(right)}");
        }

        // Past MaxLines the diff degrades to "everything removed, everything
        // added" on purpose, and that fallback still satisfies reconstruction.
        Reconstructs(result, left, NoteDiffKind.Removed, nameof(left));
        Reconstructs(result, right, NoteDiffKind.Added, nameof(right));
    }

    /// <summary>
    /// Asserts that the unchanged lines plus the lines of one side, in order,
    /// are exactly that side.
    /// </summary>
    private static void Reconstructs(
        IReadOnlyList<NoteDiffLine> result,
        string original,
        NoteDiffKind sideKind,
        string label)
    {
        List<string> rebuilt = result
            .Where(l => l.Kind == NoteDiffKind.Unchanged || l.Kind == sideKind)
            .Select(l => l.Text)
            .ToList();

        List<string> expected = NormalisedLines(original);

        if (!rebuilt.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"diff does not reconstruct {label}: {expected.Count} lines in, {rebuilt.Count} out");
        }
    }

    /// <summary>
    /// Mirrors NoteDiff's own line splitting, which normalises line endings and
    /// drops trailing blanks.
    /// </summary>
    /// <remarks>
    /// Duplicated rather than shared. A reconstruction check that called the
    /// production splitter would agree with it by construction — including when
    /// the splitter is wrong — which is the "asserting the code against itself"
    /// shape that makes an experiment insensitive.
    /// </remarks>
    private static List<string> NormalisedLines(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return [];
        }

        List<string> lines = [.. value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')];

        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }

    private static int CountLines(string value) => NormalisedLines(value).Count;

    /// <summary>
    /// Note merging, where the invariant is that Append never loses text.
    /// </summary>
    /// <param name="payload">Existing and incoming notes, NUL-separated.</param>
    private static void ResolveNotes(byte[] payload)
    {
        (string existing, string incoming) = TwoSides(payload);

        string? kept = ConflictResolver.ResolveNotes(existing, incoming, ConflictResolution.Keep);
        string? replaced = ConflictResolver.ResolveNotes(existing, incoming, ConflictResolution.Replace);
        string? appended = ConflictResolver.ResolveNotes(existing, incoming, ConflictResolution.Append);

        // Keep and Replace are projections; anything else is a defect in a code
        // path the user reached by explicitly choosing not to merge.
        if (!string.Equals(kept, existing, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Keep altered the existing note");
        }

        if (!string.Equals(replaced, incoming, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Replace did not yield the incoming note");
        }

        // The one that matters. A user choosing Append is told nothing is lost,
        // and both sides are non-empty here, so both must survive verbatim.
        if (existing.Length > 0 && incoming.Length > 0)
        {
            if (appended is null
                || !appended.Contains(existing, StringComparison.Ordinal)
                || !appended.Contains(incoming, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Append lost one side of the note");
            }
        }
    }

    /// <summary>
    /// Tag merging, where the invariant is a union with no duplicates and no
    /// reordering of what was already there.
    /// </summary>
    /// <param name="payload">Existing and incoming tags, NUL-separated, then
    /// newline-separated within each side.</param>
    private static void ResolveTags(byte[] payload)
    {
        (string left, string right) = TwoSides(payload);
        List<string> existing = [.. left.Split('\n', StringSplitOptions.RemoveEmptyEntries)];
        List<string> incoming = [.. right.Split('\n', StringSplitOptions.RemoveEmptyEntries)];

        IReadOnlyList<string> merged =
            ConflictResolver.ResolveTags(existing, incoming, ConflictResolution.Append);

        // Every existing tag survives, in its original position. A restore that
        // reorders the user's tags looks like data loss even when nothing is.
        if (!merged.Take(existing.Count).SequenceEqual(existing, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Append disturbed the existing tags");
        }

        // The contract is that Append INTRODUCES no duplicate, not that the result is
        // globally distinct. ResolveTags passes `existing` through verbatim, so a duplicate
        // the caller already had survives — correctly. The first version of this check
        // asserted global distinctness and the fuzzer falsified it in under a minute with
        // ["d","e","d","g"], which is a defect in the assertion rather than in the merge.
        List<string> appended = [.. merged.Skip(existing.Count)];

        if (appended.Distinct(StringComparer.Ordinal).Count() != appended.Count)
        {
            throw new InvalidOperationException("Append introduced a duplicate among the new tags");
        }

        foreach (string tag in appended)
        {
            if (existing.Contains(tag, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Append re-added a tag that was already present");
            }
        }

        foreach (string tag in incoming)
        {
            if (!merged.Contains(tag, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Append dropped an incoming tag");
            }
        }
    }

    /// <summary>
    /// The privacy guarantee, stated universally instead of enumerated.
    /// </summary>
    /// <param name="payload">Decoded as both a property name and its value.</param>
    /// <remarks>
    /// <para>
    /// <see cref="SentryRedactingSinkTypeTests"/> asserts that the seventeen
    /// types someone thought of are handled correctly. That is enumeration, and
    /// enumeration only ever covers the cases on the list. This asserts the
    /// actual contract: <b>for any string whatsoever, it does not reach
    /// Sentry</b> — unless its property name is on the app-authored allowlist.
    /// </para>
    /// <para>
    /// The fuzzer supplies the property NAME as well as the value, which is the
    /// part enumeration cannot reach. A name that collides with the allowlist
    /// after some normalisation the sink does not intend — trimming, casing, a
    /// culture-sensitive comparison — is exactly the bug that would let user
    /// text through, and nobody would write that test case by hand.
    /// </para>
    /// </remarks>
    private static void Redact(byte[] payload)
    {
        (string name, string value) = TwoSides(payload);

        // Serilog requires a non-empty property name and rejects some shapes
        // outright; those are its constraint, not a finding here.
        if (name.Length == 0 || value.Length == 0 || !IsValidPropertyName(name))
        {
            return;
        }

        MessageTemplate template = new MessageTemplateParser().Parse("value is {" + name + "}");
        LogEvent original = new(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            template,
            [new LogEventProperty(name, new ScalarValue(value))]);

        LogEvent redacted = SentryRedactingSink.Redact(original);

        object? forwarded = ((ScalarValue)redacted.Properties[name]).Value;

        // Only two names may carry a string, and both are written by this app.
        bool allowlisted = name is "ValidationMessage" or "Problem";

        if (!allowlisted && forwarded is string leaked && leaked != SentryRedactingSink.Redacted)
        {
            throw new InvalidOperationException(
                $"a string reached Sentry under the name '{name}'");
        }

        // Serilog hands the same instance to every sink, so redacting in place
        // would strip the local file log too, with the result depending on the
        // order the sinks happen to be registered in.
        if (!Equals(((ScalarValue)original.Properties[name]).Value, value))
        {
            throw new InvalidOperationException("the original event was mutated");
        }
    }

    /// <summary>
    /// Serilog's own rule for a usable property name, applied up front so its
    /// parser rejecting a name is not reported as a finding.
    /// </summary>
    private static bool IsValidPropertyName(string name) =>
        name.All(c => char.IsLetterOrDigit(c) || c == '_');

    /// <summary>Splits the payload at the first NUL into two UTF-8 strings.</summary>
    /// <remarks>
    /// No separator means everything is the left side and the right is empty,
    /// which is a case worth reaching rather than skipping — an empty side is
    /// how a note first written during a restore arrives.
    /// </remarks>
    private static (string Left, string Right) TwoSides(byte[] payload)
    {
        int split = Array.IndexOf(payload, SideSeparator);

        return split < 0
            ? (Encoding.UTF8.GetString(payload), string.Empty)
            : (Encoding.UTF8.GetString(payload, 0, split),
               Encoding.UTF8.GetString(payload, split + 1, payload.Length - split - 1));
    }
}
