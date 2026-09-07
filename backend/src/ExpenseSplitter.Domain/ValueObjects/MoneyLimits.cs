namespace ExpenseSplitter.Domain.ValueObjects;

public static class MoneyLimits
{
    public const decimal MaximumAmount = 792281625142643375935439503.35m;

    public static bool IsValidPositiveAmount(decimal amount) =>
        amount > 0 && IsValidNonNegativeAmount(amount);

    public static bool IsValidNonNegativeAmount(decimal amount) =>
        amount >= 0
        && amount <= MaximumAmount
        && amount % 0.01m == 0;

    internal static decimal AddExact(decimal left, decimal right)
    {
        if ((right > 0 && left > MaximumAmount - right)
            || (right < 0 && left < -MaximumAmount - right))
        {
            throw new OverflowException(
                "The monetary total exceeds the range that preserves cent precision.");
        }

        return left + right;
    }
}
