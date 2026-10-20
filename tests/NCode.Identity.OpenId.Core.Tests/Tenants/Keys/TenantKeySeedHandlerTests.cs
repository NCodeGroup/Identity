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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Mediator;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeySeedHandlerTests : BaseTests
{
    private const string TenantId = "acme";

    private static TenantKeySpecification Rs256 { get; } =
        new()
        {
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
        };

    private static TenantKeySpecification Es256 { get; } =
        new()
        {
            Algorithm = "ES256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
        };

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<ITenantKeyPolicy> MockPolicy { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private TenantKeySeedHandler Handler { get; }

    public TenantKeySeedHandlerTests()
    {
        MockPolicy = CreateStrictMock<ITenantKeyPolicy>();
        MockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantStore = CreateStrictMock<ITenantStore>();

        Handler = new TenantKeySeedHandler(
            Options.Create(OpenIdOptions),
            MockPolicy.Object,
            MockSecretGenerator.Object,
            MockCryptoService.Object,
            TimeProvider.System,
            CreateLooseMock<ILogger<TenantKeySeedHandler>>().Object
        );
    }

    private OpenIdTenantKeyProvisioningOptions KeyProvisioning =>
        OpenIdOptions.Tenant.KeyProvisioning;

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenProvisioningDisabled_LeavesTheTenantUntouched()
    {
        KeyProvisioning.Enabled = false;
        var tenant = CreateTenant([]);
        var original = tenant.Secrets;

        await Handler.HandleAsync(CreateCommand(tenant), CancellationToken.None);

        Assert.Same(original, tenant.Secrets);
    }

    [Fact]
    public async Task HandleAsync_WhenTenantHoldsNoKeys_GeneratesAndStoresOnePerSpecification()
    {
        var tenant = CreateTenant([]);
        SetupSpecifications(Rs256, Es256);
        var requests = SetupGeneration();
        var added = SetupAddSecret();

        await Handler.HandleAsync(CreateCommand(tenant), CancellationToken.None);

        Assert.Collection(
            requests,
            request => AssertRequest(Rs256, request),
            request => AssertRequest(Es256, request)
        );
        Assert.Equal(added, tenant.Secrets.Value);
        Assert.Equal(TenantId, tenant.Secrets.TenantId);
        Assert.Equal("token", tenant.Secrets.ConcurrencyToken);
    }

    [Fact]
    public async Task HandleAsync_WhenAHeldKeySatisfiesASpecification_GeneratesOnlyTheMissingKeys()
    {
        var held = CreateSecret("held");
        var tenant = CreateTenant([held]);
        SetupSpecifications(Rs256, Es256);
        MockPolicy.Setup(x => x.IsSatisfiedBy(Rs256, held)).Returns(true).Verifiable();
        MockPolicy.Setup(x => x.IsSatisfiedBy(Es256, held)).Returns(false).Verifiable();
        var requests = SetupGeneration();
        var added = SetupAddSecret();

        await Handler.HandleAsync(CreateCommand(tenant), CancellationToken.None);

        var request = Assert.Single(requests);
        AssertRequest(Es256, request);
        var generated = Assert.Single(added);
        Assert.Equal([held, generated], tenant.Secrets.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenEveryKeyIsHeld_LeavesTheTenantUntouched()
    {
        // The store and the generator are strict with no setups: generating, adding, or removing a key fails the test.
        var held = CreateSecret("held");
        var tenant = CreateTenant([held]);
        var original = tenant.Secrets;
        SetupSpecifications(Rs256);
        MockPolicy.Setup(x => x.IsSatisfiedBy(Rs256, held)).Returns(true).Verifiable();

        await Handler.HandleAsync(CreateCommand(tenant), CancellationToken.None);

        Assert.Same(original, tenant.Secrets);
    }

    [Fact]
    public async Task HandleAsync_WhenOptionsCannotBeHonored_Throws()
    {
        MockPolicy
            .Setup(x => x.ResolveSpecifications(KeyProvisioning))
            .Returns(
                new TenantKeySpecificationResolution
                {
                    Specifications = [],
                    Errors = ["The algorithm 'XX999' is not registered."],
                }
            )
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(CreateCommand(CreateTenant([])), CancellationToken.None)
        );

        Assert.Contains("'XX999'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region MediatorPriority Property Tests

    [Fact]
    public void MediatorPriority_IsLow_SoContributorsThatHealSecretsRunFirst()
    {
        Assert.Equal(DefaultMediatorPriorities.Low, Handler.MediatorPriority);
    }

    #endregion

    #region Helpers

    private static void AssertRequest(
        TenantKeySpecification specification,
        GenerateSecretRequest request
    )
    {
        Assert.Equal(specification.SecretType, request.SecretType);
        Assert.Equal(specification.KeySizeBits, request.KeySizeBits);
        Assert.Equal(specification.Use, request.Use);
        Assert.Equal(specification.Algorithm, request.Algorithm);
        Assert.Null(request.Curve);
        Assert.Equal(request.CreatedWhen.AddYears(100), request.ExpiresWhen);
    }

    private void SetupSpecifications(params TenantKeySpecification[] specifications) =>
        MockPolicy
            .Setup(x => x.ResolveSpecifications(KeyProvisioning))
            .Returns(
                new TenantKeySpecificationResolution
                {
                    Specifications = [.. specifications],
                    Errors = [],
                }
            )
            .Verifiable();

    private List<GenerateSecretRequest> SetupGeneration()
    {
        var count = 0;
        MockCryptoService
            .Setup(x => x.GenerateKey(It.IsAny<int>(), It.IsAny<BinaryEncodingType>()))
            .Returns(() => $"generated-{++count}")
            .Verifiable();

        var requests = new List<GenerateSecretRequest>();
        MockSecretGenerator
            .Setup(x => x.GenerateSecret(It.IsAny<GenerateSecretRequest>()))
            .Returns(
                (GenerateSecretRequest request) =>
                {
                    requests.Add(request);
                    return new GeneratedSecret
                    {
                        Secret = CreateSecret(request.SecretId),
                        SecretMaterial = null,
                    };
                }
            )
            .Verifiable();
        return requests;
    }

    private List<PersistedSecret> SetupAddSecret()
    {
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();

        var added = new List<PersistedSecret>();
        MockTenantStore
            .Setup(x =>
                x.AddSecretAsync(
                    TenantId,
                    It.IsAny<PersistedSecret>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((string _, PersistedSecret secret, CancellationToken _) => added.Add(secret))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        return added;
    }

    private SeedTenantCommand CreateCommand(PersistedTenant tenant) =>
        new(
            new TenantSeedContext
            {
                Tenant = tenant,
                Plane = TenantPlane.Workload,
                StoreManager = MockStoreManager.Object,
            }
        );

    private static PersistedTenant CreateTenant(IReadOnlyCollection<PersistedSecret> secrets) =>
        new()
        {
            TenantId = TenantId,
            Issuer = "https://op.example/acme",
            ConcurrencyToken = "token",
            IsDisabled = false,
            DisplayName = TenantId,
            Settings = new PersistedTenantSettings { TenantId = TenantId, Value = default },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = TenantId,
                ConcurrencyToken = "token",
                Value = secrets,
            },
        };

    private static PersistedSecret CreateSecret(string secretId) =>
        new()
        {
            SecretId = secretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(1),
            SecretType = SecretKeyTypes.Rsa,
            SecretEncoding = SecretEncodings.Pkcs8,
            KeySizeBits = 3072,
            EncodedValue = "AA",
        };

    #endregion
}
