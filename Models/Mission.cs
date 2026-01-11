using System;

namespace GuildGame.Models;

public class Mission
{
    public string Nom { get; private set; }
    public MissionType Type { get; private set; }
    public MissionPeriod Period { get; private set; }

    public int Difficulte { get; private set; } // 1..10
    public int DureePhases { get; private set; } // 1..3

    public int RecompenseOr { get; private set; }

    // ✅ La mission ne peut rapporter qu'au maximum 1 nourriture
    public int RecompenseNourriture { get; private set; } // 0..1

    // ✅ Coût de nourriture au départ
    // Escorte et Donjon coûtent 2, sinon 0
    public int CoutNourriture { get; private set; } // 0 ou 2

    public Hero? Assigne { get; private set; }
    public int PhasesRestantes { get; private set; }

    public bool EnCours => Assigne != null && PhasesRestantes > 0;

    // Pour expliquer les échecs "forcés"
    public bool ForcedFailure { get; private set; }
    public string ForcedFailureReason { get; private set; } = "";

    public Mission(string nom, MissionType type, MissionPeriod period, int difficulte, int dureePhases, int or, int nourriture)
    {
        Nom = nom;
        Type = type;
        Period = period;

        Difficulte = Math.Clamp(difficulte, 1, 10);
        DureePhases = Math.Clamp(dureePhases, 1, 3);

        RecompenseOr = Math.Max(0, or);
        RecompenseNourriture = Math.Clamp(nourriture, 0, 1);

        // ✅ Règle : Escorte + Donjon coûtent 2 nourriture
        CoutNourriture = (Type == MissionType.Escorte || Type == MissionType.Donjon) ? 2 : 0;

        PhasesRestantes = 0;
        ForcedFailure = false;
        ForcedFailureReason = "";
    }

    public bool PeutAssigner(Hero hero)
        => hero.EstDisponible() && Assigne == null;

    public void Assigner(Hero hero)
    {
        Assigne = hero;
        PhasesRestantes = DureePhases;

        ForcedFailure = false;
        ForcedFailureReason = "";
    }

    public void TickPhase()
    {
        if (Assigne == null) return;
        PhasesRestantes = Math.Max(0, PhasesRestantes - 1);
    }

    public void MarkForcedFailure(string reason)
    {
        ForcedFailure = true;
        ForcedFailureReason = reason;
    }
}
