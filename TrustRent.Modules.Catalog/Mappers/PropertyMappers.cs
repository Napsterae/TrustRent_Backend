using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Mappers;

public static class PropertyMappers
{
    // Método de extensão para converter um único Property para DTO
    public static PropertySummaryDto ToSummaryDto(this Property p)
    {
        return new PropertySummaryDto(
            p.Id,
            p.Title,
            p.District,
            p.Municipality,
            p.Parish,
            p.IsPublic,
            p.IsUnderMaintenance,
            p.Images.FirstOrDefault(i => i.IsMain)?.Url ?? ""
        );
    }

    // Método de extensão para converter uma lista inteira
    public static IEnumerable<PropertySummaryDto> ToSummaryDtoList(this IEnumerable<Property> properties)
    {
        return properties.Select(p => p.ToSummaryDto());
    }

    // 1. DTO -> Entidade (Para o Create)
    public static Property ToEntity(this CreatePropertyDto dto, Guid landlordId)
    {
        return new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = landlordId,
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            PropertyType = dto.PropertyType,
            Typology = dto.Typology,
            Area = dto.Area,
            Rooms = dto.Rooms,
            Bathrooms = dto.Bathrooms,
            Floor = dto.Floor,
            HasElevator = dto.HasElevator,
            HasAirConditioning = dto.HasAirConditioning,
            HasGarage = dto.HasGarage,
            AllowsPets = dto.AllowsPets,
            IsFurnished = dto.IsFurnished,
            FurnishedDescription = dto.FurnishedDescription,
            Street = dto.Street,
            PostalCode = dto.PostalCode,
            Municipality = dto.Municipality,
            Parish = dto.Parish,
            District = dto.District,
            DoorNumber = dto.DoorNumber,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            IsPublic = dto.IsPublic,
            ParishConcelho = dto.ParishConcelho,
            PermanentCertNumber = dto.PermanentCertNumber,
            PermanentCertOffice = dto.PermanentCertOffice,
            UsageLicenseNumber = dto.UsageLicenseNumber,
            UsageLicenseDate = dto.UsageLicenseDate,
            UsageLicenseIssuer = dto.UsageLicenseIssuer,
            Deposit = dto.Deposit,
            AdvanceRentMonths = dto.AdvanceRentMonths,
            CondominiumFeesPaidBy = dto.CondominiumFeesPaidBy,
            WaterPaidBy = dto.WaterPaidBy,
            ElectricityPaidBy = dto.ElectricityPaidBy,
            GasPaidBy = dto.GasPaidBy,
            HasOfficialContract = dto.HasOfficialContract,
            AcceptsGuarantor = dto.HasOfficialContract && dto.AcceptsGuarantor,
            GuarantorPolicyNote = (dto.HasOfficialContract && dto.AcceptsGuarantor) ? dto.GuarantorPolicyNote : null,
            LeaseRegime = Enum.TryParse<LeaseRegime>(dto.LeaseRegime, out var regime) ? regime : null,
            AllowsRenewal = true,
            NonPermanentReason = dto.NonPermanentReason,
        };
    }

    // 2. DTO -> Entidade Existente (Para o Update)
    public static void UpdateEntity(this CreatePropertyDto dto, Property property)
    {
        property.Title = dto.Title;
        property.Description = dto.Description;
        property.Price = dto.Price;
        property.PropertyType = dto.PropertyType;
        property.Typology = dto.Typology;
        property.Area = dto.Area;
        property.Rooms = dto.Rooms;
        property.Bathrooms = dto.Bathrooms;
        property.Floor = dto.Floor;
        property.HasElevator = dto.HasElevator;
        property.HasAirConditioning = dto.HasAirConditioning;
        property.HasGarage = dto.HasGarage;
        property.AllowsPets = dto.AllowsPets;
        property.IsFurnished = dto.IsFurnished;
        property.FurnishedDescription = dto.FurnishedDescription;
        property.Street = dto.Street;
        property.PostalCode = dto.PostalCode;
        property.Municipality = dto.Municipality;
        property.Parish = dto.Parish;
        property.District = dto.District;
        property.DoorNumber = dto.DoorNumber;
        property.Latitude = dto.Latitude;
        property.Longitude = dto.Longitude;
        property.IsPublic = dto.IsPublic;
        property.ParishConcelho = dto.ParishConcelho;
        property.PermanentCertNumber = dto.PermanentCertNumber;
        property.PermanentCertOffice = dto.PermanentCertOffice;
        property.UsageLicenseNumber = dto.UsageLicenseNumber;
        property.UsageLicenseDate = dto.UsageLicenseDate;
        property.UsageLicenseIssuer = dto.UsageLicenseIssuer;
        property.Deposit = dto.Deposit;
        property.AdvanceRentMonths = dto.AdvanceRentMonths;
        property.CondominiumFeesPaidBy = dto.CondominiumFeesPaidBy;
        property.WaterPaidBy = dto.WaterPaidBy;
        property.ElectricityPaidBy = dto.ElectricityPaidBy;
        property.GasPaidBy = dto.GasPaidBy;
        property.HasOfficialContract = dto.HasOfficialContract;
        property.AcceptsGuarantor = dto.HasOfficialContract && dto.AcceptsGuarantor;
        property.GuarantorPolicyNote = (dto.HasOfficialContract && dto.AcceptsGuarantor) ? dto.GuarantorPolicyNote : null;
        property.LeaseRegime = Enum.TryParse<LeaseRegime>(dto.LeaseRegime, out var regime2) ? regime2 : null;
        property.AllowsRenewal = true;
        property.NonPermanentReason = dto.NonPermanentReason;
    }

    public static PropertySearchDto ToSearchDto(this Property p)
    {
        return new PropertySearchDto(
            p.Id,
            p.Title,
            p.Municipality,
            p.Parish,
            p.Price, p.PropertyType, p.Typology,
            p.Area, p.Rooms, p.Bathrooms, p.AllowsPets,
            p.Images.FirstOrDefault(i => i.IsMain)?.Url ?? "",
            p.HasOfficialContract,
            p.TenantId == null
        );
    }
}
