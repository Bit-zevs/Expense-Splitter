using ExpenseSplitter.Domain.Enums;
using ExpenseSplitter.Domain.Services;

namespace ExpenseSplitter.Domain.Entities;

public sealed class Trip
{
    public const string DefaultCurrency = "RUB";

    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "RUB", "EUR", "USD" };

    private readonly List<Participant> _participants = [];
    private readonly List<Expense> _expenses = [];

    // Required by EF Core.
    private Trip() { }

    public Trip(string name, string currency = DefaultCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (!SupportedCurrencies.Contains(normalizedCurrency))
        {
            throw new ArgumentException(
                "Currency must be one of: RUB, EUR, USD.",
                nameof(currency));
        }

        Id = Guid.NewGuid();
        Name = name.Trim();
        Currency = normalizedCurrency;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedAt = CreatedAt.AddTicks(-(CreatedAt.Ticks % 10));
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Currency { get; private set; } = DefaultCurrency;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();

    public IReadOnlyCollection<Expense> Expenses => _expenses.AsReadOnly();

    public Participant AddParticipant(string name)
    {
        var participant = new Participant(name);
        _participants.Add(participant);
        return participant;
    }

    public Expense AddEqualExpenseForAll(
        decimal amount,
        string description,
        Guid paidByParticipantId)
    {
        return AddEqualExpense(
            amount,
            description,
            paidByParticipantId,
            _participants.Select(participant => participant.Id));
    }

    public Expense AddEqualExpense(
        decimal amount,
        string description,
        Guid paidByParticipantId,
        IEnumerable<Guid> participantIds)
    {
        ArgumentNullException.ThrowIfNull(participantIds);

        var memberIds = _participants.Select(participant => participant.Id).ToHashSet();
        if (!memberIds.Contains(paidByParticipantId))
        {
            throw new ArgumentException(
                "The payer must belong to the trip.",
                nameof(paidByParticipantId));
        }

        var selectedIds = participantIds.ToArray();
        if (selectedIds.Any(id => !memberIds.Contains(id)))
        {
            throw new ArgumentException(
                "All selected participants must belong to the trip.",
                nameof(participantIds));
        }

        var shares = EqualSplitCalculator.Calculate(amount, selectedIds);
        var expense = new Expense(
            amount,
            description,
            paidByParticipantId,
            SplitType.Equal,
            shares);

        _expenses.Add(expense);
        return expense;
    }
}
