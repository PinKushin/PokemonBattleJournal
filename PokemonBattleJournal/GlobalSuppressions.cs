using System.Diagnostics.CodeAnalysis;

// CA5392 fires on a P/Invoke declared in Microsoft's own WindowsAppSDK source
// (UndockedRegFreeWinRT-AutoInitializer.cs), which the package compiles into this
// project. We cannot add DefaultDllImportSearchPaths to a file we do not own, and
// the DLL it loads is resolved by the Windows App Runtime itself rather than by
// the search order the rule is about.
//
// Scoped to that single member on purpose. A blanket NoWarn would also hide the
// rule on any P/Invoke this project adds later, which is exactly the case it
// exists for. Suppression is acceptable here under the Zero Warnings standard
// because it is a third-party false positive, not a real issue being papered over.
[assembly: SuppressMessage(
    "Security",
    "CA5392:Use DefaultDllImportSearchPaths attribute for P/Invokes",
    Justification = "Third-party source from the WindowsAppSDK package; not editable here.",
    Scope = "member",
    Target = "~M:Microsoft.Windows.Foundation.UndockedRegFreeWinRTCS.NativeMethods.WindowsAppRuntime_EnsureIsLoaded~System.Int32")]

// CA1416 fires only on the base net10.0 TFM, which exists so the test projects
// can reference this assembly — the app never runs there. FileSaver is supported
// on every TFM the app actually ships to (Windows, Android, iOS, MacCatalyst),
// and the export command is only reachable from a UI that exists on those heads.
//
// This is the platform-TFM mismatch case the Zero Warnings standard names as
// legitimately suppressible: the call site is guarded by construction rather than
// by a runtime check. Scoped to the one method so a genuinely unguarded platform
// call elsewhere still fails the build.
[assembly: SuppressMessage(
    "Interoperability",
    "CA1416:Validate platform compatibility",
    Justification = "Only fires on the reference-only net10.0 TFM; FileSaver is supported on every TFM the app ships to.",
    Scope = "member",
    Target = "~M:PokemonBattleJournal.ViewModels.OptionsPageViewModel.SaveExportAsync(System.Func{System.Threading.Tasks.Task{System.String}},System.String)~System.Threading.Tasks.Task")]
