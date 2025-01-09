using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Layouts;


namespace PokemonBattleJournal.ViewModels
{
    [QueryProperty(nameof(IsNew), "new")]
    [QueryProperty(nameof(CardBrief), "card")]
    [QueryProperty(nameof(Name), "name")]
    [QueryProperty(nameof(Id), "id")]
    public partial class CardDetailsPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private partial bool? IsNew {  get; set; }
        [ObservableProperty]
        private partial string? Id { get; set; }
        [ObservableProperty]
        public partial TcgDexCardBrief? CardBrief { get; set; }
        [ObservableProperty]
        public partial TcgDexCard? Card { get; set; }
        [ObservableProperty]
        public partial Attack Attack { get; set; } = new Attack();
        [ObservableProperty]
        public partial List<Attack> AttackCollection { get; set; } = new List<Attack>();
        [ObservableProperty]
        public partial string? Name { get; set; }

        private bool _isInitialized = false;
        

        public CardDetailsPageViewModel()
        {
           
        }

        [RelayCommand]
        public async Task AppearingAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            await GetCardDetailsAsync();
        }

        [RelayCommand]
        public async Task GetCardDetailsAsync()
        {
            var tcgDexApiService = new TcgDexApiService();
            if (Id  == null && CardBrief != null) Id = CardBrief.Id;
            if (Id != null) Card = await tcgDexApiService.GetCardDetailsAsync(Id);
            if (Card!.Attacks.Count > 0)
            {
                foreach (var attack in Card.Attacks)
                {

                    //Not displaying attacks yet
                    AttackCollection.Add(attack);
                    
                    Attack.Damage = attack.Damage;
                }
            }
        }

        [RelayCommand]
        private static async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
