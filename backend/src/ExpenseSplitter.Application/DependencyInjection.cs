using Microsoft.Extensions.DependencyInjection;
using ExpenseSplitter.Application.Trips.CreateTrip;
using ExpenseSplitter.Application.Trips.GetTrip;

namespace ExpenseSplitter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateTripHandler>();
        services.AddScoped<GetTripHandler>();

        return services;
    }
}
