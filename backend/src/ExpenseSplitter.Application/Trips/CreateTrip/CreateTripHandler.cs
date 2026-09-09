using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Trips.CreateTrip;

public sealed record CreateTripCommand(string? Name, string? Currency = null);

public sealed record CreateTripResult(
    Guid Id,
    string Name,
    string Currency,
    DateTimeOffset CreatedAt);

public sealed class CreateTripHandler(ITripStore tripStore)
{
    public async Task<CreateTripResult> HandleAsync(
        CreateTripCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trip = new Trip(
            command.Name!,
            command.Currency ?? Trip.DefaultCurrency);
        await tripStore.AddAsync(trip, cancellationToken);
        await tripStore.SaveChangesAsync(cancellationToken);

        return new CreateTripResult(trip.Id, trip.Name, trip.Currency, trip.CreatedAt);
    }
}
