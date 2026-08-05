namespace PokemonBattleJournal.Tests.Utilities
{
    public class TaskUtilitiesTests
    {
        [Test]
        public async Task FireAndForgetSafeAsync_Success_DoesNotThrow()
        {
            // Arrange
            Task successfulTask = Task.CompletedTask;

            // Act & Assert — the observer task completes without throwing.
            // FireAndForgetSafeAsync returns the observing Task (no async void),
            // so tests can await it directly instead of sleeping.
            await successfulTask.FireAndForgetSafeAsync();
        }

        [Test]
        public async Task FireAndForgetSafeAsync_Failure_WithHandler_CallsHandler()
        {
            // Arrange
            IErrorHandler mockHandler = Substitute.For<IErrorHandler>();
            Task failingTask = Task.FromException(new Exception("Test error"));

            // Act
            await failingTask.FireAndForgetSafeAsync(mockHandler);

            // Assert
            mockHandler.Received(1).HandleError(Arg.Any<Exception>());
        }

        [Test]
        public async Task FireAndForgetSafeAsync_Failure_NullHandler_DoesNotThrow()
        {
            // Arrange
            Task failingTask = Task.FromException(new Exception("Test error"));

            // Act & Assert — should not throw even with null handler
            await TaskUtilities.FireAndForgetSafeAsync(failingTask, null);
        }

        [Test]
        public async Task FireAndForgetSafeAsync_Failure_WithLogger_LogsError()
        {
            ILogger mockLogger = Substitute.For<ILogger>();
            Task failingTask = Task.FromException(new InvalidOperationException("boom"));

            await failingTask.FireAndForgetSafeAsync(logger: mockLogger);

            mockLogger.Received(1).Log(
                LogLevel.Error,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
        }
    }
}
