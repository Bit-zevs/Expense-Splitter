using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ExpenseSplitter.Application.Access;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ExpenseSplitter.Api.Tests;

internal sealed class ExpenseSplitterApiFactory(string environment = "Testing") : WebApplicationFactory<Program>
{
    public InMemoryTripStore Store { get; } = new();

    public new HttpClient CreateClient()
    {
        var client = base.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var csrf = client.GetFromJsonAsync<JsonElement>("/auth/csrf").GetAwaiter().GetResult();
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting(
            "ConnectionStrings:ExpenseSplitter",
            "Host=localhost;Database=not_used");
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<ITripStore>();
            services.AddSingleton<ITripStore>(Store);
            services.RemoveAll<ITripAccess>();
            services.AddSingleton<ITripAccess, AllowTripAccess>();
            services.RemoveAll<ICurrentAccount>();
            services.AddSingleton<ICurrentAccount, TestCurrentAccount>();
            services.AddAuthentication("ContractTest")
                .AddScheme<AuthenticationSchemeOptions, ContractAuthenticationHandler>("ContractTest", _ => { });
        });
    }
}

internal sealed class ContractAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
        AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, TestTrips.OwnerId.ToString()), new Claim("display_name", "Owner")], Scheme.Name)), Scheme.Name)));
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
    public bool FailSaveWithConflict { get; set; }
    private readonly Dictionary<Guid, Trip> _trips = [];

    public Task AddAsync(Trip trip, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _trips.Add(trip.Id, trip);
        return Task.CompletedTask;
    }

    public Task<Trip?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindTrackedAsync(Guid id, CancellationToken cancellationToken) =>
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

    public Task<Trip?> FindWithParticipantsTrackedAsync(
        Guid id,
        CancellationToken cancellationToken) => FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindWithExpensesTrackedAsync(
        Guid id,
        CancellationToken cancellationToken) => FindTripAsync(id, cancellationToken);

    public Task<Trip?> FindWithParticipantsAndExpensesTrackedAsync(
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
        if (FailSaveWithConflict) throw new WriteConflictException(new InvalidOperationException("Concurrent write"));
        return Task.CompletedTask;
    }

    public void Remove(Trip trip) => _trips.Remove(trip.Id);

    public void Add(Trip trip) => _trips.Add(trip.Id, trip);

    private Task<Trip?> FindTripAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_trips.GetValueOrDefault(id));
    }
}
