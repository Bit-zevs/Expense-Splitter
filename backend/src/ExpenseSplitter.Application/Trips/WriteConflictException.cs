namespace ExpenseSplitter.Application.Trips;

public sealed class WriteConflictException(Exception innerException)
    : Exception("The trip changed during this operation. Reload it before retrying.", innerException);
