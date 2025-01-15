using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Layouts;
using Microsoft.Maui.Platform;


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
        private partial string? LocalId { get; set; }
        [ObservableProperty]
        public partial TcgDexCardBrief? CardBrief { get; set; }
        [ObservableProperty]
        public partial TcgDexCard? Card { get; set; }
        [ObservableProperty]
        public partial string Attack1DamageDisplay { get; set; } = "0";
        [ObservableProperty]
        public partial string Attack2DamageDisplay { get; set; } = "0";
        [ObservableProperty]
        public partial Attack Attack2 { get; set; } = new Attack();
        [ObservableProperty]
        public partial ObservableCollection<Attack> AttackCollection { get; set; } = new ObservableCollection<Attack>();
        [ObservableProperty]
        public partial string? Name { get; set; }
        [ObservableProperty]
        public partial string? Category { get; set; }
        [ObservableProperty]
        public partial string? Illustrator { get; set; }
        [ObservableProperty]
        public partial string? Rarity { get; set; }
        [ObservableProperty]
        public partial string? Details { get; set; }
        [ObservableProperty]
        public partial bool Attack1Visible { get; set; } = false;
        [ObservableProperty]
        public partial bool Attack1ShowEffect { get; set; } = false;
        [ObservableProperty]
        public partial string? Attack1Effect { get; set; }
        [ObservableProperty]
        public partial bool Attack2Visible { get; set; } = false;
        [ObservableProperty]
        public partial bool Attack2ShowEffect { get; set; } = false;
        [ObservableProperty]
        public partial string? Attack2Effect { get; set; }


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
            if (Card == null) return;
            if (Card.Category == Models.Category.Pokemon)
            {
                if (Card.Attacks.Count > 0)
                {
                    var numberOfAttacks = 0;
                    
                    foreach (var attack in Card.Attacks)
                    {
                        numberOfAttacks++;

                        //display attacks
                        AttackCollection.Add(attack);

                        if (numberOfAttacks == 1)
                        {
                            
                            Attack1Visible = true;
                        }
                        if (numberOfAttacks == 2)
                        {
                            
                            Attack2Visible = true;
                        }
                        if (attack.Effect != null)
                        {
                            if (numberOfAttacks == 1)
                            {
                                Attack1ShowEffect = true;
                                Attack1Effect = attack.Effect;
                            }
                            else if (numberOfAttacks == 2)
                            {
                                Attack2ShowEffect = true;
                                Attack2Effect = attack.Effect;
                            }
                        }
                        if (attack.Damage.HasValue)
                        {
                            if (numberOfAttacks == 1)
                            {
                                if (attack.Damage.Value.Integer != null)
                                {
                                    Attack1DamageDisplay = attack.Damage.Value.Integer.ToString()!;
                                }
                                else if (attack.Damage.Value.String != null)
                                {
                                    Attack1DamageDisplay = attack.Damage.Value.String;
                                }
                                
                               
                            }
                            else if (numberOfAttacks == 2)
                            {
                                if (attack.Damage.Value.Integer != null)
                                {
                                    Attack2DamageDisplay = attack.Damage.Value.Integer.ToString()!;
                                }
                                else if (attack.Damage.Value.String != null)
                                {
                                    Attack2DamageDisplay = attack.Damage.Value.String;
                                }
                            }
                            
                        }
                        else
                        {
                            
                            
                            Attack1DamageDisplay = "0";
                            Attack2DamageDisplay= "0";
                            
                        }                 
                    }
                }
                if (Card.Description != null)
                {
                    Details = Card.Description;
                }
            }
            else if (Card.Category == Models.Category.Trainer)
            {
                Details = Card.Effect;
            }
            else if(Card.Category == Models.Category.Energy)
            {
                Details = Card.EnergyType;
            }
            else
            {
                ModalErrorHandler modalErrorHandler = new ModalErrorHandler();
                modalErrorHandler.HandleError(new Exception("Not A valid Pokemon Card"));
            }
            
        }

        [RelayCommand]
        private static async Task GoBack()
        {
            await Shell.Current.GoToAsync("..");
        }
    }
}
