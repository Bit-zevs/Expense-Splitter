using ExpenseSplitter.Domain.Trips;
using Xunit;

namespace ExpenseSplitter.Domain.Tests;

public sealed class EqualSplitTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static Trip MakeTrip() => new()
    {
        Participants = new[] { A, B, C }.Select(id => new Participant { Id = id }).ToArray()
    };

    [Fact]
    public void SplitsAmongAllCurrentMembers()
    {
        var trip = MakeTrip();
        var expense = Expense.CreateEqual(trip, 90m, "Dinner", A, trip.Participants.Select(p => p.Id));
        Assert.Equal(3, expense.Shares.Count);
        Assert.All(expense.Shares, share => Assert.Equal(30m, share.Amount));
        Assert.Equal(90m, expense.Amount);
        Assert.Equal(SplitType.Equal, expense.SplitType);
    }

    [Fact]
    public void SplitsOnlyAmongSelectedMembersAndPayerMayBeExcluded()
    {
        var expense = Expense.CreateEqual(MakeTrip(), 100m, "Tickets", A, new[] { B, C });
        Assert.Equal(new[] { new ExpenseShare(B, 50m), new ExpenseShare(C, 50m) }, expense.Shares);
        Assert.Equal(A, expense.PaidByParticipantId);
        Assert.DoesNotContain(A, expense.ParticipantIds);
    }

    [Fact]
    public void AllocatesRemainderInStableIdOrder()
    {
        var first = Expense.CreateEqual(MakeTrip(), 100m, "", A, new[] { C, A, B });
        var second = Expense.CreateEqual(MakeTrip(), 100m, "", A, new[] { B, C, A });
        Assert.Equal(new[] { new ExpenseShare(A, 33.34m), new ExpenseShare(B, 33.33m), new ExpenseShare(C, 33.33m) }, first.Shares);
        Assert.Equal(first.Shares.ToArray(), second.Shares.ToArray());
    }

    [Fact]
    public void AmountSmallerThanParticipantCountAllowsZeroShares()
    {
        var expense = Expense.CreateEqual(MakeTrip(), 0.02m, "", A, new[] { A, B, C });
        Assert.Equal(new[] { 0.01m, 0.01m, 0m }, expense.Shares.Select(s => s.Amount));
    }

    [Fact]
    public void SingleParticipantReceivesWholeAmount()
    {
        var expense = Expense.CreateEqual(MakeTrip(), 12.34m, "", A, new[] { B });
        Assert.Equal(new ExpenseShare(B, 12.34m), Assert.Single(expense.Shares));
    }

    [Fact]
    public void SharesAreSnapshotOfSelectionAndMembership()
    {
        var members = new List<Participant> { new() { Id = A }, new() { Id = B } };
        var selection = new List<Guid> { A, B };
        var expense = Expense.CreateEqual(new Trip { Participants = members }, 10m, "", A, selection);
        selection.Clear();
        members.Add(new Participant { Id = C });
        Assert.Equal(new[] { A, B }, expense.ParticipantIds);
        Assert.Equal(10m, expense.Shares.Sum(s => s.Amount));
        Assert.Throws<NotSupportedException>(() => ((IList<ExpenseShare>)expense.Shares)[0] = new(A, 0m));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    [InlineData("79228162514264337593543950335")]
    public void RejectsInvalidAmounts(string value)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentOutOfRangeException>(() => Expense.CreateEqual(MakeTrip(), amount, "", A, new[] { A }));
    }

    [Fact]
    public void RejectsInvalidSelectionAndPayer()
    {
        var trip = MakeTrip();
        Assert.Throws<ArgumentException>(() => Expense.CreateEqual(trip, 10m, "", A, Array.Empty<Guid>()));
        Assert.Throws<ArgumentException>(() => Expense.CreateEqual(trip, 10m, "", A, new[] { B, B }));
        Assert.Throws<ArgumentException>(() => Expense.CreateEqual(trip, 10m, "", A, new[] { Guid.NewGuid() }));
        Assert.Throws<ArgumentException>(() => Expense.CreateEqual(trip, 10m, "", A, new[] { Guid.Empty }));
        Assert.Throws<ArgumentException>(() => Expense.CreateEqual(trip, 10m, "", Guid.NewGuid(), new[] { A }));
        Assert.Throws<ArgumentNullException>(() => Expense.CreateEqual(trip, 10m, "", A, null!));
    }

    [Fact]
    public void PreservesTotalAndDifferenceNeverExceedsOneCent()
    {
        var trip = MakeTrip();
        for (var cents = 1; cents <= 1000; cents++)
        {
            var amount = cents / 100m;
            var shares = Expense.CreateEqual(trip, amount, "", A, new[] { A, B, C }).Shares;
            Assert.Equal(amount, shares.Sum(s => s.Amount));
            Assert.InRange(shares.Max(s => s.Amount) - shares.Min(s => s.Amount), 0m, 0.01m);
            Assert.All(shares, s => Assert.Equal(0m, s.Amount % 0.01m));
        }
    }

    [Theory]
    [InlineData("792281625142643375935439503.35", "0")]
    [InlineData("792281625142643375935439503.34", "0.01")]
    public void LargeAmountPreservesTotal(string value, string difference)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var expectedDifference = decimal.Parse(difference, System.Globalization.CultureInfo.InvariantCulture);
        var expense = Expense.CreateEqual(MakeTrip(), amount, "", A, new[] { A, B, C });
        Assert.Equal(amount, expense.Shares.Sum(s => s.Amount));
        Assert.Equal(expectedDifference, expense.Shares.Max(s => s.Amount) - expense.Shares.Min(s => s.Amount));
    }
}
