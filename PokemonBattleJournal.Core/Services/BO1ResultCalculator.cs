namespace PokemonBattleJournal.Services
{
    public class BO1ResultCalculator : IMatchResultCalculator
    {
        public MatchResult CalculateResult(MatchResult? result1, MatchResult? result2 = null, MatchResult? result3 = null)
        {
            if (result1 == null)
            {
                throw new ArgumentNullException(nameof(result1), "Result1 cannot be null for Best of 1 calculation.");
            }

            return result1.Value;
        }
    }
}
