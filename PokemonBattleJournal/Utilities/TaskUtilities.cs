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
#pragma warning disable S3168 // "async" methods should not return "void"

		public static async void FireAndForgetSafeAsync(this Task task, IErrorHandler? handler = null)
#pragma warning restore S3168 // "async" methods should not return "void"
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