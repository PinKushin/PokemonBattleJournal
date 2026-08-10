using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Scraper.Services;
using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.Services;

public class HttpMetaDeckFetcherTests
{
    private static HttpMetaDeckFetcher BuildFetcher(HttpMessageHandler handler) =>
        new(new HttpClient(handler), NullLogger<HttpMetaDeckFetcher>.Instance);

    [Test]
    public async Task FetchAsync_ValidResponse_ReturnsHtml()
    {
        string expectedHtml = "<html><body>test</body></html>";
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(expectedHtml)
        });

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(expectedHtml);
    }

    [Test]
    public async Task FetchAsync_NetworkFailure_ReturnsEmptyString()
    {
        ThrowingHttpMessageHandler handler = new ThrowingHttpMessageHandler();

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(string.Empty);
    }

    [Test]
    public async Task FetchAsync_NonSuccessStatusCode_ReturnsEmptyString()
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(string.Empty);
    }

    [Test]
    public async Task FetchAsync_404_ReturnsEmptyString()
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound));

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(string.Empty);
    }

    [Test]
    public async Task FetchAsync_EmptyResponseBody_ReturnsEmptyString()
    {
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty)
        });

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(string.Empty);
    }

    [Test]
    public async Task FetchAsync_LargeHtmlPayload_ReturnsFullContent()
    {
        string bigHtml = string.Concat(Enumerable.Repeat("<tr><td>row</td></tr>", 500));
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(bigHtml)
        });

        HttpMetaDeckFetcher fetcher = BuildFetcher(handler);
        string result = await fetcher.FetchAsync("https://limitlesstcg.com/decks");

        result.ShouldBe(bigHtml);
    }

    // ---------------------------------------------------------------------------
    // Gaps found by mutation testing (Stryker, 2026-08-07). Every test above used
    // NullLogger, so deleting either LogWarning call — or changing its message —
    // went unnoticed. That matters here specifically: the repo standard is that a
    // guard which declines to act must say WHY at Warning
    // (docs/memory/feedback_no_silent_guards.md). Both of these swallow a failure
    // and return string.Empty, so the log is the only evidence the caller gets.
    // ---------------------------------------------------------------------------

    [Test]
    public async Task FetchAsync_NonSuccessStatus_LogsWarningWithTheStatus()
    {
        RecordingLogger<HttpMetaDeckFetcher> logger = new RecordingLogger<HttpMetaDeckFetcher>();
        FakeHttpMessageHandler handler = new FakeHttpMessageHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        HttpMetaDeckFetcher fetcher = new HttpMetaDeckFetcher(new HttpClient(handler), logger);

        string result = await fetcher.FetchAsync("https://example.test/decks");

        result.ShouldBeEmpty();
        logger.EntriesMatching(LogLevel.Warning, "non-success")
            .ShouldNotBeEmpty($"a swallowed HTTP failure must be logged. Logged:{Environment.NewLine}{logger.Dump()}");
        logger.EntriesMatching(LogLevel.Warning, "NotFound")
            .ShouldNotBeEmpty("the log must name the status, or it cannot be diagnosed from a log file");
    }

    [Test]
    public async Task FetchAsync_NetworkFailure_LogsWarningWithTheUrl()
    {
        RecordingLogger<HttpMetaDeckFetcher> logger = new RecordingLogger<HttpMetaDeckFetcher>();
        HttpMetaDeckFetcher fetcher = new HttpMetaDeckFetcher(new HttpClient(new ThrowingHttpMessageHandler()), logger);

        string result = await fetcher.FetchAsync("https://example.test/decks");

        result.ShouldBeEmpty();
        logger.EntriesMatching(LogLevel.Warning, "example.test")
            .ShouldNotBeEmpty($"a swallowed network failure must name the URL. Logged:{Environment.NewLine}{logger.Dump()}");
    }

    private sealed class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("Simulated network failure"));
    }
}

