using PokemonBattleJournal.Utilities;

namespace PokemonBattleJournal.Tests.Utilities;

// MainThreadHelper branches on DevicePlatform.Unknown — all run in test env.
public class MainThreadHelperTests
{
    [Test]
    public void IsMainThread_TestEnvironment_ReturnsTrue()
    {
        MainThreadHelper.IsMainThread.ShouldBeTrue();
    }

    [Test]
    public void BeginInvokeOnMainThread_TestEnvironment_ExecutesActionSynchronously()
    {
        bool executed = false;

        MainThreadHelper.BeginInvokeOnMainThread(() => executed = true);

        executed.ShouldBeTrue();
    }

    [Test]
    public void BeginInvokeOnMainThread_ActionThrows_ExceptionPropagates()
    {
        Should.Throw<InvalidOperationException>(() =>
            MainThreadHelper.BeginInvokeOnMainThread(() => throw new InvalidOperationException("boom")));
    }
}
