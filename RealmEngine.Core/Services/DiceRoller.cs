using System.Text.RegularExpressions;

namespace RealmEngine.Core.Services;

/// <summary>
/// Utility for rolling dice using DnD-style notation (e.g., "2d6", "1d8+3", "4d10-2").
/// </summary>
public static class DiceRoller
{
    private static readonly Regex DicePattern = new(@"^(\d+)d(\d+)([\+\-]\d+)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Rolls dice based on a dice string (e.g., "2d6", "1d20+5", "3d8-2").
    /// </summary>
    /// <param name="diceString">Dice notation string</param>
    /// <returns>Total result of the dice roll</returns>
    public static int RollDiceString(string diceString)
    {
        if (string.IsNullOrWhiteSpace(diceString))
            return 0;

        var match = DicePattern.Match(diceString.Trim());
        if (!match.Success)
            return 0;

        int count = int.Parse(match.Groups[1].Value);
        int sides = int.Parse(match.Groups[2].Value);
        int modifier = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;

        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += Random.Shared.Next(1, sides + 1);
        }

        return Math.Max(0, total + modifier);
    }

    /// <summary>
    /// Rolls a single die with the specified number of sides.
    /// </summary>
    /// <param name="sides">The number of sides on the die.</param>
    /// <returns>The result of the die roll.</returns>
    public static int Roll(int sides)
    {
        return Random.Shared.Next(1, sides + 1);
    }

    /// <summary>
    /// Rolls multiple dice and returns the total.
    /// </summary>
    /// <param name="count">The number of dice to roll.</param>
    /// <param name="sides">The number of sides on each die.</param>
    /// <returns>The total of all dice rolls.</returns>
    public static int Roll(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
        {
            total += Random.Shared.Next(1, sides + 1);
        }
        return total;
    }
}
