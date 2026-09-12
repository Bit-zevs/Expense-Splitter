using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Application.Participants;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ExpenseSplitter.Infrastructure.Persistence;

internal sealed class TripMembershipService(
    ExpenseSplitterDbContext db, ICurrentAccount account, TripAccess access, ITripStore store)
    : ITripMembershipService
{
    public async Task<string> RotateCodeAsync(Guid tripId, CancellationToken cancellationToken)
    {
        await access.RequireWriteAsync(tripId, ownerOnly: true, cancellationToken);
        var trip = await db.Trips.SingleAsync(t => t.Id == tripId, cancellationToken);
        var code = JoinCode.Generate();
        trip.SetJoinCodeHash(JoinCode.Hash(code));
        await store.SaveChangesAsync(cancellationToken);
        return code;
    }

    public async Task<JoinRequestResult> RequestAsync(string? code, CancellationToken cancellationToken)
    {
        var accountId = account.Id;
        var hash = JoinCode.Hash(code);
        await access.BeginAsync(cancellationToken);
        var tripId = await db.Trips.Where(t => t.JoinCodeHash == hash).Select(t => (Guid?)t.Id)
            .SingleOrDefaultAsync(cancellationToken) ?? throw new AccessException(404);
        await access.LockAsync(tripId, cancellationToken);
        if (await db.Participants.AnyAsync(p => EF.Property<Guid>(p, "TripId") == tripId && p.AccountId == accountId, cancellationToken))
            throw new ConflictException("You already participate in this trip.");
        if (await db.TripJoinRequests.AnyAsync(r => r.TripId == tripId && r.AccountId == accountId, cancellationToken))
            throw new ConflictException("A pending request already exists.");
        var request = new TripJoinRequest(tripId, accountId);
        await db.TripJoinRequests.AddAsync(request, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return Result(request);
    }

    public async Task<JoinRequestResult> GetRequestAsync(Guid requestId, CancellationToken cancellationToken) =>
        Result(await FindOwnAsync(requestId, cancellationToken));

    public async Task CancelAsync(Guid requestId, CancellationToken cancellationToken)
    {
        await access.BeginAsync(cancellationToken);
        var request = await FindOwnAsync(requestId, cancellationToken);
        await access.LockAsync(request.TripId, cancellationToken);
        db.TripJoinRequests.Remove(request);
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PendingMemberResult>> ListAsync(Guid tripId, CancellationToken cancellationToken)
    {
        var accountId = account.Id;
        var accessState = await db.Trips.Where(t => t.Id == tripId)
            .Select(t => new
            {
                IsOwner = t.OwnerAccountId == accountId,
                IsMember = t.OwnerAccountId == accountId || t.Participants.Any(p => p.AccountId == accountId)
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (accessState is null || !accessState.IsMember) throw new AccessException(404);
        if (!accessState.IsOwner) throw new AccessException(403);

        return await (from request in db.TripJoinRequests
                      join user in db.Users on request.AccountId equals user.Id
                      where request.TripId == tripId
                          && db.Trips.Any(t => t.Id == tripId && t.OwnerAccountId == accountId)
                      orderby request.RequestedAt, request.Id
                      select new PendingMemberResult(request.Id, user.DisplayName, request.RequestedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ParticipantResult> ApproveAsync(Guid tripId, Guid requestId, Guid? participantId, CancellationToken cancellationToken)
    {
        await access.RequireWriteAsync(tripId, ownerOnly: true, cancellationToken);
        var request = await FindAsync(tripId, requestId, cancellationToken);
        var trip = await db.Trips.Include(t => t.Participants).SingleAsync(t => t.Id == tripId, cancellationToken);
        if (trip.Participants.Any(p => p.AccountId == request.AccountId))
            throw new ConflictException("Account already participates in this trip.");
        if (participantId is { } id)
        {
            var phantom = trip.Participants.SingleOrDefault(p => p.Id == id) ?? throw new AccessException(404);
            if (phantom.AccountId is not null) throw new ConflictException("Participant already has an account.");
        }
        var name = await db.Users.Where(u => u.Id == request.AccountId).Select(u => u.DisplayName)
            .SingleAsync(cancellationToken);
        var participant = trip.AddAccountParticipant(request.AccountId, name, participantId);
        db.TripJoinRequests.Remove(request);
        await store.SaveChangesAsync(cancellationToken);
        return new ParticipantResult(participant.Id, participant.Name, IsRegistered: true, IsOwner: false);
    }

    public async Task RejectAsync(Guid tripId, Guid requestId, CancellationToken cancellationToken)
    {
        await access.RequireWriteAsync(tripId, ownerOnly: true, cancellationToken);
        db.TripJoinRequests.Remove(await FindAsync(tripId, requestId, cancellationToken));
        await store.SaveChangesAsync(cancellationToken);
    }

    public async Task TransferOwnerAsync(Guid tripId, Guid participantId, CancellationToken cancellationToken)
    {
        await access.RequireWriteAsync(tripId, ownerOnly: true, cancellationToken);
        var trip = await db.Trips.Include(t => t.Participants).SingleAsync(t => t.Id == tripId, cancellationToken);
        var participant = trip.Participants.SingleOrDefault(p => p.Id == participantId)
            ?? throw new AccessException(404);
        if (participant.AccountId is not { } accountId)
            throw new ConflictException("Ownership can only be transferred to a registered participant.");
        trip.TransferOwnership(accountId);
        await store.SaveChangesAsync(cancellationToken);
    }

    private async Task<TripJoinRequest> FindOwnAsync(Guid id, CancellationToken cancellationToken)
    {
        var accountId = account.Id;
        return await db.TripJoinRequests.SingleOrDefaultAsync(r => r.Id == id && r.AccountId == accountId, cancellationToken)
            ?? throw new AccessException(404);
    }

    private async Task<TripJoinRequest> FindAsync(Guid tripId, Guid id, CancellationToken cancellationToken) =>
        await db.TripJoinRequests.SingleOrDefaultAsync(r => r.Id == id && r.TripId == tripId, cancellationToken)
        ?? throw new AccessException(404);

    private static JoinRequestResult Result(TripJoinRequest request) => new(request.Id, request.RequestedAt);
}
