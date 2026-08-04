using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PokemonBattleJournal.Models;

namespace PokemonBattleJournal.ViewModels;

public partial class TagViewModel : ObservableObject
{
    public Tags Model { get; }
    public string Name => Model.Name ?? string.Empty;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public TagViewModel(Tags model) => Model = model;

    [RelayCommand]
    private void Toggle() => IsSelected = !IsSelected;
}
