using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ExpenseSplitter.Infrastructure.Identity;

public sealed class SmtpOptions
{
    public string? Host { get; init; }
    public int Port { get; init; } = 587;
    public string? UserName { get; init; }
    public string? Password { get; init; }
    public string? FromAddress { get; init; }
    public bool EnableSsl { get; init; } = true;
}

internal sealed class SmtpIdentityEmailSender(IOptions<SmtpOptions> options) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Expense Splitter email confirmation", confirmationLink);

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Expense Splitter password reset", resetLink);

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Expense Splitter password reset",
            $"Use this one-time code to reset your password:\n\n{resetCode}\n\nIf you did not request this, ignore the message.");

    private async Task SendAsync(string email, string subject, string body)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new InvalidOperationException("Email:Smtp:Host and Email:Smtp:FromAddress must be configured.");

        using var message = new MailMessage(settings.FromAddress, email)
        {
            Subject = subject,
            Body = body
        };
        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl
        };
        if (!string.IsNullOrWhiteSpace(settings.UserName))
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);

        await client.SendMailAsync(message);
    }
}
