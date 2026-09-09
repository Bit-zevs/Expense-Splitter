using ExpenseSplitter.Application.Participants;

namespace ExpenseSplitter.Application.Access;

public sealed record JoinRequestResult(Guid Id, Guid TripId, DateTimeOffset RequestedAt);
public sealed record PendingMemberResult(Guid Id, string DisplayName, DateTimeOffset RequestedAt);

public interface ITripMembershipService
{
    Task<string> RotateCodeAsync(Guid tripId, CancellationToken cancellationToken);
    Task<JoinRequestResult> RequestAsync(string? code, CancellationToken cancellationToken);
    Task<JoinRequestResult> GetRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task CancelAsync(Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingMemberResult>> ListAsync(Guid tripId, CancellationToken cancellationToken);
    Task<ParticipantResult> ApproveAsync(Guid tripId, Guid requestId, Guid? participantId, CancellationToken cancellationToken);
    Task RejectAsync(Guid tripId, Guid requestId, CancellationToken cancellationToken);
    Task TransferOwnerAsync(Guid tripId, Guid accountId, CancellationToken cancellationToken);
}
