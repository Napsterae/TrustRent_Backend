using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Shared.Contracts.DTOs;

namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

public interface ILeaseService
{
    Task<LeaseDto> InitiateLeaseProcedureAsync(Guid applicationId, Guid userId, InitiateLeaseProcedureDto dto);
    Task<LeaseDto> CounterProposeStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto);
    Task<LeaseDto> ConfirmLeaseStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto);
    Task<LeaseDto> UploadSignedContractAsync(Guid leaseId, Guid userId, byte[] pdfBytes, string originalFileName);
    Task<byte[]?> GetLandlordSignedContractAsync(Guid leaseId, Guid userId);
    Task<LeaseDto> RequestSignatureAsync(Guid leaseId, Guid userId, RequestLeaseSignatureDto dto);
    Task<LeaseDto> ConfirmSignatureAsync(Guid leaseId, Guid userId, ConfirmLeaseSignatureDto dto);
    Task<LeaseDto> AcceptLeaseTermsAsync(Guid leaseId, Guid userId, AcceptLeaseTermsDto dto);
    Task<LeaseDto?> GetLeaseByIdAsync(Guid leaseId, Guid userId);
    Task<LeaseDto?> GetLeaseByApplicationIdAsync(Guid applicationId, Guid userId);
    Task<LeaseSignatureStatusDto?> GetSignatureStatusAsync(Guid leaseId, Guid userId);
    Task<byte[]> GenerateContractAsync(Guid leaseId, Guid userId);
    Task<LeaseDto> CancelLeaseAsync(Guid leaseId, Guid userId, CancelLeaseDto dto);
    Task<IEnumerable<LeaseDto>> GetLeasesForTenantAsync(Guid tenantId);
}
