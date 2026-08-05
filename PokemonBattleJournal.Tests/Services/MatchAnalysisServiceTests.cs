using System.Collections.ObjectModel;

namespace PokemonBattleJournal.Tests.Services
{
    public class MatchAnalysisServiceTests
    {
        private readonly MatchAnalysisService _service = new();

        [Test]
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

            Assert.That(winRate, Is.EqualTo(62.5)); // (2 + 0.5*1) / 4 * 100
            Assert.That(wins, Is.EqualTo(2u));
            Assert.That(losses, Is.EqualTo(1u));
            Assert.That(ties, Is.EqualTo(1u));
        }

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
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

        [Test]
        public void CalculateTagUsage_TagsInGame2AndGame3_CountsAllGames()
        {
            List<MatchEntry> matches =
            [
                new()
                {
                    Game1 = new Game { Tags = [new Tags { Name = "Lucky" }] },
                    Game2 = new Game { Tags = [new Tags { Name = "Lucky" }, new Tags { Name = "Behind Early" }] },
                    Game3 = new Game { Tags = [new Tags { Name = "Behind Early" }] }
                }
            ];

            ObservableCollection<ChartDataPoint> result = _service.CalculateTagUsage(matches);

            result.Count.ShouldBe(2);
            result[0].Label.ShouldBe("Lucky");
            result[0].Value.ShouldBe(2);
            result[1].Label.ShouldBe("Behind Early");
            result[1].Value.ShouldBe(2);
        }

        [Test]
        public void GetMostPlayedArchetypes_EmptyList_ReturnsEmpty()
        {
            ObservableCollection<ChartDataPoint> result = _service.GetMostPlayedArchetypes([]);

            result.ShouldBeEmpty();
        }

        [Test]
        public void CalculateStreaks_EmptyList_ReturnsZeroes()
        {
            (int winStreak, int lossStreak, int tieStreak) = _service.CalculateStreaks([]);

            winStreak.ShouldBe(0);
            lossStreak.ShouldBe(0);
            tieStreak.ShouldBe(0);
        }

        [Test]
        public void CalculatePerformanceAgainstOpponents_WithOnlyTies_ReturnsFiftyPercent()
        {
            List<MatchEntry> matches =
            [
                new() { Against = new Archetype { Name = "Charizard" }, Result = MatchResult.Tie },
                new() { Against = new Archetype { Name = "Charizard" }, Result = MatchResult.Tie }
            ];

            ObservableCollection<ChartDataPoint> result = _service.CalculatePerformanceAgainstOpponents(matches);

            result.Count.ShouldBe(1);
            result[0].Value.ShouldBe(50); // (0 + 0.5*2) / 2 * 100 = 50%
        }

        [Test]
        public void CalculateMatchupMatrix_BasicMatchups_BuildsCorrectGrid()
        {
            List<MatchEntry> matches =
            [
                new() { Playing = new Archetype { Name = "Charizard" }, Against = new Archetype { Name = "Gardevoir" }, Result = MatchResult.Win },
                new() { Playing = new Archetype { Name = "Charizard" }, Against = new Archetype { Name = "Gardevoir" }, Result = MatchResult.Loss },
                new() { Playing = new Archetype { Name = "Gardevoir" }, Against = new Archetype { Name = "Charizard" }, Result = MatchResult.Win },
            ];

            var (played, opponents, cells) = _service.CalculateMatchupMatrix(matches);

            played.ShouldBe(["Charizard", "Gardevoir"], ignoreOrder: false);
            opponents.ShouldBe(["Charizard", "Gardevoir"], ignoreOrder: false);
            cells.Length.ShouldBe(2);

            var chariVsGarde = cells.Single(c => c.PlayedIdx == Array.IndexOf(played, "Charizard") && c.OpponentIdx == Array.IndexOf(opponents, "Gardevoir"));
            chariVsGarde.WinRate.ShouldBe(50); // 1W 1L

            var gardeVsChar = cells.Single(c => c.PlayedIdx == Array.IndexOf(played, "Gardevoir") && c.OpponentIdx == Array.IndexOf(opponents, "Charizard"));
            gardeVsChar.WinRate.ShouldBe(100);
        }

        [Test]
        public void CalculateMatchupMatrix_EmptyList_ReturnsEmptyArrays()
        {
            var (played, opponents, cells) = _service.CalculateMatchupMatrix([]);

            played.ShouldBeEmpty();
            opponents.ShouldBeEmpty();
            cells.ShouldBeEmpty();
        }

        [Test]
        public void CalculateMatchupMatrix_MatchesWithNullArchetypes_AreExcluded()
        {
            List<MatchEntry> matches =
            [
                new() { Playing = null, Against = new Archetype { Name = "Gardevoir" }, Result = MatchResult.Win },
                new() { Playing = new Archetype { Name = "Charizard" }, Against = null, Result = MatchResult.Win },
                new() { Playing = new Archetype { Name = "Charizard" }, Against = new Archetype { Name = "Gardevoir" }, Result = MatchResult.Win },
            ];

            var (played, opponents, cells) = _service.CalculateMatchupMatrix(matches);

            played.ShouldBe(["Charizard"]);
            opponents.ShouldBe(["Gardevoir"]);
            cells.Length.ShouldBe(1);
        }

        [Test]
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

        [Test]
        public void CalculateAverageMatchDuration_EmptyList_ReturnsZero()
        {
            TimeSpan result = _service.CalculateAverageMatchDuration([]);

            result.ShouldBe(TimeSpan.Zero);
        }

        /// <summary>
        /// Points must come out in chronological order regardless of the input order.
        /// </summary>
        /// <remarks>
        /// Regression for a bug found 2026-08-05. The method groups by date but never ordered by
        /// it, and <c>GroupBy</c> preserves first-occurrence order of the source — so the series
        /// came out in whatever order the matches arrived. <c>GetByTrainerIdAsync</c> issues no
        /// ORDER BY, so that is insertion order, and the win-rate line chart drew segments
        /// jumping backwards in time whenever a match was logged out of sequence.
        ///
        /// This belongs to the analysis service rather than the caller: a chart series is
        /// meaningless unordered, so ordering is part of producing it correctly, not something
        /// each consumer should remember.
        /// </remarks>
        [Test]
        public void CalculateWinRateOverTime_UnorderedInput_ReturnsPointsInChronologicalOrder()
        {
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc) },
                new() { Result = MatchResult.Loss, DatePlayed = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc) },
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc) },
            ];

            ObservableCollection<TimeDataPoint> result = _service.CalculateWinRateOverTime(matches);

            result.Select(p => p.Date).ShouldBe(
            [
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc),
            ]);
        }

        [Test]
        public void CalculateWinRateOverTime_EmptyList_ReturnsEmpty()
        {
            ObservableCollection<TimeDataPoint> result = _service.CalculateWinRateOverTime([]);

            result.ShouldBeEmpty();
        }

        [Test]
        public void CalculateWinRateOverTime_SingleDate_ReturnsSinglePoint()
        {
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win, DatePlayed = new DateTime(2026, 3, 1) }
            ];

            ObservableCollection<TimeDataPoint> result = _service.CalculateWinRateOverTime(matches);

            result.Count.ShouldBe(1);
            result[0].Date.ShouldBe(new DateTime(2026, 3, 1));
            result[0].Value.ShouldBe(100);
        }

        [Test]
        public void CalculateMatchFrequency_EmptyList_ReturnsEmpty()
        {
            ObservableCollection<TimeDataPoint> result = _service.CalculateMatchFrequency([]);

            result.ShouldBeEmpty();
        }

        [Test]
        public void CalculateMatchFrequency_MultipleDates_GroupsAndOrdersByDate()
        {
            List<MatchEntry> matches =
            [
                new() { DatePlayed = new DateTime(2026, 3, 2) },
                new() { DatePlayed = new DateTime(2026, 3, 1) },
                new() { DatePlayed = new DateTime(2026, 3, 1) },
                new() { DatePlayed = new DateTime(2026, 3, 2) },
                new() { DatePlayed = new DateTime(2026, 3, 2) }
            ];

            ObservableCollection<TimeDataPoint> result = _service.CalculateMatchFrequency(matches);

            result.Count.ShouldBe(2);
            result[0].Date.ShouldBe(new DateTime(2026, 3, 1));
            result[0].Value.ShouldBe(2);
            result[1].Date.ShouldBe(new DateTime(2026, 3, 2));
            result[1].Value.ShouldBe(3);
        }

        [Test]
        public void CalculateMatchFrequency_SingleDate_ReturnsSinglePoint()
        {
            List<MatchEntry> matches =
            [
                new() { DatePlayed = new DateTime(2026, 5, 10) },
                new() { DatePlayed = new DateTime(2026, 5, 10) }
            ];

            ObservableCollection<TimeDataPoint> result = _service.CalculateMatchFrequency(matches);

            result.Count.ShouldBe(1);
            result[0].Value.ShouldBe(2);
        }

        [Test]
        public void CalculateAverageMatchDuration_WithMatches_ReturnsAverage()
        {
            var baseDate = new DateTime(2026, 1, 1);
            List<MatchEntry> matches =
            [
                new() { StartTime = baseDate.AddHours(10), EndTime = baseDate.AddHours(10).AddMinutes(20) },
                new() { StartTime = baseDate.AddHours(11), EndTime = baseDate.AddHours(11).AddMinutes(40) }
            ];

            TimeSpan result = _service.CalculateAverageMatchDuration(matches);

            result.ShouldBe(TimeSpan.FromMinutes(30));
        }

        [Test]
        public void CalculateAverageMatchDuration_SingleMatch_ReturnsDuration()
        {
            var baseDate = new DateTime(2026, 1, 1);
            List<MatchEntry> matches =
            [
                new() { StartTime = baseDate.AddHours(9), EndTime = baseDate.AddHours(9).AddMinutes(15) }
            ];

            TimeSpan result = _service.CalculateAverageMatchDuration(matches);

            result.ShouldBe(TimeSpan.FromMinutes(15));
        }
    }
}
