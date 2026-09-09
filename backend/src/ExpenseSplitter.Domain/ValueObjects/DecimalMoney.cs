namespace ExpenseSplitter.Domain.ValueObjects;

// Both components are decimal. Keeping fractional cents separate prevents decimal
// addition from silently rounding them near its precision limit. No arbitrary integers.
internal readonly record struct DecimalMoney : IComparable<DecimalMoney>
{
    private decimal Whole { get; }
    private decimal Cents { get; }
    public static DecimalMoney Zero => default;

    private DecimalMoney(decimal whole, decimal cents)
    {
        whole = checked(whole + decimal.Truncate(cents / 100m));
        cents %= 100m;
        if (whole > 0 && cents < 0) { whole--; cents += 100m; }
        if (whole < 0 && cents > 0) { whole++; cents -= 100m; }
        Whole = whole;
        Cents = cents;
    }

    public static DecimalMoney FromDecimal(decimal amount) =>
        new(decimal.Truncate(amount), (amount % 1m) * 100m);

    public decimal ToDecimalExact()
    {
        var result = checked(Whole + Cents / 100m);
        if (decimal.Truncate(result) != Whole || (result % 1m) * 100m != Cents)
            throw new OverflowException("Money cannot be represented exactly as a decimal monetary value.");
        return result;
    }

    public int CompareTo(DecimalMoney other) => Whole != other.Whole
        ? Whole.CompareTo(other.Whole) : Cents.CompareTo(other.Cents);

    public static DecimalMoney Min(DecimalMoney left, DecimalMoney right) => left.CompareTo(right) <= 0 ? left : right;
    public static DecimalMoney operator +(DecimalMoney left, DecimalMoney right) =>
        new(checked(left.Whole + right.Whole), left.Cents + right.Cents);
    public static DecimalMoney operator -(DecimalMoney value) => new(-value.Whole, -value.Cents);
    public static DecimalMoney operator -(DecimalMoney left, DecimalMoney right) => left + -right;
}
