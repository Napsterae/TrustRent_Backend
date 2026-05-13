namespace TrustRent.Shared.Contracts.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
    Task SendEmailAsync(string to, string subject, string body, EmailSendOptions options);
}

public sealed record EmailSendOptions(string? FromAddress = null, string? FromName = null, string? ReplyToAddress = null);
