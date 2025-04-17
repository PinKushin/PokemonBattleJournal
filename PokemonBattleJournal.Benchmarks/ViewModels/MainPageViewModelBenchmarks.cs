using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PokemonBattleJournal.Interfaces;
using PokemonBattleJournal.Models;
using PokemonBattleJournal.ViewModels;

namespace PokemonBattleJournal.Benchmarking.ViewModels
{
    [MemoryDiagnoser] // Tracks memory usage during benchmarks
    public class MainPageViewModelBenchmarks
    {
        private MainPageViewModel? _viewModel;

        [GlobalSetup]
        public void Setup()
        {
            // Mock ILogger
            ILogger<MainPageViewModel> logger = new LoggerFactory().CreateLogger<MainPageViewModel>();

            // Mock ISqliteConnectionFactory
            ISqliteConnectionFactory sqliteConnectionFactory = Substitute.For<ISqliteConnectionFactory>();

            // Mock ITrainerOperations
            ITrainerOperations trainerOperations = Substitute.For<ITrainerOperations>();
            _ = trainerOperations.GetByNameAsync(Arg.Any<string>()).Returns(Task.FromResult<Trainer?>(new Trainer
            {
                Id = 1,
                Name = "TestTrainer"
            }));
            _ = trainerOperations.GetByNameAsync(Arg.Any<string>()).Returns(Task.FromResult<Trainer?>(new Trainer
            {
                Id = 1,
                Name = "TestTrainer"
            }));
            _ = trainerOperations.SaveAsync(Arg.Any<string>()).Returns(Task.FromResult(1));
            _ = sqliteConnectionFactory.Trainers.Returns(trainerOperations);

            // Mock IMatchOperations
            IMatchOperations matchOperations = Substitute.For<IMatchOperations>();
            _ = matchOperations.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>()).Returns(Task.FromResult(1));
            _ = sqliteConnectionFactory.Matches.Returns(matchOperations);

            // Mock IArchetypeOperations
            IArchetypeOperations archetypeOperations = Substitute.For<IArchetypeOperations>();
            _ = archetypeOperations.GetAllAsync().Returns(Task.FromResult(new List<Archetype>
            {
                new() { Id = 1, Name = "TestArchetype" },
                new() { Id = 2, Name = "RivalArchetype" }
            }));
            _ = sqliteConnectionFactory.Archetypes.Returns(archetypeOperations);

            // Mock ITagOperations
            ITagOperations tagOperations = Substitute.For<ITagOperations>();
            _ = tagOperations.GetAllAsync().Returns(Task.FromResult(new List<Tags>
            {
                new() { Id = 1, Name = "TestTag" }
            }));
            _ = sqliteConnectionFactory.Tags.Returns(tagOperations);

            // Mock IMatchResultsCalculatorFactory
            IMatchResultsCalculatorFactory calculatorFactory = Substitute.For<IMatchResultsCalculatorFactory>();
            IMatchResultCalculator calculator = Substitute.For<IMatchResultCalculator>();
            _ = calculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            _ = calculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(calculator);

            // Initialize the ViewModel
            _viewModel = new MainPageViewModel(logger, sqliteConnectionFactory, calculatorFactory);
        }

        [Benchmark]
        public async Task AppearingAsyncBenchmark()
        {
            // Benchmark the AppearingAsync method
            await _viewModel!.AppearingAsync();
        }

        [Benchmark]
        public async Task SaveMatchAsyncBenchmark()
        {
            // Set up necessary properties for SaveMatchAsync
            _viewModel!.TrainerName = "TestTrainer";
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "TestArchetype" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "RivalArchetype" };
            _viewModel.Result = MatchResult.Win;

            // Benchmark the SaveMatchAsync method
            _ = await _viewModel.SaveMatchAsync();
        }
    }
}
