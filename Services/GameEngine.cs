using System;
using System.Linq;
using GuildGame.Models;

namespace GuildGame.Services;

public class GameEngine
{
    private readonly Random _rng = new Random();
    private readonly GuildState _state;

    private int _missionRunId = 1;

    // Repos : 1 fois par après-midi
    private bool _restUsedThisAfternoon = false;

    public GuildState State => _state;

    public bool GameOver { get; private set; }
    public string LastGameOverReason { get; private set; } = "";

    public GameEngine(GuildState state)
    {
        _state = state;
    }

    // ============================================================
    //  PHASES : Résolution "entre" chaque phase (transition)
    // ============================================================
    public string AdvancePhase()
    {
        if (GameOver) return "La partie est terminée.";

        string log;

        // ✅ On résout ce qui s'est passé PENDANT la phase actuelle,
        // puis on passe à la phase suivante.
        switch (_state.Phase)
        {
            case DayPhase.Matin:
                log = ResolveMorningTransition(); // Matin -> Après-midi
                _state.Phase = DayPhase.ApresMidi;
                _restUsedThisAfternoon = false;   // reset repos quand on entre en après-midi
                break;

            case DayPhase.ApresMidi:
                log = ResolveAfternoonTransition(); // Après-midi -> Soir
                _state.Phase = DayPhase.Soir;
                break;

            case DayPhase.Soir:
                log = ResolveNightTransition(); // Soir -> Matin (retours de nuit)
                _state.Phase = DayPhase.Matin;
                _state.Jour++;
                break;

            default:
                log = "";
                break;
        }

        CheckDefeatConditions();
        return log;
    }

    // ---------------- Transition Matin -> Après-midi ----------------
    // Arrivages entrepôt + salaires + nourriture + marchand + entraînement
    private string ResolveMorningTransition()
    {
        int goldBefore = _state.Or;
        int foodBefore = _state.Nourriture;

        // ✅ Arrivages Entrepôt : lvl0 => 0, sinon +2 nourriture et +1 soin / niveau
        int entrepotLvl = _state.UpgradeLevel(GuildUpgradeType.Entrepot);
        int foodArrival = 2 * entrepotLvl;
        int healArrival = 1 * entrepotLvl;

        _state.Nourriture += foodArrival;
        _state.ObjetsSoin += healArrival;

        // Marchand : offres du jour
        RefreshMerchantOffers();

        // Distribution nourriture + salaires
        int living = _state.Heros.Count(h => h.Statut != HeroStatus.Mort);

        int cuisineLvl = _state.UpgradeLevel(GuildUpgradeType.Cuisine);
        double factor = 1.0 - Math.Clamp(0.08 * cuisineLvl, 0.0, 0.40);
        int foodNeeded = (int)Math.Ceiling(living * factor);

        _state.Nourriture -= foodNeeded;

        int wages = _state.Heros.Where(h => h.Statut != HeroStatus.Mort).Sum(h => h.SalaireParJour);
        var wagesDetails = string.Join(", ",
            _state.Heros.Where(h => h.Statut != HeroStatus.Mort)
                        .Select(h => $"{h.Nom}:{h.SalaireParJour}")
        );

        _state.Or -= wages;

        // XP passif salle d'entraînement
        int trainLvl = _state.UpgradeLevel(GuildUpgradeType.SalleEntrainement);
        if (trainLvl > 0)
        {
            foreach (var h in _state.Heros.Where(h => h.Statut != HeroStatus.Mort))
                h.GagnerXP(2 * trainLvl);
        }

        int goldAfter = _state.Or;
        int foodAfter = _state.Nourriture;

        return
            $"[Transition Matin→Après-midi] " +
            $"Arrivages Entrepôt (lvl {entrepotLvl}): +{foodArrival} nourriture, +{healArrival} soin. " +
            $"Nourriture distribuée: -{foodNeeded} (vivants={living}, Cuisine lvl {cuisineLvl}). " +
            $"Salaires: -{wages} or [{wagesDetails}]. " +
            $"Or: {goldBefore} -> {goldAfter} | Nourriture: {foodBefore} -> {foodAfter}.";
    }

    // ---------------- Transition Après-midi -> Soir ----------------
    // Progression missions + événements + conclusion missions du jour + récup
    private string ResolveAfternoonTransition()
    {
        int events = 0;
        string extra = "";

        // 1) progression missions du jour (durée) + événements
        foreach (var m in _state.MissionsJour.Where(x => x.Assigne != null).ToList())
        {
            if (m.PhasesRestantes > 0)
                m.TickPhase();

            var hero = m.Assigne!;
            hero.AjouterFatigue(10 + m.Difficulte);

            var ev = RandomEvents.Roll(_rng, hero, m); // 1/4 si ton RandomEvents.cs est réglé
            if (ev != null)
            {
                events++;
                ApplyEvent(ev, hero);

                if (ev.ForceFail)
                    m.MarkForcedFailure("Échec forcé par événement : " + ev.Message);

                extra += $" | {hero.Nom}: {ev.Message}";
            }
        }

        string midLog = events == 0
            ? "Missions de jour en cours."
            : $"Missions + événements:{extra}";

        // 2) conclusion des missions du jour (succès/échecs) + gains
        string dayConclusion = ResolveDayMissionsNow();

        // 3) récup / infirmerie (avant d'arriver au soir)
        int inf = _state.UpgradeLevel(GuildUpgradeType.Infirmerie);
        foreach (var h in _state.Heros.Where(h => h.Statut != HeroStatus.Mort))
        {
            h.AjouterFatigue(-6 - inf);
            if (inf > 0) h.Soigner(2 * inf);
        }

        return $"[Transition Après-midi→Soir] {midLog} | Conclusion: {dayConclusion} | Récup/Infirmerie appliquées.";
    }

    // ---------------- Transition Soir -> Matin ----------------
    // Nuit qui passe : missions de nuit reviennent le matin
    private string ResolveNightTransition()
    {
        string nightReturn = ResolveNightMissionsNow();
        return $"[Transition Soir→Matin] {nightReturn}";
    }

    // ============================================================
    //  REPOS (après-midi uniquement, 1 fois, seulement si pas en mission)
    // ============================================================
    public bool TryRest(Hero hero, out string message)
    {
        if (_state.Phase != DayPhase.ApresMidi)
        {
            message = "Repos disponible uniquement l’après-midi.";
            return false;
        }

        if (_restUsedThisAfternoon)
        {
            message = "Le repos a déjà été utilisé ce tour (cet après-midi).";
            return false;
        }

        if (hero.Statut == HeroStatus.Mort)
        {
            message = "Impossible : héros mort.";
            return false;
        }

        if (IsHeroAssignedToAnyMission(hero))
        {
            message = "Impossible : ce héros a une mission assignée (jour ou nuit).";
            return false;
        }

        hero.Reposer();
        _restUsedThisAfternoon = true;
        message = $"{hero.Nom} se repose (fatigue -30, soin +12).";
        return true;
    }

    private bool IsHeroAssignedToAnyMission(Hero hero)
        => _state.MissionsJour.Any(m => m.Assigne == hero)
           || _state.MissionsNuit.Any(m => m.Assigne == hero);

    // ============================================================
    //  MISSIONS (instances) : plusieurs héros peuvent prendre la même mission
    //  + missions de nuit reviennent le matin (durée forcée à 1)
    // ============================================================
    private Mission CreateMissionInstance(Mission template)
    {
        int duree = template.Period == MissionPeriod.Nuit ? 1 : template.DureePhases;

        return new Mission(
            nom: $"{template.Nom} #{_missionRunId++}",
            type: template.Type,
            period: template.Period,
            difficulte: template.Difficulte,
            dureePhases: duree,
            or: template.RecompenseOr,
            nourriture: template.RecompenseNourriture
        );
    }

    public bool TryAssignDayMission(Hero hero, Mission mission, out string message)
    {
        if (_state.Phase != DayPhase.Matin)
        {
            message = "Les missions de jour se lancent le matin.";
            return false;
        }
        if (mission.Period != MissionPeriod.Jour)
        {
            message = "Ce n’est pas une mission de jour.";
            return false;
        }
        if (!hero.EstDisponible())
        {
            message = "Héros indisponible (grave ou mort).";
            return false;
        }
        if (IsHeroAssignedToAnyMission(hero))
        {
            message = "Ce héros a déjà une mission assignée.";
            return false;
        }

        var run = CreateMissionInstance(mission);

        if (_state.Nourriture < run.CoutNourriture)
        {
            message = $"Pas assez de nourriture pour lancer cette mission (coût {run.CoutNourriture}).";
            return false;
        }

        _state.Nourriture -= run.CoutNourriture;

        run.Assigner(hero);
        _state.MissionsJour.Add(run);

        message = $"{hero.Nom} part sur {run.Nom} (Jour). Coût nourriture: -{run.CoutNourriture}.";
        return true;
    }

    public bool TryAssignNightMission(Hero hero, Mission mission, out string message)
    {
        if (_state.Phase != DayPhase.Soir)
        {
            message = "Les missions nocturnes se lancent le soir.";
            return false;
        }
        if (mission.Period != MissionPeriod.Nuit)
        {
            message = "Ce n’est pas une mission nocturne.";
            return false;
        }
        if (!hero.EstDisponible())
        {
            message = "Héros indisponible (grave ou mort).";
            return false;
        }
        if (IsHeroAssignedToAnyMission(hero))
        {
            message = "Ce héros a déjà une mission assignée.";
            return false;
        }

        var run = CreateMissionInstance(mission);

        if (_state.Nourriture < run.CoutNourriture)
        {
            message = $"Pas assez de nourriture pour lancer cette mission (coût {run.CoutNourriture}).";
            return false;
        }

        _state.Nourriture -= run.CoutNourriture;

        run.Assigner(hero);
        _state.MissionsNuit.Add(run);

        message = $"{hero.Nom} part sur {run.Nom} (Nuit). Coût nourriture: -{run.CoutNourriture}.";
        return true;
    }

    // ============================================================
    //  ACTIONS
    // ============================================================
    public bool TryHeal(Hero hero, out string message)
    {
        if (_state.ObjetsSoin <= 0)
        {
            message = "Plus d’objets de soin.";
            return false;
        }
        if (hero.Statut == HeroStatus.Mort)
        {
            message = "Impossible de soigner un héros mort.";
            return false;
        }

        int inf = _state.UpgradeLevel(GuildUpgradeType.Infirmerie);
        int healAmount = 30 + inf * 8;

        _state.ObjetsSoin--;
        hero.Soigner(healAmount);

        message = $"{hero.Nom} soigné (+{healAmount}).";
        return true;
    }

    public bool TryEquipWeapon(Hero hero, out string message)
    {
        if (_state.WeaponKits <= 0) { message = "Aucun kit d’arme."; return false; }
        if (hero.Statut == HeroStatus.Mort) { message = "Héros mort."; return false; }
        if (hero.WeaponLevel >= 5) { message = "Arme déjà au max."; return false; }

        _state.WeaponKits--;
        hero.UpgradeWeapon();
        message = $"{hero.Nom} améliore son arme (niveau {hero.WeaponLevel}).";
        return true;
    }

    public bool TryEquipArmor(Hero hero, out string message)
    {
        if (_state.ArmorKits <= 0) { message = "Aucun kit d’armure."; return false; }
        if (hero.Statut == HeroStatus.Mort) { message = "Héros mort."; return false; }
        if (hero.ArmorLevel >= 5) { message = "Armure déjà au max."; return false; }

        _state.ArmorKits--;
        hero.UpgradeArmor();
        message = $"{hero.Nom} améliore son armure (niveau {hero.ArmorLevel}).";
        return true;
    }

    public void DismissHero(Hero hero) => _state.Heros.Remove(hero);

    // ============================================================
    //  MARCHAND / UPGRADES
    // ============================================================
    public void RefreshMerchantOffers()
    {
        _state.HeroOffers.Clear();
        for (int i = 0; i < 3; i++)
        {
            var h = new Hero(RandomName(), (HeroClass)_rng.Next(0, 3));
            int price = 18 + _rng.Next(0, 10);
            _state.HeroOffers.Add(new HeroOffer(h, price));
        }
    }

    public bool TryRecruitOffer(HeroOffer offer, out string message)
    {
        if (_state.Or < offer.Price)
        {
            message = "Pas assez d’or.";
            return false;
        }

        _state.Or -= offer.Price;
        _state.Heros.Add(offer.Hero);
        _state.HeroOffers.Remove(offer);

        message = $"Recruté : {offer.Hero.Nom} ({offer.Hero.Classe}) pour {offer.Price} or.";
        return true;
    }

    public int UpgradeCost(GuildUpgradeType t)
    {
        int lvl = _state.UpgradeLevel(t);
        int baseCost = t switch
        {
            GuildUpgradeType.Cuisine => 20,
            GuildUpgradeType.Infirmerie => 25,
            GuildUpgradeType.SalleEntrainement => 30,
            GuildUpgradeType.Entrepot => 18,
            _ => 25
        };
        return baseCost + lvl * (baseCost / 2);
    }

    public bool TryBuyGuildUpgrade(GuildUpgradeType t, out string message)
    {
        int cost = UpgradeCost(t);
        if (_state.Or < cost)
        {
            message = "Pas assez d’or.";
            return false;
        }

        _state.Or -= cost;
        _state.LevelUp(t);
        message = $"Upgrade acheté : {t} (niveau {_state.UpgradeLevel(t)}).";
        return true;
    }

    // ============================================================
    //  RESOLUTION + RAISONS D'ÉCHEC
    // ============================================================
    private string ResolveDayMissionsNow()
    {
        int gainOr = 0, gainFood = 0, xpTotal = 0, fails = 0;
        string failDetails = "";

        foreach (var m in _state.MissionsJour.Where(x => x.Assigne != null).ToList())
        {
            if (m.PhasesRestantes > 0) continue;

            var hero = m.Assigne!;
            bool success = RollSuccessWithReason(hero, m, out string reason, out string rollInfo);

            if (success)
            {
                gainOr += m.RecompenseOr;
                gainFood += m.RecompenseNourriture;

                int xp = 18 + m.Difficulte * 6;
                hero.GagnerXP(xp);
                xpTotal += xp;
            }
            else
            {
                fails++;
                int dmg = 10 + m.Difficulte * 2 - hero.DefenseBonus();
                hero.PrendreDegats(Math.Max(0, dmg));
                hero.AjouterFatigue(10);

                failDetails += $" | ÉCHEC {hero.Nom} sur {m.Nom}: {reason} ({rollInfo})";
            }

            _state.MissionsJour.Remove(m);
        }

        _state.Or += gainOr;
        _state.Nourriture += gainFood;

        if (gainOr == 0 && gainFood == 0 && fails == 0) return "Aucune mission de jour conclue.";
        return $"+{gainOr} or, +{gainFood} nourriture, XP {xpTotal}, échecs {fails}.{failDetails}";
    }

    private string ResolveNightMissionsNow()
    {
        int gainOr = 0, gainFood = 0, xpTotal = 0, fails = 0, count = 0;
        string failDetails = "";

        foreach (var m in _state.MissionsNuit.Where(x => x.Assigne != null).ToList())
        {
            count++;

            if (m.PhasesRestantes > 0)
                m.TickPhase();

            if (m.PhasesRestantes > 0) continue;

            var hero = m.Assigne!;
            hero.AjouterFatigue(14 + m.Difficulte);

            var ev = RandomEvents.Roll(_rng, hero, m);
            if (ev != null)
            {
                ApplyEvent(ev, hero);
                if (ev.ForceFail)
                    m.MarkForcedFailure("Échec forcé par événement : " + ev.Message);
            }

            bool success = RollSuccessWithReason(hero, m, out string reason, out string rollInfo);

            if (success)
            {
                gainOr += m.RecompenseOr;
                gainFood += m.RecompenseNourriture;

                int xp = 22 + m.Difficulte * 7;
                hero.GagnerXP(xp);
                xpTotal += xp;
            }
            else
            {
                fails++;
                int dmg = 12 + m.Difficulte * 3 - hero.DefenseBonus();
                hero.PrendreDegats(Math.Max(0, dmg));
                hero.AjouterFatigue(12);

                failDetails += $" | ÉCHEC {hero.Nom} sur {m.Nom}: {reason} ({rollInfo})";
            }

            _state.MissionsNuit.Remove(m);
        }

        if (count == 0) return "Aucun retour nocturne.";

        _state.Or += gainOr;
        _state.Nourriture += gainFood;
        return $"Retours nocturnes: {count} missions. +{gainOr} or, +{gainFood} nourriture, XP {xpTotal}, échecs {fails}.{failDetails}";
    }

    private bool RollSuccessWithReason(Hero hero, Mission mission, out string reason, out string rollInfo)
    {
        if (hero.Statut == HeroStatus.Mort)
        {
            reason = "Héros mort pendant la mission";
            rollInfo = "pas de jet";
            return false;
        }

        if (mission.ForcedFailure)
        {
            reason = mission.ForcedFailureReason;
            rollInfo = "échec forcé";
            return false;
        }

        // auto-win si niveau == difficulté
        if (hero.Niveau == mission.Difficulte)
        {
            reason = "Réussite automatique (niveau = difficulté)";
            rollInfo = "auto";
            return true;
        }

        int fatiguePenalty = hero.Fatigue / 20;
        int bonus = hero.AttackBonus();
        int roll = _rng.Next(1, 21) + bonus - fatiguePenalty;
        int target = 12 + mission.Difficulte + (mission.Period == MissionPeriod.Nuit ? 1 : 0);

        rollInfo = $"jet={roll} cible={target} (d20+bonus={bonus}-fatigue={fatiguePenalty})";

        if (roll >= target)
        {
            reason = "Jet suffisant";
            return true;
        }

        reason = "Jet insuffisant";
        return false;
    }

    private void ApplyEvent(RandomEventResult ev, Hero hero)
    {
        _state.Or += ev.GoldDelta;
        _state.Nourriture += ev.FoodDelta;
        _state.ObjetsSoin += ev.HealItemsDelta;
        _state.WeaponKits += ev.WeaponKitDelta;
        _state.ArmorKits += ev.ArmorKitDelta;

        if (ev.DamageToHero > 0)
        {
            int reduced = Math.Max(0, ev.DamageToHero - hero.DefenseBonus());
            hero.PrendreDegats(reduced);
        }

        if (ev.FatigueToHero != 0)
            hero.AjouterFatigue(ev.FatigueToHero);
    }

    // ============================================================
    //  DEFAITE
    // ============================================================
    public void CheckDefeatConditions()
    {
        bool allDown = _state.Heros.Count == 0
                       || _state.Heros.All(h => h.Statut == HeroStatus.Mort || h.Statut == HeroStatus.Grave);

        if (allDown)
        {
            GameOver = true;
            LastGameOverReason = "Tous les héros sont morts ou gravement blessés.";
            return;
        }

        if (_state.Nourriture < 0)
        {
            GameOver = true;
            LastGameOverReason = "Réserves de nourriture insuffisantes.";
            return;
        }

        if (_state.Or < -60)
        {
            GameOver = true;
            LastGameOverReason = "Dettes trop élevées.";
            return;
        }
    }

    private string RandomName()
    {
        string[] names = { "Ari", "Mira", "Khan", "Lysa", "Toren", "Sable", "Nox", "Iris", "Bram", "Yuna" };
        return names[_rng.Next(0, names.Length)];
    }
}
