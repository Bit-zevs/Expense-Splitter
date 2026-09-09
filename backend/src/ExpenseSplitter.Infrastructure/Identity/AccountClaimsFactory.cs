using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ExpenseSplitter.Infrastructure.Identity;

internal sealed class AccountClaimsFactory(UserManager<ApplicationUser> manager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser>(manager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("display_name", user.DisplayName));
        return identity;
    }
}
