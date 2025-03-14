using SQLite;

namespace PokemonBattleJournal.Models
{
    public class Archetype
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        [Unique]
        public string Name { get; set; }
        public string? ImagePath { get; set; }
    }
}