namespace ExpenseSplitter.Application.Participants;

public sealed record ParticipantResult(Guid Id, string Name, bool IsRegistered, bool IsOwner);
