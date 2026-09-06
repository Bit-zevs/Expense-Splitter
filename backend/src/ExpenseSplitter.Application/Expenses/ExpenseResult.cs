using ExpenseSplitter.Domain.Entities;

namespace ExpenseSplitter.Application.Expenses;

public sealed record ExpenseShareResult(Guid ParticipantId, decimal Amount);

public sealed record ExpenseResult(
    Guid Id,
    decimal Amount,
    string Description,
    Guid PaidByParticipantId,
    string SplitType,
    DateTimeOffset CreatedAt,
    IReadOnlyCollection<ExpenseShareResult> Shares)
{
    public static ExpenseResult FromExpense(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        var shares = expense.Shares
            .OrderBy(share => share.ParticipantId)
            .Select(share => new ExpenseShareResult(share.ParticipantId, share.Amount))
            .ToArray();

        return new ExpenseResult(
            expense.Id,
            expense.Amount,
            expense.Description,
            expense.PaidByParticipantId,
            expense.SplitType.ToString().ToLowerInvariant(),
            expense.CreatedAt,
            shares);
    }
}
