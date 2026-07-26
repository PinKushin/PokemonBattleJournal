using BenchmarkDotNet.Analysers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace PokemonBattleJournal.Benchmarking
{
    // Extends ManualConfig so [Config(typeof(BenchmarkConfig))] works via Activator.CreateInstance.
    // DisableOptimizationsValidator allows running against Debug-built dependencies.
    public class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            WithOptions(ConfigOptions.DisableOptimizationsValidator);
            AddJob(Job.Default
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithWarmupCount(1)
                .WithIterationCount(3));
            AddDiagnoser(MemoryDiagnoser.Default);
            AddColumnProvider(DefaultColumnProviders.Instance);
            AddLogger(ConsoleLogger.Default);
            AddExporter(CsvExporter.Default);
            AddExporter(HtmlExporter.Default);
            AddAnalyser(GetAnalysers().ToArray());
        }

        public static IConfig Get() => new BenchmarkConfig();

        private static new IEnumerable<IAnalyser> GetAnalysers()
        {
            yield return EnvironmentAnalyser.Default;
            yield return OutliersAnalyser.Default;
            yield return MinIterationTimeAnalyser.Default;
            yield return MultimodalDistributionAnalyzer.Default;
            yield return RuntimeErrorAnalyser.Default;
            yield return ZeroMeasurementAnalyser.Default;
            yield return BaselineCustomAnalyzer.Default;
        }
    }
}
