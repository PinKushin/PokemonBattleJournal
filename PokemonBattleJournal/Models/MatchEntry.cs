using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PokemonBattleJournal.Models
{
	[JsonObject]
	public partial class MatchEntry
	{
		[JsonProperty("playing")]
		public Archetype? Playing { get; set; }

		[JsonProperty("against")]
		public Archetype? Against { get; set; }

		[JsonProperty("time")]
		public DateTime Time { get; set; }

		[JsonProperty("result")]
		public string? Result { get; set; }

		[JsonProperty("game1")]
		public Game Game1 { get; set; } = new Game();

		[JsonProperty("game2", NullValueHandling = NullValueHandling.Ignore)]
		public Game? Game2 { get; set; }

		[JsonProperty("game3", NullValueHandling = NullValueHandling.Ignore)]
		public Game? Game3 { get; set; }

		[JsonProperty("start-time", NullValueHandling = NullValueHandling.Ignore)]
		public DateTime StartTime { get; set; }

		[JsonProperty("end-time", NullValueHandling = NullValueHandling.Ignore)]
		public DateTime EndTime { get; set; }

		[JsonProperty("date-played", NullValueHandling = NullValueHandling.Ignore)]
		public DateTime DatePlayed { get; set; }
	}

	public partial class Game
	{
		[JsonProperty("result")]
		public string Result { get; set; } = "Lose";

		[JsonProperty("turn")]
		public long Turn { get; set; } = 1;

		[JsonProperty("tags")]
		public List<string> Tags { get; set; } = new();

		[JsonProperty("notes")]
		public string? Notes { get; set; }
	}
}