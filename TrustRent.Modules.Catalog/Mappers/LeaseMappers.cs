using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Mappers;

public static class LeaseMappers
{
    public static LeaseDto ToDto(this Lease lease)
    {
        return new LeaseDto
        {
            Id = lease.Id,
            PropertyId = lease.PropertyId,
            TenantId = lease.TenantId,
            LandlordId = lease.LandlordId,
            ApplicationId = lease.ApplicationId,
            StartDate = lease.StartDate,
            EndDate = lease.EndDate,
            DurationMonths = lease.DurationMonths,
            AllowsRenewal = lease.AllowsRenewal,
            RenewalDate = lease.RenewalDate,
            MonthlyRent = lease.MonthlyRent,
            Deposit = lease.Deposit,
            LeaseRegime = lease.LeaseRegime,
            ContractType = lease.ContractType,
            CondominiumFeesPaidBy = lease.CondominiumFeesPaidBy,
            WaterPaidBy = lease.WaterPaidBy,
            ElectricityPaidBy = lease.ElectricityPaidBy,
            GasPaidBy = lease.GasPaidBy,
            ContractFilePath = lease.ContractFilePath,
            ContractGeneratedAt = lease.ContractGeneratedAt,
            ContractSignedAt = lease.ContractSignedAt,
            LandlordSigned = lease.LandlordSigned,
            LandlordSignedAt = lease.LandlordSignedAt,
            TenantSigned = lease.TenantSigned,
            TenantSignedAt = lease.TenantSignedAt,
            Status = lease.Status.ToString(),
            CreatedAt = lease.CreatedAt,
            UpdatedAt = lease.UpdatedAt,
            History = lease.History?
                .Select(h => new LeaseHistoryDto
                {
                    Id = h.Id,
                    ActorId = h.ActorId,
                    Action = h.Action,
                    Message = h.Message,
                    EventData = h.EventData,
                    CreatedAt = h.CreatedAt
                })
                .OrderBy(h => h.CreatedAt)
                .ToList() ?? new()
        };
    }
}
