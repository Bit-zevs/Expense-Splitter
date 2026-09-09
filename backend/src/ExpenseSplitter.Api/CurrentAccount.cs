using System.Security.Claims;
using ExpenseSplitter.Application.Access;

namespace ExpenseSplitter.Api;

internal sealed class CurrentAccount(IHttpContextAccessor accessor) : ICurrentAccount
{
    public Guid Id => accessor.HttpContext?.User is { Identity.IsAuthenticated: true } user
        && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty
        ? id : throw new AccessException(401);

    public string DisplayName => accessor.HttpContext?.User.FindFirstValue("display_name")
        ?? throw new AccessException(401);
}
