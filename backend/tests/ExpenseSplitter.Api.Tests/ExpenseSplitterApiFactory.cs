using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ExpenseSplitter.Api.Tests;

internal sealed class ExpenseSplitterApiFactory : WebApplicationFactory<Program>
{
    public InMemoryTripStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:ExpenseSplitter",
            "Host=localhost;Database=not_used");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITripStore>();
            services.AddSingleton<ITripStore>(Store);
        });
    }
}

internal sealed class MissingConnectionStringApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ExpenseSplitter", string.Empty);
    }
}

internal sealed class InMemoryTripStore : ITripStore
{
    private readonly Dictionary<Guid, Trip> _trips = [];

    public Task AddAsync(Trip trip, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _trips.Add(trip.Id, trip);
        return Task.CompletedTask;
    }

    public Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindWithParticipantsByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindWithExpensesByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindWithParticipantsAndExpensesByIdAsync(
        Guid id,
        CancellationToken cancellationToken) => FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindWithParticipantsForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken) => FindTripAsync(id, cancellationToken);

    public async Task<Participant?> FindParticipantByIdAsync(
        Guid tripId,
        Guid participantId,
        CancellationToken cancellationToken)
    {
        var trip = await FindTripAsync(tripId, cancellationToken);
        return trip?.Participants.SingleOrDefault(participant => participant.Id == participantId);
    }

    public async Task<Expense?> FindExpenseByIdAsync(
        Guid tripId,
        Guid expenseId,
        CancellationToken cancellationToken)
    {
        var trip = await FindTripAsync(tripId, cancellationToken);
        return trip?.Expenses.SingleOrDefault(expense => expense.Id == expenseId);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public void Add(Trip trip) => _trips.Add(trip.Id, trip);

    private Task<Trip?> FindTripAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_trips.GetValueOrDefault(id));
    }
}
