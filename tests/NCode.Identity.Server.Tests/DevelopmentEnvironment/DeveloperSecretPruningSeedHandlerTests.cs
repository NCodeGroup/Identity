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


using System.Security.Cryptography;
using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DeveloperSecretPruningSeedHandlerTests : BaseTests
{
    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAllSecretsUndecryptable_PrunesAllWithoutGeneratingAKey()
    {
        const string tenantId = "tenant-1";

        var brokenA = CreatePersistedSecret("broken-a");
        var brokenB = CreatePersistedSecret("broken-b");
        var tenant = CreateTenant(tenantId, [brokenA, brokenB]);

        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecret(brokenA))
            .Throws(new CryptographicException("payload invalid"))
            .Verifiable();
        secretSerializer
            .Setup(x => x.DeserializeSecret(brokenB))
            .Throws(new CryptographicException("payload invalid"))
            .Verifiable();

        var store = CreateStrictMock<ITenantStore>();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "broken-a", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "broken-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();

        var handler = new DeveloperSecretPruningSeedHandler(secretSerializer.Object);

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        // Key provisioning, which runs after the pruner, generates the replacements.
        Assert.Empty(tenant.Secrets.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenSomeSecretsUndecryptable_PrunesOnlyBrokenAndKeepsUsable()
    {
        const string tenantId = "tenant-1";

        var usable = CreatePersistedSecret("usable");
        var broken = CreatePersistedSecret("broken");
        var tenant = CreateTenant(tenantId, [usable, broken]);

        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecret(usable))
            .Returns(CreateLooseMock<SecretKey>().Object)
            .Verifiable();
        secretSerializer
            .Setup(x => x.DeserializeSecret(broken))
            .Throws(new FormatException("payload invalid"))
            .Verifiable();

        var store = CreateStrictMock<ITenantStore>();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "broken", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();

        var handler = new DeveloperSecretPruningSeedHandler(secretSerializer.Object);

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        var remaining = Assert.Single(tenant.Secrets.Value);
        Assert.Same(usable, remaining);
        Assert.Equal("token", tenant.Secrets.ConcurrencyToken);
    }

    [Fact]
    public async Task HandleAsync_WhenAllSecretsUsable_DoesNotTouchStore()
    {
        const string tenantId = "tenant-1";

        var first = CreatePersistedSecret("first");
        var second = CreatePersistedSecret("second");
        var tenant = CreateTenant(tenantId, [first, second]);
        var originalSecrets = tenant.Secrets;

        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecret(first))
            .Returns(CreateLooseMock<SecretKey>().Object)
            .Verifiable();
        secretSerializer
            .Setup(x => x.DeserializeSecret(second))
            .Returns(CreateLooseMock<SecretKey>().Object)
            .Verifiable();

        // The store is strict with no setups: any call fails the test.
        var store = CreateStrictMock<ITenantStore>();

        var handler = new DeveloperSecretPruningSeedHandler(secretSerializer.Object);

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        Assert.Same(originalSecrets, tenant.Secrets);
    }

    [Fact]
    public async Task HandleAsync_WhenNoSecretsExist_DoesNothing()
    {
        var tenant = CreateTenant("tenant-1", []);
        var originalSecrets = tenant.Secrets;

        // DeserializeSecret is never called for an empty collection; strict mocks with no setups prove it.
        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        var store = CreateStrictMock<ITenantStore>();

        var handler = new DeveloperSecretPruningSeedHandler(secretSerializer.Object);

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        Assert.Same(originalSecrets, tenant.Secrets);
    }

    #endregion

    #region MediatorPriority Property Tests

    [Fact]
    public void MediatorPriority_IsHigh_SoKeyProvisioningRunsAfterThePrune()
    {
        var handler = new DeveloperSecretPruningSeedHandler(
            CreateStrictMock<ISecretSerializer>().Object
        );

        Assert.Equal(DefaultMediatorPriorities.High, handler.MediatorPriority);
    }

    #endregion

    #region Helpers

    private SeedTenantCommand CreateCommand(PersistedTenant tenant, Mock<ITenantStore> store)
    {
        var storeManager = CreateStrictMock<IStoreManager>();
        storeManager.Setup(x => x.GetStore<ITenantStore>()).Returns(store.Object);

        return new SeedTenantCommand(
            new TenantSeedContext
            {
                Tenant = tenant,
                Plane = TenantPlane.Workload,
                StoreManager = storeManager.Object,
            }
        );
    }

    private static PersistedTenant CreateTenant(
        string tenantId,
        IReadOnlyCollection<PersistedSecret> secrets
    ) =>
        new()
        {
            TenantId = tenantId,
            Issuer = null,
            IsDisabled = false,
            DisplayName = "Test Tenant",
            Settings = new PersistedTenantSettings { TenantId = tenantId, Value = default },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = "token",
                Value = secrets,
            },
        };

    private static PersistedSecret CreatePersistedSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = "rsa",
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.Pkcs8,
            KeySizeBits = 2048,
            EncodedValue = "AA",
        };

    #endregion
}
