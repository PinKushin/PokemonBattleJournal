
namespace PokemonBattleJournal.IntegrationTests.Infrastructure;

/// <summary>
/// Swallows errors instead of showing them — prevents the real <see cref="ModalErrorHandler"/>
/// from attempting a UI dialog during headless integration tests.
/// </summary>
/// <remarks>
/// Use this when the test does not care about error surfacing. When the test IS about error
/// surfacing, substitute a recording implementation and assert on it — that is the whole
/// reason <c>IErrorHandler</c> is injected rather than constructed inline.
/// </remarks>
public sealed class NullErrorHandler : IErrorHandler
{
    public void HandleError(Exception ex)
    {
        // Intentionally does nothing: there is no UI to surface to in an integration test.
        // Not a silent catch — the production logger still records the exception at the call
        // site, and this type exists precisely so that behaviour is explicit and swappable.
    }
}
