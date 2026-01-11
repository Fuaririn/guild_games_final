using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GuildGame.Models;
using GuildGame.Services;

namespace GuildGame.ViewModels;

public class MerchantViewModel : ViewModelBase
{
    private readonly GuildState _state;
    private readonly GameEngine _engine;
    private readonly Action<string> _pushLog;

    public ObservableCollection<HeroOffer> Offers { get; }
    public HeroOffer? SelectedOffer { get; set; }

    public int Or => _state.Or;
    public int Nourriture => _state.Nourriture;
    public int Soins => _state.ObjetsSoin;
    public int WeaponKits => _state.WeaponKits;
    public int ArmorKits => _state.ArmorKits;

    public int InfirmerieLvl => _state.UpgradeLevel(GuildUpgradeType.Infirmerie);
    public int CuisineLvl => _state.UpgradeLevel(GuildUpgradeType.Cuisine);
    public int EntrainementLvl => _state.UpgradeLevel(GuildUpgradeType.SalleEntrainement);
    public int EntrepotLvl => _state.UpgradeLevel(GuildUpgradeType.Entrepot);

    public ICommand BuyFoodCommand { get; }
    public ICommand BuyHealItemCommand { get; }
    public ICommand BuyWeaponKitCommand { get; }
    public ICommand BuyArmorKitCommand { get; }

    public ICommand RecruitCommand { get; }
    public ICommand RefreshOffersCommand { get; }

    public ICommand UpgradeInfirmerieCommand { get; }
    public ICommand UpgradeCuisineCommand { get; }
    public ICommand UpgradeTrainingCommand { get; }
    public ICommand UpgradeEntrepotCommand { get; }

    public string CostsInfo
        => $"Upgrades (coût actuel) : Infirmerie {_engine.UpgradeCost(GuildUpgradeType.Infirmerie)} | Cuisine {_engine.UpgradeCost(GuildUpgradeType.Cuisine)} | Entraînement {_engine.UpgradeCost(GuildUpgradeType.SalleEntrainement)} | Entrepôt {_engine.UpgradeCost(GuildUpgradeType.Entrepot)}";

    public MerchantViewModel(GuildState state, GameEngine engine, Action<string> pushLog)
    {
        _state = state;
        _engine = engine;
        _pushLog = pushLog;

        Offers = new ObservableCollection<HeroOffer>(_state.HeroOffers);

        BuyFoodCommand = new RelayCommand(_ =>
        {
            if (_state.Or < 5) { _pushLog("Pas assez d’or."); return; }
            _state.Or -= 5; _state.Nourriture += 5;
            _pushLog("Achat: +5 nourriture (5 or).");
            Refresh();
        });

        BuyHealItemCommand = new RelayCommand(_ =>
        {
            if (_state.Or < 8) { _pushLog("Pas assez d’or."); return; }
            _state.Or -= 8; _state.ObjetsSoin += 1;
            _pushLog("Achat: +1 objet de soin (8 or).");
            Refresh();
        });

        BuyWeaponKitCommand = new RelayCommand(_ =>
        {
            if (_state.Or < 10) { _pushLog("Pas assez d’or."); return; }
            _state.Or -= 10; _state.WeaponKits += 1;
            _pushLog("Achat: +1 kit arme (10 or).");
            Refresh();
        });

        BuyArmorKitCommand = new RelayCommand(_ =>
        {
            if (_state.Or < 10) { _pushLog("Pas assez d’or."); return; }
            _state.Or -= 10; _state.ArmorKits += 1;
            _pushLog("Achat: +1 kit armure (10 or).");
            Refresh();
        });

        RecruitCommand = new RelayCommand(_ =>
        {
            if (SelectedOffer == null) { _pushLog("Choisis une offre de héros."); return; }
            _engine.TryRecruitOffer(SelectedOffer, out var msg);
            _pushLog(msg);
            RefreshOffers();
            Refresh();
        });

        RefreshOffersCommand = new RelayCommand(_ =>
        {
            if (_state.Or < 2) { _pushLog("Pas assez d’or pour rafraîchir (2 or)."); return; }
            _state.Or -= 2;
            _engine.RefreshMerchantOffers();
            _pushLog("Offres rafraîchies.");
            RefreshOffers();
            Refresh();
        });

        UpgradeInfirmerieCommand = new RelayCommand(_ => BuyUpgrade(GuildUpgradeType.Infirmerie));
        UpgradeCuisineCommand = new RelayCommand(_ => BuyUpgrade(GuildUpgradeType.Cuisine));
        UpgradeTrainingCommand = new RelayCommand(_ => BuyUpgrade(GuildUpgradeType.SalleEntrainement));
        UpgradeEntrepotCommand = new RelayCommand(_ => BuyUpgrade(GuildUpgradeType.Entrepot));
    }

    private void BuyUpgrade(GuildUpgradeType t)
    {
        _engine.TryBuyGuildUpgrade(t, out var msg);
        _pushLog(msg);
        Refresh();
    }

    private void RefreshOffers()
    {
        Offers.Clear();
        foreach (var o in _state.HeroOffers) Offers.Add(o);
        OnPropertyChanged(nameof(Offers));
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Or));
        OnPropertyChanged(nameof(Nourriture));
        OnPropertyChanged(nameof(Soins));
        OnPropertyChanged(nameof(WeaponKits));
        OnPropertyChanged(nameof(ArmorKits));

        OnPropertyChanged(nameof(InfirmerieLvl));
        OnPropertyChanged(nameof(CuisineLvl));
        OnPropertyChanged(nameof(EntrainementLvl));
        OnPropertyChanged(nameof(EntrepotLvl));
        OnPropertyChanged(nameof(CostsInfo));
    }
}
