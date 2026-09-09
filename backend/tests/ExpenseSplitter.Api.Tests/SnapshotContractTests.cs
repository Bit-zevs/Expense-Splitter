using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;
using Xunit;

namespace ExpenseSplitter.Api.Tests;

public sealed class SnapshotContractTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task SnapshotReturnsBaseDataEvenWhenCalculationFails(int expenseCount)
    {
        await using var factory = new ExpenseSplitterApiFactory();
        var trip = new Trip("Trip");
        var payer = trip.AddParticipant("Payer");
        var debtor = trip.AddParticipant("Debtor");
        for (var i = 0; i < expenseCount; i++)
            trip.AddEqualExpense(MoneyLimits.MaximumAmount, "Expense", payer.Id, [debtor.Id]);
        factory.Store.Add(trip);
        using var client = factory.CreateClient();
        var snapshot = await client.GetFromJsonAsync<JsonElement>($"/trips/{trip.Id}/snapshot");
        Assert.Equal(trip.Id, snapshot.GetProperty("trip").GetProperty("id").GetGuid());
        Assert.Equal(2, snapshot.GetProperty("participants").GetArrayLength());
        Assert.Equal(expenseCount, snapshot.GetProperty("expenses").GetArrayLength());
        Assert.Equal(JsonValueKind.String, snapshot.GetProperty("expenses")[0].GetProperty("amount").ValueKind);
        Assert.Equal(expenseCount == 3, snapshot.GetProperty("settlement").ValueKind == JsonValueKind.Null);
        Assert.Equal(expenseCount == 3, snapshot.GetProperty("calculationError").ValueKind == JsonValueKind.String);
        using var missing = await client.GetAsync($"/trips/{Guid.NewGuid()}/snapshot");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task OpenApiDescribesMoneyStringsAndAllWriteConflicts()
    {
        await using var factory = new ExpenseSplitterApiFactory("Development");
        using var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonElement>("/openapi/v1.json");
        var schemas = document.GetProperty("components").GetProperty("schemas");
        foreach (var name in new[] { "ExpenseResult", "ExpenseShareResult", "SettlementTransferResult" })
        {
            var money = schemas.GetProperty(name).GetProperty("properties").GetProperty("amount");
            Assert.Equal("string", money.GetProperty("type").GetString());
            Assert.True(money.TryGetProperty("pattern", out _));
        }
        var count = 0;
        foreach (var path in document.GetProperty("paths").EnumerateObject())
        foreach (var operation in path.Value.EnumerateObject())
        {
            if (operation.Name is not ("post" or "delete")) continue;
            count++;
            Assert.True(operation.Value.GetProperty("responses").TryGetProperty("409", out _), path.Name);
        }
        Assert.Equal(6, count);
    }
}
