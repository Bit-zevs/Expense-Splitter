using System.Numerics;

namespace ExpenseSplitter.Domain.ValueObjects;

internal static class MoneyCents
{
    // Multiplying the entire decimal by 100 would overflow for large derived values.
    public static BigInteger FromDecimal(decimal amount) =>
        new BigInteger(decimal.Truncate(amount)) * 100
        + new BigInteger((amount % 1m) * 100m);

    public static decimal ToDecimalExact(BigInteger cents)
    {
        var coefficient = cents;
        var scale = 2;
        var maximumCoefficient = new BigInteger(decimal.MaxValue);
        while (BigInteger.Abs(coefficient) > maximumCoefficient)
        {
            if (scale == 0 || coefficient % 10 != 0)
                throw new OverflowException("The calculated monetary value cannot be represented exactly as decimal.");
            coefficient /= 10;
            scale--;
        }

        return (decimal)coefficient / (scale == 2 ? 100m : scale == 1 ? 10m : 1m);
    }
}
