
using System.Collections.ObjectModel;

namespace PokemonBattleJournal.Interfaces
{
    public interface IMatchAnalysisService
    {
        ObservableCollection<ChartDataPoint> CalculateArchetypeWinRate(List<MatchEntry> matches);
        ObservableCollection<TimeDataPoint> CalculateMatchFrequency(List<MatchEntry> matches);
        ObservableCollection<ChartDataPoint> CalculateTagUsage(List<MatchEntry> matches);
        double CalculateWinRate(List<MatchEntry> matches, out uint wins, out uint losses, out uint ties);
        ObservableCollection<TimeDataPoint> CalculateWinRateOverTime(List<MatchEntry> matches);
        ObservableCollection<ChartDataPoint> GetMostPlayedArchetypes(List<MatchEntry> matches);
        ObservableCollection<ChartDataPoint> CalculatePerformanceAgainstOpponents(List<MatchEntry> matches);
        TimeSpan CalculateAverageMatchDuration(List<MatchEntry> matches);
        ObservableCollection<ChartDataPoint> CalculateWinRateByMatchLength(List<MatchEntry> matches);
        ObservableCollection<ChartDataPoint> CalculateFirstTurnAdvantage(List<MatchEntry> matches);
        (int LongestWinStreak, int LongestLossStreak, int LongestTieStreak) CalculateStreaks(List<MatchEntry> matches);
    }
}
