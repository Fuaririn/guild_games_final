using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GuildGame.Models;
using GuildGame.Services;

namespace GuildGame.ViewModels;

public class ResourceViewModel : ViewModelBase
{
    private readonly GuildState _state;
    private readonly GameEngine _engine;
    private readonly Action _onChanged;

    public ObservableCollection<Hero> Heros { get; }
    public Hero? SelectedHero { get; set; }

    public int Or => _state.Or;
    public int Nourriture => _state.Nourriture;
    public int Soins => _state.ObjetsSoin;
    public int WeaponKits => _state.WeaponKits;
    public int ArmorKits => _state.ArmorKits;

    public string ActionMessage { get; private set; } = "";

    public ICommand HealCommand { get; }
    public ICommand RestCommand { get; }
    public ICommand DismissCommand { get; }
    public ICommand EquipWeaponCommand { get; }
    public ICommand EquipArmorCommand { get; }

    public string SelectedHeroInfo
        => SelectedHero == null
            ? "Aucun héros sélectionné."
            : $"{SelectedHero.Nom} | {SelectedHero.Classe} | PV {SelectedHero.Vie}/{SelectedHero.VieMax} | " +
              $"Fatigue {SelectedHero.Fatigue} | Arme {SelectedHero.WeaponLevel} | Armure {SelectedHero.ArmorLevel} | " +
              $"Niveau {SelectedHero.Niveau} | XP {SelectedHero.Experience}/100 (reste {SelectedHero.XpAvantNiveauSuivant}) | " +
              $"Statut {SelectedHero.Statut}";

    public ResourceViewModel(GuildState state, GameEngine engine, Action onChanged)
    {
        _state = state;
        _engine = engine;
        _onChanged = onChanged;

        Heros = new ObservableCollection<Hero>(_state.Heros);

        HealCommand = new RelayCommand(_ =>
        {
            if (SelectedHero == null) return;
            _engine.TryHeal(SelectedHero, out var msg);
            ActionMessage = msg;
            Refresh();
        });

        // ✅ Repos contrôlé par les règles (1 fois / après-midi / pas en mission)
        RestCommand = new RelayCommand(_ =>
        {
            if (SelectedHero == null) return;
            _engine.TryRest(SelectedHero, out var msg);
            ActionMessage = msg;
            Refresh();
        });

        DismissCommand = new RelayCommand(_ =>
        {
            if (SelectedHero == null) return;
            _engine.DismissHero(SelectedHero);
            SelectedHero = null;
            ActionMessage = "Héros renvoyé.";
            Refresh();
        });

        EquipWeaponCommand = new RelayCommand(_ =>
        {
            if (SelectedHero == null) return;
            _engine.TryEquipWeapon(SelectedHero, out var msg);
            ActionMessage = msg;
            Refresh();
        });

        EquipArmorCommand = new RelayCommand(_ =>
        {
            if (SelectedHero == null) return;
            _engine.TryEquipArmor(SelectedHero, out var msg);
            ActionMessage = msg;
            Refresh();
        });
    }

    private void Refresh()
    {
        Heros.Clear();
        foreach (var h in _state.Heros) Heros.Add(h);

        OnPropertyChanged(nameof(Or));
        OnPropertyChanged(nameof(Nourriture));
        OnPropertyChanged(nameof(Soins));
        OnPropertyChanged(nameof(WeaponKits));
        OnPropertyChanged(nameof(ArmorKits));
        OnPropertyChanged(nameof(SelectedHeroInfo));
        OnPropertyChanged(nameof(ActionMessage));

        _onChanged();
    }
}
