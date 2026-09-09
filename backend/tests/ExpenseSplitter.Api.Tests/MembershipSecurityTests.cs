using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExpenseSplitter.Infrastructure.Persistence;
using ExpenseSplitter.Infrastructure.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static ExpenseSplitter.Api.Tests.SecurityApiFactory;

namespace ExpenseSplitter.Api.Tests;

public sealed class MembershipSecurityTests(PostgreSqlFixture database) : IClassFixture<PostgreSqlFixture>
{
    [Fact]
    public async Task IdentityEnforcesUniqueEmailPasswordChangesAndLockout()
    {
        await using var factory = await CreateAsync(database);
        using var client = factory.Browser();
        await RefreshCsrfAsync(client);
        var email = "identity@example.test";
        var registration = new { email, password = Password, displayName = "Same name" };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/auth/register", registration)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/auth/register",
            new { email = email.ToUpperInvariant(), password = Password, displayName = "Other name" })).StatusCode);
        using var sameName = await factory.RegisterAsync("Same name");
        using var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith("__Host-ExpenseSplitter.Auth="));
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/trips", new { name = "Stale CSRF" })).StatusCode);
        await RefreshCsrfAsync(client);
        var newPassword = "Changed-password-2026!";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/auth/change-password",
            new { currentPassword = Password, newPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/auth/logout", new { })).StatusCode);
        await RefreshCsrfAsync(client);
        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/login", new { email, password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/auth/login", new { email, password = newPassword })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<ExpenseSplitterDbContext>().Users.SingleAsync(u => u.Email == email);
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ConcurrentRequestsAndApprovalsDoNotCreateDuplicateMemberships()
    {
        await using var factory = await CreateAsync(database);
        using var owner = await factory.RegisterAsync();
        using var applicant = await factory.RegisterAsync();
        var trip = await PostAsync(owner, "/trips", new { name = "Trip" });
        var id = trip.GetProperty("id").GetGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => applicant.PostAsJsonAsync(
            "/trip-join-requests", new { code = trip.GetProperty("joinCode").GetString() })));
        var created = Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var request = await created.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var response in responses) response.Dispose();
        var approvals = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => owner.PostAsJsonAsync(
            $"/trips/{id}/join-requests/{request.GetProperty("id").GetGuid()}/approve", new { })));
        Assert.Single(approvals, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(approvals, r => r.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound);
        foreach (var response in approvals) response.Dispose();
        var participants = await owner.GetFromJsonAsync<JsonElement>($"/trips/{id}/participants");
        Assert.Equal(2, participants.GetArrayLength());
    }

    [Fact]
    public async Task CookieLoginRequiresCsrfAndLogoutRevokesBrowserAccess()
    {
        await using var factory = await CreateAsync(database);
        using var anonymous = factory.Browser();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/trips/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/trips", new { name = "Trip" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/auth/register",
            new { email = "x@example.test", password = Password, displayName = "X" })).StatusCode);
        using var owner = await factory.RegisterAsync();
        var trip = await PostAsync(owner, "/trips", new { name = "Trip" });
        var id = trip.GetProperty("id").GetGuid();
        owner.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.DeleteAsync($"/trips/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/trips/{id}")).StatusCode);
        await RefreshCsrfAsync(owner);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync("/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync($"/trips/{id}")).StatusCode);
    }

    [Fact]
    public async Task PendingUserCannotReadTripAndApprovalCanPreservePhantomExpenses()
    {
        await using var factory = await CreateAsync(database);
        using var owner = await factory.RegisterAsync("Owner");
        using var applicant = await factory.RegisterAsync("Account name");
        var trip = await PostAsync(owner, "/trips", new { name = "Private trip" });
        var id = trip.GetProperty("id").GetGuid();
        var phantom = await PostAsync(owner, $"/trips/{id}/participants", new { name = "Local name" });
        var phantomId = phantom.GetProperty("id").GetGuid();
        var expense = await PostAsync(owner, $"/trips/{id}/expenses", new
        { amount = "12.34", description = "Dinner", paidByParticipantId = phantomId, participantIds = new[] { phantomId } });
        var request = await PostAsync(applicant, "/trip-join-requests", new { code = trip.GetProperty("joinCode").GetString()!.ToLowerInvariant() });
        var requestId = request.GetProperty("id").GetGuid();
        foreach (var suffix in new[] { "", "/snapshot", "/expenses", "/participants", "/balances", "/settlements",
                     $"/participants/{phantomId}", $"/expenses/{expense.GetProperty("id").GetGuid()}" })
            Assert.Equal(HttpStatusCode.NotFound, (await applicant.GetAsync($"/trips/{id}{suffix}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await applicant.PostAsJsonAsync($"/trips/{id}/expenses", new
        { amount = "1", description = "Unauthorized", paidByParticipantId = phantomId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await applicant.DeleteAsync($"/trips/{id}/participants/{phantomId}")).StatusCode);
        var pending = await owner.GetFromJsonAsync<JsonElement>($"/trips/{id}/join-requests");
        Assert.Equal("Account name", pending[0].GetProperty("displayName").GetString());
        Assert.False(pending[0].TryGetProperty("email", out _));
        var participant = await PostAsync(owner, $"/trips/{id}/join-requests/{requestId}/approve", new { participantId = phantomId });
        Assert.Equal(phantomId, participant.GetProperty("id").GetGuid());
        Assert.Equal("Local name", participant.GetProperty("name").GetString());
        Assert.NotEqual(Guid.Empty, participant.GetProperty("accountId").GetGuid());
        var snapshot = await applicant.GetFromJsonAsync<JsonElement>($"/trips/{id}/snapshot");
        Assert.Single(snapshot.GetProperty("expenses").EnumerateArray());
        Assert.Equal("12.34", snapshot.GetProperty("expenses")[0].GetProperty("amount").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await applicant.GetAsync($"/trip-join-requests/{requestId}")).StatusCode);
    }

    [Fact]
    public async Task MemberRightsOwnershipTransferAndDestructiveRemovalAreEnforced()
    {
        await using var factory = await CreateAsync(database);
        using var owner = await factory.RegisterAsync("Owner");
        using var member = await factory.RegisterAsync("Member");
        var trip = await PostAsync(owner, "/trips", new { name = "Trip" });
        var id = trip.GetProperty("id").GetGuid();
        var request = await PostAsync(member, "/trip-join-requests", new { code = trip.GetProperty("joinCode").GetString() });
        var participant = await PostAsync(owner, $"/trips/{id}/join-requests/{request.GetProperty("id").GetGuid()}/approve", new { });
        var participantId = participant.GetProperty("id").GetGuid();
        var accountId = participant.GetProperty("accountId").GetGuid();
        Assert.Equal("Member", participant.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/trips/{id}/participants", new { name = "Phantom" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/trips/{id}/participants/{participantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.DeleteAsync($"/trips/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.PostAsJsonAsync($"/trips/{id}/join-code", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync($"/trips/{id}/join-requests")).StatusCode);
        var expenses = await PostAsync(member, $"/trips/{id}/expenses", new
        { amount = "10", description = "Dinner", paidByParticipantId = participantId });
        Assert.Equal(HttpStatusCode.NoContent, (await member.DeleteAsync($"/trips/{id}/expenses/{expenses.GetProperty("id").GetGuid()}")).StatusCode);
        var people = await owner.GetFromJsonAsync<JsonElement>($"/trips/{id}/participants");
        var ownerParticipantId = people.EnumerateArray().Single(p => p.GetProperty("accountId").GetGuid() == trip.GetProperty("ownerAccountId").GetGuid()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.DeleteAsync($"/trips/{id}/participants/{ownerParticipantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync($"/trips/{id}/owner", new { accountId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/trips/{id}/owner", new { accountId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync($"/trips/{id}/join-code", new { })).StatusCode);
        await PostAsync(member, $"/trips/{id}/expenses", new { amount = "20", description = "Shared", paidByParticipantId = participantId });
        await PostAsync(member, $"/trips/{id}/expenses", new { amount = "3", description = "Keep", paidByParticipantId = participantId, participantIds = new[] { participantId } });
        Assert.Equal(HttpStatusCode.NoContent, (await member.DeleteAsync($"/trips/{id}/participants/{ownerParticipantId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/trips/{id}")).StatusCode);
        var remaining = await member.GetFromJsonAsync<JsonElement>($"/trips/{id}/expenses");
        Assert.Single(remaining.EnumerateArray());
        Assert.Equal("Keep", remaining[0].GetProperty("description").GetString());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpenseSplitterDbContext>();
        Assert.True(await db.Users.AnyAsync(u => u.Id == trip.GetProperty("ownerAccountId").GetGuid()));
    }

    [Fact]
    public async Task RotationDuplicatesRejectionAndCancellationRespectRequestOwnership()
    {
        await using var factory = await CreateAsync(database);
        using var owner = await factory.RegisterAsync();
        using var applicant = await factory.RegisterAsync();
        using var stranger = await factory.RegisterAsync();
        var trip = await PostAsync(owner, "/trips", new { name = "Trip" });
        var id = trip.GetProperty("id").GetGuid();
        var oldCode = trip.GetProperty("joinCode").GetString();
        var request = await PostAsync(applicant, "/trip-join-requests", new { code = oldCode });
        var requestId = request.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Conflict, (await applicant.PostAsJsonAsync("/trip-join-requests", new { code = oldCode })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/trip-join-requests/{requestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/trip-join-requests/{requestId}")).StatusCode);
        var code = (await PostAsync(owner, $"/trips/{id}/join-code", new { })).GetProperty("code").GetString();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync("/trip-join-requests", new { code = oldCode })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await applicant.GetAsync($"/trip-join-requests/{requestId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/trips/{id}/join-requests/{requestId}")).StatusCode);
        var retry = await PostAsync(applicant, "/trip-join-requests", new { code });
        var retryId = retry.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await applicant.DeleteAsync($"/trip-join-requests/{retryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await applicant.DeleteAsync($"/trip-join-requests/{retryId}")).StatusCode);
        var final = await PostAsync(applicant, "/trip-join-requests", new { code });
        await PostAsync(owner, $"/trips/{id}/join-requests/{final.GetProperty("id").GetGuid()}/approve", new { });
        Assert.Equal(HttpStatusCode.Conflict, (await applicant.PostAsJsonAsync("/trip-join-requests", new { code })).StatusCode);
    }

    [Fact]
    public async Task InvalidApprovalDoesNotConsumeRequestAndCrossTripIdsAreRejected()
    {
        await using var factory = await CreateAsync(database);
        using var owner = await factory.RegisterAsync();
        using var applicant = await factory.RegisterAsync();
        var trip = await PostAsync(owner, "/trips", new { name = "First" });
        var other = await PostAsync(owner, "/trips", new { name = "Other" });
        var id = trip.GetProperty("id").GetGuid();
        var otherId = other.GetProperty("id").GetGuid();
        var phantom = await PostAsync(owner, $"/trips/{otherId}/participants", new { name = "Phantom" });
        var request = await PostAsync(applicant, "/trip-join-requests", new { code = trip.GetProperty("joinCode").GetString() });
        var requestId = request.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync($"/trips/{otherId}/join-requests/{requestId}/approve", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync($"/trips/{id}/join-requests/{requestId}/approve",
            new { participantId = phantom.GetProperty("id").GetGuid() })).StatusCode);
        var people = await owner.GetFromJsonAsync<JsonElement>($"/trips/{id}/participants");
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsJsonAsync($"/trips/{id}/join-requests/{requestId}/approve",
            new { participantId = people[0].GetProperty("id").GetGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await applicant.GetAsync($"/trip-join-requests/{requestId}")).StatusCode);
        await PostAsync(owner, $"/trips/{id}/join-requests/{requestId}/approve", new { });
    }

    [Fact]
    public async Task CodeGuessingIsRateLimited()
    {
        await using var factory = await CreateAsync(database);
        using var applicant = await factory.RegisterAsync();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.NotFound, (await applicant.PostAsJsonAsync("/trip-join-requests", new { code = "000000000000" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await applicant.PostAsJsonAsync("/trip-join-requests", new { code = "000000000000" })).StatusCode);
    }
}
