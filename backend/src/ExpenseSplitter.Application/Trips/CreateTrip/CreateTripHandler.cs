using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Trips.CreateTrip;

public sealed record CreateTripCommand(string? Name);

public sealed record CreateTripResult(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed class CreateTripHandler(ITripStore tripStore)
{
    public async Task<CreateTripResult> HandleAsync(
        CreateTripCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var trip = new Trip(command.Name!);
        await tripStore.AddAsync(trip, cancellationToken);

        return new CreateTripResult(trip.Id, trip.Name, trip.CreatedAt);
    }
}
