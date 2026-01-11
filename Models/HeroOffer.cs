namespace GuildGame.Models;

public class HeroOffer
{
    public Hero Hero { get; private set; }
    public int Price { get; private set; }

    public HeroOffer(Hero hero, int price)
    {
        Hero = hero;
        Price = price;
    }
}
