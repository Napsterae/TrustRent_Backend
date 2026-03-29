namespace TrustRent.Shared.Contracts.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}
