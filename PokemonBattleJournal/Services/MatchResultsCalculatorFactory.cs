namespace PokemonBattleJournal.Services
{
    public class MatchResultCalculatorFactory : IMatchResultsCalculatorFactory
    {
        public IMatchResultCalculator GetCalculator(bool isBestOf3)
        {
            return isBestOf3 ? new BO3ResultCalculator() : new BO1ResultCalculator();
        }
    }
}
