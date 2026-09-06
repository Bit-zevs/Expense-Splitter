using ExpenseSplitter.Domain.Entities;
using ExpenseSplitter.Domain.ValueObjects;
using ExpenseSplitter.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ExpenseSplitter.Infrastructure.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void MapsOnlyPersistedStateAndUsesBackingFields()
    {
        using var context = CreateContext();
        var model = context.Model;
        Assert.Equal(4, model.GetEntityTypes().Count());
        Assert.Null(model.FindEntityType(typeof(SettlementTransfer)));

        var trip = model.FindEntityType(typeof(Trip))!;
        var expense = model.FindEntityType(typeof(Expense))!;
        var share = model.GetEntityTypes().Single(entity => entity.ClrType == typeof(ExpenseShare));

        Assert.Null(expense.FindProperty(nameof(Expense.ParticipantIds)));
        Assert.Null(expense.FindNavigation(nameof(Expense.ParticipantIds)));
        Assert.True(share.IsOwned());
        Assert.Equal(new[] { "ExpenseId", "ParticipantId" },
            share.FindPrimaryKey()!.Properties.Select(property => property.Name));

        AssertNavigation(trip, nameof(Trip.Participants), "_participants");
        AssertNavigation(trip, nameof(Trip.Expenses), "_expenses");
        AssertNavigation(expense, nameof(Expense.Shares), "_shares");
    }

    [Fact]
    public void RelationalSchemaHasExactMoneyTypesAndNoRedundantIndexes()
    {
        using var context = CreateContext();
        var model = context.GetService<IDesignTimeModel>().Model;
        var tables = model.GetRelationalModel().Tables.ToDictionary(table => table.Name);

        Assert.Equal(4, tables.Count);
        Assert.Equal("numeric(29,2)", tables["Expenses"].FindColumn("Amount")!.StoreType);
        Assert.Equal("numeric(29,2)", tables["ExpenseParticipants"].FindColumn("Amount")!.StoreType);
        Assert.Equal(new[] { "TripId" },
            Assert.Single(tables["Participants"].Indexes).Columns.Select(column => column.Name));
        Assert.Equal(2, tables["Expenses"].Indexes.Count());
        Assert.Contains(tables["Expenses"].Indexes,
            index => index.Columns.Select(column => column.Name).SequenceEqual(new[] { "TripId", "CreatedAt" }));
        Assert.Contains(tables["Expenses"].Indexes,
            index => index.Columns.Select(column => column.Name).SequenceEqual(new[] { "PaidByParticipantId" }));
        Assert.Equal(new[] { "ParticipantId" },
            Assert.Single(tables["ExpenseParticipants"].Indexes).Columns.Select(column => column.Name));
        Assert.Empty(tables["Trips"].Indexes);

        Assert.All(model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()), foreignKey =>
        {
            Assert.True(foreignKey.IsRequired);
            Assert.Equal(
                foreignKey.PrincipalEntityType.ClrType == typeof(Participant)
                    ? DeleteBehavior.NoAction
                    : DeleteBehavior.Cascade,
                foreignKey.DeleteBehavior);
        });
    }

    [Fact]
    public void MigrationsMatchCurrentModel()
    {
        using var context = CreateContext();
        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static ExpenseSplitterDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ExpenseSplitterDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);

    private static void AssertNavigation(IEntityType entity, string name, string field)
    {
        var navigation = entity.FindNavigation(name)!;
        Assert.Equal(field, navigation.FieldInfo!.Name);
        Assert.Equal(PropertyAccessMode.Field, navigation.GetPropertyAccessMode());
    }
}
