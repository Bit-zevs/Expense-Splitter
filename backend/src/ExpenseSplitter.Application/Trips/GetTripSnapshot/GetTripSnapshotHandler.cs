using ExpenseSplitter.Application.Expenses;
using ExpenseSplitter.Application.Participants;
using ExpenseSplitter.Application.Settlements.GetSettlements;
using ExpenseSplitter.Application.Trips.GetTrip;
using ExpenseSplitter.Domain.Services;

namespace ExpenseSplitter.Application.Trips.GetTripSnapshot;

public sealed record TripSnapshotResult(
    GetTripResult Trip,
    IReadOnlyCollection<ParticipantResult> Participants,
    IReadOnlyCollection<ExpenseResult> Expenses,
    GetSettlementsResult? Settlement,
    string? CalculationError);

public sealed class GetTripSnapshotHandler(ITripStore tripStore)
{
    public async Task<TripSnapshotResult?> HandleAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var trip = await tripStore.FindWithParticipantsAndExpensesByIdAsync(tripId, cancellationToken);
        if (trip is null) return null;

        GetSettlementsResult? settlement = null;
        string? calculationError = null;
        try
        {
            var balances = ParticipantBalanceCalculator.Calculate(trip);
            settlement = new GetSettlementsResult(
                balances.Select(b => new SettlementBalanceResult(b.ParticipantId, b.Amount)).ToArray(),
                SettlementCalculator.CalculateFromBalances(balances)
                    .Select(t => new SettlementTransferResult(t.FromParticipantId, t.ToParticipantId, t.Amount)).ToArray());
        }
        catch (OverflowException)
        {
            calculationError = "Money cannot be represented exactly as a decimal monetary value.";
        }

        return new TripSnapshotResult(
            new GetTripResult(trip.Id, trip.Name, trip.Currency, trip.CreatedAt),
            trip.Participants.OrderBy(p => p.Id).Select(p => new ParticipantResult(p.Id, p.Name)).ToArray(),
            trip.Expenses.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(ExpenseResult.FromExpense).ToArray(),
            settlement, calculationError);
    }
}
