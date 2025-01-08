using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Maui.Core.Extensions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PokemonBattleJournal.ViewModels
{
    public partial class DeckBuilderPageViewModel : ObservableObject
    {
        [ObservableProperty]
        public partial ObservableCollection<TcgDexCardBrief>? CardList { get; set; }

        [ObservableProperty]
        public partial string? SearchTerm { get; set; }

        private bool _isInitialized = false;

        public DeckBuilderPageViewModel()
        {

        }

        [RelayCommand]
        public async Task AppearingAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            await GetAllCardsAsync();
        }

        [RelayCommand]
        public async Task GetAllCardsAsync()
        {
            var tcgDexApiService = new TcgDexApiService();
            CardList = await tcgDexApiService.AllCardsAsync();
            if (CardList == null) return;
            foreach (var card in CardList)
            {
                if (card.Image != "card_backside_atomicmonkeytcg.png")
                {
                    card.Image += "/high.png";
                }
                else if (card.Image == "" || card.Image == null)
                {
                    card.Image = "card_backside_atomicmonkeytcg.png";
                }
            }
        }
        [RelayCommand]
        public async Task SearchCards(string filterText)
        {
            if (CardList == null) await GetAllCardsAsync();
            if (string.IsNullOrEmpty(filterText)) await GetAllCardsAsync();
            var prevList = CardList;
            if (prevList == null) return;
            if (CardList != null)
            {
                if (string.IsNullOrEmpty(filterText)) CardList = prevList;
                if (!CardList.Where(x => x.Name.StartsWith(filterText, StringComparison.OrdinalIgnoreCase)).Any())
                {
                    CardList = prevList;
                }
                CardList = CardList.Where(x => x.Name.StartsWith(filterText, StringComparison.OrdinalIgnoreCase)).ToObservableCollection();
            }


        }
    }
}
