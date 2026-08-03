using System.Text.Json.Serialization;

namespace PokemonBattleJournal.Services.Import
{
    internal record TrainerHillEntry
    {
        [JsonPropertyName("playing")] public string Playing { get; init; } = string.Empty;
        [JsonPropertyName("against")] public string Against { get; init; } = string.Empty;
        [JsonPropertyName("time")] public string Time { get; init; } = string.Empty;
        [JsonPropertyName("result")] public string Result { get; init; } = string.Empty;
        [JsonPropertyName("game1")] public TrainerHillGame? Game1 { get; init; }
        [JsonPropertyName("game2")] public TrainerHillGame? Game2 { get; init; }
        [JsonPropertyName("game3")] public TrainerHillGame? Game3 { get; init; }
    }

    internal record TrainerHillGame
    {
        [JsonPropertyName("result")] public string Result { get; init; } = string.Empty;
        [JsonPropertyName("turn")] public System.Text.Json.JsonElement Turn { get; init; }
        [JsonPropertyName("tags")] public List<string>? Tags { get; init; }
        [JsonPropertyName("notes")] public string? Notes { get; init; }
    }
}
