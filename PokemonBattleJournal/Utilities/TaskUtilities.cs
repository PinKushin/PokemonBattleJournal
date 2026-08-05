namespace PokemonBattleJournal.Utilities
{
    /// <summary>
    /// Task Utilities.
    /// </summary>
    public static class TaskUtilities
    {
        /// <summary>
        /// Observes a fire-and-forget task: awaits it on a continuation and routes any
        /// exception to the logger and/or error handler instead of crashing the process.
        /// Returns the observing task so tests (or interested callers) can await the
        /// completion of the error handling itself; typical call sites discard it with
        /// <c>_ =</c>. Deliberately NOT async void — async void invocations can't be
        /// awaited or observed and trip analyzer PH_S030 at every call site.
        /// </summary>
        /// <param name="task">Task to fire and forget.</param>
        /// <param name="handler">Error handler invoked on failure.</param>
        /// <param name="logger">Logger for the failure.</param>
        public static Task FireAndForgetSafeAsync(this Task task, IErrorHandler? handler = null, ILogger? logger = null)
        {
            return ObserveAsync(task, handler, logger);

            static async Task ObserveAsync(Task task, IErrorHandler? handler, ILogger? logger)
            {
                try
                {
                    await task;
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Unhandled exception in fire-and-forget task");
                    handler?.HandleError(ex);
                }
            }
        }
    }
}
