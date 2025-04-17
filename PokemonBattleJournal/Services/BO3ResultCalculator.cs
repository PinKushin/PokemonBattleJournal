namespace PokemonBattleJournal.Services
{
    public class BO3ResultCalculator : IMatchResultCalculator
    {
        public MatchResult CalculateResult(MatchResult? result1, MatchResult? result2 = null, MatchResult? result3 = null)
        {
            if ((result1 == null && result2 == null) || (result1 == null && result3 == null) || (result2 == null && result3 == null))
            {
                throw new ArgumentNullException(nameof(result1), "Need results for at least 2 games");
            }

            int wins = 0, losses = 0;

            if (result1 == MatchResult.Win)
            {
                wins++;
            }

            if (result1 == MatchResult.Loss)
            {
                losses++;
            }

            if (result2 == MatchResult.Win)
            {
                wins++;
            }

            if (result2 == MatchResult.Loss)
            {
                losses++;
            }

            if (result3 == MatchResult.Win)
            {
                wins++;
            }

            if (result3 == MatchResult.Loss)
            {
                losses++;
            }

            //Return the result based on the number of wins and losses
            //Implicitly handles all PTCG Official Result Rules for Best of 3
            //When tied going into game 3, the winner of game 3 takes all, a pseudo sudden death
            if (wins > losses)
            {
                return MatchResult.Win;
            }

            if (losses > wins)
            {
                return MatchResult.Loss;
            }

            return MatchResult.Tie;
        }
    }
}
