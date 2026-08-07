// ImplicitUsings is disabled. What the SDK (UseMaui=true here, so the MAUI set applies) and the packages would have injected is written out here
// instead, copied from the generated obj/**/*.GlobalUsings.g.cs rather than guessed.
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Maui;
global using Microsoft.Maui.Accessibility;
global using Microsoft.Maui.ApplicationModel;
global using Microsoft.Maui.ApplicationModel.Communication;
global using Microsoft.Maui.ApplicationModel.DataTransfer;
global using Microsoft.Maui.Authentication;
global using Microsoft.Maui.Controls;
global using Microsoft.Maui.Controls.Hosting;
global using Microsoft.Maui.Controls.Xaml;
global using Microsoft.Maui.Devices;
global using Microsoft.Maui.Devices.Sensors;
global using Microsoft.Maui.Dispatching;
global using Microsoft.Maui.Graphics;
global using Microsoft.Maui.Hosting;
global using Microsoft.Maui.Media;
global using Microsoft.Maui.Networking;
global using Microsoft.Maui.Storage;
global using Sentry;
global using System;
global using System.Collections.Generic;
global using System.IO;
global using System.Linq;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;
global using PokemonBattleJournal.IntegrationTests.Infrastructure;

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
