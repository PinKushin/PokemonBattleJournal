namespace PokemonBattleJournal.Utilities
{
    public static class Calculations
    {
        /// <summary>
        /// Calculate the average win rate of the trainer
        /// using ((Wins + (0.5 * Ties)) / TotalGames * 100
        /// </summary>
        /// <param name="matchList">List of Matches</param>
        /// <param name="Wins">Reference to Win property for display</param>
        /// <param name="Losses">Reference to Losses property for display</param>
        /// <param name="Ties">Reference to Ties property for display</param>
        public static double CalculateWinRate(List<MatchEntry> matchList, out uint Wins, out uint Losses, out uint Ties)
        {
            uint wins = 0;
            uint losses = 0;
            uint ties = 0;

            foreach (MatchEntry match in matchList)
            {
                switch (match.Result)
                {
                    case MatchResult.Win:
                        wins++;
                        break;
                    case MatchResult.Tie:
                        ties++;
                        break;
                    case null:
                        break;
                    default:
                        losses++;
                        break;
                }
            }
            Wins = wins;
            Losses = losses;
            Ties = ties;
            double winRate;
            if (wins + losses + ties == 0)
            {
                winRate = 0;
            }
            else
            {
                winRate = (wins + (0.5 * ties)) / (wins + losses + ties) * 100;
            }

            return winRate;
        }
    }
}
