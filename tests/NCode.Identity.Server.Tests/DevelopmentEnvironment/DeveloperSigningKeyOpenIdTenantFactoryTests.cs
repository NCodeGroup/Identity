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
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.Logic;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Logic;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DeveloperSigningKeyOpenIdTenantFactoryTests : BaseTests
{
    #region EnsureSigningKeyAsync Tests

    [Fact]
    public async Task EnsureSigningKeyAsync_WhenAllSecretsUndecryptable_PrunesAllAndSeedsNewKey()
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

        var generatedSecret = CreatePersistedSecret("new-key");
        var secretGenerator = CreateStrictMock<ISecretGenerator>();
        secretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
            .Returns(generatedSecret)
            .Verifiable();

        var cryptoService = CreateStrictMock<ICryptoService>();
        cryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<BinaryEncodingType>()))
            .Returns("new-key")
            .Verifiable();

        var refreshedSecrets = new PersistedTenantSecrets
        {
            TenantId = tenantId,
            Value = [generatedSecret],
        };

        var store = CreateStrictMock<ITenantStore>();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "broken-a", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "broken-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        store
            .Setup(x => x.AddSecretAsync(tenantId, generatedSecret, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        store
            .Setup(x => x.GetSecretsAsync(tenantId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(refreshedSecrets))
            .Verifiable();

        var storeManager = CreateStoreManager(store);
        var storeManagerFactory = CreateStoreManagerFactory(storeManager);

        var factory = CreateFactory(
            storeManagerFactory,
            secretSerializer,
            secretGenerator,
            cryptoService
        );

        await factory.EnsureSigningKeyAsync(tenant, CancellationToken.None);

        Assert.Same(refreshedSecrets, tenant.Secrets);
    }

    [Fact]
    public async Task EnsureSigningKeyAsync_WhenSomeSecretsUndecryptable_PrunesOnlyBrokenAndKeepsUsable()
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
            .Throws(new CryptographicException("payload invalid"))
            .Verifiable();

        var refreshedSecrets = new PersistedTenantSecrets { TenantId = tenantId, Value = [usable] };

        var store = CreateStrictMock<ITenantStore>();
        store
            .Setup(x => x.RemoveSecretAsync(tenantId, "broken", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(true))
            .Verifiable();
        store
            .Setup(x => x.GetSecretsAsync(tenantId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(refreshedSecrets))
            .Verifiable();

        var storeManager = CreateStoreManager(store);
        var storeManagerFactory = CreateStoreManagerFactory(storeManager);

        // The seed path must not run when a usable key survives: these strict mocks have no setups,
        // so any call to them fails the test.
        var secretGenerator = CreateStrictMock<ISecretGenerator>();
        var cryptoService = CreateStrictMock<ICryptoService>();

        var factory = CreateFactory(
            storeManagerFactory,
            secretSerializer,
            secretGenerator,
            cryptoService
        );

        await factory.EnsureSigningKeyAsync(tenant, CancellationToken.None);

        Assert.Same(refreshedSecrets, tenant.Secrets);
    }

    [Fact]
    public async Task EnsureSigningKeyAsync_WhenAllSecretsUsable_DoesNotTouchStore()
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

        // All store and seed collaborators are strict with no setups: any call fails the test.
        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        var secretGenerator = CreateStrictMock<ISecretGenerator>();
        var cryptoService = CreateStrictMock<ICryptoService>();

        var factory = CreateFactory(
            storeManagerFactory,
            secretSerializer,
            secretGenerator,
            cryptoService
        );

        await factory.EnsureSigningKeyAsync(tenant, CancellationToken.None);

        Assert.Same(originalSecrets, tenant.Secrets);
    }

    [Fact]
    public async Task EnsureSigningKeyAsync_WhenNoSecretsExist_SeedsNewKey()
    {
        const string tenantId = "tenant-1";

        var tenant = CreateTenant(tenantId, []);

        // DeserializeSecret is never called for an empty collection; a strict mock with no setups proves it.
        var secretSerializer = CreateStrictMock<ISecretSerializer>();

        var generatedSecret = CreatePersistedSecret("new-key");
        var secretGenerator = CreateStrictMock<ISecretGenerator>();
        secretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
            .Returns(generatedSecret)
            .Verifiable();

        var cryptoService = CreateStrictMock<ICryptoService>();
        cryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<BinaryEncodingType>()))
            .Returns("new-key")
            .Verifiable();

        var refreshedSecrets = new PersistedTenantSecrets
        {
            TenantId = tenantId,
            Value = [generatedSecret],
        };

        var store = CreateStrictMock<ITenantStore>();
        store
            .Setup(x => x.AddSecretAsync(tenantId, generatedSecret, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        store
            .Setup(x => x.GetSecretsAsync(tenantId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(refreshedSecrets))
            .Verifiable();

        var storeManager = CreateStoreManager(store);
        var storeManagerFactory = CreateStoreManagerFactory(storeManager);

        var factory = CreateFactory(
            storeManagerFactory,
            secretSerializer,
            secretGenerator,
            cryptoService
        );

        await factory.EnsureSigningKeyAsync(tenant, CancellationToken.None);

        Assert.Same(refreshedSecrets, tenant.Secrets);
    }

    #endregion

    #region Helpers

    private Mock<IStoreManager> CreateStoreManager(Mock<ITenantStore> store)
    {
        var storeManager = CreateStrictMock<IStoreManager>();
        storeManager.Setup(x => x.GetStore<ITenantStore>()).Returns(store.Object).Verifiable();
        storeManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        return storeManager;
    }

    private Mock<IStoreManagerFactory> CreateStoreManagerFactory(Mock<IStoreManager> storeManager)
    {
        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(storeManager.Object))
            .Verifiable();
        return storeManagerFactory;
    }

    private DeveloperSigningKeyOpenIdTenantFactory CreateFactory(
        Mock<IStoreManagerFactory> storeManagerFactory,
        Mock<ISecretSerializer> secretSerializer,
        Mock<ISecretGenerator> secretGenerator,
        Mock<ICryptoService> cryptoService
    ) =>
        new(
            CreateStrictMock<ITenantResolver>().Object,
            CreateStrictMock<TemplateBinderFactory>().Object,
            Options.Create(new OpenIdOptions()),
            storeManagerFactory.Object,
            CreateStrictMock<IOpenIdTenantCache>().Object,
            CreateStrictMock<IReadOnlySettingCollectionProviderFactory>().Object,
            CreateStrictMock<ISettingSerializer>().Object,
            secretSerializer.Object,
            CreateStrictMock<ISecretKeyCollectionProviderFactory>().Object,
            CreateStrictMock<ICollectionDataSourceFactory>().Object,
            secretGenerator.Object,
            cryptoService.Object,
            TimeProvider.System
        );

    private static PersistedTenant CreateTenant(
        string tenantId,
        IReadOnlyCollection<PersistedSecret> secrets
    ) =>
        new()
        {
            TenantId = tenantId,
            DomainName = null,
            IsDisabled = false,
            DisplayName = "Test Tenant",
            Settings = new PersistedTenantSettings { TenantId = tenantId, Value = default },
            Secrets = new PersistedTenantSecrets { TenantId = tenantId, Value = secrets },
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
            KeySizeBits = 2048,
            EncodedValue = "AA",
        };

    #endregion
}
