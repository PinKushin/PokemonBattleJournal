using SQLiteNetExtensions.Attributes;
namespace PokemonBattleJournal.Models
{
    public class Tags
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }
        [Unique]
        public string? Name { get; set; }

        //trainer foreign key for profile
        [ForeignKey(typeof(Trainer)), Indexed]
        public uint? TrainerId { get; set; }

        [ManyToOne]
        public Trainer? Trainer { get; set; }

        [ManyToMany(typeof(TagGame), CascadeOperations = CascadeOperation.All)]
        public List<Game>? Games { get; set; }
    }
}
