// Ignore Spelling: Api Tcg

using System.Net.Http.Json;

namespace PokemonBattleJournal.Services
{
    public class TcgDexApiService
    {
        public HttpClient? HttpClient;

        public TcgDexApiService()
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return;
            HttpClient = new HttpClient();
        }

        public async Task<TcgDexCardBriefCollection?> AllCardsAsync()
        {
            if (HttpClient == null) HttpClient = new();
            TcgDexCardBriefCollection? json = await HttpClient.GetFromJsonAsync<TcgDexCardBriefCollection>("https://api.tcgdex.net/v2/en/cards");
            return json;
        }
    }
}
