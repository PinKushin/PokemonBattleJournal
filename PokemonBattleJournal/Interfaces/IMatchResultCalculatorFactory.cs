namespace PokemonBattleJournal.Interfaces
{
    public interface IMatchResultsCalculatorFactory
    {
        IMatchResultCalculator GetCalculator(bool isBestOf3);
    }
}
