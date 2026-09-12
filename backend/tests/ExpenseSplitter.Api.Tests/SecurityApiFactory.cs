using System.Net.Http.Json;
using System.Text.Json;
using ExpenseSplitter.Infrastructure.Persistence;
using ExpenseSplitter.Infrastructure.Tests;
using ExpenseSplitter.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ExpenseSplitter.Api.Tests;

internal sealed class SecurityApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    public const string Password = "Test-password-2026!";
    public TestPasswordResetCodeSender PasswordResetCodes { get; } = new();

    public static async Task<SecurityApiFactory> CreateAsync(PostgreSqlFixture fixture)
    {
        var options = await fixture.CreateDatabaseAsync();
        await using var db = new ExpenseSplitterDbContext(options);
        return new SecurityApiFactory(db.Database.GetConnectionString()!);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:ExpenseSplitter", connectionString);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<IEmailSender<ApplicationUser>>();
            services.AddSingleton<IEmailSender<ApplicationUser>>(PasswordResetCodes);
        });
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    public async Task<HttpClient> RegisterAsync(string displayName = "Traveler")
    {
        var client = Browser();
        var email = $"{Guid.NewGuid():N}@example.test";
        await RefreshCsrfAsync(client);
        using var registration = await client.PostAsJsonAsync("/auth/register", new { email, password = Password, displayName });
        registration.EnsureSuccessStatusCode();
        using var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        await RefreshCsrfAsync(client);
        return client;
    }

    public static async Task RefreshCsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    }

    public static async Task<JsonElement> PostAsync(HttpClient client, string uri, object body)
    {
        using var response = await client.PostAsJsonAsync(uri, body);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{uri}: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

internal sealed class TestPasswordResetCodeSender : IEmailSender<ApplicationUser>
{
    private readonly Dictionary<string, string> codes = new(StringComparer.OrdinalIgnoreCase);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        lock (codes) codes[email] = resetCode;
        return Task.CompletedTask;
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        Task.CompletedTask;

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        Task.CompletedTask;

    public string Get(string email)
    {
        lock (codes) return codes[email];
    }

    public int Count
    {
        get { lock (codes) return codes.Count; }
    }
}
