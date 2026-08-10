
namespace PokemonBattleJournal.Services
{
    public class MatchAnalysisService : IMatchAnalysisService
    {
        public double CalculateWinRate(List<MatchEntry> matches, out uint wins, out uint losses, out uint ties)
        {
            return Calculations.CalculateWinRate(matches, out wins, out losses, out ties);
        }

        public ObservableCollection<ChartDataPoint> GetMostPlayedArchetypes(List<MatchEntry> matches)
        {
            return [.. matches.GroupBy(m => m.Playing?.Name ?? "Unknown")
              .Select(g => new ChartDataPoint { Label = g.Key, Value = g.Count() })
              .OrderByDescending(x => x.Value)];
        }

        public ObservableCollection<TimeDataPoint> CalculateWinRateOverTime(List<MatchEntry> matches)
        {
            // OrderBy is load-bearing, not cosmetic. GroupBy preserves first-occurrence order of
            // the source, and GetByTrainerIdAsync issues no ORDER BY — so without this the line
            // chart drew segments jumping backwards in time as soon as a match was logged out of
            // sequence.
            return [.. matches.GroupBy(m => m.DatePlayed.Date)
              .OrderBy(g => g.Key)
              .Select(g => new TimeDataPoint
              {
                  Date = g.Key,
                  Value = Calculations.CalculateWinRate(g.ToList(), out _, out _, out _)
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
                    Value = Calculations.CalculateWinRate(g.ToList(), out _, out _, out _)
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
            IOrderedEnumerable<ChartDataPoint> results = matches
                .Where(m => m.Against != null)
                .GroupBy(m => m.Against!.Name)
                .Select(g => new ChartDataPoint
                {
                    Label = g.Key,
                    Value = Calculations.CalculateWinRate(g.ToList(), out _, out _, out _)
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
            IEnumerable<MatchEntry> shortMatches = matches.Where(m => (m.EndTime - m.StartTime).TotalMinutes <= 10);
            IEnumerable<MatchEntry> longMatches = matches.Where(m => (m.EndTime - m.StartTime).TotalMinutes > 10);

            return new ObservableCollection<ChartDataPoint>
            {
                new() { Label = "Short Matches", Value = CalculateWinRate(shortMatches.ToList(), out _, out _, out _) },
                new() { Label = "Long Matches", Value = CalculateWinRate(longMatches.ToList(), out _, out _, out _) }
            };
        }


        public ObservableCollection<ChartDataPoint> CalculateFirstTurnAdvantage(List<MatchEntry> matches)
        {
            IEnumerable<Game> allGames = matches.SelectMany(m => new[] { m.Game1, m.Game2, m.Game3 }).OfType<Game>();

            List<Game> firstTurnGames = allGames.Where(g => g.Turn == 1).ToList();
            List<Game> secondTurnGames = allGames.Where(g => g.Turn == 2).ToList();

            static double FirstTurnRate(List<Game> games) => games.Count > 0
                ? (games.Count(g => g.Result == MatchResult.Win) + 0.5 * games.Count(g => g.Result == MatchResult.Tie)) / games.Count * 100
                : 0;

            return new ObservableCollection<ChartDataPoint>
            {
                new() { Label = "First Turn", Value = FirstTurnRate(firstTurnGames) },
                new() { Label = "Second Turn", Value = FirstTurnRate(secondTurnGames) }
            };
        }
        public (string[] PlayedArchetypes, string[] OpponentArchetypes, (int PlayedIdx, int OpponentIdx, double WinRate)[] Cells) CalculateMatchupMatrix(List<MatchEntry> matches)
        {
            List<MatchEntry> relevant = matches.Where(m => m.Playing?.Name != null && m.Against?.Name != null).ToList();

            string[] played = relevant.Select(m => m.Playing?.Name).OfType<string>().Distinct().OrderBy(x => x).ToArray();
            string[] opponents = relevant.Select(m => m.Against?.Name).OfType<string>().Distinct().OrderBy(x => x).ToArray();

            Dictionary<string, int> playedIdx = played.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
            Dictionary<string, int> opponentIdx = opponents.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);

            (int PlayedIdx, int OpponentIdx, double WinRate)[] cells = relevant
                .GroupBy(m => (Playing: m.Playing?.Name ?? string.Empty, Against: m.Against?.Name ?? string.Empty))
                .Select(g => (
                    PlayedIdx: playedIdx[g.Key.Playing],
                    OpponentIdx: opponentIdx[g.Key.Against],
                    WinRate: Calculations.CalculateWinRate(g.ToList(), out _, out _, out _)
                ))
                .ToArray();

            return (played, opponents, cells);
        }

        public (int LongestWinStreak, int LongestLossStreak, int LongestTieStreak) CalculateStreaks(List<MatchEntry> matches)
        {
            int currentWinStreak = 0, longestWinStreak = 0;
            int currentLossStreak = 0, longestLossStreak = 0;
            int currentTieStreak = 0, longestTieStreak = 0;

#pragma warning disable S3267 // Mutable streak accumulators cannot be expressed with LINQ Select
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
#pragma warning restore S3267

            return (longestWinStreak, longestLossStreak, longestTieStreak);
        }
    }
}
