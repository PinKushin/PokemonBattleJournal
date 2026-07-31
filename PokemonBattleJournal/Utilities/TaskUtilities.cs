namespace PokemonBattleJournal.Utilities
{
    /// <summary>
    /// Task Utilities.
    /// </summary>
    public static class TaskUtilities
    {
        /// <summary>
        /// Fire and Forget Safe Async.
        /// </summary>
        /// <param name="task">Task to Fire and Forget.</param>
        /// <param name="handler">Error Handler.</param>


        [System.Diagnostics.CodeAnalysis.SuppressMessage("S3168", "S3168:\"async\" methods should not return \"void\"")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("PH_S", "PH_S030:async void method invocation",
            Justification = "Intentional fire-and-forget extension method; async void is the correct pattern here.")]
        public static async void FireAndForgetSafeAsync(this Task task, IErrorHandler? handler = null)

        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                handler?.HandleError(ex);
            }
        }
    }
}