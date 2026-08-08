namespace PokemonBattleJournal.Benchmarks;

using System.Reflection;
using BenchmarkDotNet.Running;

/// <summary>
/// Entry point. Run with <c>dotnet run -c Release --project PokemonBattleJournal.Benchmarks</c>.
/// </summary>
/// <remarks>
/// Switcher rather than a fixed type so new benchmark classes need no change here, and so a
/// single class can be selected with <c>--filter</c> when iterating on one measurement.
/// </remarks>
internal static class Program
{
    private static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
}
