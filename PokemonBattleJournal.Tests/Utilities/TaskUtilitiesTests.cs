namespace PokemonBattleJournal.Tests.Utilities
{
    public class TaskUtilitiesTests
    {
        [Fact]
        public void FireAndForgetSafeAsync_Success_DoesNotThrow()
        {
            // Arrange
            Task successfulTask = Task.CompletedTask;

            // Act & Assert — should not throw
            successfulTask.FireAndForgetSafeAsync();
        }

        [Fact]
        public async Task FireAndForgetSafeAsync_Failure_WithHandler_CallsHandler()
        {
            // Arrange
            IErrorHandler mockHandler = Substitute.For<IErrorHandler>();
            Task failingTask = Task.FromException(new Exception("Test error"));

            // Act
            failingTask.FireAndForgetSafeAsync(mockHandler);

            // Allow fire-and-forget to complete
            await Task.Delay(100);

            // Assert
            mockHandler.Received(1).HandleError(Arg.Any<Exception>());
        }

        [Fact]
        public async Task FireAndForgetSafeAsync_Failure_NullHandler_DoesNotThrow()
        {
            // Arrange
            Task failingTask = Task.FromException(new Exception("Test error"));

            // Act & Assert — should not throw even with null handler
            TaskUtilities.FireAndForgetSafeAsync(failingTask, null);

            // Allow fire-and-forget to complete
            await Task.Delay(100);
        }
    }
}
