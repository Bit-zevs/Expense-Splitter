using Microsoft.Extensions.DependencyInjection;
using ExpenseSplitter.Application.Trips.CreateTrip;

namespace ExpenseSplitter.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateTripHandler>();

        return services;
    }
}
