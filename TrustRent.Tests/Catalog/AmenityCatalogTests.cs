using TrustRent.Modules.Catalog;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Tests.Catalog;

public class AmenityCatalogTests
{
    [Fact]
    public void NormalizeForPropertyForm_MergesFeatureFlagsIntoAmenities()
    {
        var dto = new CreatePropertyDto
        {
            HasElevator = true,
            HasGarage = true,
            AllowsPets = true,
            IsFurnished = true,
            FurnishedDescription = "Cozinha equipada"
        };

        var amenityIds = AmenityCatalog.NormalizeForPropertyForm(new[] { AmenityCatalog.WifiId }, dto);

        Assert.Contains(AmenityCatalog.WifiId, amenityIds);
        Assert.Contains(AmenityCatalog.ElevatorId, amenityIds);
        Assert.Contains(AmenityCatalog.GarageId, amenityIds);
        Assert.Contains(AmenityCatalog.PetsId, amenityIds);
        Assert.Contains(AmenityCatalog.FurnishedId, amenityIds);
        Assert.True(dto.HasElevator);
        Assert.True(dto.HasGarage);
        Assert.True(dto.AllowsPets);
        Assert.True(dto.IsFurnished);
        Assert.Equal("Cozinha equipada", dto.FurnishedDescription);
    }

    [Fact]
    public void NormalizeForPropertyForm_SyncsFlagsFromAmenities()
    {
        var dto = new CreatePropertyDto
        {
            HasElevator = false,
            HasAirConditioning = false,
            HasGarage = false,
            AllowsPets = false,
            IsFurnished = false
        };

        var amenityIds = AmenityCatalog.NormalizeForPropertyForm(new[]
        {
            AmenityCatalog.AirConditioningId,
            AmenityCatalog.ElevatorId,
            AmenityCatalog.GarageId,
            AmenityCatalog.PetsId,
            AmenityCatalog.FurnishedId
        }, dto);

        Assert.True(dto.HasElevator);
        Assert.True(dto.HasAirConditioning);
        Assert.True(dto.HasGarage);
        Assert.True(dto.AllowsPets);
        Assert.True(dto.IsFurnished);
        Assert.Equal(5, amenityIds.Count);
    }

    [Fact]
    public void ApplyPropertyFeatureFlags_ClearsFurnishedDescription_WhenFurnishedAmenityMissing()
    {
        var property = new Property
        {
            FurnishedDescription = "Mesa e sofá"
        };

        AmenityCatalog.ApplyPropertyFeatureFlags(property, new[] { AmenityCatalog.WifiId });

        Assert.False(property.IsFurnished);
        Assert.Null(property.FurnishedDescription);
    }
}