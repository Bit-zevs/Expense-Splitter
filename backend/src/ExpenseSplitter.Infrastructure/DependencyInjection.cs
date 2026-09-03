using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ExpenseSplitter.Infrastructure.Persistence;

namespace ExpenseSplitter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<ExpenseSplitterDbContext>(options =>
            options.UseInMemoryDatabase("expense-splitter"));

        return services;
    }
}
