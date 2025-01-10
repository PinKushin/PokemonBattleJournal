using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PokemonBattleJournal.Models
{
    [JsonObject]
    public partial class MatchEntry
    {
        [JsonProperty("playing")]
        public string Playing { get; set; } = string.Empty;

        [JsonProperty("against")]
        public string Against { get; set; } = string.Empty;

        [JsonProperty("time")]
        public DateTimeOffset Time { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; } = string.Empty;

        [JsonProperty("game1")]
        public Game Game1 { get; set; } = new();

        [JsonProperty("game2", NullValueHandling = NullValueHandling.Ignore)]
        public Game? Game2 { get; set; }

        [JsonProperty("game3", NullValueHandling = NullValueHandling.Ignore)]
        public Game? Game3 { get; set; }

        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public DateTime? DatePlayed { get; set; }

    }
    public partial class Game
    {
        [JsonProperty("result")]
        public string Result { get; set; } = "Draw";

        [JsonProperty("turn")]
        [JsonConverter(typeof(ParseStringConverter))]
        public long Turn { get; set; } = 1;

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonProperty("notes")]
        public string? Notes { get; set; }
    }
}
