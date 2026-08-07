// ImplicitUsings is disabled for this project. Everything the SDK and the packages would have
// injected is declared here instead, taken verbatim from the generated
// obj/**/PokemonBattleJournal.GlobalUsings.g.cs rather than guessed.
//
// Three of these are exactly why the implicit set is worth writing down. A MAUI head does NOT
// get Microsoft.Extensions.DependencyInjection from the base SDK — MAUI adds it, and without it
// `builder.Services.AddSingleton<TInterface, TImpl>()` silently binds to MAUI's OWN
// AddSingleton<TView, TViewModel> overload and fails with constraint errors about
// BindableObject that say nothing about the real cause. And `Sentry` is contributed by the
// Sentry.Maui PACKAGE, not the SDK at all, so it would come and go with a dependency.
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

// Project-wide namespaces, declared here since before the implicit set was turned off.
global using System.Collections.ObjectModel;
global using CommunityToolkit.Mvvm.ComponentModel;
global using CommunityToolkit.Mvvm.Input;
global using Microsoft.Extensions.Logging;
global using PokemonBattleJournal.Interfaces;
global using PokemonBattleJournal.Models;
global using PokemonBattleJournal.Resources.Fonts;
global using PokemonBattleJournal.Services;
global using PokemonBattleJournal.Utilities;
global using PokemonBattleJournal.ViewModels;
global using PokemonBattleJournal.Views;
global using SQLite;