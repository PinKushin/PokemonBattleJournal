using System.Collections.ObjectModel;

namespace PokemonBattleJournal.Services
{
    public class MatchAnalysisService : IMatchAnalysisService
    {
        public double CalculateWinRate(List<MatchEntry> matches, out uint wins, out uint losses, out uint ties)
        {
            wins = (uint) matches.Count(m => m.Result == MatchResult.Win);
            losses = (uint) matches.Count(m => m.Result == MatchResult.Loss);
            ties = (uint) matches.Count(m => m.Result == MatchResult.Tie);

            uint totalMatches = wins + losses + ties;
            return totalMatches > 0 ? (double) wins / totalMatches * 100 : 0;
        }

        public ObservableCollection<ChartDataPoint> GetMostPlayedArchetypes(List<MatchEntry> matches)
        {
            return [.. matches.GroupBy(m => m.Playing?.Name ?? "Unknown")
              .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Count() })
              .OrderByDescending(x => x.Value)];
        }

        public ObservableCollection<TimeDataPoint> CalculateWinRateOverTime(List<MatchEntry> matches)
        {
            return [.. matches.GroupBy(m => m.DatePlayed.Date)
              .Select(g => new TimeDataPoint
              {
                  Date = g.Key,
                  Value = (double) g.Count(m => m.Result == MatchResult.Win) / g.Count() * 100
              })];
        }

        public ObservableCollection<ChartDataPoint> CalculateArchetypeWinRate(List<MatchEntry> matches)
        {
            IOrderedEnumerable<ChartDataPoint> results = matches
                .Where(m => m.Playing != null)
                .GroupBy(m => m.Playing!.Name)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = (double) g.Count(m => m.Result == MatchResult.Win) / g.Count() * 100
                })
                .OrderByDescending(x => x.Value);

            return [.. results];
        }

        public ObservableCollection<TimeDataPoint> CalculateMatchFrequency(List<MatchEntry> matches)
        {
            IOrderedEnumerable<TimeDataPoint> results = matches
                .GroupBy(m => m.DatePlayed.Date)
                .Select(g => new TimeDataPoint
                {
                    Date = g.Key,
                    Value = g.Count()
                })
                .OrderBy(x => x.Date);

            return [.. results];
        }
        public ObservableCollection<ChartDataPoint> CalculateTagUsage(List<MatchEntry> matches)
        {
            IOrderedEnumerable<ChartDataPoint> results = matches
                .SelectMany(m => m.Game1?.Tags ?? [])
                .Concat(matches.SelectMany(m => m.Game2?.Tags ?? []))
                .Concat(matches.SelectMany(m => m.Game3?.Tags ?? []))
                .GroupBy(t => t.Name ?? "Unknown")
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = g.Count()
                })
                .OrderByDescending(x => x.Value);

            return [.. results];
        }

        public ObservableCollection<ChartDataPoint> CalculatePerformanceAgainstOpponents(List<MatchEntry> matches)
        {
            var results = matches
                .Where(m => m.Against != null)
                .GroupBy(m => m.Against!.Name)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = (double) g.Count(m => m.Result == MatchResult.Win) / g.Count() * 100
                })
                .OrderByDescending(x => x.Value);

            return new ObservableCollection<ChartDataPoint>(results);
        }

        public TimeSpan CalculateAverageMatchDuration(List<MatchEntry> matches)
        {
            if (matches.Count == 0)
            {
                return TimeSpan.Zero;
            }

            double totalDuration = matches
                .Select(m => (m.EndTime - m.StartTime).TotalMinutes)
                .Sum();

            return TimeSpan.FromMinutes(totalDuration / matches.Count);
        }
        public ObservableCollection<ChartDataPoint> CalculateWinRateByMatchLength(List<MatchEntry> matches)
        {
            var shortMatches = matches.Where(m => (m.EndTime - m.StartTime).TotalMinutes <= 10);
            var longMatches = matches.Where(m => (m.EndTime - m.StartTime).TotalMinutes > 10);

            return new ObservableCollection<ChartDataPoint>
            {
                new() { Label = "Short Matches", Value = CalculateWinRate(shortMatches.ToList(), out _, out _, out _) },
                new() { Label = "Long Matches", Value = CalculateWinRate(longMatches.ToList(), out _, out _, out _) }
            };
        }


        public ObservableCollection<ChartDataPoint> CalculateFirstTurnAdvantage(List<MatchEntry> matches)
        {
            int firstTurnWins = matches
                .SelectMany(m => new[] { m.Game1, m.Game2, m.Game3 })
                .Where(g => g != null && g.Turn == 1 && g.Result == MatchResult.Win)
                .Count();

            int secondTurnWins = matches
                .SelectMany(m => new[] { m.Game1, m.Game2, m.Game3 })
                .Where(g => g != null && g.Turn == 2 && g.Result == MatchResult.Win)
                .Count();

            int totalFirstTurnGames = matches
                .SelectMany(m => new[] { m.Game1, m.Game2, m.Game3 })
                .Where(g => g != null && g.Turn == 1)
                .Count();

            int totalSecondTurnGames = matches
                .SelectMany(m => new[] { m.Game1, m.Game2, m.Game3 })
                .Where(g => g != null && g.Turn == 2)
                .Count();

            return new ObservableCollection<ChartDataPoint>
            {
                new()
                {
                    Label = "First Turn",
                    Value = totalFirstTurnGames > 0 ? (double)firstTurnWins / totalFirstTurnGames * 100 : 0
                },
                new()
                {
                    Label = "Second Turn",
                    Value = totalSecondTurnGames > 0 ? (double)secondTurnWins / totalSecondTurnGames * 100 : 0
                }
            };
        }
        public (int LongestWinStreak, int LongestLossStreak, int LongestTieStreak) CalculateStreaks(List<MatchEntry> matches)
        {
            int currentWinStreak = 0, longestWinStreak = 0;
            int currentLossStreak = 0, longestLossStreak = 0;
            int currentTieStreak = 0, longestTieStreak = 0;

            foreach (MatchEntry? match in matches.OrderBy(m => m.DatePlayed))
            {
                if (match.Result == MatchResult.Win)
                {
                    currentWinStreak++;
                    longestWinStreak = Math.Max(longestWinStreak, currentWinStreak);
                    currentLossStreak = 0;
                    currentTieStreak = 0;
                }
                else if (match.Result == MatchResult.Loss)
                {
                    currentLossStreak++;
                    longestLossStreak = Math.Max(longestLossStreak, currentLossStreak);
                    currentWinStreak = 0;
                    currentTieStreak = 0;
                }
                else if (match.Result == MatchResult.Tie)
                {
                    currentTieStreak++;
                    longestTieStreak = Math.Max(longestTieStreak, currentTieStreak);
                    currentWinStreak = 0;
                    currentLossStreak = 0;
                }
            }

            return (longestWinStreak, longestLossStreak, longestTieStreak);
        }
    }
}
