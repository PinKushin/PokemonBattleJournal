using SQLite;
using SQLiteNetExtensions.Attributes;

namespace PokemonBattleJournal.Models
{
    public class Archetype
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        [Unique]
        public string Name { get; set; } = string.Empty;
        public string? ImagePath { get; set; }

        [Column("trainer_id"), ForeignKey(typeof(Trainer))]
        public uint TrainerId { get; set; }
    }
}