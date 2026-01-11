using System;

namespace GuildGame.Models;

public class Hero
{
    public string Nom { get; private set; }
    public HeroClass Classe { get; private set; }

    public int Niveau { get; private set; }
    public int Experience { get; private set; }

    // ✅ XP restant avant le prochain niveau (0..100)
    public int XpAvantNiveauSuivant => Math.Max(0, 100 - Experience);

    public int VieMax { get; private set; }
    public int Vie { get; private set; }

    public int Fatigue { get; private set; } // 0..100
    public HeroStatus Statut { get; private set; }

    public int WeaponLevel { get; private set; } // 0..5
    public int ArmorLevel { get; private set; }  // 0..5

    // ✅ Salaire : lvl1=2, puis +1 par niveau
    public int SalaireParJour => Niveau + 1;

    public Hero(string nom, HeroClass classe)
    {
        Nom = nom;
        Classe = classe;

        Niveau = 1;
        Experience = 0;

        VieMax = 100;
        Vie = 100;

        Fatigue = 0;
        Statut = HeroStatus.Ok;

        WeaponLevel = 0;
        ArmorLevel = 0;
    }

    public bool EstDisponible()
        => Statut != HeroStatus.Mort && Statut != HeroStatus.Grave;

    public int AttackBonus()
    {
        int classBonus = Classe switch
        {
            HeroClass.Guerrier => 2,
            HeroClass.Voleur => 1,
            HeroClass.Mage => 2,
            _ => 0
        };

        return classBonus + WeaponLevel * 2 + Niveau;
    }

    public int DefenseBonus()
    {
        int classBonus = Classe switch
        {
            HeroClass.Guerrier => 2,
            HeroClass.Voleur => 1,
            HeroClass.Mage => 0,
            _ => 0
        };

        return classBonus + ArmorLevel * 2;
    }

    public void UpgradeWeapon() => WeaponLevel = Math.Clamp(WeaponLevel + 1, 0, 5);
    public void UpgradeArmor() => ArmorLevel = Math.Clamp(ArmorLevel + 1, 0, 5);

    public void AjouterFatigue(int amount)
    {
        Fatigue = Math.Clamp(Fatigue + amount, 0, 100);
        RecalculerStatut();
    }

    public void PrendreDegats(int amount)
    {
        Vie = Math.Clamp(Vie - Math.Max(0, amount), 0, VieMax);
        RecalculerStatut();
    }

    public void Soigner(int amount)
    {
        if (Statut == HeroStatus.Mort) return;
        Vie = Math.Clamp(Vie + Math.Max(0, amount), 0, VieMax);
        RecalculerStatut();
    }

    public void Reposer()
    {
        if (Statut == HeroStatus.Mort) return;
        AjouterFatigue(-30);
        Soigner(12);
    }

    public void GagnerXP(int amount)
    {
        if (Statut == HeroStatus.Mort) return;

        Experience += Math.Max(0, amount);
        while (Experience >= 100)
        {
            Experience -= 100;
            Niveau++;
            VieMax += 10;
            Vie = VieMax;
        }
    }

    private void RecalculerStatut()
    {
        if (Vie <= 0)
        {
            Statut = HeroStatus.Mort;
            return;
        }

        if (Vie < 20) Statut = HeroStatus.Grave;
        else if (Vie < 50) Statut = HeroStatus.Blesse;
        else if (Fatigue > 70) Statut = HeroStatus.Fatigue;
        else Statut = HeroStatus.Ok;
    }
}
