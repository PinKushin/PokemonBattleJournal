// Ignore Spelling: Tcg

using System.Collections.ObjectModel;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace PokemonBattleJournal.Models
{
    public partial class TcgDexCardBriefCollection : ObservableCollection<TcgDexCardBrief>
    {
        public List<TcgDexCardBrief>? CardBriefCollection { get; set; }
        public TcgDexCardBriefCollection()
        {
        }
    }
}
