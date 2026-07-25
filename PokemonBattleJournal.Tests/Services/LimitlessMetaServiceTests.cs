using Microsoft.Extensions.Logging.Abstractions;
using PokemonBattleJournal.Scraper.Interfaces;
using PokemonBattleJournal.Scraper.Models;
using PokemonBattleJournal.Scraper.Services;

namespace PokemonBattleJournal.Tests.Services;

public class LimitlessMetaServiceTests
{
    private static LimitlessMetaService BuildService(IMetaDeckFetcher fetcher, IMetaDeckParser parser) =>
        new(fetcher, parser, NullLogger<LimitlessMetaService>.Instance);

    [Fact]
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

    [Fact]
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
}
