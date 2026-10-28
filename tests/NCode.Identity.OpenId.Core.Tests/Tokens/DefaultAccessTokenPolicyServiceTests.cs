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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens;

// The policy is exercised over the real OpenID setting descriptors and scopes, the real setting collection (whose
// Combine is the unit's core), and real JOSE keys and credential selection, so the assertions observe the algorithms
// and keys an access token would actually be protected with.
public sealed class DefaultAccessTokenPolicyServiceTests : BaseTests, IAsyncLifetime
{
    private const string Ps256 = AlgorithmCodes.DigitalSignature.RsaSsaPssSha256;
    private const string Rs256 = AlgorithmCodes.DigitalSignature.RsaSha256;
    private const string Es256 = AlgorithmCodes.DigitalSignature.EcdsaSha256;
    private const string RsaOaep256 = AlgorithmCodes.KeyManagement.RsaOaep256;
    private const string RsaOaep = AlgorithmCodes.KeyManagement.RsaOaep;
    private const string EcdhEs = AlgorithmCodes.KeyManagement.EcdhEs;
    private const string A128CbcHs256 = AlgorithmCodes.AuthenticatedEncryption.Aes128CbcHmacSha256;
    private const string A256Gcm = AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm;
    private const string Jkt = OpenIdConstants.ConfirmationMethods.JwkThumbprint;
    private const string X5tS256 = OpenIdConstants
        .ConfirmationMethods
        .X509CertificateSha256Thumbprint;

    private static TokenConfirmation DPoPBound { get; } =
        new() { JwkThumbprint = "jkt-thumbprint" };

    private static TokenConfirmation CertificateBound { get; } =
        new() { X509CertificateSha256Thumbprint = "x5t-thumbprint" };

    private readonly ServiceProvider _serviceProvider;
    private readonly RSA _tenantRsa = RSA.Create(2048);
    private readonly RSA _resourceServerRsa = RSA.Create(2048);

    public DefaultAccessTokenPolicyServiceTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        var algorithmCollectionProvider =
            _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>();
        var nullChangeToken = CreateStrictMock<INullChangeToken>().Object;

        Descriptors = new DefaultSettingDescriptorCollection(
            new DefaultSettingDescriptorDataSource(
                nullChangeToken,
                algorithmCollectionProvider,
                [],
                []
            ).Collection
        );

        var mockDescriptorProvider = CreateLooseMock<ISettingDescriptorCollectionProvider>();
        mockDescriptorProvider.SetupGet(x => x.Collection).Returns(Descriptors);
        DescriptorProvider = mockDescriptorProvider.Object;

        var mockScopeProvider = CreateLooseMock<ISettingScopeCollectionProvider>();
        mockScopeProvider
            .SetupGet(x => x.Collection)
            .Returns(
                new DefaultSettingScopeCollection(
                    new DefaultSettingScopeDataSource(nullChangeToken).Collection
                )
            );

        var secretKeyFactory = _serviceProvider.GetRequiredService<ISecretKeyFactory>();
        TenantKey = secretKeyFactory.CreateRsa(new KeyMetadata { KeyId = "tenant" }, _tenantRsa);
        ResourceServerKey = secretKeyFactory.CreateRsaPublicKey(
            new KeyMetadata { KeyId = "resource-server", Use = SecretKeyUses.Encryption },
            _resourceServerRsa
        );

        Service = new DefaultAccessTokenPolicyService(
            algorithmCollectionProvider,
            _serviceProvider.GetRequiredService<ICredentialSelector>(),
            mockScopeProvider.Object
        );
    }

    private ISettingDescriptorCollection Descriptors { get; }
    private ISettingDescriptorCollectionProvider DescriptorProvider { get; }
    private SecretKey TenantKey { get; }
    private SecretKey ResourceServerKey { get; }
    private string? CreatedErrorCode { get; set; }
    private DefaultAccessTokenPolicyService Service { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        _tenantRsa.Dispose();
        _resourceServerRsa.Dispose();
    }

    #region Scaffolding

    // The values the server's root settings give every scope, in the server's order; each scope's effective settings
    // descend from them.
    private static Dictionary<string, object> RootValues() =>
        new(StringComparer.Ordinal)
        {
            [OpenIdSettingNames.AccessTokenSigningAlgValuesSupported] = new[]
            {
                Ps256,
                Rs256,
                Es256,
            },
            [OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported] = new[]
            {
                RsaOaep256,
                EcdhEs,
            },
            [OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported] = new[]
            {
                A128CbcHs256,
                A256Gcm,
            },
            [OpenIdSettingNames.AccessTokenLifetime] = TimeSpan.FromMinutes(5),
            [OpenIdSettingNames.AccessTokenType] = JoseTokenTypes.AccessTokenJwt,
        };

    private IReadOnlySettingCollection CreateEffectiveSettings(
        IReadOnlyDictionary<string, object>? overrides
    )
    {
        var values = RootValues();
        foreach (var (name, value) in overrides ?? new Dictionary<string, object>())
        {
            values[name] = value;
        }

        var settings = values.Select(pair =>
            Descriptors.TryGet(pair.Key, out var descriptor)
                ? descriptor.Create(pair.Value)
                : throw new InvalidOperationException($"'{pair.Key}' is not registered.")
        );

        return new DefaultSettingCollection(DescriptorProvider, settings);
    }

    private ISecretKeyCollection CreateKeys(params SecretKey[] keys) =>
        _serviceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create(keys);

    private SecretKey CreateTenantEncryptionKey(ECAlgorithm key) =>
        _serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateEcc(new KeyMetadata { KeyId = "tenant-ec" }, key);

    private OpenIdContext CreateContext(params SecretKey[] tenantKeys)
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSecretsProvider = CreateLooseMock<ISecretKeyCollectionProvider>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();

        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object);
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        mockTenant.SetupGet(x => x.SecretsProvider).Returns(mockSecretsProvider.Object);
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(CreateKeys(tenantKeys));
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => CreatedErrorCode = code)
            .Returns(mockError.Object);

        return mockContext.Object;
    }

    private OpenIdContext CreateContext() => CreateContext(TenantKey);

    private OpenIdClient CreateClient(IReadOnlyDictionary<string, object>? overrides = null)
    {
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient
            .SetupGet(x => x.Settings)
            .Returns(CreateEffectiveSettings(overrides))
            .Verifiable();
        return mockClient.Object;
    }

    private ResolvedResourceServer CreateResourceServer(
        string resourceServerId,
        IReadOnlyDictionary<string, object>? overrides = null,
        params SecretKey[] keys
    ) => CreateResourceServer(resourceServerId, isSystem: false, overrides, keys);

    private ResolvedResourceServer CreateResourceServer(
        string resourceServerId,
        bool isSystem,
        IReadOnlyDictionary<string, object>? overrides,
        SecretKey[] keys
    )
    {
        var mockSecretsProvider = CreateLooseMock<ISecretKeyCollectionProvider>();
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(CreateKeys(keys));

        return new ResolvedResourceServer
        {
            ResourceServerId = resourceServerId,
            Identifier = $"urn:{resourceServerId}",
            Scopes = [],
            IsSystem = isSystem,
            EffectiveSettings = CreateEffectiveSettings(overrides),
            SecretsProvider = mockSecretsProvider.Object,
        };
    }

    private static ResourceServerCredentialResolution Targeting(
        params ResolvedResourceServer[] resourceServers
    ) => new() { ResourceServers = resourceServers };

    private AccessTokenPolicy Evaluate(
        OpenIdClient client,
        ResourceServerCredentialResolution resolution,
        TokenConfirmation confirmation = default
    ) => Service.Evaluate(CreateContext(), client, confirmation, resolution);

    private void AssertRefused(string expectedErrorCode, Action evaluate)
    {
        Assert.Throws<OpenIdException>(evaluate);
        Assert.Equal(expectedErrorCode, CreatedErrorCode);
    }

    private static Dictionary<string, object> Values(params (string Name, object Value)[] values) =>
        values.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);

    #endregion

    #region Evaluate Tests — governing settings

    [Fact]
    public void Evaluate_WhenNoResourceServerIsTargeted_TheClientsSettingsGovern()
    {
        var client = CreateClient();

        var policy = Evaluate(client, Targeting());

        Assert.Same(client.Settings, policy.Settings);
    }

    [Theory]
    [InlineData(1, 5, 1)]
    [InlineData(5, 2, 2)]
    public void Evaluate_WhenTheClientAndAResourceServerCapTheLifetime_TheShorterGoverns(
        int clientMinutes,
        int resourceServerMinutes,
        int expectedMinutes
    )
    {
        var client = CreateClient(
            Values((OpenIdSettingNames.AccessTokenLifetime, TimeSpan.FromMinutes(clientMinutes)))
        );
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values(
                (
                    OpenIdSettingNames.AccessTokenLifetime,
                    TimeSpan.FromMinutes(resourceServerMinutes)
                )
            )
        );

        var policy = Evaluate(client, Targeting(resourceServer));

        Assert.Equal(
            TimeSpan.FromMinutes(expectedMinutes),
            policy.Settings.GetValue(OpenIdSettingKeys.AccessTokenLifetime)
        );
    }

    [Fact]
    public void Evaluate_WhenSeveralResourceServersAreTargeted_EveryOneBindsTheToken()
    {
        var client = CreateClient();
        var first = CreateResourceServer(
            "rs-a",
            Values((OpenIdSettingNames.AccessTokenLifetime, TimeSpan.FromMinutes(3)))
        );
        var second = CreateResourceServer(
            "rs-b",
            Values((OpenIdSettingNames.AccessTokenLifetime, TimeSpan.FromMinutes(2)))
        );

        var policy = Evaluate(client, Targeting(first, second));

        Assert.Equal(
            TimeSpan.FromMinutes(2),
            policy.Settings.GetValue(OpenIdSettingKeys.AccessTokenLifetime)
        );
    }

    [Fact]
    public void Evaluate_WhenAResourceServerCarriesASettingThatDoesNotApplyToIt_TheClientsValueGoverns()
    {
        // A resource server's effective settings carry every value it inherits; only those that apply to resource
        // servers bound the token, so a client setting merged by projection is never replaced.
        var client = CreateClient(
            Values((OpenIdSettingNames.AccessTokenType, JoseTokenTypes.AccessTokenJwt))
        );
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values((OpenIdSettingNames.AccessTokenType, "application/at+jwt"))
        );

        var policy = Evaluate(client, Targeting(resourceServer));

        Assert.Equal(
            JoseTokenTypes.AccessTokenJwt,
            policy.Settings.GetValue(OpenIdSettingKeys.AccessTokenType)
        );
    }

    #endregion

    #region Evaluate Tests — signing

    [Fact]
    public void Evaluate_WhenNothingNarrowsSigning_SignsWithTheTenantKeyInTheServersOrder()
    {
        var client = CreateClient();

        var policy = Evaluate(client, Targeting());

        Assert.Same(TenantKey, policy.SigningCredentials.SecretKey);
        Assert.Equal(Ps256, policy.SigningCredentials.SignatureAlgorithm.Code);
    }

    [Fact]
    public void Evaluate_WhenAResourceServerNarrowsTheSigningAlgorithms_KeepsTheServersOrderAmongThem()
    {
        // A resource server states a preference by narrowing, not by reordering: the server's order governs.
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values(
                (OpenIdSettingNames.AccessTokenSigningAlgValuesSupported, new[] { Es256, Rs256 })
            )
        );

        var policy = Evaluate(client, Targeting(resourceServer));

        Assert.Equal(
            [Rs256, Es256],
            policy.Settings.GetValue(OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported)
        );
        Assert.Equal(Rs256, policy.SigningCredentials.SignatureAlgorithm.Code);
    }

    [Fact]
    public void Evaluate_WhenTheTenantHasNoKeyForAPermittedSigningAlgorithm_FailsWithServerError()
    {
        var client = CreateClient(
            Values((OpenIdSettingNames.AccessTokenSigningAlgValuesSupported, new[] { Es256 }))
        );

        AssertRefused(OpenIdProtocol.ErrorCodes.ServerError, () => Evaluate(client, Targeting()));
    }

    #endregion

    #region Evaluate Tests — sender constraint

    [Fact]
    public void Evaluate_WhenNoSenderConstraintIsRequired_AcceptsAnUnboundToken()
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer("rs-api");

        var policy = Evaluate(client, Targeting(resourceServer));

        Assert.NotNull(policy.SigningCredentials);
    }

    [Fact]
    public void Evaluate_WhenAResourceServerRequiresASenderConstraintAndTheTokenIsUnbound_IsRefused()
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values((OpenIdSettingNames.AccessTokenSenderConstraintRequired, true))
        );

        AssertRefused(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            () => Evaluate(client, Targeting(resourceServer))
        );
    }

    [Fact]
    public void Evaluate_WhenTheClientRequiresASenderConstraint_ItBindsATokenForNoResourceServer()
    {
        var client = CreateClient(
            Values((OpenIdSettingNames.AccessTokenSenderConstraintRequired, true))
        );

        AssertRefused(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            () => Evaluate(client, Targeting())
        );
    }

    [Theory]
    [InlineData(Jkt)]
    [InlineData(X5tS256)]
    public void Evaluate_WhenASenderConstraintIsRequiredAndNoMethodIsNamed_AcceptsEveryBinding(
        string confirmationMethod
    )
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values((OpenIdSettingNames.AccessTokenSenderConstraintRequired, true))
        );
        var confirmation = confirmationMethod == Jkt ? DPoPBound : CertificateBound;

        var policy = Evaluate(client, Targeting(resourceServer), confirmation);

        Assert.NotNull(policy.SigningCredentials);
    }

    [Fact]
    public void Evaluate_WhenTheTokenIsBoundByAMethodTheResourceServerVerifies_IsAccepted()
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values(
                (OpenIdSettingNames.AccessTokenSenderConstraintRequired, true),
                (OpenIdSettingNames.AccessTokenConfirmationMethodsSupported, new[] { Jkt })
            )
        );

        var policy = Evaluate(client, Targeting(resourceServer), DPoPBound);

        Assert.NotNull(policy.SigningCredentials);
    }

    [Fact]
    public void Evaluate_WhenTheTokenIsBoundOnlyByAMethodTheResourceServerDoesNotVerify_IsRefused()
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values(
                (OpenIdSettingNames.AccessTokenSenderConstraintRequired, true),
                (OpenIdSettingNames.AccessTokenConfirmationMethodsSupported, new[] { Jkt })
            )
        );

        AssertRefused(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            () => Evaluate(client, Targeting(resourceServer), CertificateBound)
        );
    }

    [Fact]
    public void Evaluate_WhenTheTargetedResourceServersVerifyNoCommonMethod_IsRefused()
    {
        var client = CreateClient();
        var first = CreateResourceServer(
            "rs-a",
            Values(
                (OpenIdSettingNames.AccessTokenSenderConstraintRequired, true),
                (OpenIdSettingNames.AccessTokenConfirmationMethodsSupported, new[] { Jkt })
            )
        );
        var second = CreateResourceServer(
            "rs-b",
            Values((OpenIdSettingNames.AccessTokenConfirmationMethodsSupported, new[] { X5tS256 }))
        );

        AssertRefused(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            () => Evaluate(client, Targeting(first, second), DPoPBound)
        );
    }

    #endregion

    #region Evaluate Tests — encryption

    [Fact]
    public void Evaluate_WhenEncryptionIsNotRequired_DoesNotEncrypt()
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer("rs-api", null, ResourceServerKey);

        var policy = Evaluate(client, Targeting(resourceServer));

        Assert.Null(policy.EncryptionCredentials);
    }

    [Fact]
    public void Evaluate_WhenTheClientRequiresEncryptionForAResourceServerWithAKey_EncryptsToItsKey()
    {
        // The client's requirement binds once a resource server is targeted (issue #100).
        var client = CreateClient(Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true)));
        var resourceServer = CreateResourceServer("rs-api", null, ResourceServerKey);

        var policy = Evaluate(client, Targeting(resourceServer));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(ResourceServerKey, credentials.SecretKey);
        Assert.Equal(RsaOaep256, credentials.KeyManagementAlgorithm.Code);
    }

    [Fact]
    public void Evaluate_WhenTheResourceServerRequiresEncryption_EncryptsToItsKey()
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true)),
            ResourceServerKey
        );

        var policy = Evaluate(client, Targeting(resourceServer));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(ResourceServerKey, credentials.SecretKey);
    }

    [Fact]
    public void Evaluate_WhenTheClientNarrowsTheContentEncryption_TheResourceServersKeyUsesIt()
    {
        var client = CreateClient(
            Values(
                (OpenIdSettingNames.AccessTokenEncryptionRequired, true),
                (OpenIdSettingNames.AccessTokenEncryptionEncValuesSupported, new[] { A256Gcm })
            )
        );
        var resourceServer = CreateResourceServer("rs-api", null, ResourceServerKey);

        var policy = Evaluate(client, Targeting(resourceServer));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(ResourceServerKey, credentials.SecretKey);
        Assert.Equal(A256Gcm, credentials.AuthenticatedEncryptionAlgorithm.Code);
    }

    [Fact]
    public void Evaluate_WhenTheClientsAlgorithmsExcludeEveryKeyTheResourceServerDecryptsWith_FailsWithServerError()
    {
        // The resource server decrypts its tokens itself, so a token encrypted to the tenant's key instead would be one
        // it cannot read; the conflict is refused rather than misdirected. The tenant holds a key the combined settings
        // permit, so a fallback to it would succeed — only the rule refuses.
        using var tenantEc = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var tenantEncryptionKey = CreateTenantEncryptionKey(tenantEc);
        var client = CreateClient(
            Values(
                (OpenIdSettingNames.AccessTokenEncryptionRequired, true),
                (OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported, new[] { EcdhEs })
            )
        );
        var resourceServer = CreateResourceServer("rs-api", null, ResourceServerKey);

        AssertRefused(
            OpenIdProtocol.ErrorCodes.ServerError,
            () =>
                Service.Evaluate(
                    CreateContext(TenantKey, tenantEncryptionKey),
                    client,
                    default,
                    Targeting(resourceServer)
                )
        );
    }

    [Fact]
    public void Evaluate_WhenOnlyTheTenantHoldsAKeyForTheClientsAlgorithms_EncryptsToTheTenantKey()
    {
        // The control for the refusal above: with no resource server that decrypts its own tokens, the same tenant key
        // receives the token.
        using var tenantEc = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var tenantEncryptionKey = CreateTenantEncryptionKey(tenantEc);
        var client = CreateClient(
            Values(
                (OpenIdSettingNames.AccessTokenEncryptionRequired, true),
                (OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported, new[] { EcdhEs })
            )
        );
        var resourceServer = CreateResourceServer("rs-api");

        var policy = Service.Evaluate(
            CreateContext(TenantKey, tenantEncryptionKey),
            client,
            default,
            Targeting(resourceServer)
        );

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(tenantEncryptionKey, credentials.SecretKey);
        Assert.Equal(EcdhEs, credentials.KeyManagementAlgorithm.Code);
    }

    [Fact]
    public void Evaluate_WhenTheResourceServerHoldsNoKey_EncryptsToTheTenantKey()
    {
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true))
        );

        var policy = Evaluate(client, Targeting(resourceServer));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(TenantKey, credentials.SecretKey);
    }

    [Fact]
    public void Evaluate_WhenTheResourceServerHoldsOnlyASigningKey_EncryptsToTheTenantKey()
    {
        // A key the resource server cannot decrypt with does not make it a reader of its tokens.
        var client = CreateClient(Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true)));
        var signingKey = _serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsaPublicKey(
                new KeyMetadata { KeyId = "rs-sig", Use = SecretKeyUses.Signature },
                _resourceServerRsa
            );
        var resourceServer = CreateResourceServer("rs-api", null, signingKey);

        var policy = Evaluate(client, Targeting(resourceServer));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(TenantKey, credentials.SecretKey);
    }

    [Fact]
    public void Evaluate_WhenTheResourceServersOwnSettingsExcludeItsKey_EncryptsToTheTenantKey()
    {
        // The resource server's key is pinned to RSA-OAEP-256, which its own settings no longer permit, so it does not
        // decrypt its tokens; the token is encrypted to the tenant's key within every bound.
        var client = CreateClient(
            Values(
                (OpenIdSettingNames.AccessTokenEncryptionRequired, true),
                (
                    OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported,
                    new[] { RsaOaep256, RsaOaep }
                )
            )
        );
        var pinnedKey = _serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsaPublicKey(
                new KeyMetadata
                {
                    KeyId = "rs-pinned",
                    Use = SecretKeyUses.Encryption,
                    Algorithm = RsaOaep256,
                },
                _resourceServerRsa
            );
        var resourceServer = CreateResourceServer(
            "rs-api",
            Values((OpenIdSettingNames.AccessTokenEncryptionAlgValuesSupported, new[] { RsaOaep })),
            pinnedKey
        );

        var policy = Evaluate(client, Targeting(resourceServer));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(TenantKey, credentials.SecretKey);
        Assert.Equal(RsaOaep, credentials.KeyManagementAlgorithm.Code);
    }

    [Fact]
    public void Evaluate_WhenNoResourceServerIsTargeted_EncryptsToTheTenantKey()
    {
        var client = CreateClient(Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true)));

        var policy = Evaluate(client, Targeting());

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(TenantKey, credentials.SecretKey);
    }

    [Fact]
    public void Evaluate_WhenSeveralResourceServersAreTargetedAndOneDecryptsItsOwnTokens_IsRefused()
    {
        var client = CreateClient(Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true)));
        var reader = CreateResourceServer("rs-a", null, ResourceServerKey);
        var other = CreateResourceServer("rs-b");

        AssertRefused(
            OpenIdProtocol.ErrorCodes.InvalidRequest,
            () => Evaluate(client, Targeting(reader, other))
        );
    }

    [Fact]
    public void Evaluate_WhenSeveralResourceServersAreTargetedAndNoneDecryptsItsOwnTokens_EncryptsToTheTenantKey()
    {
        // A resource server that requires encryption but holds no key introspects its tokens, whatever else they target.
        var client = CreateClient();
        var first = CreateResourceServer(
            "rs-a",
            Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true))
        );
        var second = CreateResourceServer("rs-b");

        var policy = Evaluate(client, Targeting(first, second));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(TenantKey, credentials.SecretKey);
    }

    [Fact]
    public void Evaluate_WhenASystemResourceServerHoldsAKey_EncryptsToTheTenantKey()
    {
        // The key would be used for an ordinary resource server (Evaluate_WhenTheResourceServerRequiresEncryption_
        // EncryptsToItsKey); a system resource server is read by the server itself, which decrypts with the tenant's.
        var client = CreateClient();
        var resourceServer = CreateResourceServer(
            "rs-system",
            isSystem: true,
            Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true)),
            [ResourceServerKey]
        );

        var policy = Evaluate(client, Targeting(resourceServer));

        var credentials = Assert.IsType<JoseEncryptionCredentials>(policy.EncryptionCredentials);
        Assert.Same(TenantKey, credentials.SecretKey);
    }

    [Fact]
    public void Evaluate_WhenTheTenantHasNoEncryptionKey_FailsWithServerError()
    {
        var client = CreateClient(Values((OpenIdSettingNames.AccessTokenEncryptionRequired, true)));
        var signingOnlyKey = _serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(
                new KeyMetadata { KeyId = "tenant-sig", Use = SecretKeyUses.Signature },
                _tenantRsa
            );

        Assert.Throws<OpenIdException>(() =>
            Service.Evaluate(CreateContext(signingOnlyKey), client, default, Targeting())
        );
        Assert.Equal(OpenIdProtocol.ErrorCodes.ServerError, CreatedErrorCode);
    }

    #endregion
}
