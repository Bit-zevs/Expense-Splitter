using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Services;

namespace ExpenseSplitter.Application.Settlements.GetSettlements;

public sealed record SettlementBalanceResult(Guid ParticipantId, decimal Balance);

public sealed record SettlementTransferResult(
    Guid FromParticipantId,
    Guid ToParticipantId,
    decimal Amount);

public sealed record GetSettlementsResult(
    IReadOnlyCollection<SettlementBalanceResult> Balances,
    IReadOnlyCollection<SettlementTransferResult> Transfers);

public sealed class GetSettlementsHandler(ITripStore tripStore, ITripAccess access)
{
    public async Task<GetSettlementsResult?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        await access.RequireAsync(tripId, ownerOnly: false, write: false, cancellationToken);
        var trip = await tripStore.FindWithParticipantsAndExpensesByIdAsync(
            tripId,
            cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var calculatedBalances = ParticipantBalanceCalculator.Calculate(trip);
        var balances = calculatedBalances
            .Select(balance => new SettlementBalanceResult(
                balance.ParticipantId,
                balance.Amount))
            .ToArray();

        var transfers = SettlementCalculator.CalculateFromBalances(calculatedBalances)
            .Select(transfer => new SettlementTransferResult(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.Amount))
            .ToArray();

        return new GetSettlementsResult(balances, transfers);
    }
}
