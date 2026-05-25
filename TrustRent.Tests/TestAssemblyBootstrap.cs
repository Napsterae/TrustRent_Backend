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
                ["Encryption:Key"] = "12345678901234567890123456789012",
                ["Encryption:IV"] = "1234567890123456"
            })
            .Build();

        EncryptionHelper.Initialize(configuration);
    }
}