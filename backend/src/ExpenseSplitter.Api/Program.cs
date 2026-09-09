using Microsoft.OpenApi;
using ExpenseSplitter.Api;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Application;
using ExpenseSplitter.Api.Endpoints;
using ExpenseSplitter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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
    .AllowAnyMethod()));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (WriteConflictException)
    {
        await Results.Problem(statusCode: 409,
            title: "Поездка изменилась. Обновите данные перед повторной попыткой.").ExecuteAsync(context);
    }
});
// Liveness only: this endpoint does not check PostgreSQL readiness.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapTripEndpoints();
app.MapParticipantEndpoints();
app.MapExpenseEndpoints();
app.MapBalanceEndpoints();
app.MapSettlementEndpoints();

app.Run();

public partial class Program;
