#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class SecretProtectionRegistrationTests : BaseTests
{
    private sealed class StubHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?> configurationData
    ) => new ConfigurationBuilder().AddInMemoryCollection(configurationData).Build();

    private ServiceCollection AddSecretProtection(
        Dictionary<string, string?> configurationData,
        ServiceCollection? serviceCollection = null
    )
    {
        serviceCollection ??= [];

        var builder = CreateStrictMock<IIdentityServerBuilder>();
        builder.Setup(x => x.ServiceCollection).Returns(serviceCollection).Verifiable();

        SecretProtectionRegistration.AddSecretProtection(
            builder.Object,
            CreateConfiguration(configurationData)
        );

        return serviceCollection;
    }

    private static bool IsWarmup(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationType == typeof(ServerSecretProtectorWarmup);

    private static bool IsKeyRingGate(ServiceDescriptor descriptor) =>
        descriptor.ServiceType == typeof(IHostedService)
        && descriptor.ImplementationType == typeof(DataProtectionKeyRingGate);

    #region AddSecretProtection Tests

    [Fact]
    public void AddSecretProtection_UnknownChild_Throws()
    {
        // The builder is never touched: the strict mock rejects any access, so the check runs before anything is registered.
        var builder = CreateStrictMock<IIdentityServerBuilder>();
        var configuration = CreateConfiguration(
            new Dictionary<string, string?> { ["SecretProtection:Provider"] = "local" }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            SecretProtectionRegistration.AddSecretProtection(builder.Object, configuration)
        );

        Assert.Contains("'SecretProtection:Provider'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SecretProtection:RootKeyProvider")]
    [InlineData("secretprotection:ROOTKEYPROVIDER")]
    [InlineData("SecretProtection:recovery:Disabled")]
    [InlineData("SecretProtection:ROOTKEYPROVIDERS:Local:KeyPath")]
    public void AddSecretProtection_KnownChildInAnyCase_DoesNotThrow(string key)
    {
        var serviceCollection = AddSecretProtection(
            new Dictionary<string, string?> { [key] = "true" }
        );

        Assert.Single(serviceCollection, IsWarmup);
    }

    [Fact]
    public void AddSecretProtection_BindsRootKeyProviderAndRecovery()
    {
        var serviceCollection = AddSecretProtection(
            new Dictionary<string, string?>
            {
                ["SecretProtection:RootKeyProvider"] = "aws-kms",
                ["SecretProtection:Recovery:PublicKeyPath"] = "/recovery/recovery-public.pem",
                ["SecretProtection:Recovery:Disabled"] = "false",
            }
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<SecretProtectionOptions>>().Value;

        Assert.Equal("aws-kms", options.RootKeyProvider);
        Assert.Equal("/recovery/recovery-public.pem", options.Recovery.PublicKeyPath);
        Assert.False(options.Recovery.Disabled);
    }

    [Fact]
    public void AddSecretProtection_WhenRecoveryHasUnknownKey_Throws()
    {
        var serviceCollection = AddSecretProtection(
            new Dictionary<string, string?> { ["SecretProtection:Recovery:Path"] = "/recovery" }
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<SecretProtectionOptions>>().Value
        );

        Assert.Contains("'Path'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSecretProtection_BindsLocalOptions()
    {
        var serviceCollection = AddSecretProtection(
            new Dictionary<string, string?>
            {
                ["SecretProtection:RootKeyProviders:Local:KeyPath"] = "/root-key/key",
                ["SecretProtection:RootKeyProviders:Local:Key"] = "inline-key",
                ["SecretProtection:RootKeyProviders:Local:PreviousKeyPaths:0"] = "/root-key/old-1",
                ["SecretProtection:RootKeyProviders:Local:PreviousKeyPaths:1"] = "/root-key/old-2",
            }
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<LocalKeyWrapperOptions>>().Value;

        Assert.Equal("/root-key/key", options.KeyPath);
        Assert.Equal("inline-key", options.Key);
        Assert.Equal(["/root-key/old-1", "/root-key/old-2"], options.PreviousKeyPaths);
    }

    [Fact]
    public void AddSecretProtection_WhenLocalHasUnknownKey_Throws()
    {
        var serviceCollection = AddSecretProtection(
            new Dictionary<string, string?>
            {
                ["SecretProtection:RootKeyProviders:Local:Path"] = "/root-key/key",
            }
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<LocalKeyWrapperOptions>>().Value
        );

        Assert.Contains("'Path'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSecretProtection_BindsTheRevokedKeyRingKeyIds()
    {
        var firstKeyId = Guid.Parse("6d1f7c0e-0c62-4d6e-9a71-1d6a2f3d9b11");
        var secondKeyId = Guid.Parse("0b9c2b5e-3f4d-4f6a-8c7e-5a1d2e3f4a5b");
        var serviceCollection = AddSecretProtection(
            new Dictionary<string, string?>
            {
                ["DataProtectionKeyRing:RevokedKeyIds:0"] = firstKeyId.ToString(),
                ["DataProtectionKeyRing:RevokedKeyIds:1"] = secondKeyId.ToString(),
            }
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = serviceProvider
            .GetRequiredService<IOptions<DataProtectionKeyRingOptions>>()
            .Value;

        Assert.Equal([firstKeyId, secondKeyId], options.RevokedKeyIds);
    }

    [Fact]
    public void AddSecretProtection_WhenTheKeyRingSectionHasAnUnknownKey_Throws()
    {
        var serviceCollection = AddSecretProtection(
            new Dictionary<string, string?>
            {
                ["DataProtectionKeyRing:RevokedKeys:0"] = "6d1f7c0e-0c62-4d6e-9a71-1d6a2f3d9b11",
            }
        );
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<DataProtectionKeyRingOptions>>().Value
        );

        Assert.Contains("'RevokedKeys'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSecretProtection_InsertsTheWarmupThenTheKeyRingGateFirst()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IHostedService, StubHostedService>();

        AddSecretProtection([], serviceCollection);

        Assert.True(IsWarmup(serviceCollection[0]));
        Assert.True(IsKeyRingGate(serviceCollection[1]));
        Assert.Equal(typeof(StubHostedService), serviceCollection[2].ImplementationType);
    }

    [Fact]
    public void AddSecretProtection_CalledTwice_RegistersWarmupOnce()
    {
        var serviceCollection = AddSecretProtection([]);
        AddSecretProtection([], serviceCollection);

        Assert.Single(serviceCollection, IsWarmup);
        Assert.Single(serviceCollection, IsKeyRingGate);
        Assert.Single(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(ServerSecretProtectorHolder)
        );
    }

    [Fact]
    public void AddSecretProtection_SetsXmlEncryptor()
    {
        var serviceCollection = AddSecretProtection([]);
        using var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;

        Assert.IsType<MasterKeyXmlEncryptor>(options.XmlEncryptor);
    }

    #endregion
}
