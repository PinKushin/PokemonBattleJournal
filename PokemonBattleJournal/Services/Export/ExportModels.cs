using System.Text.Json.Serialization;

namespace PokemonBattleJournal.Services.Export
{
    /// <summary>
    /// Write-side mirror of <c>TrainerHillEntry</c>.
    /// </summary>
    /// <remarks>
    /// A separate type rather than reusing the import record because the two have genuinely
    /// different needs. Import must tolerate what TrainerHill actually emits — notably a
    /// <c>turn</c> that is sometimes an int and sometimes a string, hence its
    /// <c>JsonElement</c> — while export should emit one canonical shape. Writing through a
    /// <c>JsonElement</c> would mean constructing one just to serialize a number.
    ///
    /// Nullable game slots are omitted rather than written as null, because a BO1 match has no
    /// second game and TrainerHill's own exports leave the key out entirely.
    /// </remarks>
    internal sealed record ExportGame
    {
        [JsonPropertyName("result")] public string Result { get; init; } = string.Empty;
        [JsonPropertyName("turn")] public uint Turn { get; init; } = 1;
        [JsonPropertyName("tags")] public List<string> Tags { get; init; } = [];
        [JsonPropertyName("notes")] public string? Notes { get; init; }
    }

    internal sealed record ExportEntry
    {
        /// <summary>Archetype slug in TrainerHill exports; the verbatim name in backups.</summary>
        [JsonPropertyName("playing")] public string Playing { get; init; } = string.Empty;
        [JsonPropertyName("against")] public string Against { get; init; } = string.Empty;
        [JsonPropertyName("time")] public string Time { get; init; } = string.Empty;

        /// <summary>
        /// Match start, backups only. Null for TrainerHill exports, and omitted when null.
        /// </summary>
        /// <remarks>
        /// <c>MatchEntry</c> keeps <c>StartTime</c>, <c>EndTime</c> and <c>DatePlayed</c>
        /// separately, and <c>time</c> above carries only <c>DatePlayed</c>. Without these a
        /// restored match has a zero duration, which silently corrupts
        /// <c>CalculateAverageMatchDuration</c> and <c>CalculateWinRateByMatchLength</c>.
        ///
        /// They are nullable rather than always-written because the two formats share this
        /// type and TrainerHill's has no such keys — its whole value is being
        /// indistinguishable from a file of theirs. <c>JsonIgnoreCondition.WhenWritingNull</c>
        /// is what keeps them out of that file.
        ///
        /// Restores must treat them as optional: backups written before this existed have
        /// neither, and should fall back to <c>time</c>.
        /// </remarks>
        [JsonPropertyName("startTime")] public string? StartTime { get; init; }

        /// <inheritdoc cref="StartTime"/>
        [JsonPropertyName("endTime")] public string? EndTime { get; init; }

        [JsonPropertyName("result")] public string Result { get; init; } = string.Empty;
        [JsonPropertyName("game1")] public ExportGame? Game1 { get; init; }
        [JsonPropertyName("game2")] public ExportGame? Game2 { get; init; }
        [JsonPropertyName("game3")] public ExportGame? Game3 { get; init; }
    }

    /// <summary>
    /// An archetype and its icons. Backup envelope only.
    /// </summary>
    /// <remarks>
    /// Icons are user data — OptionsPage lets a custom archetype be given a chosen icon, and
    /// <c>ImagePath2</c> carries the second icon of a dual-icon deck. A name alone cannot
    /// recover a deliberate choice, so a backup that stored only names was not lossless.
    ///
    /// Recorded once at the envelope level rather than on every entry, because
    /// <c>Archetype.Name</c> is <c>[Unique]</c> across the whole table — two trainers cannot
    /// hold the same archetype name, so per-entry copies would repeat themselves without
    /// adding information.
    ///
    /// It is still not a global table, though. Each row carries a <c>TrainerId</c>: the
    /// Limitless scrape and the offline seed are shared, but a custom archetype belongs to the
    /// trainer who created it (user, 2026-08-06). So ownership has to be exported or a restore
    /// would hand every custom archetype to whoever happened to run it.
    ///
    /// Ownership travels as a trainer *name*, not an id. Row ids are not stable across
    /// databases — restoring into a fresh install renumbers everything — and the envelope
    /// already keys trainers by name everywhere else.
    ///
    /// TrainerHill's format has no equivalent and is not given one — icons are knowingly lost
    /// there, because that file's value is being indistinguishable from one of theirs.
    /// </remarks>
    internal sealed record ExportArchetype
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("imagePath")] public string ImagePath { get; init; } = string.Empty;
        [JsonPropertyName("imagePath2")] public string? ImagePath2 { get; init; }

        /// <summary>
        /// Creating trainer's name, or null for archetypes with no owner — the seeded and
        /// scraped ones. Null is meaningful, not missing data.
        /// </summary>
        [JsonPropertyName("trainerName")] public string? TrainerName { get; init; }
    }

    internal sealed record ExportTrainer
    {
        [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
        [JsonPropertyName("matches")] public List<ExportEntry> Matches { get; init; } = [];
    }

    /// <summary>
    /// Backup envelope. Distinguishable from a TrainerHill export by its root token: an object
    /// rather than an array, which is what a future restore should branch on.
    /// </summary>
    internal sealed record ExportBackup
    {
        /// <summary>Bumped only on a breaking shape change, so a restore can refuse what it cannot read.</summary>
        [JsonPropertyName("version")] public int Version { get; init; } = 1;
        [JsonPropertyName("exportedUtc")] public string ExportedUtc { get; init; } = string.Empty;

        /// <summary>
        /// Every archetype in the database, not only those a match references — a custom
        /// archetype created but never played is still user data and must survive a restore.
        /// Absent in backups written before icons were carried; a restore must tolerate that
        /// and fall back to resolving an icon from the name.
        /// </summary>
        [JsonPropertyName("archetypes")] public List<ExportArchetype> Archetypes { get; init; } = [];

        [JsonPropertyName("trainers")] public List<ExportTrainer> Trainers { get; init; } = [];
    }
}
