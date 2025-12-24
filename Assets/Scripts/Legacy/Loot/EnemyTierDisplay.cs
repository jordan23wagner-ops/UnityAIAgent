using System;

public static class EnemyTierDisplay
{
    public static string ToDisplayString(this EnemyTier tier)
    {
        return tier switch
        {
            EnemyTier.Trash => "Trash Mob",
            _ => tier.ToString()
        };
    }
}
