using Microsoft.EntityFrameworkCore;

namespace ExpenseSplitter.Infrastructure.Persistence;

public sealed class ExpenseSplitterDbContext(DbContextOptions<ExpenseSplitterDbContext> options)
    : DbContext(options)
{
}
