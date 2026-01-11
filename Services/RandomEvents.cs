using System;
using GuildGame.Models;

namespace GuildGame.Services;

public class RandomEventResult
{
    public string Message { get; set; } = "";
    public int GoldDelta { get; set; }
    public int FoodDelta { get; set; }
    public int HealItemsDelta { get; set; }
    public int WeaponKitDelta { get; set; }
    public int ArmorKitDelta { get; set; }

    public int DamageToHero { get; set; }
    public int FatigueToHero { get; set; }

    public bool ForceFail { get; set; }
}

public static class RandomEvents
{
    private static readonly string[] Discoveries =
    {
        "Découverte d’un coffre !",
        "Trouvaille de provisions sur la route.",
        "Un voyageur offre une récompense."
    };

    private static readonly string[] Ambushes =
    {
        "Embuscade de bandits !",
        "Attaque surprise dans un passage étroit.",
        "Piège déclenché dans les ruines."
    };

    public static RandomEventResult? Roll(Random rng, Hero hero, Mission mission)
    {
        // ✅ 1 mission sur 4 (25%) => événement
        // Si on veut EXACTEMENT 1/4, on ne dépend pas de la difficulté.
        if (rng.Next(4) != 0)
            return null;

        bool isDiscovery = rng.NextDouble() < 0.60;

        if (isDiscovery)
        {
            int gold = rng.Next(2, 7) + mission.Difficulte; // petit bonus selon difficulté
            int food = rng.Next(0, 2); // 0 ou 1 (cohérent avec ta règle nourriture faible)
            int kitRoll = rng.Next(0, 100);

            var res = new RandomEventResult
            {
                Message = $"{Discoveries[rng.Next(Discoveries.Length)]} +{gold} or, +{food} nourriture.",
                GoldDelta = gold,
                FoodDelta = food,
                HealItemsDelta = (kitRoll < 8) ? 1 : 0,
                WeaponKitDelta = (kitRoll >= 8 && kitRoll < 12) ? 1 : 0,
                ArmorKitDelta = (kitRoll >= 12 && kitRoll < 16) ? 1 : 0,
                FatigueToHero = 4
            };

            if (res.HealItemsDelta > 0) res.Message += " +1 objet de soin !";
            if (res.WeaponKitDelta > 0) res.Message += " +1 kit arme !";
            if (res.ArmorKitDelta > 0) res.Message += " +1 kit armure !";

            return res;
        }

        int dmg = rng.Next(6, 16) + mission.Difficulte;
        int fat = rng.Next(6, 12);

        return new RandomEventResult
        {
            Message = $"{Ambushes[rng.Next(Ambushes.Length)]} {hero.Nom} subit -{dmg} PV.",
            DamageToHero = dmg,
            FatigueToHero = fat,
            ForceFail = rng.NextDouble() < 0.20
        };
    }
}
