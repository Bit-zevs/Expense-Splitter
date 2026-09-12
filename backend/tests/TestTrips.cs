global using ExpenseSplitter.Testing;
using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Testing;

internal static class TestTrips
{
    public static Guid OwnerId { get; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
    // Calculator/persistence fixtures construct only the graph relevant to their test.
    public static Trip Create(string name, string currency = Trip.DefaultCurrency) => new(name, OwnerId, currency);
}
