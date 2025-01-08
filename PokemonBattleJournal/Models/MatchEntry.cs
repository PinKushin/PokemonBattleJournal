using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace PokemonBattleJournal.Model
{
    [JsonObject]
    public class MatchEntry
    {
        public string PlayerSelected = "Other";
        public string RivalSelected = "Other";
        public TimeSpan StartTime;
        public TimeSpan EndTime;
        public DateTime DatePlayed;
        public bool FirstCheck;
        public string Result = "";
        public IList<string> TagsSelected = [];
        public string Note = "";
    }
}
