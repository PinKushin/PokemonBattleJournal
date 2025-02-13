using Newtonsoft.Json;
using PokemonBattleJournal.Models.Interfaces;

namespace PokemonBattleJournal.Models
{
	public class Archetype : IArchetype
	{
		public Archetype(string name, string imagePath = "ball_icon.png")
		{
			Name = name;
			ImagePath = imagePath;
		}

		[JsonProperty("name")]
		public string Name { get; set; }
		[JsonProperty("imagePath")]
		public string ImagePath { get; set; } = "ball_icon.png";

		public override bool Equals(object? obj)
		{
			return obj is IArchetype archetype &&
				   Name == archetype.Name;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Name);
		}

		public static bool operator ==(Archetype? left, Archetype? right)
		{
			return EqualityComparer<Archetype>.Default.Equals(left, right);
		}

		public static bool operator !=(Archetype? left, Archetype? right)
		{
			return !(left == right);
		}
	}
}
