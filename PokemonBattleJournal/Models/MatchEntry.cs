using SQLite;
using SQLiteNetExtensions.Attributes;

namespace PokemonBattleJournal.Models
{
    public partial class MatchEntry
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }
        [ForeignKey(typeof(Trainer))]
        public uint TrainerId { get; set; }

        [ForeignKey(typeof(Archetype))]
        public uint PlayingId { get; set; }
        [ManyToOne]
        public Archetype? Playing { get; set; }

        [ForeignKey(typeof(Archetype))]
        public uint AgainstId { get; set; }
        [ManyToOne]
        public Archetype? Against { get; set; }

        public MatchResult? Result { get; set; }

        [ForeignKey(typeof(Game))]
        public uint? Game1Id { get; set; }
        [OneToOne]
        public Game? Game1 { get; set; }

        [ForeignKey(typeof(Game))]
        public uint Game2Id { get; set; }
        [OneToOne]
        public Game? Game2 { get; set; }

        [ForeignKey(typeof(Game))]
        public uint Game3Id { get; set; }
        [OneToOne]
        public Game? Game3 { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public DateTime DatePlayed { get; set; }
    }

    public partial class Game
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        [ForeignKey(typeof(MatchEntry))]
        public uint MatchEntryId { get; set; }

        public MatchResult? Result { get; set; }

        public uint Turn { get; set; } = 1;

        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<Tags>? Tags { get; set; }

        public string? Notes { get; set; }
    }
}