using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ExpenseSplitter.Application.Trips;
using ExpenseSplitter.Infrastructure.Persistence;
using ExpenseSplitter.Application.Access;
using ExpenseSplitter.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;

namespace ExpenseSplitter.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ExpenseSplitter");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ExpenseSplitter' must be configured.");
        }

        services.AddDbContext<ExpenseSplitterDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<ITripStore, TripStore>();
        services.AddScoped<TripAccess>();
        services.AddScoped<ITripAccess>(sp => sp.GetRequiredService<TripAccess>());
        services.AddScoped<ITripMembershipService, TripMembershipService>();
        services.Configure<SmtpOptions>(configuration.GetSection("Email:Smtp"));
        services.AddScoped<IEmailSender<ApplicationUser>, SmtpIdentityEmailSender>();
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
            .AddEntityFrameworkStores<ExpenseSplitterDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<AccountClaimsFactory>()
            .AddDefaultTokenProviders();
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "__Host-ExpenseSplitter.Auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        return services;
    }
}
