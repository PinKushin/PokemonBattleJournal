using SQLiteNetExtensions.Attributes;
namespace PokemonBattleJournal.Models
{
    public partial class MatchEntry
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }
        [ForeignKey(typeof(Trainer)), Indexed]
        public uint TrainerId { get; set; }
        [ManyToOne]
        public Trainer? Trainer { get; set; }

        [ForeignKey(typeof(Archetype)), Indexed]
        public uint PlayingId { get; set; }
        [ManyToOne]
        public Archetype? Playing { get; set; }

        [ForeignKey(typeof(Archetype)), Indexed]
        public uint AgainstId { get; set; }
        [ManyToOne]
        public Archetype? Against { get; set; }

        public MatchResult? Result { get; set; }

        [ForeignKey(typeof(Game)), Indexed]
        public uint? Game1Id { get; set; }
        [OneToOne(CascadeOperations = CascadeOperation.All)]
        public Game? Game1 { get; set; }

        [ForeignKey(typeof(Game)), Indexed]
        public uint? Game2Id { get; set; }
        [OneToOne(CascadeOperations = CascadeOperation.All)]
        public Game? Game2 { get; set; }

        [ForeignKey(typeof(Game)), Indexed]
        public uint? Game3Id { get; set; }
        [OneToOne(CascadeOperations = CascadeOperation.All)]
        public Game? Game3 { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public DateTime DatePlayed { get; set; }
    }
}
