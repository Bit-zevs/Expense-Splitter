using ExpenseSplitter.Application.Expenses;
using ExpenseSplitter.Application.Expenses.CreateEqualExpense;
using ExpenseSplitter.Application.Expenses.GetExpense;
using ExpenseSplitter.Application.Expenses.GetExpenses;

namespace ExpenseSplitter.Api.Endpoints;

public static class ExpenseEndpoints
{
    public static IEndpointRouteBuilder MapExpenseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/trips/{tripId:guid}/expenses", CreateEqualExpenseAsync)
            .WithName("CreateEqualExpense")
            .WithSummary("Creates an equally split expense")
            .Produces<ExpenseResult>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/trips/{tripId:guid}/expenses", GetExpensesAsync)
            .WithName("GetExpenses")
            .WithSummary("Gets a trip's expenses")
            .Produces<IReadOnlyCollection<ExpenseResult>>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet(
                "/trips/{tripId:guid}/expenses/{expenseId:guid}",
                GetExpenseAsync)
            .WithName("GetExpense")
            .WithSummary("Gets a trip expense by ID")
            .Produces<ExpenseResult>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateEqualExpenseAsync(
        Guid tripId,
        CreateEqualExpenseRequest request,
        CreateEqualExpenseHandler handler,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateEqualExpenseCommand(
                request.Amount,
                request.Description,
                request.PaidByParticipantId,
                request.ParticipantIds);
            var result = await handler.HandleAsync(tripId, command, cancellationToken);

            return result is null
                ? Results.NotFound()
                : Results.CreatedAtRoute(
                    "GetExpense",
                    new { tripId, expenseId = result.Id },
                    result);
        }
        catch (ArgumentException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "expense"] = [exception.Message]
            });
        }
    }

    private static async Task<IResult> GetExpensesAsync(
        Guid tripId,
        GetExpensesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    private static async Task<IResult> GetExpenseAsync(
        Guid tripId,
        Guid expenseId,
        GetExpenseHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(tripId, expenseId, cancellationToken);

        return result is null
            ? Results.NotFound()
            : Results.Ok(result);
    }

    public sealed record CreateEqualExpenseRequest(
        decimal Amount,
        string? Description,
        Guid PaidByParticipantId,
        IReadOnlyCollection<Guid>? ParticipantIds);
}
