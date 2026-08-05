global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Logging.Abstractions;
global using NSubstitute;
global using NUnit.Framework;
global using PokemonBattleJournal.Interfaces;
global using PokemonBattleJournal.Models;
global using PokemonBattleJournal.Scraper.Interfaces;
global using PokemonBattleJournal.Scraper.Models;
global using PokemonBattleJournal.Services;
global using Shouldly;
global using SQLite;

// Deliberately NOT global — each is used by only one or two files, so a local using keeps
// the dependency visible where it matters: System.Reflection, System.Text,
// PokemonBattleJournal.Services.Import, PokemonBattleJournal.Scraper.Services, and
// `using static PokemonBattleJournal.Models.MatchResult`.
