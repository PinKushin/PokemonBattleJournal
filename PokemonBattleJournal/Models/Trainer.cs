using SQLiteNetExtensions.Attributes;
namespace PokemonBattleJournal.Models
{
    public class Trainer
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        [Unique]
        public string? Name { get; set; }

        [OneToMany("TrainerId", CascadeOperations = CascadeOperation.All)]
        public List<Archetype>? Archetypes { get; set; }
        [OneToMany("Id", CascadeOperations = CascadeOperation.All)]
        public List<Tags>? Tags { get; set; }
        [OneToMany("TrainerId", CascadeOperations = CascadeOperation.All)]
        public List<MatchEntry>? Matches { get; set; }

    }
}
