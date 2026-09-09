using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Trips.CreateTrip;

public sealed record CreateTripCommand(string? Name, string? Currency = null);

public sealed record CreateTripResult(
    Guid Id,
    string Name,
    string Currency,
    DateTimeOffset CreatedAt,
    Guid OwnerAccountId,
    string JoinCode);

public sealed class CreateTripHandler(ITripStore tripStore, ICurrentAccount account)
{
    public async Task<CreateTripResult> HandleAsync(
        CreateTripCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trip = new Trip(
            command.Name!,
            account.Id,
            command.Currency ?? Trip.DefaultCurrency);
        trip.AddAccountParticipant(account.Id, account.DisplayName);
        var code = JoinCode.Generate();
        trip.SetJoinCodeHash(JoinCode.Hash(code));
        await tripStore.AddAsync(trip, cancellationToken);
        await tripStore.SaveChangesAsync(cancellationToken);

        return new CreateTripResult(trip.Id, trip.Name, trip.Currency, trip.CreatedAt, trip.OwnerAccountId, code);
    }
}
