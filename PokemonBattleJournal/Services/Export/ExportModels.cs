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
        [JsonPropertyName("result")] public string Result { get; init; } = string.Empty;
        [JsonPropertyName("game1")] public ExportGame? Game1 { get; init; }
        [JsonPropertyName("game2")] public ExportGame? Game2 { get; init; }
        [JsonPropertyName("game3")] public ExportGame? Game3 { get; init; }
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
        [JsonPropertyName("trainers")] public List<ExportTrainer> Trainers { get; init; } = [];
    }
}
