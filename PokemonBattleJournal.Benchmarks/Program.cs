using BenchmarkDotNet.Running;

namespace PokemonBattleJournal.Benchmarking
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, BenchmarkConfig.Get());
        }
    }
}