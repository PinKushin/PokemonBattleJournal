using Newtonsoft.Json;

namespace PokemonBattleJournal.Models.Interfaces
{
	public interface IArchetype
	{
		[JsonProperty("name")]
		public string Name { get; set; }
	}
}
