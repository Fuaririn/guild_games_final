namespace GuildGame.Models;

public enum HeroClass { Guerrier, Voleur, Mage }
public enum HeroStatus { Ok, Fatigue, Blesse, Grave, Mort }
public enum DayPhase { Matin, ApresMidi, Soir }

public enum MissionType { Chasse, Escorte, Donjon, Recolte }
public enum MissionPeriod { Jour, Nuit }

public enum GuildUpgradeType
{
    Infirmerie,
    Cuisine,
    SalleEntrainement,
    Entrepot
}
