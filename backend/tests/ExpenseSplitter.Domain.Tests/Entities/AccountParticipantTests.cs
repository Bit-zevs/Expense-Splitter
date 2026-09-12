using Xunit;

namespace ExpenseSplitter.Domain.Tests.Entities;

public sealed class AccountParticipantTests
{
    [Fact]
    public void ClaimingPhantomPreservesIdNameAndExpenseShares()
    {
        var trip = TestTrips.Create("Trip");
        var phantom = trip.AddParticipant("Local name");
        var expense = trip.AddEqualExpenseForAll(12.34m, "Meal", phantom.Id);
        var accountId = Guid.NewGuid();
        var claimed = trip.AddAccountParticipant(accountId, "Profile name", phantom.Id);
        Assert.Same(phantom, claimed);
        Assert.Equal("Local name", claimed.Name);
        Assert.Equal(accountId, claimed.AccountId);
        Assert.Equal(claimed.Id, expense.PaidByParticipantId);
        Assert.Equal(12.34m, Assert.Single(expense.Shares).Amount);
        Assert.Throws<InvalidOperationException>(() => trip.AddAccountParticipant(accountId, "Duplicate"));
        Assert.Throws<InvalidOperationException>(() => trip.AddAccountParticipant(Guid.NewGuid(), "Other", phantom.Id));
    }

    [Fact]
    public void OwnershipMustBeTransferredBeforeOwnerCanBeRemoved()
    {
        var trip = TestTrips.Create("Trip");
        var owner = trip.AddAccountParticipant(TestTrips.OwnerId, "Owner");
        var next = trip.AddAccountParticipant(Guid.NewGuid(), "Next");
        Assert.Throws<InvalidOperationException>(() => trip.RemoveParticipant(owner.Id));
        Assert.Throws<ArgumentException>(() => trip.TransferOwnership(Guid.NewGuid()));
        trip.TransferOwnership(next.AccountId!.Value);
        Assert.True(trip.RemoveParticipant(owner.Id));
        Assert.Equal(next.AccountId, trip.OwnerAccountId);
    }
}
