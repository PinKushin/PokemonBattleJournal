using System.Collections.ObjectModel;

namespace PokemonBattleJournal.Tests.Services
{
    public class MatchAnalysisServiceTests
    {
        private readonly MatchAnalysisService _service = new();

        [Fact]
        public void CalculateWinRate_ShouldReturnCorrectValues()
        {
            List<MatchEntry> matches =
            [
            new() { Result = MatchResult.Win },
            new() { Result = MatchResult.Loss },
            new() { Result = MatchResult.Tie },
            new() { Result = MatchResult.Win }
        ];

            double winRate = _service.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            Assert.Equal(50, winRate);
            Assert.Equal(2u, wins);
            Assert.Equal(1u, losses);
            Assert.Equal(1u, ties);
        }

        [Fact]
        public void GetMostPlayedArchetypes_ShouldReturnCorrectCounts()
        {
            List<MatchEntry> matches =
            [
            new() { Playing = new Archetype { Name = "Fire" } },
            new() { Playing = new Archetype { Name = "Water" } },
            new() { Playing = new Archetype { Name = "Fire" } }
        ];

            ObservableCollection<ChartDataPoint> result = _service.GetMostPlayedArchetypes(matches);

            result.ShouldBeInOrder();
            result[0].Label.ShouldBe("Fire");
            result[0].Value.ShouldBe(2);
            result[1].Label.ShouldBe("Water");
            result[1].Value.ShouldBe(1);
        }
    }
}
