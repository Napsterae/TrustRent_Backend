using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface IContractGenerationService
{
    /// <summary>
    /// Generates the lease contract PDF, saves it to disk, and returns the file path.
    /// </summary>
    Task<string> GenerateContractPdfAsync(Lease lease, string landlordName, string landlordNif,
        string landlordAddress, string tenantName, string tenantNif, string tenantAddress);

    /// <summary>
    /// Returns the raw PDF bytes for download.
    /// </summary>
    Task<byte[]> GetContractBytesAsync(string contractFilePath);
}
