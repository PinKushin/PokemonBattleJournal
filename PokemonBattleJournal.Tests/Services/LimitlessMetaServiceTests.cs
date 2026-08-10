using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Services;
using PokemonBattleJournal.Tests.Fixtures;

namespace PokemonBattleJournal.Tests.Services;

public class LimitlessMetaServiceTests
{
    private static readonly string SentinelPath =
        Path.Combine(Path.GetTempPath(), "PokemonBattleJournal.uitest");
    private bool _sentinelExisted;

    [SetUp]
    public void SetUp()
    {
        _sentinelExisted = File.Exists(SentinelPath);
        if (_sentinelExisted)
            File.Delete(SentinelPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (_sentinelExisted && !File.Exists(SentinelPath))
            File.WriteAllText(SentinelPath, string.Empty);
    }

    private static LimitlessMetaService BuildService(IMetaDeckFetcher fetcher, IMetaDeckParser parser) =>
        new(fetcher, parser, NullLogger<LimitlessMetaService>.Instance);

    // ---------------------------------------------------------------------------
    // Gaps found by mutation testing (Stryker, 2026-08-07). Every test here used
    // NullLogger, so both LogInformation calls could be deleted unnoticed. Both
    // sit on paths that return an EMPTY LIST — a caller cannot tell "the meta is
    // genuinely empty" from "we skipped the fetch" or "we were offline" without
    // them, which is the distinction these lines exist to record.
    // ---------------------------------------------------------------------------

    [Test]
    public async Task GetTopDecksAsync_UiTestSentinelPresent_SaysWhyItSkippedTheFetch()
    {
        RecordingLogger<LimitlessMetaService> logger = new RecordingLogger<LimitlessMetaService>();
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        LimitlessMetaService service = new LimitlessMetaService(fetcher, Substitute.For<IMetaDeckParser>(), logger);

        File.WriteAllText(SentinelPath, string.Empty);
        try
        {
            List<MetaDeck> result = await service.GetTopDecksAsync();

            result.ShouldBeEmpty();
            await fetcher.DidNotReceive().FetchAsync(Arg.Any<string>());
            logger.EntriesMatching(LogLevel.Information, "sentinel")
                .ShouldNotBeEmpty($"skipping the network must be stated, or an empty meta list looks like a real result. Logged:{Environment.NewLine}{logger.Dump()}");
        }
        finally
        {
            File.Delete(SentinelPath);
        }
    }

    [Test]
    public async Task GetTopDecksAsync_EmptyResponse_SaysItMayBeOffline()
    {
        RecordingLogger<LimitlessMetaService> logger = new RecordingLogger<LimitlessMetaService>();
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        fetcher.FetchAsync(Arg.Any<string>()).Returns(string.Empty);
        LimitlessMetaService service = new LimitlessMetaService(fetcher, Substitute.For<IMetaDeckParser>(), logger);

        List<MetaDeck> result = await service.GetTopDecksAsync();

        result.ShouldBeEmpty();
        logger.EntriesMatching(LogLevel.Information, "empty response")
            .ShouldNotBeEmpty($"an empty fetch must be distinguishable from an empty meta. Logged:{Environment.NewLine}{logger.Dump()}");
    }

    [Test]
    public async Task GetTopDecksAsync_FetcherReturnsHtml_DelegatesParseWithCount()
    {
        const string html = "<html>deck table html</html>";
        const int count = 10;

        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();

        fetcher.FetchAsync(Arg.Any<string>()).Returns(html);
        parser.Parse(html, count).Returns([new MetaDeck("Dragapult ex", "https://example.com/dragapult.png")]);

        LimitlessMetaService service = BuildService(fetcher, parser);
        List<MetaDeck> result = await service.GetTopDecksAsync(count);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Dragapult ex");
        parser.Received(1).Parse(html, count);
    }

    [Test]
    public async Task GetTopDecksAsync_FetcherReturnsEmptyString_ReturnsEmptyListWithoutParsing()
    {
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();

        fetcher.FetchAsync(Arg.Any<string>()).Returns(string.Empty);

        LimitlessMetaService service = BuildService(fetcher, parser);
        List<MetaDeck> result = await service.GetTopDecksAsync();

        result.ShouldBeEmpty();
        parser.DidNotReceive().Parse(Arg.Any<string>(), Arg.Any<int>());
    }

    [Test]
    public async Task GetTopDecksAsync_FetcherReturnsWhitespace_ReturnsEmptyListWithoutParsing()
    {
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();

        fetcher.FetchAsync(Arg.Any<string>()).Returns("   \n  ");

        LimitlessMetaService service = BuildService(fetcher, parser);
        List<MetaDeck> result = await service.GetTopDecksAsync();

        result.ShouldBeEmpty();
        parser.DidNotReceive().Parse(Arg.Any<string>(), Arg.Any<int>());
    }

    [Test]
    public async Task GetTopDecksAsync_PassesDecksUrlToFetcher()
    {
        const string expectedUrl = "https://limitlesstcg.com/decks";
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();

        fetcher.FetchAsync(Arg.Any<string>()).Returns("<html>data</html>");
        parser.Parse(Arg.Any<string>(), Arg.Any<int>()).Returns([]);

        LimitlessMetaService service = BuildService(fetcher, parser);
        await service.GetTopDecksAsync(5);

        await fetcher.Received(1).FetchAsync(expectedUrl);
    }

    [Test]
    public async Task GetTopDecksAsync_DefaultCount_PassesTenToParser()
    {
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();

        fetcher.FetchAsync(Arg.Any<string>()).Returns("<html>data</html>");
        parser.Parse(Arg.Any<string>(), Arg.Any<int>()).Returns([]);

        LimitlessMetaService service = BuildService(fetcher, parser);
        await service.GetTopDecksAsync();

        parser.Received(1).Parse(Arg.Any<string>(), 10);
    }

    [Test]
    public async Task GetTopDecksAsync_WhenSentinelFilePresent_ReturnsEmptyWithoutCallingFetcher()
    {
        File.WriteAllText(SentinelPath, string.Empty);
        _sentinelExisted = true; // ensure TearDown preserves it

        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();

        LimitlessMetaService service = BuildService(fetcher, parser);
        List<MetaDeck> result = await service.GetTopDecksAsync();

        result.ShouldBeEmpty();
        await fetcher.DidNotReceive().FetchAsync(Arg.Any<string>());
    }
}

public class MetaServiceFactoryTests
{
    // Same sentinel dance as LimitlessMetaServiceTests. The service skips its fetch entirely
    // when %TEMP%\PokemonBattleJournal.uitest exists, and that file is left behind by any UI
    // test run on this machine — so without this, a test asserting the fetcher was called fails
    // for a reason that has nothing to do with the code under test. It did.
    private static readonly string SentinelPath =
        Path.Combine(Path.GetTempPath(), "PokemonBattleJournal.uitest");
    private bool _sentinelExisted;

    [SetUp]
    public void SetUp()
    {
        _sentinelExisted = File.Exists(SentinelPath);
        if (_sentinelExisted)
            File.Delete(SentinelPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (_sentinelExisted && !File.Exists(SentinelPath))
            File.WriteAllText(SentinelPath, string.Empty);
    }

    [Test]
    public void Create_ReturnsLimitlessMetaService()
    {
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();
        MetaServiceFactory factory = new(fetcher, parser, Microsoft.Extensions.Logging.Abstractions.NullLogger<LimitlessMetaService>.Instance);

        ILimitlessMetaService service = factory.Create();

        service.ShouldNotBeNull();
        service.ShouldBeOfType<LimitlessMetaService>();
    }

    /// <summary>
    /// The created service must actually USE the injected dependencies.
    /// </summary>
    /// <remarks>
    /// Found by mutation testing (Stryker, 2026-08-07): deleting the factory's entire
    /// constructor body survived. The type check above passes with every field left null,
    /// because construction never dereferences them — the failure would surface later as a
    /// NullReferenceException from inside a service the factory appeared to build correctly.
    /// Only exercising the built service pins the wiring.
    /// </remarks>
    [Test]
    public async Task Create_ReturnsServiceWiredToTheInjectedDependencies()
    {
        IMetaDeckFetcher fetcher = Substitute.For<IMetaDeckFetcher>();
        fetcher.FetchAsync(Arg.Any<string>()).Returns("<html></html>");

        IMetaDeckParser parser = Substitute.For<IMetaDeckParser>();
        parser.Parse(Arg.Any<string>(), Arg.Any<int>())
            .Returns([new MetaDeck("Dragapult ex", "dragapult.png", null)]);

        MetaServiceFactory factory = new(fetcher, parser,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<LimitlessMetaService>.Instance);

        List<MetaDeck> decks = await factory.Create().GetTopDecksAsync(5);

        await fetcher.Received(1).FetchAsync(Arg.Any<string>());
        parser.Received(1).Parse("<html></html>", 5);
        decks.ShouldHaveSingleItem().Name.ShouldBe("Dragapult ex");
    }
}
