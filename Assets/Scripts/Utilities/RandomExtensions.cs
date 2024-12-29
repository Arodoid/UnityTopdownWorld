using System;

public static class RandomExtensions
{
    public static float NextFloat(this Random random)
    {
        return (float)random.NextDouble();
    }

    public static float NextFloat(this Random random, float min, float max)
    {
        return min + ((float)random.NextDouble() * (max - min));
    }
}