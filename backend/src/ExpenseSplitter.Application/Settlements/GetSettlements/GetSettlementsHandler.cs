using System.Numerics;
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

public sealed class GetSettlementsHandler(ITripStore tripStore)
{
    public async Task<GetSettlementsResult?> HandleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindAggregateByIdAsync(tripId, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var balances = ParticipantBalanceCalculator.Calculate(trip)
            .Select(balance => new SettlementBalanceResult(
                balance.ParticipantId,
                ToDecimalAmount(balance.AmountInCents)))
            .ToArray();

        var transfers = SettlementCalculator.Calculate(trip)
            .Select(transfer => new SettlementTransferResult(
                transfer.FromParticipantId,
                transfer.ToParticipantId,
                transfer.Amount))
            .ToArray();

        return new GetSettlementsResult(balances, transfers);
    }

    private static decimal ToDecimalAmount(BigInteger amountInCents) =>
        (decimal)amountInCents / 100m;
}
