// Mirrors the subset of the app head's GlobalUsings.cs that the moved files actually use.
// Deliberately absent: CommunityToolkit.Mvvm, PokemonBattleJournal.Resources.Fonts,
// .ViewModels and .Views — nothing here may depend on those, and leaving them out means the
// compiler enforces that rather than a convention doing it.
global using System.Collections.ObjectModel;
global using Microsoft.Extensions.Logging;
global using PokemonBattleJournal.Interfaces;
global using PokemonBattleJournal.Models;
global using PokemonBattleJournal.Services;
global using PokemonBattleJournal.Utilities;
global using SQLite;
