using System.Collections.Generic;

namespace GuildGame.Models;

public class GuildState
{
    public int Jour { get; set; }
    public DayPhase Phase { get; set; }

    public int Or { get; set; }
    public int Nourriture { get; set; }
    public int ObjetsSoin { get; set; }

    public int WeaponKits { get; set; }
    public int ArmorKits { get; set; }

    public Dictionary<GuildUpgradeType, int> Upgrades { get; private set; }

    public List<Hero> Heros { get; private set; }
    public List<Mission> MissionsJour { get; private set; }
    public List<Mission> MissionsNuit { get; private set; }

    public List<HeroOffer> HeroOffers { get; private set; }

    public GuildState()
    {
        Jour = 1;
        Phase = DayPhase.Matin;

        // ✅ Départ minimal demandé
        Or = 10;
        Nourriture = 5;

        // Le reste à 0 (tu n’as demandé que 10/5)
        ObjetsSoin = 0;
        WeaponKits = 0;
        ArmorKits = 0;

        Upgrades = new Dictionary<GuildUpgradeType, int>
        {
            { GuildUpgradeType.Infirmerie, 0 },
            { GuildUpgradeType.Cuisine, 0 },
            { GuildUpgradeType.SalleEntrainement, 0 },
            { GuildUpgradeType.Entrepot, 0 }
        };

        Heros = new List<Hero>();
        MissionsJour = new List<Mission>();
        MissionsNuit = new List<Mission>();
        HeroOffers = new List<HeroOffer>();
    }

    public int UpgradeLevel(GuildUpgradeType t) => Upgrades.TryGetValue(t, out var lvl) ? lvl : 0;

    public void LevelUp(GuildUpgradeType t)
    {
        Upgrades[t] = UpgradeLevel(t) + 1;
    }
}