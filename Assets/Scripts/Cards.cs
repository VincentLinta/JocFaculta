using UnityEngine;

/// <summary>Special-power cards the player can find in cabinets. Add new cards here.</summary>
public enum CardType { Fortify, Escape, FightBack, Decoy, Girlfriend, Flashbang }

public static class CardInfo
{
    public static string Name(CardType t)
    {
        switch (t)
        {
            case CardType.Fortify: return "FORTIFY";
            case CardType.Escape: return "ESCAPE";
            case CardType.FightBack: return "FIGHT BACK";
            case CardType.Decoy: return "DECOY";
            case CardType.Girlfriend: return "GIRLFRIEND";
            case CardType.Flashbang: return "FLASHBANG";
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
            case CardType.Decoy: return "F at a hiding spot places it; hide, press F again and the clown goes there first";
            case CardType.Girlfriend: return "F places her as a trap anywhere (not next to a hiding spot); if the clown walks past her he is stunned for 2s while she tells him off";
            case CardType.Flashbang: return "G when hidden and the clown is at your spot or close: he is blinded for a few seconds and you run to another hiding spot";
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
            case CardType.Decoy: return new Color(0.75f, 0.95f, 0.45f);
            case CardType.Girlfriend: return new Color(1f, 0.55f, 0.78f);
            case CardType.Flashbang: return new Color(1f, 0.98f, 0.6f);
            default: return Color.white;
        }
    }
}
