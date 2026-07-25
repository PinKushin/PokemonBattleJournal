using Microsoft.Extensions.Logging;
using PokemonBattleJournal.Scraper.Interfaces;

namespace PokemonBattleJournal.Scraper.Services;

public class HttpMetaDeckFetcher : IMetaDeckFetcher
{
    private readonly HttpClient _http;
    private readonly ILogger<HttpMetaDeckFetcher> _logger;

    public HttpMetaDeckFetcher(HttpClient http, ILogger<HttpMetaDeckFetcher> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<string> FetchAsync(string url)
    {
        try
        {
            HttpResponseMessage response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FetchAsync received non-success status {Status} from {Url}", response.StatusCode, url);
                return string.Empty;
            }
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "FetchAsync failed for {Url} — likely offline", url);
            return string.Empty;
        }
    }
}
