using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PokemonBattleJournal.Interfaces;
using PokemonBattleJournal.Models;
using PokemonBattleJournal.ViewModels;

namespace PokemonBattleJournal.Benchmarks.ViewModels
{
    [MemoryDiagnoser] // Tracks memory usage during benchmarks
    public class MainPageViewModelBenchmarks
    {
        private MainPageViewModel _viewModel;

        [GlobalSetup]
        public void Setup()
        {
            // Mock ILogger
            var logger = new LoggerFactory().CreateLogger<MainPageViewModel>();

            // Mock ISqliteConnectionFactory
            var sqliteConnectionFactory = Substitute.For<ISqliteConnectionFactory>();

            // Mock ITrainerOperations
            ITrainerOperations trainerOperations = Substitute.For<ITrainerOperations>();
            trainerOperations.GetByNameAsync(Arg.Any<string>()).Returns(Task.FromResult<Trainer?>(new Trainer
            {
                Id = 1,
                Name = "TestTrainer"
            }));
            trainerOperations.GetByNameAsync(Arg.Any<string>()).Returns(Task.FromResult<Trainer?>(new Trainer
            {
                Id = 1,
                Name = "TestTrainer"
            }));
            trainerOperations.SaveAsync(Arg.Any<string>()).Returns(Task.FromResult(1));
            sqliteConnectionFactory.Trainers.Returns(trainerOperations);

            // Mock IMatchOperations
            var matchOperations = Substitute.For<IMatchOperations>();
            matchOperations.SaveAsync(Arg.Any<MatchEntry>(), Arg.Any<List<Game>>()).Returns(Task.FromResult(1));
            sqliteConnectionFactory.Matches.Returns(matchOperations);

            // Mock IArchetypeOperations
            var archetypeOperations = Substitute.For<IArchetypeOperations>();
            archetypeOperations.GetAllAsync().Returns(Task.FromResult(new List<Archetype>
            {
                new Archetype { Id = 1, Name = "TestArchetype" },
                new Archetype { Id = 2, Name = "RivalArchetype" }
            }));
            sqliteConnectionFactory.Archetypes.Returns(archetypeOperations);

            // Mock ITagOperations
            var tagOperations = Substitute.For<ITagOperations>();
            tagOperations.GetAllAsync().Returns(Task.FromResult(new List<Tags>
            {
                new Tags { Id = 1, Name = "TestTag" }
            }));
            sqliteConnectionFactory.Tags.Returns(tagOperations);

            // Mock IMatchResultsCalculatorFactory
            var calculatorFactory = Substitute.For<IMatchResultsCalculatorFactory>();
            var calculator = Substitute.For<IMatchResultCalculator>();
            calculator.CalculateResult(Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>(), Arg.Any<MatchResult?>())
                .Returns(MatchResult.Win);
            calculatorFactory.GetCalculator(Arg.Any<bool>()).Returns(calculator);

            // Initialize the ViewModel
            _viewModel = new MainPageViewModel(logger, sqliteConnectionFactory, calculatorFactory);
        }

        [Benchmark]
        public async Task AppearingAsyncBenchmark()
        {
            // Benchmark the AppearingAsync method
            await _viewModel.AppearingAsync();
        }

        [Benchmark]
        public async Task SaveMatchAsyncBenchmark()
        {
            // Set up necessary properties for SaveMatchAsync
            _viewModel.TrainerName = "TestTrainer";
            _viewModel.PlayerSelected = new Archetype { Id = 1, Name = "TestArchetype" };
            _viewModel.RivalSelected = new Archetype { Id = 2, Name = "RivalArchetype" };
            _viewModel.Result = MatchResult.Win;

            // Benchmark the SaveMatchAsync method
            await _viewModel.SaveMatchAsync();
        }
    }
}
