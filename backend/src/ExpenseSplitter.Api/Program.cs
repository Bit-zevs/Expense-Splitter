using ExpenseSplitter.Application;
using ExpenseSplitter.Api.Endpoints;
using ExpenseSplitter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapTripEndpoints();
app.MapParticipantEndpoints();

app.Run();

public partial class Program;
