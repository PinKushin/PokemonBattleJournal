using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Scraper.Services;

namespace PokemonBattleJournal.IntegrationTests.Scraper;

/// <summary>
/// Live network tests against limitlesstcg.com. Run on a schedule, not on every push.
/// A failure here means Limitless changed their page structure — update LimitlessDeckParser
/// and refresh PokemonBattleJournal.Tests/Fixtures/limitless_decks_snapshot.html.
/// </summary>
[Category("LiveWeb")]
public class LimitlessLiveWebTests
{
    private LimitlessMetaService _service = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        HttpClient http = new();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PokemonBattleJournal/1.0 (+https://github.com/PinKushin/PokemonBattleJournal)");

        HttpMetaDeckFetcher fetcher = new(http, NullLogger<HttpMetaDeckFetcher>.Instance);
        LimitlessDeckParser parser = new();
        _service = new LimitlessMetaService(fetcher, parser, NullLogger<LimitlessMetaService>.Instance);
    }

    [Test]
    public async Task LiveFetch_ReturnsNonEmptyDeckList()
    {
        List<MetaDeck> decks = await _service.GetTopDecksAsync(10);
        decks.ShouldNotBeEmpty("Limitless returned no decks — page structure may have changed or site is down");
    }

    [Test]
    public async Task LiveFetch_ReturnsRequestedCount()
    {
        List<MetaDeck> decks = await _service.GetTopDecksAsync(10);
        decks.Count.ShouldBe(10, "Expected exactly 10 decks — parser row selector may be broken");
    }

    [Test]
    public async Task LiveFetch_AllDecksHaveName()
    {
        List<MetaDeck> decks = await _service.GetTopDecksAsync(10);
        decks.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d.Name),
            "Every deck must have a name — check td:nth-child(3) a selector in LimitlessDeckParser");
    }

    [Test]
    public async Task LiveFetch_AllDecksHaveImageUrl()
    {
        List<MetaDeck> decks = await _service.GetTopDecksAsync(10);
        decks.ShouldAllBe(d => !string.IsNullOrWhiteSpace(d.ImageUrl),
            "Every deck must have an image URL — check img.pokemon selector in LimitlessDeckParser");
    }

    [Test]
    public async Task LiveFetch_ImageUrlsAreAbsolute()
    {
        List<MetaDeck> decks = await _service.GetTopDecksAsync(10);
        decks.ShouldAllBe(d => d.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase),
            "Image URLs must be absolute HTTP URLs — Limitless may have changed their CDN path");
    }

    [Test]
    public async Task LiveFetch_NoDuplicateNames()
    {
        List<MetaDeck> decks = await _service.GetTopDecksAsync(10);
        int distinct = decks.Select(d => d.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        distinct.ShouldBe(decks.Count, "Duplicate deck names — parser may be double-counting rows");
    }

    [Test]
    public async Task LiveFetch_HttpFetcher_ReturnsNonEmptyHtml()
    {
        HttpClient http = new();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("PokemonBattleJournal/1.0");
        HttpMetaDeckFetcher fetcher = new(http, NullLogger<HttpMetaDeckFetcher>.Instance);

        string html = await fetcher.FetchAsync("https://limitlesstcg.com/decks");
        html.ShouldNotBeNullOrWhiteSpace("Fetch returned empty — site may be down or blocking requests");
        html.ShouldContain("<html", Case.Insensitive, "Response is not HTML — endpoint may have changed");
    }
}
