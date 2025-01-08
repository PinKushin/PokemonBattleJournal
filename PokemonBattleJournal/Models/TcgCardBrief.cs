// Ignore Spelling: Tcg

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PokemonBattleJournal.Models
{
    public class TcgDexCardBrief
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "0";

        [JsonPropertyName("localId")]
        public string LocalId { get; set; } = "0";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "Card Name";

        [JsonPropertyName("image")]
        public string Image { get; set; } = "card_backside_atomicmonkeytcg.png";

        public TcgDexCardBrief() { }
    }
}
