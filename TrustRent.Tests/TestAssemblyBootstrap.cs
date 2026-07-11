using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using TrustRent.Shared.Security;

namespace TrustRent.Tests;

internal static class TestAssemblyBootstrap
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // 32-byte keys Base64-encoded for EncryptionHelperV2 (AES-256-GCM + HMAC-SHA256)
                ["Encryption:V2:DataKey"] = Convert.ToBase64String(new byte[32]),
                ["Encryption:V2:BlindIndexKey"] = Convert.ToBase64String(new byte[32])
            })
            .Build();

        EncryptionHelperV2.Initialize(configuration);
        IpHashHelper.Initialize(configuration);
    }
}