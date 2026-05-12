using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using TrustRent.Modules.Admin.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public static class StagingSimulationPolicy
{
    public static async Task<bool> IsEnabledAsync(IWebHostEnvironment env, IStagingAccessService stagingAccessService, CancellationToken ct = default)
    {
        if (env.IsDevelopment()) return true;
        if (!env.IsStaging()) return false;
        return await stagingAccessService.GetSimulationOverrideAsync(ct) ?? true;
    }
}