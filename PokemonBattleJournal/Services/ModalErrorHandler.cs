namespace PokemonBattleJournal.Services
{
	/// <summary>
	/// Modal Error Handler.
	/// </summary>
	public class ModalErrorHandler : IErrorHandler
	{
		private static readonly SemaphoreSlim _semaphore = new(1, 1);

		/// <summary>
		/// Handle error in UI.
		/// </summary>
		/// <param name="ex">Exception.</param>
		public void HandleError(Exception ex)
		{
			DisplayAlertAsync(ex).FireAndForgetSafeAsync();
		}

		private static async Task DisplayAlertAsync(Exception ex)
		{
			try
			{
				await _semaphore.WaitAsync();
				if (Shell.Current is Shell shell)
					await shell.DisplayAlert("Error", ex.Message, "OK");
			}
			finally
			{
				_ = _semaphore.Release();
			}
		}
	}
}