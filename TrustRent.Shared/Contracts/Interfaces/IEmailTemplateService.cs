namespace TrustRent.Shared.Contracts.Interfaces;

public interface IEmailTemplateService
{
    string RenderTransactionalEmail(string subject, string body);
}