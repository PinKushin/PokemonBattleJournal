using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Services;

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
}
