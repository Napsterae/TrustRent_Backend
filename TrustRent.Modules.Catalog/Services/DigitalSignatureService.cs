using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Catalog.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Services;

/// <summary>
/// Mock implementation of CMD (Chave Móvel Digital) signature service.
/// Replace with real CMD API integration when registered as entity with AMA/IRN.
/// CMD API: https://www.autenticacao.gov.pt/cmd-assinatura
/// </summary>
public class DigitalSignatureService : IDigitalSignatureService
{
    private readonly bool _mockEnabled;
    private readonly ILogger<DigitalSignatureService> _logger;

    // In-memory store for mock OTPs (processId -> otp). Production: use distributed cache.
    private static readonly Dictionary<string, (string Otp, DateTime Expiry, string Phone, string Email)> _mockSessions = new();
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public DigitalSignatureService(IConfiguration configuration, ILogger<DigitalSignatureService> logger)
    {
        var raw = configuration["DigitalSignature:CMD:MockEnabled"];
        _mockEnabled = string.IsNullOrEmpty(raw) || bool.Parse(raw);
        _logger = logger;
    }

    public async Task<CmdSignatureInitResult> InitiateCmdSignatureAsync(
        string documentHash, string phoneNumber, string userEmail)
    {
        if (_mockEnabled)
        {
            var processId = Guid.NewGuid().ToString("N");
            var otp = new Random().Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(10);

            await _semaphore.WaitAsync();
            try { _mockSessions[processId] = (otp, expiry, phoneNumber, userEmail); }
            finally { _semaphore.Release(); }

            _logger.LogInformation("[CMD MOCK] OTP para {Phone}: {Otp} (processId: {ProcessId})", phoneNumber, otp, processId);
            // Em produção: enviar OTP via SMS através da API CMD da AMA

            return new CmdSignatureInitResult(true, processId);
        }

        // TODO: Integrar com API real CMD
        // POST https://cmd.autenticacao.gov.pt/Ama.Authentication.Service/CCMovelDigitalSign.svc
        // RequiredParams: applicationId, documentHash, phoneNumber
        throw new NotImplementedException("Integração CMD real ainda não implementada. Configure DigitalSignature:CMD:MockEnabled=true para utilizar o mock.");
    }

    public async Task<CmdSignatureConfirmResult> VerifyCmdSignatureAsync(string processId, string otpCode)
    {
        if (_mockEnabled)
        {
            (string Otp, DateTime Expiry, string Phone, string Email) session;

            await _semaphore.WaitAsync();
            try
            {
                if (!_mockSessions.TryGetValue(processId, out session))
                    return new CmdSignatureConfirmResult(false, null, "Processo de assinatura não encontrado ou expirado.");
            }
            finally { _semaphore.Release(); }

            if (DateTime.UtcNow > session.Expiry)
            {
                await _semaphore.WaitAsync();
                try { _mockSessions.Remove(processId); }
                finally { _semaphore.Release(); }
                return new CmdSignatureConfirmResult(false, null, "O código OTP expirou. Por favor solicite um novo código.");
            }

            if (session.Otp != otpCode)
                return new CmdSignatureConfirmResult(false, null, "Código OTP inválido.");

            var signatureRef = $"CMD-MOCK-{processId[..8].ToUpper()}-{DateTime.UtcNow:yyyyMMddHHmmss}";

            await _semaphore.WaitAsync();
            try { _mockSessions.Remove(processId); }
            finally { _semaphore.Release(); }

            _logger.LogInformation("[CMD MOCK] Assinatura confirmada. Ref: {Ref}", signatureRef);
            return new CmdSignatureConfirmResult(true, signatureRef);
        }

        throw new NotImplementedException("Integração CMD real ainda não implementada.");
    }

    public async Task<CmdSignatureStatus> GetSignatureStatusAsync(string processId)
    {
        if (_mockEnabled)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_mockSessions.TryGetValue(processId, out var session))
                {
                    var state = DateTime.UtcNow > session.Expiry ? "Expired" : "Pending";
                    return new CmdSignatureStatus(processId, state, null);
                }
            }
            finally { _semaphore.Release(); }

            return new CmdSignatureStatus(processId, "Completed", DateTime.UtcNow);
        }

        throw new NotImplementedException("Integração CMD real ainda não implementada.");
    }
}
