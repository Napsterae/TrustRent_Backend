namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

public interface IDigitalSignatureService
{
    Task<CmdSignatureInitResult> InitiateCmdSignatureAsync(string documentHash, string phoneNumber, string userEmail);
    Task<CmdSignatureConfirmResult> VerifyCmdSignatureAsync(string processId, string otpCode);
    Task<CmdSignatureStatus> GetSignatureStatusAsync(string processId);
}

public record CmdSignatureInitResult(bool Success, string ProcessId, string? ErrorMessage = null);
public record CmdSignatureConfirmResult(bool Success, string? SignatureRef, string? ErrorMessage = null);
public record CmdSignatureStatus(string ProcessId, string State, DateTime? CompletedAt);
