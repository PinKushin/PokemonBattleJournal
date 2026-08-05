using static PokemonBattleJournal.Models.MatchResult;

namespace PokemonBattleJournal.IntegrationTests.Services;

public class MatchAnalysisTests
{
    private readonly MatchAnalysisService _analysis = new();

    private static MatchEntry Match(DateTime date, MatchResult result, Archetype? playing = null, Archetype? against = null, Game[]? games = null) => new()
    {
        DatePlayed = date.Date,
        StartTime = date,
        EndTime = date.AddMinutes(15),
        Result = result,
        Playing = playing,
        Against = against,
        Game1 = games?.Length > 0 ? games[0] : null,
        Game2 = games?.Length > 1 ? games[1] : null,
        Game3 = games?.Length > 2 ? games[2] : null,
    };

    private static Archetype Arch(string name) => new() { Id = (uint)(name.Length * 100), Name = name };

    [Test]
    public void CalculateWinRate_ThreeWinsNoLosses_Returns100()
    {
        List<MatchEntry> matches = [
            Match(DateTime.Today, Win),
            Match(DateTime.Today.AddDays(1), Win),
            Match(DateTime.Today.AddDays(2), Win),
        ];

        double rate = _analysis.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);
        rate.ShouldBe(100);
        ((int)wins).ShouldBe(3);
        ((int)losses).ShouldBe(0);
        ((int)ties).ShouldBe(0);
    }

    [Test]
    public void CalculateWinRate_OneWinOneLossOneTie_Returns50()
    {
        List<MatchEntry> matches = [
            Match(DateTime.Today, Win),
            Match(DateTime.Today.AddDays(1), Loss),
            Match(DateTime.Today.AddDays(2), Tie),
        ];

        double rate = _analysis.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);
        rate.ShouldBe(50, tolerance: 0.01);
        ((int)wins).ShouldBe(1);
        ((int)losses).ShouldBe(1);
        ((int)ties).ShouldBe(1);
    }

    [Test]
    public void CalculateWinRate_EmptyList_ReturnsZero()
    {
        double rate = _analysis.CalculateWinRate([], out uint wins, out uint losses, out uint ties);
        rate.ShouldBe(0);
        ((int)wins).ShouldBe(0);
        ((int)losses).ShouldBe(0);
        ((int)ties).ShouldBe(0);
    }

    [Test]
    public void CalculateStreaks_WinLossWinWinWin_ReturnsLongestWin3()
    {
        List<MatchEntry> matches = [
            Match(DateTime.Today, Win),
            Match(DateTime.Today.AddDays(1), Loss),
            Match(DateTime.Today.AddDays(2), Win),
            Match(DateTime.Today.AddDays(3), Win),
            Match(DateTime.Today.AddDays(4), Win),
        ];

        var (longestWin, longestLoss, longestTie) = _analysis.CalculateStreaks(matches);
        ((int)longestWin).ShouldBe(3);
        ((int)longestLoss).ShouldBe(1);
        ((int)longestTie).ShouldBe(0);
    }

    [Test]
    public void CalculateStreaks_EmptyList_ReturnsZeros()
    {
        var (longestWin, longestLoss, longestTie) = _analysis.CalculateStreaks([]);
        ((int)longestWin).ShouldBe(0);
        ((int)longestLoss).ShouldBe(0);
        ((int)longestTie).ShouldBe(0);
    }

    [Test]
    public void CalculateStreaks_TwoLossStreak_ReturnsLongestLoss2()
    {
        List<MatchEntry> matches = [
            Match(DateTime.Today, Loss),
            Match(DateTime.Today.AddDays(1), Loss),
        ];

        var (longestWin, longestLoss, longestTie) = _analysis.CalculateStreaks(matches);
        longestLoss.ShouldBe(2);
    }

    [Test]
    public void GetMostPlayedArchetypes_ThreeArchetypesOrderedByCount()
    {
        Archetype charizard = Arch("Charizard");
        Archetype pikachu = Arch("Pikachu");
        Archetype blastoise = Arch("Blastoise");

        List<MatchEntry> matches = [
            Match(DateTime.Today, Win, charizard),
            Match(DateTime.Today.AddDays(1), Loss, pikachu),
            Match(DateTime.Today.AddDays(2), Win, charizard),
            Match(DateTime.Today.AddDays(3), Tie, blastoise),
            Match(DateTime.Today.AddDays(4), Win, charizard),
        ];

        var result = _analysis.GetMostPlayedArchetypes(matches);
        result.ShouldNotBeEmpty();
        result[0].Label.ShouldBe("Charizard");
        result[0].Value.ShouldBe(3);
        result[1].Label.ShouldBe("Pikachu");
        result[1].Value.ShouldBe(1);
    }

    [Test]
    public void CalculateMatchupMatrix_TwoArchetypes_ReturnsCorrectCells()
    {
        Archetype charizard = Arch("Charizard");
        Archetype blastoise = Arch("Blastoise");

        List<MatchEntry> matches = [
            Match(DateTime.Today, Win, charizard, blastoise),
            Match(DateTime.Today.AddDays(1), Loss, charizard, blastoise),
            Match(DateTime.Today.AddDays(2), Win, blastoise, charizard),
        ];

        var (played, opponents, cells) = _analysis.CalculateMatchupMatrix(matches);

        played.ShouldContain("Blastoise");
        played.ShouldContain("Charizard");
        opponents.ShouldContain("Blastoise");
        opponents.ShouldContain("Charizard");
        cells.Length.ShouldBe(2);

        // Charizard vs Blastoise: 1 win, 1 loss → 50% win rate
        var charizardVsBlastoise = cells.First(c => played[c.PlayedIdx] == "Charizard" && opponents[c.OpponentIdx] == "Blastoise");
        charizardVsBlastoise.WinRate.ShouldBe(50);
    }

    [Test]
    public void CalculateAverageMatchDuration_ThreeMatches_ReturnsAverage()
    {
        List<MatchEntry> matches = [
            new() { StartTime = DateTime.Today, EndTime = DateTime.Today.AddMinutes(30) },
            new() { StartTime = DateTime.Today, EndTime = DateTime.Today.AddMinutes(20) },
            new() { StartTime = DateTime.Today, EndTime = DateTime.Today.AddMinutes(10) },
        ];

        TimeSpan avg = _analysis.CalculateAverageMatchDuration(matches);
        avg.TotalMinutes.ShouldBe(20);
    }

    [Test]
    public void CalculateAverageMatchDuration_EmptyList_ReturnsZero()
    {
        TimeSpan avg = _analysis.CalculateAverageMatchDuration([]);
        avg.ShouldBe(TimeSpan.Zero);
    }

    [Test]
    public void CalculateWinRateByMatchLength_ShortAndLongMatches_ReturnsTwoBars()
    {
        Archetype arch = Arch("TestDeck");
        List<MatchEntry> matches = [
            new() { StartTime = DateTime.Today, EndTime = DateTime.Today.AddMinutes(5), Playing = arch },
            new() { StartTime = DateTime.Today.AddDays(1), EndTime = DateTime.Today.AddDays(1).AddMinutes(20), Playing = arch },
        ];

        var result = _analysis.CalculateWinRateByMatchLength(matches);
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Label == "Short Matches");
        result.ShouldContain(c => c.Label == "Long Matches");
    }

    [Test]
    public void CalculateFirstTurnAdvantage_OneTurn1WinOneTurn2Loss_ReturnsCorrectRates()
    {
        Archetype arch = Arch("TestDeck");

        Game game1 = new() { Result = Win, Turn = 1 };
        Game game2 = new() { Result = Loss, Turn = 2 };

        List<MatchEntry> matches = [
            new()
            {
                DatePlayed = DateTime.Today,
                Playing = arch,
                Game1 = game1,
                Game2 = game2,
            },
        ];

        var result = _analysis.CalculateFirstTurnAdvantage(matches);
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Label == "First Turn" && c.Value == 100);
        result.ShouldContain(c => c.Label == "Second Turn" && c.Value == 0);
    }

    [Test]
    public void CalculateFirstTurnAdvantage_EmptyMatches_ReturnsZeros()
    {
        var result = _analysis.CalculateFirstTurnAdvantage([]);
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Label == "First Turn" && c.Value == 0);
        result.ShouldContain(c => c.Label == "Second Turn" && c.Value == 0);
    }

    [Test]
    public void CalculateTagUsage_MatchWithTags_ReturnsTagCounts()
    {
        Tags tag1 = new() { Id = 1, Name = "Aggro" };
        Tags tag2 = new() { Id = 2, Name = "Control" };

        List<MatchEntry> matches = [
            new()
            {
                Game1 = new Game { Result = Win, Tags = [tag1] },
                Game2 = new Game { Result = Loss, Tags = [tag1, tag2] },
            },
        ];

        var result = _analysis.CalculateTagUsage(matches);
        result.ShouldNotBeEmpty();
        result.ShouldContain(c => c.Label == "Aggro" && c.Value == 2);
        result.ShouldContain(c => c.Label == "Control" && c.Value == 1);
    }

    [Test]
    public void CalculateTagUsage_EmptyMatches_ReturnsEmpty()
    {
        var result = _analysis.CalculateTagUsage([]);
        result.ShouldBeEmpty();
    }

    [Test]
    public void CalculatePerformanceAgainstOpponents_TwoOpponents_ReturnsWinRates()
    {
        Archetype myDeck = Arch("MyDeck");
        Archetype opp1 = Arch("Opponent1");
        Archetype opp2 = Arch("Opponent2");

        List<MatchEntry> matches = [
            new() { DatePlayed = DateTime.Today, Playing = myDeck, Against = opp1, Result = Win },
            new() { DatePlayed = DateTime.Today.AddDays(1), Playing = myDeck, Against = opp1, Result = Loss },
            new() { DatePlayed = DateTime.Today.AddDays(2), Playing = myDeck, Against = opp2, Result = Win },
        ];

        var result = _analysis.CalculatePerformanceAgainstOpponents(matches);
        result.ShouldNotBeEmpty();
        // Opponent1: 1 win, 1 loss → 50% win rate
        result.ShouldContain(c => c.Label == "Opponent1" && c.Value == 50);
        // Opponent2: 1 win, 0 loss → 100% win rate
        result.ShouldContain(c => c.Label == "Opponent2" && c.Value == 100);
    }

    [Test]
    public void CalculateMatchFrequency_GroupedByDate_ReturnsCounts()
    {
        List<MatchEntry> matches = [
            new() { DatePlayed = DateTime.Today, Result = Win },
            new() { DatePlayed = DateTime.Today, Result = Loss },
            new() { DatePlayed = DateTime.Today.AddDays(1), Result = Win },
        ];

        var result = _analysis.CalculateMatchFrequency(matches);
        result.Count.ShouldBe(2);
        result.ShouldContain(t => t.Date == DateTime.Today.Date && t.Value == 2);
        result.ShouldContain(t => t.Date == DateTime.Today.AddDays(1).Date && t.Value == 1);
    }

    [Test]
    public void CalculateWinRateOverTime_GroupedByDate_ReturnsWinRatePerDay()
    {
        List<MatchEntry> matches = [
            new() { DatePlayed = DateTime.Today, Result = Win },
            new() { DatePlayed = DateTime.Today, Result = Loss },
            new() { DatePlayed = DateTime.Today.AddDays(1), Result = Win },
            new() { DatePlayed = DateTime.Today.AddDays(1), Result = Win },
        ];

        var result = _analysis.CalculateWinRateOverTime(matches);
        result.Count.ShouldBe(2);
        result.ShouldContain(t => t.Date == DateTime.Today.Date && t.Value == 50);
        result.ShouldContain(t => t.Date == DateTime.Today.AddDays(1).Date && t.Value == 100);
    }

    [Test]
    public void CalculateArchetypeWinRate_TwoArchetypes_ReturnsWinRatesOrderedDesc()
    {
        Archetype charizard = Arch("Charizard");
        Archetype pikachu = Arch("Pikachu");

        List<MatchEntry> matches = [
            Match(DateTime.Today, Win, charizard),
            Match(DateTime.Today.AddDays(1), Win, charizard),
            Match(DateTime.Today.AddDays(2), Loss, pikachu),
        ];

        var result = _analysis.CalculateArchetypeWinRate(matches);
        result.ShouldNotBeEmpty();
        result.ShouldContain(c => c.Label == "Charizard" && c.Value == 100);
        result.ShouldContain(c => c.Label == "Pikachu" && c.Value == 0);
        result[0].Value.ShouldBeGreaterThanOrEqualTo(result[1].Value);
    }

    [Test]
    public void CalculateArchetypeWinRate_EmptyList_ReturnsEmpty()
    {
        var result = _analysis.CalculateArchetypeWinRate([]);
        result.ShouldBeEmpty();
    }

    [Test]
    public void CalculateArchetypeWinRate_NullPlayingArchetype_ExcludesEntry()
    {
        List<MatchEntry> matches = [
            Match(DateTime.Today, Win),
            Match(DateTime.Today.AddDays(1), Win, Arch("Bulbasaur")),
        ];

        var result = _analysis.CalculateArchetypeWinRate(matches);
        result.Count.ShouldBe(1);
        result[0].Label.ShouldBe("Bulbasaur");
    }

    [Test]
    public void CalculateWinRate_MatchWithNullResult_NotCounted()
    {
        // null Result hits 'case null: break;' — excluded from totals entirely
        List<MatchEntry> matches = [
            Match(DateTime.Today, Win),
            new() { DatePlayed = DateTime.Today.AddDays(1), Result = null },
        ];

        double rate = _analysis.CalculateWinRate(matches, out uint wins, out uint losses, out uint ties);
        ((int)wins).ShouldBe(1);
        ((int)losses).ShouldBe(0);
        ((int)ties).ShouldBe(0);
        rate.ShouldBe(100, tolerance: 0.01);
    }

    [Test]
    public void GetMostPlayedArchetypes_EmptyList_ReturnsEmpty()
    {
        var result = _analysis.GetMostPlayedArchetypes([]);
        result.ShouldBeEmpty();
    }

    [Test]
    public void GetMostPlayedArchetypes_NullPlaying_GroupedAsUnknown()
    {
        List<MatchEntry> matches = [
            Match(DateTime.Today, Win),
            Match(DateTime.Today.AddDays(1), Loss),
        ];

        var result = _analysis.GetMostPlayedArchetypes(matches);
        result.ShouldContain(c => c.Label == "Unknown" && c.Value == 2);
    }

    [Test]
    public void CalculateStreaks_AllTies_ReturnsLongestTie()
    {
        List<MatchEntry> matches = [
            Match(DateTime.Today, Tie),
            Match(DateTime.Today.AddDays(1), Tie),
            Match(DateTime.Today.AddDays(2), Tie),
        ];

        var (longestWin, longestLoss, longestTie) = _analysis.CalculateStreaks(matches);
        ((int)longestTie).ShouldBe(3);
        ((int)longestWin).ShouldBe(0);
        ((int)longestLoss).ShouldBe(0);
    }

    [Test]
    public void CalculatePerformanceAgainstOpponents_EmptyList_ReturnsEmpty()
    {
        var result = _analysis.CalculatePerformanceAgainstOpponents([]);
        result.ShouldBeEmpty();
    }

    [Test]
    public void CalculatePerformanceAgainstOpponents_NullAgainst_Excluded()
    {
        List<MatchEntry> matches = [
            new() { DatePlayed = DateTime.Today, Playing = Arch("Deck"), Against = null, Result = Win },
        ];

        var result = _analysis.CalculatePerformanceAgainstOpponents(matches);
        result.ShouldBeEmpty();
    }

    [Test]
    public void CalculateMatchFrequency_EmptyList_ReturnsEmpty()
    {
        var result = _analysis.CalculateMatchFrequency([]);
        result.ShouldBeEmpty();
    }

    [Test]
    public void CalculateWinRateOverTime_EmptyList_ReturnsEmpty()
    {
        var result = _analysis.CalculateWinRateOverTime([]);
        result.ShouldBeEmpty();
    }

    [Test]
    public void CalculateMatchupMatrix_EmptyList_ReturnsEmpty()
    {
        var (played, opponents, cells) = _analysis.CalculateMatchupMatrix([]);
        played.ShouldBeEmpty();
        opponents.ShouldBeEmpty();
        cells.ShouldBeEmpty();
    }

    [Test]
    public void CalculateWinRateByMatchLength_EmptyList_ReturnsBothZero()
    {
        var result = _analysis.CalculateWinRateByMatchLength([]);
        result.Count.ShouldBe(2);
        result.ShouldContain(c => c.Label == "Short Matches" && c.Value == 0);
        result.ShouldContain(c => c.Label == "Long Matches" && c.Value == 0);
    }

    [Test]
    public void CalculateTagUsage_NoTags_ReturnsEmpty()
    {
        List<MatchEntry> matches = [
            new() { Game1 = new Game { Result = Win, Tags = [] } },
        ];

        var result = _analysis.CalculateTagUsage(matches);
        result.ShouldBeEmpty();
    }

    [Test]
    public void CalculateFirstTurnAdvantage_TieGame_CountsPartialInWinRate()
    {
        // Tie on turn 1: (0 wins + 0.5*1 tie) / 1 game * 100 = 50
        Game game1 = new() { Result = Tie, Turn = 1 };

        List<MatchEntry> matches = [
            new() { DatePlayed = DateTime.Today, Game1 = game1 },
        ];

        var result = _analysis.CalculateFirstTurnAdvantage(matches);
        result.ShouldContain(c => c.Label == "First Turn" && c.Value == 50);
        result.ShouldContain(c => c.Label == "Second Turn" && c.Value == 0);
    }
}
