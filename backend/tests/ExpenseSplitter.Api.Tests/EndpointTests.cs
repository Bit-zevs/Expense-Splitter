using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;
using Xunit;

namespace ExpenseSplitter.Api.Tests;

public sealed class EndpointTests
{
    [Fact]
    public async Task FrontendOriginIsAllowedByCorsPolicy()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/trips");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            "http://localhost:5173",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public void ApplicationFailsToStartWithoutConnectionString()
    {
        using var factory = new MissingConnectionStringApiFactory();

        var error = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Equal(
            "Connection string 'ExpenseSplitter' must be configured.",
            error.Message);
    }

    [Fact]
    public async Task CreatedTripIsSerializedAndAvailableAtLocation()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/trips",
            new { name = "Summer trip", currency = "EUR" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var created = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal("Summer trip", created.RootElement.GetProperty("name").GetString());
        Assert.Equal("EUR", created.RootElement.GetProperty("currency").GetString());
        Assert.True(created.RootElement.TryGetProperty("createdAt", out _));
        Assert.False(created.RootElement.TryGetProperty("CreatedAt", out _));

        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(created.RootElement.GetProperty("id").GetGuid(), fetched.GetProperty("id").GetGuid());
        Assert.Equal(
            created.RootElement.GetProperty("createdAt").GetDateTimeOffset(),
            fetched.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal("EUR", fetched.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task UnsupportedCurrencyReturnsValidationProblem()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/trips",
            new { name = "Summer trip", currency = "GBP" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("currency", out _));
    }

    [Fact]
    public async Task CreatedParticipantLocationTargetsRegisteredItemEndpoint()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/trips/{trip.Id}/participants",
            new { name = "Alice" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var participant = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Alice", participant.GetProperty("name").GetString());

        using var wrongTripResponse = await client.GetAsync(
            $"/trips/{Guid.NewGuid()}/participants/{participant.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, wrongTripResponse.StatusCode);
    }

    [Fact]
    public async Task CreatedExpenseLocationTargetsRegisteredItemEndpoint()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        var payer = trip.AddParticipant("Alice");
        var participant = trip.AddParticipant("Bob");
        var occurredAt = new DateTimeOffset(2026, 9, 8, 14, 37, 42, TimeSpan.FromHours(5));
        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/trips/{trip.Id}/expenses",
            new
            {
                amount = 12.34m,
                description = "Coffee",
                paidByParticipantId = payer.Id,
                participantIds = new[] { participant.Id },
                occurredAt
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        using var getResponse = await client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var expense = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12.34m, expense.GetProperty("amount").GetDecimal());
        Assert.Equal("equal", expense.GetProperty("splitType").GetString());
        Assert.Single(expense.GetProperty("shares").EnumerateArray());
        Assert.Equal(
            new DateTimeOffset(2026, 9, 8, 9, 37, 0, TimeSpan.Zero),
            expense.GetProperty("occurredAt").GetDateTimeOffset());
        Assert.Equal(
            created.GetProperty("occurredAt").GetDateTimeOffset(),
            expense.GetProperty("occurredAt").GetDateTimeOffset());

        using var wrongTripResponse = await client.GetAsync(
            $"/trips/{Guid.NewGuid()}/expenses/{expense.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, wrongTripResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteEndpointsRemoveExpenseParticipantAndTrip()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        var alice = trip.AddParticipant("Alice");
        var bob = trip.AddParticipant("Bob");
        var firstExpense = trip.AddEqualExpense(10m, "First", alice.Id, [bob.Id]);
        var secondExpense = trip.AddEqualExpense(20m, "Second", alice.Id, [bob.Id]);
        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        using var expenseDelete = await client.DeleteAsync(
            $"/trips/{trip.Id}/expenses/{firstExpense.Id}");
        using var participantDelete = await client.DeleteAsync(
            $"/trips/{trip.Id}/participants/{bob.Id}");
        using var tripDelete = await client.DeleteAsync($"/trips/{trip.Id}");
        using var getDeletedTrip = await client.GetAsync($"/trips/{trip.Id}");

        Assert.Equal(HttpStatusCode.NoContent, expenseDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, participantDelete.StatusCode);
        Assert.DoesNotContain(trip.Expenses, expense => expense.Id == secondExpense.Id);
        Assert.Equal(HttpStatusCode.NoContent, tripDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getDeletedTrip.StatusCode);
    }

    [Fact]
    public async Task DeleteEndpointsReturnNotFoundForMissingOrAlreadyDeletedEntities()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");
        var expense = trip.AddEqualExpenseForAll(10m, "Coffee", participant.Id);
        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        using var expenseDelete = await client.DeleteAsync(
            $"/trips/{trip.Id}/expenses/{expense.Id}");
        using var repeatedExpenseDelete = await client.DeleteAsync(
            $"/trips/{trip.Id}/expenses/{expense.Id}");
        using var participantDelete = await client.DeleteAsync(
            $"/trips/{trip.Id}/participants/{participant.Id}");
        using var repeatedParticipantDelete = await client.DeleteAsync(
            $"/trips/{trip.Id}/participants/{participant.Id}");
        using var tripDelete = await client.DeleteAsync($"/trips/{trip.Id}");
        using var repeatedTripDelete = await client.DeleteAsync($"/trips/{trip.Id}");
        using var missingTripExpenseDelete = await client.DeleteAsync(
            $"/trips/{Guid.NewGuid()}/expenses/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NoContent, expenseDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, repeatedExpenseDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, participantDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, repeatedParticipantDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, tripDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, repeatedTripDelete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingTripExpenseDelete.StatusCode);
    }

    [Theory]
    [InlineData("/trips")]
    public async Task InvalidRequestBodyReturnsBadRequest(string path)
    {
        await using var factory = new ExpenseSplitterApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent("{", Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(path, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DomainValidationReturnsValidationProblem()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/trips", new { name = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task ParticipantAndExpenseValidationReturnValidationProblems()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");
        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        using var participantResponse = await client.PostAsJsonAsync(
            $"/trips/{trip.Id}/participants",
            new { name = " " });
        using var expenseResponse = await client.PostAsJsonAsync(
            $"/trips/{trip.Id}/expenses",
            new
            {
                amount = 0m,
                description = "Dinner",
                paidByParticipantId = participant.Id,
                participantIds = new[] { participant.Id }
            });
        using var excessiveExpenseResponse = await client.PostAsJsonAsync(
            $"/trips/{trip.Id}/expenses",
            new
            {
                amount = decimal.MaxValue,
                description = "Dinner",
                paidByParticipantId = participant.Id,
                participantIds = new[] { participant.Id }
            });

        Assert.Equal(HttpStatusCode.BadRequest, participantResponse.StatusCode);
        Assert.Equal("application/problem+json", participantResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, expenseResponse.StatusCode);
        Assert.Equal("application/problem+json", expenseResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, excessiveExpenseResponse.StatusCode);
        Assert.Equal(
            "application/problem+json",
            excessiveExpenseResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CollectionAndCalculationEndpointsReturnExpectedJson()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        var payer = trip.AddParticipant("Alice");
        var debtor = trip.AddParticipant("Bob");
        trip.AddEqualExpense(12.34m, "Coffee", payer.Id, new[] { debtor.Id });
        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<JsonElement>("/health");
        var participants = await client.GetFromJsonAsync<JsonElement>(
            $"/trips/{trip.Id}/participants");
        var expenses = await client.GetFromJsonAsync<JsonElement>(
            $"/trips/{trip.Id}/expenses");
        var balances = await client.GetFromJsonAsync<JsonElement>(
            $"/trips/{trip.Id}/balances?participantIds={debtor.Id}");
        var settlements = await client.GetFromJsonAsync<JsonElement>(
            $"/trips/{trip.Id}/settlements");

        Assert.Equal("ok", health.GetProperty("status").GetString());
        Assert.Equal(2, participants.GetArrayLength());
        Assert.Single(expenses.EnumerateArray());
        var balance = Assert.Single(balances.EnumerateArray());
        Assert.Equal(debtor.Id, balance.GetProperty("participantId").GetGuid());
        Assert.Equal(-12.34m, balance.GetProperty("amount").GetDecimal());
        Assert.Equal(2, settlements.GetProperty("balances").GetArrayLength());
        Assert.Single(settlements.GetProperty("transfers").EnumerateArray());
    }

    [Fact]
    public async Task MissingResourcesAndInvalidRouteValuesReturnNotFound()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        using var client = factory.CreateClient();
        var missingTripId = Guid.NewGuid();

        using var participants = await client.GetAsync($"/trips/{missingTripId}/participants");
        using var trip = await client.GetAsync($"/trips/{missingTripId}");
        using var balances = await client.GetAsync($"/trips/{missingTripId}/balances");
        using var settlements = await client.GetAsync($"/trips/{missingTripId}/settlements");
        using var expensePost = await client.PostAsJsonAsync(
            $"/trips/{missingTripId}/expenses",
            new
            {
                amount = 10m,
                description = "Dinner",
                paidByParticipantId = Guid.NewGuid(),
                participantIds = new[] { Guid.NewGuid() }
            });
        using var invalidRoute = await client.GetAsync("/trips/not-a-guid");

        Assert.Equal(HttpStatusCode.NotFound, participants.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, trip.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, balances.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, settlements.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, expensePost.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invalidRoute.StatusCode);
    }

    [Fact]
    public async Task InvalidBalanceQueryReturnsValidationProblem()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        var participant = trip.AddParticipant("Alice");
        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            $"/trips/{trip.Id}/balances?participantIds={participant.Id}&participantIds={participant.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task CalculationOutsideDecimalRangeReturnsUnprocessableEntity()
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Extreme trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "First", payer.Id, new[] { debtor.Id });
        trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Second", payer.Id, new[] { debtor.Id });

        factory.Store.Add(trip);
        using var client = factory.CreateClient();

        using var balanceResponse = await client.GetAsync($"/trips/{trip.Id}/balances");
        using var response = await client.GetAsync($"/trips/{trip.Id}/settlements");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, balanceResponse.StatusCode);
        Assert.Equal("application/problem+json", balanceResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
