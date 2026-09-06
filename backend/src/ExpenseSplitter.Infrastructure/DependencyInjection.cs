using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExpenseSplitter.Infrastructure.Persistence;

namespace ExpenseSplitter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ExpenseSplitter");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ExpenseSplitter' must be configured.");
        }

        services.AddDbContext<ExpenseSplitterDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
