namespace ExpenseSplitter.Application.Participants;

public sealed record ParticipantResult(Guid Id, string Name, Guid? AccountId = null);
