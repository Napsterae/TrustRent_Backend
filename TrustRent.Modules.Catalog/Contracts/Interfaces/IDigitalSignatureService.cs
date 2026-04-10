namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IDigitalSignatureService
{
    /// <summary>
    /// Initiates CMD signature process. Returns a processId (transactionId) and sends OTP via SMS.
    /// </summary>
    Task<CmdSignatureInitResult> InitiateCmdSignatureAsync(string documentHash, string phoneNumber, string userEmail);

    /// <summary>
    /// Verifies the OTP and confirms the signature. Returns the signature reference on success.
    /// </summary>
    Task<CmdSignatureConfirmResult> VerifyCmdSignatureAsync(string processId, string otpCode);

    /// <summary>
    /// Queries the current status of a CMD signature process.
    /// </summary>
    Task<CmdSignatureStatus> GetSignatureStatusAsync(string processId);
}

public record CmdSignatureInitResult(bool Success, string ProcessId, string? ErrorMessage = null);
public record CmdSignatureConfirmResult(bool Success, string? SignatureRef, string? ErrorMessage = null);
public record CmdSignatureStatus(string ProcessId, string State, DateTime? CompletedAt);
