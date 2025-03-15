namespace PokemonBattleJournal.Utilities
{
    public static class Calculations
    {
        public static MatchResult CalculateOverallResult(MatchResult? result1, MatchResult? result2, MatchResult? result3)
        {
            uint wins = 0;
            uint draws = 0;

            foreach (var result in new[] { result1, result2, result3 })
            {
                if (result == MatchResult.Win)
                    wins++;
                else if (result == MatchResult.Tie)
                    draws++;
            }

            if (wins >= 2)
                return MatchResult.Win;
            if (draws >= 2 || (draws == 1 && wins == 1))
                return MatchResult.Tie;
            return MatchResult.Loss;
        }
    }
}
