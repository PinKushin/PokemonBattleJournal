using SQLiteNetExtensions.Attributes;

namespace PokemonBattleJournal.Models
{
    /// <summary>
    /// Represents a single game in a match, storing the result, tags, and notes
    /// </summary>
    public class Game
    {
        [PrimaryKey, AutoIncrement]
        public uint Id { get; set; }

        /// <summary>
        /// The result of this game
        /// </summary>
        public MatchResult? Result { get; set; }

        /// <summary>
        /// Indicates which player went first in this game
        /// 1 = Player, 2 = Opponent
        /// </summary>
        public uint Turn { get; set; } = 1;

        /// <summary>
        /// Collection of tags associated with this game
        /// </summary>
        [ManyToMany(typeof(TagGame), CascadeOperations = CascadeOperation.All)]
        public List<Tags>? Tags { get; set; }

        /// <summary>
        /// Optional notes about this game
        /// </summary>
        public string? Notes { get; set; }
    }
}

