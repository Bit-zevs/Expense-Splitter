using Microsoft.OpenApi;
using ExpenseSplitter.Api;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Application;
using ExpenseSplitter.Api.Endpoints;
using ExpenseSplitter.Infrastructure;
using ExpenseSplitter.Application.Access;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using System.Threading.RateLimiting;
using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
var parsedKnownProxies = knownProxies.Select(value =>
    IPAddress.TryParse(value, out var address)
        ? address
        : throw new InvalidOperationException($"ReverseProxy:KnownProxies contains an invalid IP address: '{value}'."))
    .ToArray();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.RequireHeaderSymmetry = true;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    foreach (var address in parsedKnownProxies) options.KnownProxies.Add(address);
});

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName(builder.Configuration["DataProtection:ApplicationName"] ?? "ExpenseSplitter");
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    var absoluteKeysPath = Path.GetFullPath(dataProtectionKeysPath, builder.Environment.ContentRootPath);
    Directory.CreateDirectory(absoluteKeysPath);
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(absoluteKeysPath));
}
else if (builder.Environment.IsProduction())
{
    throw new InvalidOperationException("DataProtection:KeysPath must point to persistent storage in Production.");
}

builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, cancellationToken) =>
{
    if (context.JsonTypeInfo.Type == typeof(decimal))
    {
        schema.Type = JsonSchemaType.String;
        schema.Format = null;
        schema.Pattern = @"^-?[0-9]+(?:\.[0-9]{1,2})?$";
        schema.Description = "Exact decimal money encoded as a string. JSON numbers are accepted for legacy input only.";
    }
    return Task.CompletedTask;
}));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new DecimalJsonConverter()));
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowCredentials()
    .AllowAnyMethod()));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentAccount, CurrentAccount>();
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-ExpenseSplitter.Csrf";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("join", context => RateLimitPartition.GetFixedWindowLimiter(
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    try
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method) && context.GetEndpoint() is not null)
            await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context);
        await next(context);
    }
    catch (AntiforgeryValidationException)
    {
        await Results.Problem(statusCode: 400, title: "Invalid CSRF token. Fetch /auth/csrf and retry.").ExecuteAsync(context);
    }
    catch (AccessException exception)
    {
        await Results.Problem(statusCode: exception.StatusCode, title: exception.StatusCode == 404 ? "Not found." : "Access denied.").ExecuteAsync(context);
    }
    catch (ConflictException exception)
    {
        await Results.Problem(statusCode: 409, title: exception.Message).ExecuteAsync(context);
    }
    catch (ArgumentException exception)
    {
        await Results.ValidationProblem(new Dictionary<string, string[]> { [exception.ParamName ?? "request"] = [exception.Message] }).ExecuteAsync(context);
    }
    catch (WriteConflictException)
    {
        await Results.Problem(statusCode: 409,
            title: "Поездка изменилась. Обновите данные перед повторной попыткой.").ExecuteAsync(context);
    }
});
// Liveness only: this endpoint does not check PostgreSQL readiness.
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
var protectedApi = app.MapGroup("").RequireAuthorization()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden)
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status409Conflict)
    .ProducesValidationProblem();
protectedApi.MapMembershipEndpoints();
protectedApi.MapTripEndpoints();
protectedApi.MapParticipantEndpoints();
protectedApi.MapExpenseEndpoints();
protectedApi.MapBalanceEndpoints();
protectedApi.MapSettlementEndpoints();

app.Run();

public partial class Program;
