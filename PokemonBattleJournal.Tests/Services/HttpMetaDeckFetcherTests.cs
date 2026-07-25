using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Scraper.Services;
using System.Net;

namespace PokemonBattleJournal.Tests.Services;

public class HttpMetaDeckFetcherTests
{
    private static HttpMetaDeckFetcher BuildFetcher(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<HttpMetaDeckFetcher>.Instance);

    [Fact]
    public async Task FetchAsync_ValidResponse_ReturnsHtml()
    {
        string expectedHtml = "<html><body>test</body></html>";
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(expectedHtml)
        });

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(expectedHtml);
    }

    [Fact]
    public async Task FetchAsync_NetworkFailure_ReturnsEmptyString()
    {
        var handler = new ThrowingHttpMessageHandler();

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task FetchAsync_NonSuccessStatusCode_ReturnsEmptyString()
    {
        var handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(string.Empty);
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated network failure");
    }
}
