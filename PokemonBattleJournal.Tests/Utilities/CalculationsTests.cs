namespace PokemonBattleJournal.Tests.Utilities
{
    public class CalculationsTests
    {
        [Test]
        public void CalculateWinRate_EmptyList_ReturnsZero()
        {
            // Arrange
            List<MatchEntry> matches = [];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert
            result.ShouldBe(0);
            wins.ShouldBe(0u);
            losses.ShouldBe(0u);
            ties.ShouldBe(0u);
        }

        [Test]
        public void CalculateWinRate_AllWins_Returns100()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Win }
            ];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert
            result.ShouldBe(100);
            wins.ShouldBe(3u);
            losses.ShouldBe(0u);
            ties.ShouldBe(0u);
        }

        [Test]
        public void CalculateWinRate_AllTies_Returns50()
        {
            // Arrange
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Tie },
                new() { Result = MatchResult.Tie }
            ];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert — ties count as 0.5 each: (0 + 0.5*2) / 2 * 100 = 50
            result.ShouldBe(50);
            wins.ShouldBe(0u);
            losses.ShouldBe(0u);
            ties.ShouldBe(2u);
        }

        [Test]
        public void CalculateWinRate_MixedResults_ReturnsCorrectRate()
        {
            // Arrange — 2 wins, 1 loss, 1 tie = (2 + 0.5) / 4 * 100 = 62.5
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Loss },
                new() { Result = MatchResult.Tie }
            ];

            // Act
            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            // Assert
            result.ShouldBe(62.5);
            wins.ShouldBe(2u);
            losses.ShouldBe(1u);
            ties.ShouldBe(1u);
        }

        [Test]
        public void CalculateWinRate_AllLosses_ReturnsZero()
        {
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Loss },
                new() { Result = MatchResult.Loss }
            ];

            double result = Calculations.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);

            result.ShouldBe(0);
            wins.ShouldBe(0u);
            losses.ShouldBe(2u);
            ties.ShouldBe(0u);
        }

        [Test]
        public void CalculateWinRate_SingleWin_Returns100()
        {
            List<MatchEntry> matches = [new() { Result = MatchResult.Win }];

            double result = Calculations.CalculateWinRate(matches, out uint wins, out _, out _);

            result.ShouldBe(100);
            wins.ShouldBe(1u);
        }

        [Test]
        public void CalculateWinRate_SingleTie_Returns50()
        {
            List<MatchEntry> matches = [new() { Result = MatchResult.Tie }];

            double result = Calculations.CalculateWinRate(matches, out _, out _, out uint ties);

            result.ShouldBe(50);
            ties.ShouldBe(1u);
        }
    }
}
