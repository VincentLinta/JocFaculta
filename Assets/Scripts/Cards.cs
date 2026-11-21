using UnityEngine;

/// <summary>Special-power cards the player can find in cabinets. Add new cards here.</summary>
public enum CardType { Fortify, Escape }

public static class CardInfo
{
    public static string Name(CardType t)
    {
        switch (t)
        {
            case CardType.Fortify: return "FORTIFY";
            case CardType.Escape: return "ESCAPE";
            default: return t.ToString().ToUpper();
        }
    }

    public static string Description(CardType t)
    {
        switch (t)
        {
            case CardType.Fortify: return "the clown searches your hiding spot much slower";
            case CardType.Escape: return "opens a random exit when you hide; Q = run to it, but the clown rushes you";
            default: return "";
        }
    }

    public static Color Tint(CardType t)
    {
        switch (t)
        {
            case CardType.Fortify: return new Color(0.45f, 0.8f, 1f);
            case CardType.Escape: return new Color(1f, 0.65f, 0.25f);
            default: return Color.white;
        }
    }
}
