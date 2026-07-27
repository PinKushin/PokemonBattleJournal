using PokemonBattleJournal.Utilities;

namespace PokemonBattleJournal.Tests.Utilities;

// MainThreadHelper branches on DevicePlatform.Unknown — all run in test env.
public class MainThreadHelperTests
{
    [Fact]
    public void IsMainThread_TestEnvironment_ReturnsTrue()
    {
        MainThreadHelper.IsMainThread.ShouldBeTrue();
    }

    [Fact]
    public void BeginInvokeOnMainThread_TestEnvironment_ExecutesActionSynchronously()
    {
        bool executed = false;

        MainThreadHelper.BeginInvokeOnMainThread(() => executed = true);

        executed.ShouldBeTrue();
    }

    [Fact]
    public void BeginInvokeOnMainThread_ActionThrows_ExceptionPropagates()
    {
        Should.Throw<InvalidOperationException>(() =>
            MainThreadHelper.BeginInvokeOnMainThread(() => throw new InvalidOperationException("boom")));
    }
}
