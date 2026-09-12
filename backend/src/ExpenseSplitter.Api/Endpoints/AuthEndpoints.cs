using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using ExpenseSplitter.Infrastructure.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace ExpenseSplitter.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/auth").RequireRateLimiting("auth")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem();
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            TypedResults.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken }))
            .AllowAnonymous();
        auth.MapPost("/register", async (RegisterRequest request, UserManager<ApplicationUser> users, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 100)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = ["Display name must contain 1 to 100 characters."] });
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 || !new EmailAddressAttribute().IsValid(request.Email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email is required."] });
            if (string.IsNullOrEmpty(request.Password) || request.Password.Length > 128)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["Password must contain 1 to 128 characters and satisfy Identity password rules."] });
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = request.Email.Trim(), Email = request.Email.Trim(),
                DisplayName = request.DisplayName.Trim()
            };
            var result = await users.CreateAsync(user, request.Password);
            if (!result.Succeeded) return IdentityProblem(result);
            // Match the Identity registration contract: register does not sign the account in.
            return Results.Ok();
        }).AllowAnonymous();
        auth.MapPost("/login", async (LoginRequest request, SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrEmpty(request.Password)
                || request.Email.Length > 256 || request.Password.Length > 128) return Results.Unauthorized();
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null) return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
            return result.Succeeded ? Results.Ok() : Results.Unauthorized();
        }).AllowAnonymous();
        auth.MapPost("/forgotPassword", async (ForgotPasswordRequest request,
            UserManager<ApplicationUser> users, IEmailSender<ApplicationUser> sender,
            CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidEmail(request.Email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email is required."] });

            var email = request.Email!.Trim();
            var user = await users.FindByEmailAsync(email);
            if (user is not null)
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                await sender.SendPasswordResetCodeAsync(user, email, HtmlEncoder.Default.Encode(code));
            }

            // Deliberately identical for known and unknown accounts.
            return Results.Ok();
        }).AllowAnonymous();
        auth.MapPost("/resetPassword", async (ResetPasswordRequest request,
            UserManager<ApplicationUser> users, CancellationToken cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!ValidEmail(request.Email))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["A valid email is required."] });
            if (string.IsNullOrWhiteSpace(request.ResetCode))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["resetCode"] = ["Reset code is required."] });
            if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length > 128)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["newPassword"] = ["A password of at most 128 characters is required."] });

            var user = await users.FindByEmailAsync(request.Email!.Trim());
            IdentityResult result;
            try
            {
                var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(request.ResetCode));
                result = user is null
                    ? IdentityResult.Failed(users.ErrorDescriber.InvalidToken())
                    : await users.ResetPasswordAsync(user, token, request.NewPassword);
            }
            catch (FormatException)
            {
                result = IdentityResult.Failed(users.ErrorDescriber.InvalidToken());
            }

            return result.Succeeded ? Results.Ok() : IdentityProblem(result);
        }).AllowAnonymous();
        auth.MapPost("/logout", async (SignInManager<ApplicationUser> signIn) =>
        {
            await signIn.SignOutAsync();
            return TypedResults.NoContent();
        }).RequireAuthorization();
        auth.MapPost("/change-password", async (ChangePasswordRequest request, HttpContext context,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword)
                || request.CurrentPassword.Length > 128 || request.NewPassword.Length > 128)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["A password of at most 128 characters is required."] });
            var user = await users.GetUserAsync(context.User);
            if (user is null) return Results.Unauthorized();
            var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded) return IdentityProblem(result);
            await signIn.RefreshSignInAsync(user);
            return Results.NoContent();
        }).RequireAuthorization();
    }

    private static IResult IdentityProblem(IdentityResult result) => Results.ValidationProblem(
        result.Errors.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));

    private static bool ValidEmail(string? email) => !string.IsNullOrWhiteSpace(email)
        && email.Length <= 256 && new EmailAddressAttribute().IsValid(email);

    public sealed record RegisterRequest(string? Email, string? Password, string? DisplayName);
    public sealed record LoginRequest(string? Email, string? Password, bool RememberMe = false);
    public sealed record ForgotPasswordRequest(string? Email);
    public sealed record ResetPasswordRequest(string? Email, string? ResetCode, string? NewPassword);
    public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
}
