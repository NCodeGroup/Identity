// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientSecretIssuerTests : BaseTests
{
    private const string KeyedMethod = "keyed_method";
    private const string ComparedMethod = "compared_method";
    private const string KeylessMethod = "keyless_method";

    private static DateTimeOffset Now { get; } = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

    private static JsonElement TenantDocument { get; } =
        JsonSerializer.SerializeToElement(new { tenant = true });

    private static JsonElement ClientDocument { get; } =
        JsonSerializer.SerializeToElement(new { client = true });

    private Mock<IClientAuthenticationMethodCatalog> MockCatalog { get; }
    private Mock<IEffectiveSettingsResolver> MockResolver { get; }
    private Mock<ISecretGenerator> MockSecretGenerator { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private ISecretProtector Protector { get; }

    public DefaultClientSecretIssuerTests()
    {
        MockCatalog = CreateStrictMock<IClientAuthenticationMethodCatalog>();
        MockResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockSecretGenerator = CreateStrictMock<ISecretGenerator>();
        MockCryptoService = CreateStrictMock<ICryptoService>();
        Protector = CreateStrictMock<ISecretProtector>().Object;
    }

    #region Helpers

    private DefaultClientSecretIssuer CreateIssuer() =>
        new(
            MockCatalog.Object,
            MockResolver.Object,
            MockSecretGenerator.Object,
            MockCryptoService.Object,
            TimeProvider
        );

    private void SetupMethod(string method, bool requiresClientSecret, bool usesClientSecretAsKey)
    {
        ClientAuthenticationMethodDescriptor? descriptor = new()
        {
            Name = method,
            RequiresClientSecret = requiresClientSecret,
            UsesClientSecretAsKey = usesClientSecretAsKey,
        };
        MockCatalog.Setup(x => x.TryGet(method, out descriptor)).Returns(true).Verifiable();
    }

    private void SetupKeyedMethod() =>
        SetupMethod(KeyedMethod, requiresClientSecret: true, usesClientSecretAsKey: true);

    private void SetupComparedMethod() =>
        SetupMethod(ComparedMethod, requiresClientSecret: true, usesClientSecretAsKey: false);

    private void SetupKeylessMethod() =>
        SetupMethod(KeylessMethod, requiresClientSecret: false, usesClientSecretAsKey: false);

    private void SetupUnregisteredMethod(string method)
    {
        ClientAuthenticationMethodDescriptor? descriptor = null;
        MockCatalog.Setup(x => x.TryGet(method, out descriptor)).Returns(false).Verifiable();
    }

    private void SetupResourceIds(params string[] resourceIds)
    {
        var sequence = MockCryptoService.SetupSequence(x =>
            x.GenerateKey(It.IsAny<int>(), BinaryEncodings.Base64Url)
        );
        foreach (var resourceId in resourceIds)
        {
            sequence = sequence.Returns(resourceId);
        }
    }

    private static SharedSecretRequest CreateRequest() =>
        new()
        {
            SecretId = "secret-1",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
        };

    private static GeneratedSecret CreateGeneratedSecret(string secretId, string secretEncoding) =>
        new()
        {
            Secret = CreatePersistedSecret(secretId, SecretTypes.Symmetric, secretEncoding),
            SecretMaterial = "the-client-secret",
        };

    private static PersistedSecret CreatePersistedSecret(
        string secretId,
        string secretType,
        string secretEncoding,
        DateTimeOffset? expiresWhen = null
    ) =>
        new PersistedSecretBuilder
        {
            SecretId = secretId,
            ExpiresWhen = expiresWhen ?? DateTimeOffset.MaxValue,
            RetiresWhen = expiresWhen ?? DateTimeOffset.MaxValue,
            SecretType = secretType,
            SecretEncoding = secretEncoding,
            EncodedValue = "nce1.value",
        }.Build();

    private static PersistedSecret CreateReversibleSecret(string secretId) =>
        CreatePersistedSecret(secretId, SecretTypes.Symmetric, SecretEncodings.SymmetricRaw);

    private static PersistedSecret CreateVerifierSecret(
        string secretId,
        DateTimeOffset? expiresWhen = null
    ) =>
        CreatePersistedSecret(
            secretId,
            SecretTypes.Symmetric,
            SecretEncodings.Verifier,
            expiresWhen
        );

    private static PersistedSecret CreatePublicKey(string secretId) =>
        CreatePersistedSecret(secretId, SecretTypes.Rsa, SecretEncodings.Spki);

    private void SetupResolution(Mock<IReadOnlySettingCollection> mockClientSettings)
    {
        var teamSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        var tenantSettings = CreateStrictMock<IReadOnlySettingCollection>().Object;
        MockResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ReturnsAsync(teamSettings)
            .Verifiable();
        MockResolver
            .Setup(x =>
                x.ResolveStored(
                    teamSettings,
                    OpenIdSettingScopes.Tenant,
                    "tenant-1",
                    It.Is<JsonElement>(element => element.GetProperty("tenant").GetBoolean())
                )
            )
            .Returns(tenantSettings)
            .Verifiable();
        MockResolver
            .Setup(x =>
                x.ResolveStored(
                    tenantSettings,
                    OpenIdSettingScopes.Client,
                    "client-1",
                    It.Is<JsonElement>(element => element.GetProperty("client").GetBoolean())
                )
            )
            .Returns(mockClientSettings.Object)
            .Verifiable();
    }

    #endregion

    #region GetTokenEndpointAuthMethodAsync Tests

    [Fact]
    public async Task GetTokenEndpointAuthMethodAsync_ResolvesTheClientsEffectiveMethod()
    {
        var mockClientSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockClientSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(KeyedMethod)
            .Verifiable();
        SetupResolution(mockClientSettings);

        var method = await CreateIssuer()
            .GetTokenEndpointAuthMethodAsync(
                "platform",
                "tenant-1",
                TenantDocument,
                "client-1",
                ClientDocument,
                CancellationToken.None
            );

        Assert.Equal(KeyedMethod, method);
    }

    [Fact]
    public async Task GetTokenEndpointAuthMethodAsync_WhenTheTenantHasNoSettings_ResolvesNoMethod()
    {
        var method = await CreateIssuer()
            .GetTokenEndpointAuthMethodAsync(
                "platform",
                "tenant-1",
                null,
                "client-1",
                ClientDocument,
                CancellationToken.None
            );

        Assert.Null(method);
    }

    [Fact]
    public async Task GetTokenEndpointAuthMethodAsync_WhenASettingsDocumentIsUnreadable_ResolvesNoMethod()
    {
        MockResolver
            .Setup(x => x.ResolveTeamAsync("platform", It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new UnreadableSettingsException(
                    OpenIdSettingScopes.Team,
                    "platform",
                    new JsonException("unreadable")
                )
            )
            .Verifiable();

        var method = await CreateIssuer()
            .GetTokenEndpointAuthMethodAsync(
                "platform",
                "tenant-1",
                TenantDocument,
                "client-1",
                ClientDocument,
                CancellationToken.None
            );

        Assert.Null(method);
    }

    #endregion

    #region RequiresClientSecret Tests

    [Fact]
    public void RequiresClientSecret_ReadsTheMethodsDescriptor()
    {
        SetupComparedMethod();
        SetupKeylessMethod();
        SetupUnregisteredMethod("unregistered_method");
        var issuer = CreateIssuer();

        Assert.True(issuer.RequiresClientSecret(ComparedMethod));
        Assert.False(issuer.RequiresClientSecret(KeylessMethod));
        Assert.False(issuer.RequiresClientSecret("unregistered_method"));
    }

    #endregion

    #region GenerateClientSecret Tests

    [Fact]
    public void GenerateClientSecret_WhenTheMethodUsesTheSecretAsAKey_GeneratesAReversibleSecret()
    {
        SetupKeyedMethod();
        var request = CreateRequest();
        var expected = CreateGeneratedSecret("secret-1", SecretEncodings.SymmetricRaw);
        MockSecretGenerator
            .Setup(x => x.GenerateReversibleSharedSecret(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, KeyedMethod, request);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GenerateClientSecret_WhenTheMethodComparesTheSecret_GeneratesAVerifier()
    {
        SetupComparedMethod();
        var request = CreateRequest();
        var expected = CreateGeneratedSecret("secret-1", SecretEncodings.Verifier);
        MockSecretGenerator
            .Setup(x => x.GenerateSharedSecretVerifier(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, ComparedMethod, request);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GenerateClientSecret_WhenTheMethodIsNotRegistered_GeneratesAVerifier()
    {
        SetupUnregisteredMethod("unregistered_method");
        var request = CreateRequest();
        var expected = CreateGeneratedSecret("secret-1", SecretEncodings.Verifier);
        MockSecretGenerator
            .Setup(x => x.GenerateSharedSecretVerifier(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, "unregistered_method", request);

        Assert.Same(expected, actual);
    }

    [Fact]
    public void GenerateClientSecret_WhenTheMethodCannotBeResolved_GeneratesAVerifier()
    {
        var request = CreateRequest();
        var expected = CreateGeneratedSecret("secret-1", SecretEncodings.Verifier);
        MockSecretGenerator
            .Setup(x => x.GenerateSharedSecretVerifier(Protector, request))
            .Returns(expected)
            .Verifiable();

        var actual = CreateIssuer().GenerateClientSecret(Protector, null, request);

        Assert.Same(expected, actual);
    }

    #endregion

    #region ReconcileClientSecrets Tests

    [Fact]
    public void ReconcileClientSecrets_WhenTheMethodComparesTheSecret_ConvertsEachReversibleSecret()
    {
        SetupComparedMethod();
        SetupResourceIds("converted-1");
        var reversible = CreateReversibleSecret("reversible-1");
        var verifier = CreateVerifierSecret("verifier-1");
        var publicKey = CreatePublicKey("key-1");
        var converted = CreateVerifierSecret("converted-1");
        MockSecretGenerator
            .Setup(x => x.ConvertToSharedSecretVerifier(Protector, reversible, "converted-1"))
            .Returns(converted)
            .Verifiable();

        var reconciliation = CreateIssuer()
            .ReconcileClientSecrets(Protector, ComparedMethod, [reversible, verifier, publicKey]);

        Assert.Equal([converted, verifier, publicKey], reconciliation.Secrets);
        Assert.Equal([reversible], reconciliation.RemovedSecrets);
        Assert.Equal([converted], reconciliation.AddedSecrets);
        Assert.Null(reconciliation.IssuedSecret);
    }

    [Fact]
    public void ReconcileClientSecrets_WhenTheMethodNeedsNoSecret_ConvertsEachReversibleSecretAndIssuesNone()
    {
        SetupKeylessMethod();
        SetupResourceIds("converted-1");
        var reversible = CreateReversibleSecret("reversible-1");
        var converted = CreateVerifierSecret("converted-1");
        MockSecretGenerator
            .Setup(x => x.ConvertToSharedSecretVerifier(Protector, reversible, "converted-1"))
            .Returns(converted)
            .Verifiable();

        var reconciliation = CreateIssuer()
            .ReconcileClientSecrets(Protector, KeylessMethod, [reversible]);

        Assert.Equal([converted], reconciliation.Secrets);
        Assert.Null(reconciliation.IssuedSecret);
    }

    [Fact]
    public void ReconcileClientSecrets_WhenTheMethodUsesTheSecretAsAKeyAndOneServesIt_KeepsEverySecret()
    {
        SetupKeyedMethod();
        var reversible = CreateReversibleSecret("reversible-1");
        var verifier = CreateVerifierSecret("verifier-1");

        var reconciliation = CreateIssuer()
            .ReconcileClientSecrets(Protector, KeyedMethod, [reversible, verifier]);

        Assert.Equal([reversible, verifier], reconciliation.Secrets);
        Assert.Empty(reconciliation.RemovedSecrets);
        Assert.Empty(reconciliation.AddedSecrets);
        Assert.Null(reconciliation.IssuedSecret);
    }

    [Fact]
    public void ReconcileClientSecrets_WhenTheMethodUsesTheSecretAsAKeyAndOnlyVerifiersAreHeld_IssuesAReversibleSecret()
    {
        SetupKeyedMethod();
        SetupResourceIds("issued-1");
        var verifier = CreateVerifierSecret("verifier-1");
        var publicKey = CreatePublicKey("key-1");
        var issued = CreateGeneratedSecret("issued-1", SecretEncodings.SymmetricRaw);
        MockSecretGenerator
            .Setup(x =>
                x.GenerateReversibleSharedSecret(
                    Protector,
                    It.Is<SharedSecretRequest>(request =>
                        request.SecretId == "issued-1"
                        && request.CreatedWhen == Now
                        && request.ExpiresWhen == DateTimeOffset.MaxValue
                        && request.EntropyBits == SharedSecretPolicy.MinimumEntropyBits
                    )
                )
            )
            .Returns(issued)
            .Verifiable();

        var reconciliation = CreateIssuer()
            .ReconcileClientSecrets(Protector, KeyedMethod, [verifier, publicKey]);

        Assert.Equal([publicKey, issued.Secret], reconciliation.Secrets);
        Assert.Equal([verifier], reconciliation.RemovedSecrets);
        Assert.Equal([issued.Secret], reconciliation.AddedSecrets);
        Assert.Same(issued, reconciliation.IssuedSecret);
    }

    [Fact]
    public void ReconcileClientSecrets_WhenEverySharedSecretHasExpired_IssuesASecretInTheMethodsForm()
    {
        SetupComparedMethod();
        SetupResourceIds("issued-1");
        var expired = CreateVerifierSecret("verifier-1", expiresWhen: Now);
        var issued = CreateGeneratedSecret("issued-1", SecretEncodings.Verifier);
        MockSecretGenerator
            .Setup(x =>
                x.GenerateSharedSecretVerifier(
                    Protector,
                    It.Is<SharedSecretRequest>(request => request.SecretId == "issued-1")
                )
            )
            .Returns(issued)
            .Verifiable();

        var reconciliation = CreateIssuer()
            .ReconcileClientSecrets(Protector, ComparedMethod, [expired]);

        Assert.Equal([issued.Secret], reconciliation.Secrets);
        Assert.Equal([expired], reconciliation.RemovedSecrets);
        Assert.Same(issued, reconciliation.IssuedSecret);
    }

    [Fact]
    public void ReconcileClientSecrets_WhenTheMethodNeedsNoSecretAndNoneIsHeld_ChangesNothing()
    {
        SetupKeylessMethod();
        var publicKey = CreatePublicKey("key-1");

        var reconciliation = CreateIssuer()
            .ReconcileClientSecrets(Protector, KeylessMethod, [publicKey]);

        Assert.Equal([publicKey], reconciliation.Secrets);
        Assert.Empty(reconciliation.RemovedSecrets);
        Assert.Empty(reconciliation.AddedSecrets);
    }

    [Fact]
    public void ReconcileClientSecrets_WhenTheMethodCannotBeResolved_KeepsEverySecret()
    {
        // Strict mocks: no catalog or generator setup, so converting a secret would fail the test.
        var reversible = CreateReversibleSecret("reversible-1");

        var reconciliation = CreateIssuer().ReconcileClientSecrets(Protector, null, [reversible]);

        Assert.Equal([reversible], reconciliation.Secrets);
        Assert.Empty(reconciliation.RemovedSecrets);
        Assert.Empty(reconciliation.AddedSecrets);
        Assert.Null(reconciliation.IssuedSecret);
    }

    #endregion
}
