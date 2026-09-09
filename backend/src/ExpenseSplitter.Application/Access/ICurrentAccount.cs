namespace ExpenseSplitter.Application.Access;

public interface ICurrentAccount
{
    Guid Id { get; }
    string DisplayName { get; }
}

public sealed class AccessException(int statusCode) : Exception("Access denied.")
{
    public int StatusCode { get; } = statusCode;
}

public sealed class ConflictException(string message) : Exception(message);

// Writes hold a trip row lock until SaveChangesAsync (or scoped DbContext disposal).
public interface ITripAccess
{
    Task RequireAsync(Guid tripId, bool ownerOnly, bool write, CancellationToken cancellationToken);
}
