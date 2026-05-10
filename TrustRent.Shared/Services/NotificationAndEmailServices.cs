using System.Threading;
using Microsoft.Extensions.Logging;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Shared.Services;

public class EmailService : IEmailService
{
    private static int _stubModeLogged;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        if (Interlocked.Exchange(ref _stubModeLogged, 1) == 0)
        {
            _logger.LogWarning("EmailService está em modo stub. O backend regista os envios mas ainda não comunica com um provider externo.");
        }

        _logger.LogInformation("Stub email queued for {Recipient} with subject {Subject}", to, subject);
        await Task.CompletedTask;
    }
}
