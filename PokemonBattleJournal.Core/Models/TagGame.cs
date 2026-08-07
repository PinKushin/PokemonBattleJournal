using SQLiteNetExtensions.Attributes;

namespace PokemonBattleJournal.Models
{
    /// <summary>
    /// Junction table for many-to-many relationship between Game and Tags
    /// </summary>
    [Table("TagGame")]
    public class TagGame
    {
        [ForeignKey(typeof(Game)), Indexed(Name = "TagGame_GameId_TagId", Order = 1)]
        public uint GameId { get; set; }

        [ManyToOne]
        public Game? Game { get; set; }

        [ForeignKey(typeof(Tags)), Indexed(Name = "TagGame_GameId_TagId", Order = 2)]
        public uint TagId { get; set; }

        [ManyToOne]
        public Tags? Tag { get; set; }
    }
}

