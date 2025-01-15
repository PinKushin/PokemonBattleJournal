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

        public async Task<TcgDexCardBriefCollection?> GetAllCardsAsync()
        {
            if (HttpClient == null) HttpClient = new();
            try
            {
                TcgDexCardBriefCollection? json = await HttpClient.GetFromJsonAsync<TcgDexCardBriefCollection>("https://api.tcgdex.net/v2/en/cards?sort:field=name");
                return json;
            }
            catch (Exception ex)
            {
                ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
                modalErrorHandler.HandleError(ex);
                return null;
            }
        }

        public async Task<TcgDexCard?> GetCardDetailsAsync(string id)
        {
            if (HttpClient == null) HttpClient = new();
            try
            {
                string? json = await HttpClient.GetStringAsync($"https://api.tcgdex.net/v2/en/cards/{id}");
                var tcgDexCard = TcgDexCard.FromJson(json);
                return tcgDexCard;
            }
            catch (Exception ex)
            {

                ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
                modalErrorHandler.HandleError(ex);
                return null;

            }
        }

        public async Task<TcgDexCardBriefCollection?> GetStandardCardsAsync()
        {
            if (HttpClient == null) HttpClient = new();
            try
            {
                //Filter Standard Legal Cards
                TcgDexCardBriefCollection? json = await HttpClient.GetFromJsonAsync<TcgDexCardBriefCollection>(
                    "https://api.tcgdex.net/v2/en/cards?legal.standard=true&sort:field=name"
                    );
                return json;
            }
            catch (Exception ex)
            {
                ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
                modalErrorHandler.HandleError(ex);
                return null;
            }
        }
    }
}
