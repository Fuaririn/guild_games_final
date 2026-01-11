using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using GuildGame.Models;
using GuildGame.Services;

namespace GuildGame.ViewModels;

public class MainViewModel : ViewModelBase
{
    public GuildState State { get; private set; }
    public GameEngine Engine { get; private set; }

    public int Jour => State.Jour;
    public DayPhase Phase => State.Phase;
    public int Or => State.Or;
    public int Nourriture => State.Nourriture;
    public int Soins => State.ObjetsSoin;
    public int WeaponKits => State.WeaponKits;
    public int ArmorKits => State.ArmorKits;

    public ObservableCollection<Hero> Heros { get; private set; }
    public ObservableCollection<Mission> MissionsJour { get; private set; }
    public ObservableCollection<Mission> MissionsNuit { get; private set; }

    public Hero? SelectedHero { get; set; }
    public Mission? SelectedDayMission { get; set; }
    public Mission? SelectedNightMission { get; set; }

    public ObservableCollection<string> Log { get; private set; }

    public ICommand NextPhaseCommand { get; private set; }
    public ICommand AssignDayCommand { get; private set; }
    public ICommand AssignNightCommand { get; private set; }
    public ICommand OpenResourcesCommand { get; private set; }
    public ICommand OpenMerchantCommand { get; private set; }

    public MainViewModel()
    {
        State = new GuildState();
        Seed(State);

        Engine = new GameEngine(State);
        Engine.RefreshMerchantOffers();

        Heros = new ObservableCollection<Hero>(State.Heros);
        MissionsJour = new ObservableCollection<Mission>(State.MissionsJour);
        MissionsNuit = new ObservableCollection<Mission>(State.MissionsNuit);

        Log = new ObservableCollection<string>();
        Log.Insert(0, $"Départ: {State.Or} or, {State.Nourriture} nourriture. 1 héros au hasard.");

        NextPhaseCommand = new RelayCommand(_ =>
        {
            string msg = Engine.AdvancePhase();
            Log.Insert(0, msg);

            if (Engine.GameOver)
                Log.Insert(0, $"=== DÉFAITE === {Engine.LastGameOverReason}");

            RefreshCollections();
            RaiseAll();
        });

        AssignDayCommand = new RelayCommand(_ =>
        {
            if (SelectedHero == null || SelectedDayMission == null)
            {
                Log.Insert(0, "Choisis un héros ET une mission de jour.");
                return;
            }

            if (Engine.TryAssignDayMission(SelectedHero, SelectedDayMission, out var m))
                Log.Insert(0, m);
            else
                Log.Insert(0, $"Impossible: {m}");

            RefreshCollections();
            RaiseAll();
        });

        AssignNightCommand = new RelayCommand(_ =>
        {
            if (SelectedHero == null || SelectedNightMission == null)
            {
                Log.Insert(0, "Choisis un héros ET une mission nocturne.");
                return;
            }

            if (Engine.TryAssignNightMission(SelectedHero, SelectedNightMission, out var m))
                Log.Insert(0, m);
            else
                Log.Insert(0, $"Impossible: {m}");

            RefreshCollections();
            RaiseAll();
        });

        OpenResourcesCommand = new RelayCommand(_ =>
        {
            var win = new Views.ResourceWindow
            {
                DataContext = new ResourceViewModel(State, Engine, () =>
                {
                    RefreshCollections();
                    RaiseAll();
                })
            };
            win.Show();
        });

        OpenMerchantCommand = new RelayCommand(_ =>
        {
            var win = new Views.MerchantWindow
            {
                DataContext = new MerchantViewModel(State, Engine, (s) =>
                {
                    Log.Insert(0, s);
                    RefreshCollections();
                    RaiseAll();
                })
            };
            win.Show();
        });
    }

    private void Seed(GuildState s)
    {
        // ✅ 1 seul héros, classe aléatoire
        var rng = new Random();
        var cls = (HeroClass)rng.Next(0, 3);
        var hero = new Hero("Aventurier", cls);
        s.Heros.Add(hero);

        // Templates de missions (la nourriture rapportée est clampée à 0..1 dans Mission)
        s.MissionsJour.Add(new Mission("Chasse en forêt", MissionType.Chasse, MissionPeriod.Jour, 2, 1, 8, 1));
        s.MissionsJour.Add(new Mission("Escorte caravane", MissionType.Escorte, MissionPeriod.Jour, 4, 2, 18, 0));
        s.MissionsJour.Add(new Mission("Petit donjon", MissionType.Donjon, MissionPeriod.Jour, 6, 2, 30, 1));
        s.MissionsJour.Add(new Mission("Récolte d’herbes", MissionType.Recolte, MissionPeriod.Jour, 1, 1, 4, 1));

        s.MissionsNuit.Add(new Mission("Patrouille de nuit", MissionType.Escorte, MissionPeriod.Nuit, 3, 1, 10, 0));
        s.MissionsNuit.Add(new Mission("Traque de bêtes", MissionType.Chasse, MissionPeriod.Nuit, 5, 1, 22, 1));
        s.MissionsNuit.Add(new Mission("Infiltration", MissionType.Donjon, MissionPeriod.Nuit, 7, 1, 35, 0));

        // petit message de départ
        // (le journal est géré dans le ViewModel, donc pas ici)
    }

    private void RefreshCollections()
    {
        Heros.Clear();
        foreach (var h in State.Heros) Heros.Add(h);

        MissionsJour.Clear();
        foreach (var m in State.MissionsJour) MissionsJour.Add(m);

        MissionsNuit.Clear();
        foreach (var m in State.MissionsNuit) MissionsNuit.Add(m);
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Jour));
        OnPropertyChanged(nameof(Phase));
        OnPropertyChanged(nameof(Or));
        OnPropertyChanged(nameof(Nourriture));
        OnPropertyChanged(nameof(Soins));
        OnPropertyChanged(nameof(WeaponKits));
        OnPropertyChanged(nameof(ArmorKits));

        OnPropertyChanged(nameof(Heros));
        OnPropertyChanged(nameof(MissionsJour));
        OnPropertyChanged(nameof(MissionsNuit));

        OnPropertyChanged(nameof(SelectedHero));
        OnPropertyChanged(nameof(SelectedDayMission));
        OnPropertyChanged(nameof(SelectedNightMission));
    }
}
