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

            result.Count.ShouldBe(2);
            result[0].Label.ShouldBe("Fire");
            result[0].Value.ShouldBe(2);
            result[1].Label.ShouldBe("Water");
            result[1].Value.ShouldBe(1);
        }

        [Fact]
        public void CalculateWinRate_EmptyList_ShouldReturnZero()
        {
            // Arrange
            List<MatchEntry> matches = [];

            // Act
            double winRate = _service.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert
            winRate.ShouldBe(0);
            wins.ShouldBe(0u);
            losses.ShouldBe(0u);
            ties.ShouldBe(0u);
        }

        [Fact]
        public void CalculateWinRateOverTime_ShouldGroupByDateAndCalculateCorrectly()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 1, 1) },
                new() { Result = MatchResult.Loss, DatePlayed = new DateTime(2026, 1, 1) },
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 1, 2) }
            ];

            // Act
            ObservableCollection<TimeDataPoint> result = _service.CalculateWinRateOverTime(matches);

            // Assert
            result.Count.ShouldBe(2);
            result[0].Date.ShouldBe(new DateTime(2026, 1, 1));
            result[0].Value.ShouldBe(50); // 1 win out of 2
            result[1].Date.ShouldBe(new DateTime(2026, 1, 2));
            result[1].Value.ShouldBe(100); // 1 win out of 1
        }

        [Fact]
        public void CalculateArchetypeWinRate_ShouldCalculatePerArchetype()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Playing = new Archetype { Name = "Fire" }, Result = MatchResult.Win },
                new() { Playing = new Archetype { Name = "Fire" }, Result = MatchResult.Loss },
                new() { Playing = new Archetype { Name = "Water" }, Result = MatchResult.Win }
            ];

            // Act
            ObservableCollection<ChartDataPoint> result = _service.CalculateArchetypeWinRate(matches);

            // Assert
            result.Count.ShouldBe(2);
            // Water has 100% win rate, Fire has 50%
            result[0].Label.ShouldBe("Water");
            result[0].Value.ShouldBe(100);
            result[1].Label.ShouldBe("Fire");
            result[1].Value.ShouldBe(50);
        }

        [Fact]
        public void CalculateTagUsage_ShouldCountAcrossAllGames()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new()
                {
                    Game1 = new Game
                    {
                        Tags = [new Tags { Name = "Lucky" }, new Tags { Name = "Behind Early" }]
                    }
                },
                new()
                {
                    Game1 = new Game
                    {
                        Tags = [new Tags { Name = "Lucky" }]
                    }
                }
            ];

            // Act
            ObservableCollection<ChartDataPoint> result = _service.CalculateTagUsage(matches);

            // Assert
            result.Count.ShouldBe(2);
            result[0].Label.ShouldBe("Lucky");
            result[0].Value.ShouldBe(2);
            result[1].Label.ShouldBe("Behind Early");
            result[1].Value.ShouldBe(1);
        }

        [Fact]
        public void CalculatePerformanceAgainstOpponents_ShouldCalculatePerOpponent()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Against = new Archetype { Name = "Charizard" }, Result = MatchResult.Win },
                new() { Against = new Archetype { Name = "Charizard" }, Result = MatchResult.Win },
                new() { Against = new Archetype { Name = "Charizard" }, Result = MatchResult.Loss },
                new() { Against = new Archetype { Name = "Gardevoir" }, Result = MatchResult.Loss }
            ];

            // Act
            ObservableCollection<ChartDataPoint> result = _service.CalculatePerformanceAgainstOpponents(matches);

            // Assert
            result.Count.ShouldBe(2);
            result[0].Label.ShouldBe("Charizard");
            result[0].Value.ShouldBe(66.66666666666667, 0.001);
            result[1].Label.ShouldBe("Gardevoir");
            result[1].Value.ShouldBe(0);
        }

        [Fact]
        public void CalculateAverageMatchDuration_ShouldReturnCorrectAverage()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new()
                {
                    StartTime = new DateTime(2026, 1, 1, 10, 0, 0),
                    EndTime = new DateTime(2026, 1, 1, 10, 30, 0) // 30 min
                },
                new()
                {
                    StartTime = new DateTime(2026, 1, 1, 11, 0, 0),
                    EndTime = new DateTime(2026, 1, 1, 11, 10, 0) // 10 min
                }
            ];

            // Act
            TimeSpan result = _service.CalculateAverageMatchDuration(matches);

            // Assert
            result.ShouldBe(TimeSpan.FromMinutes(20)); // (30 + 10) / 2 = 20
        }

        [Fact]
        public void CalculateWinRateByMatchLength_ShouldSplitShortAndLong()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new()
                {
                    Result = MatchResult.Win,
                    StartTime = new DateTime(2026, 1, 1, 10, 0, 0),
                    EndTime = new DateTime(2026, 1, 1, 10, 5, 0) // 5 min (short)
                },
                new()
                {
                    Result = MatchResult.Win,
                    StartTime = new DateTime(2026, 1, 1, 11, 0, 0),
                    EndTime = new DateTime(2026, 1, 1, 11, 3, 0) // 3 min (short)
                },
                new()
                {
                    Result = MatchResult.Loss,
                    StartTime = new DateTime(2026, 1, 1, 12, 0, 0),
                    EndTime = new DateTime(2026, 1, 1, 12, 15, 0) // 15 min (long)
                }
            ];

            // Act
            ObservableCollection<ChartDataPoint> result = _service.CalculateWinRateByMatchLength(matches);

            // Assert
            result.Count.ShouldBe(2);
            result[0].Label.ShouldBe("Short Matches");
            result[0].Value.ShouldBe(100); // 2 wins out of 2 short matches
            result[1].Label.ShouldBe("Long Matches");
            result[1].Value.ShouldBe(0); // 0 wins out of 1 long match
        }

        [Fact]
        public void CalculateFirstTurnAdvantage_ShouldCalculatePerTurnOrder()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new()
                {
                    Game1 = new Game { Turn = 1, Result = MatchResult.Win },
                    Game2 = new Game { Turn = 2, Result = MatchResult.Win }
                },
                new()
                {
                    Game1 = new Game { Turn = 1, Result = MatchResult.Loss },
                    Game2 = new Game { Turn = 2, Result = MatchResult.Loss }
                }
            ];

            // Act
            ObservableCollection<ChartDataPoint> result = _service.CalculateFirstTurnAdvantage(matches);

            // Assert
            result.Count.ShouldBe(2);
            result[0].Label.ShouldBe("First Turn");
            result[0].Value.ShouldBe(50); // 1 win out of 2 first-turn games
            result[1].Label.ShouldBe("Second Turn");
            result[1].Value.ShouldBe(50); // 1 win out of 2 second-turn games
        }

        [Fact]
        public void CalculateStreaks_ShouldReturnLongestStreaks()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 1, 1) },
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 1, 2) },
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 1, 3) },
                new() { Result = MatchResult.Loss, DatePlayed = new DateTime(2026, 1, 4) },
                new() { Result = MatchResult.Loss, DatePlayed = new DateTime(2026, 1, 5) },
                new() { Result = MatchResult.Tie, DatePlayed = new DateTime(2026, 1, 6) }
            ];

            // Act
            (int winStreak, int lossStreak, int tieStreak) = _service.CalculateStreaks(matches);

            // Assert
            winStreak.ShouldBe(3);
            lossStreak.ShouldBe(2);
            tieStreak.ShouldBe(1);
        }
    }
}
