using ExpenseSplitter.Application.Trips.GetTripSnapshot;
using Microsoft.Extensions.DependencyInjection;
using ExpenseSplitter.Application.Balances.GetBalances;
using ExpenseSplitter.Application.Expenses.CreateEqualExpense;
using ExpenseSplitter.Application.Expenses.DeleteExpense;
using ExpenseSplitter.Application.Expenses.GetExpense;
using ExpenseSplitter.Application.Expenses.GetExpenses;
using ExpenseSplitter.Application.Participants.AddParticipant;
using ExpenseSplitter.Application.Participants.DeleteParticipant;
using ExpenseSplitter.Application.Participants.GetParticipant;
using ExpenseSplitter.Application.Participants.GetParticipants;
using ExpenseSplitter.Application.Settlements.GetSettlements;
using ExpenseSplitter.Application.Trips.CreateTrip;
using ExpenseSplitter.Application.Trips.DeleteTrip;
using ExpenseSplitter.Application.Trips.GetTrip;

namespace ExpenseSplitter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateTripHandler>();
        services.AddScoped<DeleteTripHandler>();
        services.AddScoped<GetTripHandler>();
        services.AddScoped<GetTripSnapshotHandler>();
        services.AddScoped<AddParticipantHandler>();
        services.AddScoped<DeleteParticipantHandler>();
        services.AddScoped<GetParticipantHandler>();
        services.AddScoped<GetParticipantsHandler>();
        services.AddScoped<CreateEqualExpenseHandler>();
        services.AddScoped<DeleteExpenseHandler>();
        services.AddScoped<GetExpenseHandler>();
        services.AddScoped<GetExpensesHandler>();
        services.AddScoped<GetBalancesHandler>();
        services.AddScoped<GetSettlementsHandler>();

        return services;
    }
}
