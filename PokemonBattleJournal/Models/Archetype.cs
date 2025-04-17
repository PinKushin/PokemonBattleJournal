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

        [ForeignKey(typeof(Trainer)), Indexed]
        public uint TrainerId { get; set; }
        [ManyToOne]
        public Trainer? Trainer { get; set; }

        [OneToMany("PlayingId", CascadeOperations = CascadeOperation.All)]
        public List<MatchEntry>? PlayingMatches { get; set; }

        [OneToMany("AgainstId", CascadeOperations = CascadeOperation.All)]
        public List<MatchEntry>? AgainstMatches { get; set; }
    }
}