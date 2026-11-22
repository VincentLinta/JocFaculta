using UnityEngine;

/// <summary>Special-power cards the player can find in cabinets. Add new cards here.</summary>
public enum CardType { Fortify, Escape, FightBack }

public static class CardInfo
{
    public static string Name(CardType t)
    {
        switch (t)
        {
            case CardType.Fortify: return "FORTIFY";
            case CardType.Escape: return "ESCAPE";
            case CardType.FightBack: return "FIGHT BACK";
            default: return t.ToString().ToUpper();
        }
    }

    public static string Description(CardType t)
    {
        switch (t)
        {
            case CardType.Fortify: return "the clown searches your hiding spot much slower";
            case CardType.Escape: return "opens a random exit when you hide; Q = run to it, but the clown rushes you";
            case CardType.FightBack: return "if the clown finds you, you have a 50% chance to beat him and win";
            default: return "";
        }
    }

    public static Color Tint(CardType t)
    {
        switch (t)
        {
            case CardType.Fortify: return new Color(0.45f, 0.8f, 1f);
            case CardType.Escape: return new Color(1f, 0.65f, 0.25f);
            case CardType.FightBack: return new Color(1f, 0.4f, 0.4f);
            default: return Color.white;
        }
    }
}
