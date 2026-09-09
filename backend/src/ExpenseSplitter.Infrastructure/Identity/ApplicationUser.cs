using Microsoft.AspNetCore.Identity;

namespace ExpenseSplitter.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
