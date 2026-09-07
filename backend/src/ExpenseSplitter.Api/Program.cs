using ExpenseSplitter.Application;
using ExpenseSplitter.Api.Endpoints;
using ExpenseSplitter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapTripEndpoints();
app.MapParticipantEndpoints();
app.MapExpenseEndpoints();
app.MapBalanceEndpoints();
app.MapSettlementEndpoints();

app.Run();

public partial class Program;
