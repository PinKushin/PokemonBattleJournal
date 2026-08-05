namespace PokemonBattleJournal.Tests.ViewModels
{
    public class ReadJournalPageViewModelTests
    {
        private ReadJournalPageViewModel _viewModel = null!;
        private ISqliteConnectionFactory _mockConnectionFactory = null!;
        private ILogger<ReadJournalPageViewModel> _mockLogger = null!;
        private ITrainerSwitchService _mockSwitchService = null!;

        [SetUp]
        public void SetUp()
        {
            // Mocks
            _mockLogger = Substitute.For<ILogger<ReadJournalPageViewModel>>();
            _mockConnectionFactory = Substitute.For<ISqliteConnectionFactory>();
            _mockSwitchService = Substitute.For<ITrainerSwitchService>();

            _mockConnectionFactory.Trainers.Returns(Substitute.For<ITrainerOperations>());
            _mockConnectionFactory.Matches.Returns(Substitute.For<IMatchOperations>());

            // SUT
            _viewModel = new ReadJournalPageViewModel(_mockLogger, _mockConnectionFactory, _mockSwitchService, Substitute.For<IErrorHandler>());
        }

        /// <summary>
        /// The journal must list the most recently played match first.
        /// </summary>
        /// <remarks>
        /// Regression for a bug found 2026-08-05. <c>GetByTrainerIdAsync</c> issues no ORDER BY,
        /// so matches arrive in insertion order and the view model assigned them straight to
        /// <c>MatchHistory</c> — the journal listed oldest first. It went unnoticed because
        /// DebugDataSeeder inserts in date order for a fresh database, making insertion order
        /// and date order agree; they diverge as soon as a match is added out of sequence, which
        /// the seeder itself does by dating its BO3 matches a day ahead.
        ///
        /// It also silently broke ReadJournalPage_BO3Match_ShowsGame2And3TagViews, whose helper
        /// clicks the first row on the documented assumption that it is the newest match. The
        /// first row was actually the oldest — a BO1 — and the test only passed because
        /// phantom Game2/Game3 objects made the views appear on every match
        /// (see MatchOperations.LoadRelatedDataAsync).
        /// </remarks>
        [Test]
        public async Task AppearingAsync_Matches_AreOrderedNewestFirst()
        {
            Trainer trainer = new() { Id = 1, Name = "Ash", IsActive = true };
            _mockConnectionFactory.Trainers.GetActiveAsync().Returns(Task.FromResult<Trainer?>(trainer));

            // Deliberately handed over oldest-first, the order the database returns.
            List<MatchEntry> matches =
            [
                new() { Id = 1, DatePlayed = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc) },
                new() { Id = 2, DatePlayed = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc) },
                new() { Id = 3, DatePlayed = new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc) },
            ];
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(trainer.Id, true)
                .Returns(Task.FromResult(matches));

            await _viewModel.AppearingAsync();

            _viewModel.MatchHistory.ShouldNotBeNull();
            _viewModel.MatchHistory!.Select(m => m.Id).ShouldBe([2u, 3u, 1u]);
        }

        [Test]
        public void ReadJournalPageViewModel_Constructor_SetsWelcomeMsg()
        {
            // Assert
            _viewModel.ShouldNotBeNull();
            _viewModel.WelcomeMsg.ShouldNotBeNullOrEmpty();
        }

        [Test]
        public void LoadMatch_NullSelectedMatch_ResetsDisplay()
        {
            // Arrange
            _viewModel.SelectedMatch = null;

            // Act
            _viewModel.LoadMatch();

            // Assert
            _viewModel.SelectedNote.ShouldBe("Select Match");
            _viewModel.PlayingName.ShouldBe("other");
            _viewModel.AgainstName.ShouldBe("other");
        }

        [Test]
        public void LoadMatch_ValidMatchWithGame1_PopulatesPlayingAndResult()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Win,
                Playing = new Archetype { Name = "Charizard", ImagePath = "charizard.png" },
                Against = new Archetype { Name = "Gardevoir", ImagePath = "gardevoir.png" },
                Game1 = new Game
                {
                    Result = MatchResult.Win,
                    Notes = "Good start",
                    Tags = [new Tags { Name = "Lucky" }]
                }
            };

            _viewModel.LoadMatch();

            _viewModel.PlayingName.ShouldBe("Charizard");
            _viewModel.AgainstName.ShouldBe("Gardevoir");
            _viewModel.PlayingIconSource.ShouldBe("charizard.png");
            _viewModel.AgainstIconSource.ShouldBe("gardevoir.png");
            _viewModel.OverallResult.ShouldBe(MatchResult.Win);
            _viewModel.ResultGame1.ShouldBe(MatchResult.Win);
            _viewModel.SelectedNote.ShouldBe("Good start");
            _viewModel.HasGame1Tags.ShouldBeTrue();
            _viewModel.Game1TagsInfo.ShouldBe("Game 1: 1 tags");
        }

        [Test]
        public void LoadMatch_MatchWithNoGames_SetsGameResultsToNull()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Loss,
                Playing = new Archetype { Name = "Fire" },
                Against = new Archetype { Name = "Water" }
            };

            _viewModel.LoadMatch();

            _viewModel.ResultGame1.ShouldBeNull();
            _viewModel.ResultGame2.ShouldBeNull();
            _viewModel.ResultGame3.ShouldBeNull();
            _viewModel.HasGame1Tags.ShouldBeFalse();
        }

        [Test]
        public async Task AppearingAsync_WithTrainerAndMatches_PopulatesMatchHistory()
        {
            List<MatchEntry> matches =
            [
                new() { Result = MatchResult.Win },
                new() { Result = MatchResult.Loss }
            ];

            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, Arg.Any<bool>())
                .Returns(Task.FromResult(matches));

            await _viewModel.AppearingAsync();

            _viewModel.MatchHistory.ShouldNotBeNull();
            _viewModel.MatchHistory!.Count.ShouldBe(2);
        }

        [Test]
        public async Task AppearingAsync_NoTrainer_SetsEmptyMatchHistory()
        {
            // Arrange
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(null));

            // Act
            await _viewModel.AppearingAsync();

            // Assert
            _viewModel.MatchHistory.ShouldBeNull();
        }

        [Test]
        public void LoadMatch_BO3AllThreeGames_PopulatesAllGameResults()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Win,
                Playing = new Archetype { Name = "Fire" },
                Against = new Archetype { Name = "Water" },
                Game1 = new Game { Result = MatchResult.Win, Tags = [] },
                Game2 = new Game { Result = MatchResult.Loss, Tags = [] },
                Game3 = new Game { Result = MatchResult.Win, Tags = [new Tags { Name = "Clutch" }] }
            };

            _viewModel.LoadMatch();

            _viewModel.ResultGame1.ShouldBe(MatchResult.Win);
            _viewModel.ResultGame2.ShouldBe(MatchResult.Loss);
            _viewModel.ResultGame3.ShouldBe(MatchResult.Win);
            _viewModel.HasGame3Tags.ShouldBeTrue();
            _viewModel.Game3TagsInfo.ShouldBe("Game 3: 1 tags");
        }

        [Test]
        public void LoadMatch_Game1WithTags_PopulatesTagsSelectedGame1()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Win,
                Playing = new Archetype { Name = "Fire" },
                Against = new Archetype { Name = "Water" },
                Game1 = new Game
                {
                    Result = MatchResult.Win,
                    Tags = [new Tags { Name = "Aggro" }, new Tags { Name = "Lucky" }]
                }
            };

            _viewModel.LoadMatch();

            _viewModel.TagsSelectedGame1.ShouldNotBeNull();
            _viewModel.TagsSelectedGame1!.Count.ShouldBe(2);
            _viewModel.TagsSelectedGame1.ShouldContain(t => t.Name == "Aggro");
        }

        [Test]
        public void LoadMatch_NullArchetype_FallsBackToUnknown()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Loss,
                Playing = null,
                Against = null
            };

            _viewModel.LoadMatch();

            _viewModel.PlayingName.ShouldBe("Unknown");
            _viewModel.AgainstName.ShouldBe("Unknown");
        }

        [Test]
        public async Task AppearingAsync_EmptyMatchList_SetsEmptyMatchHistory()
        {
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, Arg.Any<bool>())
                .Returns(Task.FromResult(new List<MatchEntry>()));

            await _viewModel.AppearingAsync();

            _viewModel.MatchHistory.ShouldNotBeNull();
            _viewModel.MatchHistory!.Count.ShouldBe(0);
        }

        [Test]
        public async Task AppearingAsync_DatabaseThrows_DoesNotRethrow()
        {
            _mockConnectionFactory.Trainers.GetActiveAsync()
                .Returns(Task.FromResult<Trainer?>(new Trainer { Id = 1, Name = "Test" }));
            _mockConnectionFactory.Matches.GetByTrainerIdAsync(1, Arg.Any<bool>())
                .Returns(Task.FromException<List<MatchEntry>>(new InvalidOperationException("DB failure")));

            // Exception must not propagate — caught and handled internally.
            await Should.NotThrowAsync(() => _viewModel.AppearingAsync());
        }

        [Test]
        public void LoadMatch_Game2WithTags_PopulatesTagsSelectedGame2()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Win,
                Playing = new Archetype { Name = "Fire" },
                Against = new Archetype { Name = "Water" },
                Game1 = new Game { Result = MatchResult.Win, Tags = [] },
                Game2 = new Game
                {
                    Result = MatchResult.Loss,
                    Tags = [new Tags { Name = "Tag1" }, new Tags { Name = "Tag2" }]
                }
            };

            _viewModel.LoadMatch();

            _viewModel.TagsSelectedGame2.ShouldNotBeNull();
            _viewModel.TagsSelectedGame2!.Count.ShouldBe(2);
            _viewModel.HasGame2Tags.ShouldBeTrue();
            _viewModel.Game2TagsInfo.ShouldBe("Game 2: 2 tags");
        }

        [Test]
        public void LoadMatch_Game3WithoutTags_SetsGame3NoTagsInfo()
        {
            _viewModel.SelectedMatch = new MatchEntry
            {
                Result = MatchResult.Win,
                Playing = new Archetype { Name = "Fire" },
                Against = new Archetype { Name = "Water" },
                Game1 = new Game { Result = MatchResult.Win, Tags = [] },
                Game2 = new Game { Result = MatchResult.Loss, Tags = [] },
                Game3 = new Game { Result = MatchResult.Tie, Tags = [] }
            };

            _viewModel.LoadMatch();

            _viewModel.ResultGame3.ShouldBe(MatchResult.Tie);
            _viewModel.HasGame3Tags.ShouldBeFalse();
            _viewModel.Game3TagsInfo.ShouldBe("Game 3: No tags");
            _viewModel.TagsSelectedGame3.ShouldNotBeNull();
            _viewModel.TagsSelectedGame3!.Count.ShouldBe(0);
        }

        // ---------------------------------------------------------------------------
        // OnTrainerChanged
        // ---------------------------------------------------------------------------

        [Test]
        public void OnTrainerChanged_EventRaised_UpdatesTrainerName()
        {
            var trainer = new Trainer { Id = 7, Name = "Brock" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.TrainerName.ShouldBe("Brock");
        }

        [Test]
        public void OnTrainerChanged_EventRaised_UpdatesWelcomeMsg()
        {
            var trainer = new Trainer { Id = 7, Name = "Brock" };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.WelcomeMsg.ShouldBe("Brock's Journal");
        }

        [Test]
        public void OnTrainerChanged_EventRaised_NullName_SetsEmptyTrainerName()
        {
            var trainer = new Trainer { Id = 7, Name = null };

            _mockSwitchService.TrainerChanged += Raise.Event<EventHandler<Trainer>>(this, trainer);

            _viewModel.TrainerName.ShouldBe(string.Empty);
        }
    }
}
