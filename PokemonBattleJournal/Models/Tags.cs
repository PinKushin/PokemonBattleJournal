using SQLite;
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
        [Column("trainer_id"), ForeignKey(typeof(Trainer))]
        public uint? TrainerId { get; set; }
    }
}
