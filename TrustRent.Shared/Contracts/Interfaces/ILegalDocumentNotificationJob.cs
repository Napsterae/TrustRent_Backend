namespace TrustRent.Shared.Contracts.Interfaces;

public interface ILegalDocumentNotificationJob
{
    Task SendPublishedVersionNotificationAsync(Guid legalDocumentVersionId);
}