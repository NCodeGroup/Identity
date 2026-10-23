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
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
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

        var protector = CreateStrictMock<ISecretProtector>();
        var protectorProvider = CreateProtectorProvider(tenantId, [brokenA, brokenB], protector);

        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecret(protector.Object, brokenA))
            .Throws(new CryptographicException("payload invalid"))
            .Verifiable();
        secretSerializer
            .Setup(x => x.DeserializeSecret(protector.Object, brokenB))
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

        // The master keys are readable, so the factory and the key store are never used: strict mock with no setups.
        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();

        var handler = new DeveloperSecretPruningSeedHandler(
            secretSerializer.Object,
            protectorProvider.Object,
            storeManagerFactory.Object
        );

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        // Key provisioning, which runs after the pruner, generates the replacements.
        Assert.Empty(tenant.Secrets.Value);
    }

    [Fact]
    public async Task Handle_UnreadableEnvelope_RemovesSecret()
    {
        const string tenantId = "tenant-1";

        var usable = CreatePersistedSecret("usable");
        var broken = CreatePersistedSecret("broken");
        var tenant = CreateTenant(tenantId, [usable, broken]);

        var protector = CreateStrictMock<ISecretProtector>();
        var protectorProvider = CreateProtectorProvider(tenantId, [usable, broken], protector);

        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecret(protector.Object, usable))
            .Returns(CreateLooseMock<SecretKey>().Object)
            .Verifiable();
        secretSerializer
            .Setup(x => x.DeserializeSecret(protector.Object, broken))
            .Throws(new FormatException("payload invalid"))
            .Verifiable();

        var store = CreateStrictMock<ITenantStore>();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "broken", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();

        // The master keys are readable, so the factory and the key store are never used: strict mock with no setups.
        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();

        var handler = new DeveloperSecretPruningSeedHandler(
            secretSerializer.Object,
            protectorProvider.Object,
            storeManagerFactory.Object
        );

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        var remaining = Assert.Single(tenant.Secrets.Value);
        Assert.Same(usable, remaining);
        Assert.Equal("token", tenant.Secrets.ConcurrencyToken);
    }

    [Fact]
    public async Task Handle_MasterKeyUnreadable_RemovesMasterKeysAndPrivateSecrets()
    {
        const string tenantId = "tenant-1";

        var owner = SecretOwner.ForTenant(tenantId);
        var privateA = CreatePersistedSecret("private-a");
        var privateB = CreatePersistedSecret("private-b", SecretEncodings.SymmetricRaw);
        var publicKey = CreatePersistedSecret("public", SecretEncodings.Spki);
        var tenant = CreateTenant(tenantId, [privateA, publicKey, privateB]);

        var calls = new List<string>();

        // Only the private secrets are passed to the provider, which cannot unwrap the master keys.
        var protectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        protectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    owner,
                    It.Is<IReadOnlyCollection<string>>(values => values.Count == 2),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new CryptographicException("master key invalid"))
            .Verifiable();
        protectorProvider
            .Setup(x => x.Evict(owner))
            .Callback(() => calls.Add("evict"))
            .Verifiable();

        var masterKeyStore = CreateStrictMock<IMasterKeyStore>();
        masterKeyStore
            .Setup(x => x.RemoveAllAsync(owner, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("remove"))
            .Returns(ValueTask.FromResult(1))
            .Verifiable();

        var keyStoreManager = CreateStrictMock<IStoreManager>();
        keyStoreManager
            .Setup(x => x.GetStore<IMasterKeyStore>())
            .Returns(masterKeyStore.Object)
            .Verifiable();
        keyStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("save"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        keyStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(keyStoreManager.Object))
            .Verifiable();

        // Nothing is deserialized: strict mock with no setups.
        var secretSerializer = CreateStrictMock<ISecretSerializer>();

        var store = CreateStrictMock<ITenantStore>();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "private-a", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "private-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();

        var handler = new DeveloperSecretPruningSeedHandler(
            secretSerializer.Object,
            protectorProvider.Object,
            storeManagerFactory.Object
        );

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        // The deletion is committed before the key ring forgets the master keys, so its next load sees the deletion.
        Assert.Equal(["remove", "save", "evict"], calls);

        var remaining = Assert.Single(tenant.Secrets.Value);
        Assert.Same(publicKey, remaining);
    }

    [Fact]
    public async Task Handle_AllReadable_RemovesNothing()
    {
        const string tenantId = "tenant-1";

        var first = CreatePersistedSecret("first");
        var second = CreatePersistedSecret("second");
        var tenant = CreateTenant(tenantId, [first, second]);
        var originalSecrets = tenant.Secrets;

        var protector = CreateStrictMock<ISecretProtector>();
        var protectorProvider = CreateProtectorProvider(tenantId, [first, second], protector);

        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        secretSerializer
            .Setup(x => x.DeserializeSecret(protector.Object, first))
            .Returns(CreateLooseMock<SecretKey>().Object)
            .Verifiable();
        secretSerializer
            .Setup(x => x.DeserializeSecret(protector.Object, second))
            .Returns(CreateLooseMock<SecretKey>().Object)
            .Verifiable();

        // The tenant store and the store manager factory are strict with no setups: any call fails the test.
        var store = CreateStrictMock<ITenantStore>();
        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();

        var handler = new DeveloperSecretPruningSeedHandler(
            secretSerializer.Object,
            protectorProvider.Object,
            storeManagerFactory.Object
        );

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        Assert.Same(originalSecrets, tenant.Secrets);
    }

    [Fact]
    public async Task HandleAsync_WhenNoSecretsExist_DoesNothing()
    {
        const string tenantId = "tenant-1";

        var tenant = CreateTenant(tenantId, []);
        var originalSecrets = tenant.Secrets;

        var protector = CreateStrictMock<ISecretProtector>();
        var protectorProvider = CreateProtectorProvider(tenantId, [], protector);

        // DeserializeSecret is never called for an empty collection; strict mocks with no setups prove it.
        var secretSerializer = CreateStrictMock<ISecretSerializer>();
        var store = CreateStrictMock<ITenantStore>();
        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();

        var handler = new DeveloperSecretPruningSeedHandler(
            secretSerializer.Object,
            protectorProvider.Object,
            storeManagerFactory.Object
        );

        await handler.HandleAsync(CreateCommand(tenant, store), CancellationToken.None);

        Assert.Same(originalSecrets, tenant.Secrets);
    }

    #endregion

    #region MediatorPriority Property Tests

    [Fact]
    public void MediatorPriority_IsHigh_SoKeyProvisioningRunsAfterThePrune()
    {
        var handler = new DeveloperSecretPruningSeedHandler(
            CreateStrictMock<ISecretSerializer>().Object,
            CreateStrictMock<ISecretProtectorProvider>().Object,
            CreateStrictMock<IStoreManagerFactory>().Object
        );

        Assert.Equal(DefaultMediatorPriorities.High, handler.MediatorPriority);
    }

    #endregion

    #region Helpers

    // Sets up the provider to hand out the protector for the private secrets of the tenant, which is the only part of
    // the secrets that the provider is asked about.
    private Mock<ISecretProtectorProvider> CreateProtectorProvider(
        string tenantId,
        IReadOnlyCollection<PersistedSecret> secrets,
        Mock<ISecretProtector> protector
    )
    {
        var owner = SecretOwner.ForTenant(tenantId);
        List<string> protectedValues =
        [
            .. secrets
                .Where(secret => !SecretEncodings.IsPublic(secret.SecretEncoding))
                .Select(secret => secret.EncodedValue),
        ];

        var protectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        protectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    owner,
                    It.Is<IReadOnlyCollection<string>>(values =>
                        values.SequenceEqual(protectedValues)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.FromResult(protector.Object))
            .Verifiable();

        return protectorProvider;
    }

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

    private static PersistedSecret CreatePersistedSecret(
        string secretId,
        string secretEncoding = SecretEncodings.Pkcs8
    ) =>
        new()
        {
            SecretId = secretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = "rsa",
            SecretEncoding = secretEncoding,
            KeySizeBits = 2048,
            EncodedValue = "AA",
        };

    #endregion
}
