using System.Collections.ObjectModel;
using Newtonsoft.Json;

namespace PokemonBattleJournal.Utilities
{
	public static class DataPopulationHelper
	{
		public static async Task<ObservableCollection<Archetype>> PopulateArchetypesAsync()
		{
			//This is a wrapper for supporting unit tests
			if (DeviceInfo.Platform == DevicePlatform.Unknown)
			{
				return [];
			}
			string filePath = FileHelper.GetAppDataPath() + "\\Archetypes.json";
			ObservableCollection<Archetype>? archetypes = new();
			if (FileHelper.Exists(filePath))
			{
				string? saveFile = await FileHelper.ReadFileAsync(filePath);
				archetypes = JsonConvert.DeserializeObject<ObservableCollection<Archetype>>(saveFile);
				return archetypes ?? [];
			}
			else
			{
				archetypes.Add(new Archetype("Regidrago", "regidrago.png"));
				archetypes.Add(new Archetype("Charizard", "charizard.png"));
				archetypes.Add(new Archetype("Klawf", "klawf.png"));
				archetypes.Add(new Archetype("Snorlax Stall", "snorlax.png"));
				archetypes.Add(new Archetype("Raging Bolt", "raging_bolt.png"));
				archetypes.Add(new Archetype("Gardevoir", "gardevoir.png"));
				archetypes.Add(new Archetype("Miraidon", "miraidon.png"));
				archetypes.Add(new Archetype("Other", "ball_icon.png"));
				string saveFile = JsonConvert.SerializeObject(archetypes, Formatting.Indented);
				await FileHelper.WriteFileAsync(filePath, saveFile);
				return archetypes;
			}
		}

		public static async Task<ObservableCollection<string>> PopulateTagsAsync()
		{
			ObservableCollection<string> tagCollection = [];
			string filePath = FileHelper.GetAppDataPath() + "\\Tags.json";
			if (!FileHelper.Exists(filePath))
			{
				FileHelper.CreateFile(filePath);
				tagCollection.Add("Early Start");
				tagCollection.Add("Behind Early");
				tagCollection.Add("Donked Rival");
				tagCollection.Add("Got Donked");
				tagCollection.Add("Lucky");
				tagCollection.Add("Unlucky");
				tagCollection.Add("Never Punished");
				tagCollection.Add("Punished");
				string saveFile = JsonConvert.SerializeObject(tagCollection, Formatting.Indented);
				await FileHelper.WriteFileAsync(filePath, saveFile);
			}
			else
			{
				string savedTags = await FileHelper.ReadFileAsync(filePath);
				tagCollection = JsonConvert.DeserializeObject<ObservableCollection<string>>(savedTags) ?? [];
			}
			return tagCollection;
		}
	}
}