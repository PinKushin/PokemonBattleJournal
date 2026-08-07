namespace PokemonBattleJournal.Interfaces
{
    public interface IMatchResultCalculator
    {
        MatchResult CalculateResult(MatchResult? result1, MatchResult? result2 = null, MatchResult? result3 = null);
    }
}
