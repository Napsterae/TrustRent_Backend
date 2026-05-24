using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog;

public static class AmenityCatalog
{
    public static readonly Guid WifiId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid EquippedKitchenId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    public static readonly Guid WashingMachineId = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    public static readonly Guid IronId = Guid.Parse("a0000000-0000-0000-0000-000000000004");
    public static readonly Guid AirConditioningId = Guid.Parse("a0000000-0000-0000-0000-000000000005");
    public static readonly Guid CentralHeatingId = Guid.Parse("a0000000-0000-0000-0000-000000000006");
    public static readonly Guid TelevisionId = Guid.Parse("a0000000-0000-0000-0000-000000000007");
    public static readonly Guid CribId = Guid.Parse("a0000000-0000-0000-0000-000000000008");
    public static readonly Guid PoolId = Guid.Parse("a0000000-0000-0000-0000-000000000009");
    public static readonly Guid JacuzziId = Guid.Parse("a0000000-0000-0000-0000-00000000000a");
    public static readonly Guid GymId = Guid.Parse("a0000000-0000-0000-0000-00000000000b");
    public static readonly Guid BarbecueId = Guid.Parse("a0000000-0000-0000-0000-00000000000c");
    public static readonly Guid FireExtinguisherId = Guid.Parse("a0000000-0000-0000-0000-00000000000d");
    public static readonly Guid SmokeDetectorId = Guid.Parse("a0000000-0000-0000-0000-00000000000e");
    public static readonly Guid AlarmId = Guid.Parse("a0000000-0000-0000-0000-00000000000f");
    public static readonly Guid PetsId = Guid.Parse("a0000000-0000-0000-0000-000000000010");
    public static readonly Guid SupermarketId = Guid.Parse("a0000000-0000-0000-0000-000000000011");
    public static readonly Guid TransportId = Guid.Parse("a0000000-0000-0000-0000-000000000012");
    public static readonly Guid ElevatorId = Guid.Parse("a0000000-0000-0000-0000-000000000013");
    public static readonly Guid GarageId = Guid.Parse("a0000000-0000-0000-0000-000000000014");
    public static readonly Guid FurnishedId = Guid.Parse("a0000000-0000-0000-0000-000000000015");

    public static IReadOnlyList<Guid> MergeWithPropertyFeatureAmenities(
        IEnumerable<Guid>? amenityIds,
        bool hasElevator,
        bool hasAirConditioning,
        bool hasGarage,
        bool allowsPets,
        bool isFurnished)
    {
        var normalized = new HashSet<Guid>(amenityIds ?? Enumerable.Empty<Guid>());

        if (hasElevator)
        {
            normalized.Add(ElevatorId);
        }

        if (hasAirConditioning)
        {
            normalized.Add(AirConditioningId);
        }

        if (hasGarage)
        {
            normalized.Add(GarageId);
        }

        if (allowsPets)
        {
            normalized.Add(PetsId);
        }

        if (isFurnished)
        {
            normalized.Add(FurnishedId);
        }

        return normalized.ToList();
    }

    public static IReadOnlyList<Guid> NormalizeForPropertyForm(IEnumerable<Guid>? amenityIds, CreatePropertyDto dto)
    {
        var normalized = MergeWithPropertyFeatureAmenities(
            amenityIds,
            dto.HasElevator,
            dto.HasAirConditioning,
            dto.HasGarage,
            dto.AllowsPets,
            dto.IsFurnished);

        ApplyPropertyFeatureFlags(dto, normalized);
        return normalized;
    }

    public static void ApplyPropertyFeatureFlags(CreatePropertyDto dto, IEnumerable<Guid>? amenityIds)
    {
        var idSet = amenityIds?.ToHashSet() ?? new HashSet<Guid>();

        dto.HasElevator = idSet.Contains(ElevatorId);
        dto.HasAirConditioning = idSet.Contains(AirConditioningId);
        dto.HasGarage = idSet.Contains(GarageId);
        dto.AllowsPets = idSet.Contains(PetsId);
        dto.IsFurnished = idSet.Contains(FurnishedId);

        if (!dto.IsFurnished)
        {
            dto.FurnishedDescription = null;
        }
    }

    public static void ApplyPropertyFeatureFlags(Property property, IEnumerable<Guid>? amenityIds = null)
    {
        var idSet = (amenityIds ?? property.Amenities.Select(a => a.AmenityId)).ToHashSet();

        property.HasElevator = idSet.Contains(ElevatorId);
        property.HasAirConditioning = idSet.Contains(AirConditioningId);
        property.HasGarage = idSet.Contains(GarageId);
        property.AllowsPets = idSet.Contains(PetsId);
        property.IsFurnished = idSet.Contains(FurnishedId);

        if (!property.IsFurnished)
        {
            property.FurnishedDescription = null;
        }
    }
}