// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientKeyImporterTests : IAsyncLifetime
{
    // RFC 8037 Appendix A.2's Ed25519 public key and Appendix A.6's X25519 public key: both are 256-bit OKP keys, so
    // only the curve tells them apart.
    private const string Ed25519PublicKey = "11qYAYKxCrfVS_7TyWQHOg7hcvPapiMlrwIaaPcHURo";
    private const string X25519PublicKey = "3p7bfXt9wbTTW2HC7OQ1Nz-DQ8hbeGdNrfx-FG-IK08";

    private ServiceProvider ServiceProvider { get; }
    private RSA Rsa { get; } = RSA.Create(2048);
    private ECDsa Ecdsa { get; } = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public DefaultClientKeyImporterTests()
    {
        // The real key-type catalog, secret importer, and algorithms are the policy's sources of truth.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddIdentityLibrary();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddPersistenceLibrary();
        secrets.AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Importer = new DefaultClientKeyImporter(
            ServiceProvider.GetRequiredService<ILogger<DefaultClientKeyImporter>>(),
            ServiceProvider.GetRequiredService<ICryptoService>(),
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            ServiceProvider.GetRequiredService<ISecretImporter>(),
            ServiceProvider.GetRequiredService<ISecretSerializer>(),
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>()
        );
    }

    private DefaultClientKeyImporter Importer { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        Rsa.Dispose();
        Ecdsa.Dispose();
    }

    #region Scaffolding

    private JsonObject RsaKey(string? kid)
    {
        var parameters = Rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new JsonObject
        {
            ["kty"] = "RSA",
            ["n"] = Base64Url.EncodeToString(parameters.Modulus),
            ["e"] = Base64Url.EncodeToString(parameters.Exponent),
        };
        if (kid is not null)
            jwk["kid"] = kid;
        return jwk;
    }

    private JsonObject EcKey(string kid)
    {
        var parameters = Ecdsa.ExportParameters(includePrivateParameters: false);
        return new JsonObject
        {
            ["kty"] = "EC",
            ["kid"] = kid,
            ["crv"] = "P-256",
            ["x"] = Base64Url.EncodeToString(parameters.Q.X),
            ["y"] = Base64Url.EncodeToString(parameters.Q.Y),
        };
    }

    private static JsonObject With(JsonObject jwk, string name, JsonNode? value)
    {
        jwk[name] = value;
        return jwk;
    }

    private static JsonElement KeySet(params JsonNode[] keys) =>
        JsonSerializer.SerializeToElement(new JsonObject { ["keys"] = new JsonArray(keys) });

    private bool TryImportRegistered(
        JsonElement keySet,
        out IReadOnlyList<PersistedSecret>? secrets,
        out string? error
    ) =>
        Importer.TryImportRegisteredKeySet(
            keySet,
            DateTimeOffset.UnixEpoch,
            out secrets,
            out error
        );

    private bool TryImportPublished(
        JsonElement keySet,
        out IReadOnlyList<PersistedSecret>? secrets,
        out string? error
    ) =>
        Importer.TryImportPublishedKeySet(keySet, DateTimeOffset.UnixEpoch, out secrets, out error);

    private static SecretKeyImportRequest CreateImportRequest(
        string format,
        string keyMaterial,
        string? keyId = null,
        string? use = null,
        string? algorithm = null
    ) =>
        new()
        {
            SecretId = "secret-1",
            KeyId = keyId,
            SecretType = SecretTypes.Rsa,
            Format = format,
            KeyMaterial = keyMaterial,
            Use = use,
            Algorithm = algorithm,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
        };

    #endregion

    #region TryImportRegisteredKeySet Tests

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeysAreValid_IdentifiesEachByItsKid()
    {
        var keySet = KeySet(
            With(With(RsaKey("rsa-1"), "use", "sig"), "alg", "RS256"),
            EcKey("ec-1")
        );

        var imported = TryImportRegistered(keySet, out var secrets, out var error);

        Assert.True(imported, error);
        Assert.NotNull(secrets);
        Assert.Equal(["rsa-1", "ec-1"], secrets.Select(secret => secret.KeyId));
        Assert.Equal([SecretTypes.Rsa, SecretTypes.Ecc], secrets.Select(x => x.SecretType));
        Assert.DoesNotContain(secrets, secret => secret.SecretId == secret.KeyId);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenEmpty_ImportsNothing()
    {
        var imported = TryImportRegistered(KeySet(), out var secrets, out _);

        Assert.True(imported);
        Assert.NotNull(secrets);
        Assert.Empty(secrets);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"keys":{}}""")]
    [InlineData("""{"kty":"RSA"}""")]
    public void TryImportRegisteredKeySet_WhenNotAKeySet_ReturnsError(string json)
    {
        var imported = TryImportRegistered(JsonElement.Parse(json), out _, out var error);

        Assert.False(imported);
        Assert.Contains("'keys' array", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenAMemberIsNotAKey_ReturnsError()
    {
        var imported = TryImportRegistered(KeySet(RsaKey("rsa-1"), 1), out _, out var error);

        Assert.False(imported);
        Assert.Contains("'kty'", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("d")]
    [InlineData("p")]
    [InlineData("k")]
    public void TryImportRegisteredKeySet_WhenKeyCarriesPrivateMaterial_ReturnsError(
        string privateMember
    )
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), privateMember, "AAAA"));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("private or symmetric", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyIsSymmetric_ReturnsError()
    {
        var keySet = KeySet(new JsonObject { ["kty"] = "oct", ["kid"] = "oct-1" });

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("secret key material", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyTypeIsUnsupported_ReturnsError()
    {
        var keySet = KeySet(new JsonObject { ["kty"] = "XYZ", ["kid"] = "xyz-1" });

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("'XYZ'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKidIsMissing_NamesTheKeyByItsThumbprint()
    {
        var json = RsaKey(kid: null).ToJsonString();
        var expected = JsonWebKey.Parse(json).ComputeThumbprint();

        var imported = TryImportRegistered(
            KeySet(RsaKey(kid: null)),
            out var secrets,
            out var error
        );

        Assert.True(imported, error);
        Assert.NotNull(secrets);
        var secret = Assert.Single(secrets);
        Assert.Equal(expected, secret.KeyId);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKidIsEmpty_NamesTheKeyByTheSameThumbprint()
    {
        var expected = JsonWebKey.Parse(RsaKey(kid: null).ToJsonString()).ComputeThumbprint();

        var imported = TryImportRegistered(KeySet(RsaKey(kid: "")), out var secrets, out var error);

        Assert.True(imported, error);
        Assert.NotNull(secrets);
        var secret = Assert.Single(secrets);
        Assert.Equal(expected, secret.KeyId);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKidIsNull_NamesTheKeyByItsThumbprint()
    {
        var expected = JsonWebKey.Parse(RsaKey(kid: null).ToJsonString()).ComputeThumbprint();
        var keySet = KeySet(With(RsaKey(kid: null), "kid", null));

        var imported = TryImportRegistered(keySet, out var secrets, out var error);

        Assert.True(imported, error);
        Assert.NotNull(secrets);
        var secret = Assert.Single(secrets);
        Assert.Equal(expected, secret.KeyId);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenAnUnnamedKeyTypeIsUnsupported_ReturnsError()
    {
        var keySet = KeySet(new JsonObject { ["kty"] = "XYZ" });

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("not supported", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenTwoKeysHaveNoKidAndAreTheSameKey_ReturnsError()
    {
        var keySet = KeySet(RsaKey(kid: null), RsaKey(kid: null));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("is not unique", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenAKeyDeclaresAnotherKeysThumbprint_ReturnsError()
    {
        var json = RsaKey(kid: null).ToJsonString();
        var thumbprint = JsonWebKey.Parse(json).ComputeThumbprint();
        Assert.True(
            TryImportRegistered(KeySet(RsaKey(kid: null)), out _, out var rsaError),
            rsaError
        );
        Assert.True(
            TryImportRegistered(KeySet(EcKey(thumbprint)), out _, out var ecError),
            ecError
        );
        var keySet = KeySet(RsaKey(kid: null), EcKey(thumbprint));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("is not unique", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKidIsRepeated_ReturnsError()
    {
        var keySet = KeySet(RsaKey("shared"), EcKey("shared"));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("not unique", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKidIsTooLong_ReturnsError()
    {
        var keyId = new string('k', SecretMaxLengths.KeyId + 1);

        var imported = TryImportRegistered(KeySet(RsaKey(keyId)), out _, out var error);

        Assert.False(imported);
        Assert.Contains("must not exceed", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenAlgorithmIsNotAString_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "alg", 5));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("'alg' member must be a string", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenUseIsUnknown_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "use", "both"));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("'both'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyOperationsCombineUses_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "key_ops", new JsonArray("verify", "encrypt")));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("must not combine", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyOperationsContradictUse_ReturnsError()
    {
        var key = With(With(RsaKey("rsa-1"), "use", "sig"), "key_ops", new JsonArray("encrypt"));

        var imported = TryImportRegistered(KeySet(key), out _, out var error);

        Assert.False(imported);
        Assert.Contains("inconsistent", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyOperationsRepeat_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "key_ops", new JsonArray("verify", "verify")));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("must not repeat", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyOperationsAreNotStrings_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "key_ops", "verify"));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("array of strings", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyOperationsImplyAUseTheAlgorithmContradicts_ReturnsError()
    {
        var key = With(With(RsaKey("rsa-1"), "key_ops", new JsonArray("encrypt")), "alg", "RS256");

        var imported = TryImportRegistered(KeySet(key), out _, out var error);

        Assert.False(imported);
        Assert.Contains("'sig'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenAlgorithmContradictsUse_ReturnsError()
    {
        var key = With(With(RsaKey("rsa-1"), "use", "enc"), "alg", "RS256");

        var imported = TryImportRegistered(KeySet(key), out _, out var error);

        Assert.False(imported);
        Assert.Contains("'sig'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenAlgorithmDoesNotFitTheKeyType_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "alg", "ES256"));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("'ES256'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenAlgorithmIsUnknown_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "alg", "XX999"));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("'XX999'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportRegisteredKeySet_WhenKeyMaterialIsMalformed_ReturnsError()
    {
        var keySet = KeySet(With(RsaKey("rsa-1"), "n", "not base64url!"));

        var imported = TryImportRegistered(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("The key is invalid", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ed25519", Ed25519PublicKey, "EdDSA", true)]
    [InlineData("X25519", X25519PublicKey, "ECDH-ES", true)]
    [InlineData("Ed25519", Ed25519PublicKey, "ECDH-ES", false)]
    [InlineData("X25519", X25519PublicKey, "EdDSA", false)]
    [InlineData("X25519", X25519PublicKey, "Ed25519", false)]
    public async Task TryImportRegisteredKeySet_WhenOkpKeyDeclaresAnAlgorithm_AcceptsOnlyAnAlgorithmOfItsCurve(
        string curve,
        string x,
        string algorithm,
        bool expected
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddIdentityLibrary();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddPersistenceLibrary();
        secrets.AddJoseLibrary().AddEdDsa();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();
        var importer = new DefaultClientKeyImporter(
            serviceProvider.GetRequiredService<ILogger<DefaultClientKeyImporter>>(),
            serviceProvider.GetRequiredService<ICryptoService>(),
            serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            serviceProvider.GetRequiredService<ISecretImporter>(),
            serviceProvider.GetRequiredService<ISecretSerializer>(),
            serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>()
        );
        var keySet = KeySet(
            new JsonObject
            {
                ["kty"] = "OKP",
                ["kid"] = "okp-1",
                ["crv"] = curve,
                ["x"] = x,
                ["alg"] = algorithm,
            }
        );

        var imported = importer.TryImportRegisteredKeySet(
            keySet,
            DateTimeOffset.UnixEpoch,
            out _,
            out var error
        );

        Assert.Equal(expected, imported);
        if (!expected)
        {
            Assert.Contains($"The curve '{curve}'", error, StringComparison.Ordinal);
        }
    }

    #endregion

    #region TryImportPublishedKeySet Tests

    [Fact]
    public void TryImportPublishedKeySet_WhenAKeyIsUnusable_IgnoresIt()
    {
        var keySet = KeySet(
            1,
            new JsonObject { ["kty"] = "XYZ", ["kid"] = "xyz-1" },
            RsaKey(kid: null),
            With(EcKey("ec-1"), "alg", "XX999"),
            With(EcKey("ec-2"), "alg", "RS256"),
            RsaKey("rsa-1")
        );

        var imported = TryImportPublished(keySet, out var secrets, out var error);

        Assert.True(imported, error);
        Assert.NotNull(secrets);
        var secret = Assert.Single(secrets);
        Assert.Equal("rsa-1", secret.KeyId);
    }

    [Fact]
    public void TryImportPublishedKeySet_WhenAKeyCarriesPrivateMaterial_RefusesTheSet()
    {
        var keySet = KeySet(RsaKey("rsa-1"), With(EcKey("ec-1"), "d", "AAAA"));

        var imported = TryImportPublished(keySet, out var secrets, out var error);

        Assert.False(imported);
        Assert.Null(secrets);
        Assert.Contains("private or symmetric", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportPublishedKeySet_WhenAContributedKeyCarriesItsPrivateMember_RefusesTheSet()
    {
        var keyType = new JsonWebKeyTypeDescriptor("XKP", ["pub"], ["sk"]);
        ISecretKeyTypeDescriptor? descriptor = Mock.Of<ISecretKeyTypeDescriptor>(x =>
            x.KeyType == "xkp" && x.JwkKeyType == keyType
        );
        var catalog = new Mock<ISecretKeyTypeCatalog>();
        catalog.Setup(x => x.TryGetByJwkKeyType("XKP", out descriptor)).Returns(true);
        var importer = new DefaultClientKeyImporter(
            ServiceProvider.GetRequiredService<ILogger<DefaultClientKeyImporter>>(),
            ServiceProvider.GetRequiredService<ICryptoService>(),
            catalog.Object,
            ServiceProvider.GetRequiredService<ISecretImporter>(),
            ServiceProvider.GetRequiredService<ISecretSerializer>(),
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>()
        );
        var keySet = KeySet(
            new JsonObject
            {
                ["kty"] = "XKP",
                ["kid"] = "x-1",
                ["pub"] = "AAAA",
                ["sk"] = "AAAA",
            }
        );

        var imported = importer.TryImportPublishedKeySet(
            keySet,
            DateTimeOffset.UnixEpoch,
            out _,
            out var error
        );

        Assert.False(imported);
        Assert.Contains("private or symmetric", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportPublishedKeySet_WhenAnUnrecognizedKeyCarriesPrivateMaterial_RefusesTheSet()
    {
        var keySet = KeySet(
            RsaKey("rsa-1"),
            new JsonObject
            {
                ["kty"] = "XYZ",
                ["kid"] = "xyz-1",
                ["d"] = "AAAA",
            }
        );

        var imported = TryImportPublished(keySet, out _, out _);

        Assert.False(imported);
    }

    [Fact]
    public void TryImportPublishedKeySet_WhenTwoUsableKeysShareAKid_RefusesTheSet()
    {
        var keySet = KeySet(RsaKey("shared"), EcKey("shared"));

        var imported = TryImportPublished(keySet, out _, out var error);

        Assert.False(imported);
        Assert.Contains("not unique", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportPublishedKeySet_WhenNotAKeySet_ReturnsError()
    {
        var imported = TryImportPublished(JsonElement.Parse("[]"), out _, out var error);

        Assert.False(imported);
        Assert.Contains("'keys' array", error, StringComparison.Ordinal);
    }

    #endregion

    #region TryImportKey Tests

    [Fact]
    public void TryImportKey_WhenJwkDeclaresMetadata_KeepsIt()
    {
        var jwk = With(With(RsaKey("client-kid"), "use", "sig"), "alg", "PS256");
        var request = CreateImportRequest(SecretKeyImportFormats.Jwk, jwk.ToJsonString());

        var imported = Importer.TryImportKey(request, out var secret, out var error);

        Assert.True(imported, error);
        Assert.NotNull(secret);
        Assert.Equal("secret-1", secret.SecretId);
        Assert.Equal("client-kid", secret.KeyId);
        Assert.Equal("sig", secret.Use);
        Assert.Equal("PS256", secret.Algorithm);
    }

    [Fact]
    public void TryImportKey_WhenRequestRepeatsTheJwkMetadata_Imports()
    {
        var jwk = With(RsaKey("client-kid"), "alg", "RS256");
        var request = CreateImportRequest(
            SecretKeyImportFormats.Jwk,
            jwk.ToJsonString(),
            keyId: "client-kid",
            algorithm: "RS256"
        );

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.True(imported, error);
    }

    [Theory]
    [InlineData("other-kid", null, null, "'kid'")]
    [InlineData(null, "enc", null, "'use'")]
    [InlineData(null, null, "PS512", "'alg'")]
    public void TryImportKey_WhenRequestContradictsTheJwk_ReturnsError(
        string? keyId,
        string? use,
        string? algorithm,
        string member
    )
    {
        var jwk = With(With(RsaKey("client-kid"), "use", "sig"), "alg", "RS256");
        var request = CreateImportRequest(
            SecretKeyImportFormats.Jwk,
            jwk.ToJsonString(),
            keyId,
            use,
            algorithm
        );

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains(member, error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenJwkCarriesPrivateMaterial_ReturnsError()
    {
        var jwk = With(RsaKey("client-kid"), "d", "AAAA");
        var request = CreateImportRequest(SecretKeyImportFormats.Jwk, jwk.ToJsonString());

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("private or symmetric", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenJwkKeyOperationsContradictTheRequestedUse_ReturnsError()
    {
        var jwk = With(RsaKey("client-kid"), "key_ops", new JsonArray("verify"));
        var request = CreateImportRequest(
            SecretKeyImportFormats.Jwk,
            jwk.ToJsonString(),
            use: "enc"
        );

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("inconsistent", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenMaterialIsNotAJwk_ReturnsError()
    {
        var request = CreateImportRequest(SecretKeyImportFormats.Jwk, "not json");

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("not a JSON Web Key", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("openssh")]
    [InlineData("PEM")]
    public void TryImportKey_WhenTheKeyTypeDoesNotImportTheFormat_ReturnsError(string format)
    {
        var request = CreateImportRequest(format, Rsa.ExportSubjectPublicKeyInfoPem());

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains($"'{format}'", error, StringComparison.Ordinal);
        Assert.Contains("'jwk', 'pem', 'certificate'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenPem_UsesTheRequestedMetadata()
    {
        var request = CreateImportRequest(
            SecretKeyImportFormats.Pem,
            Rsa.ExportSubjectPublicKeyInfoPem(),
            use: "sig",
            algorithm: "RS256"
        );

        var imported = Importer.TryImportKey(request, out var secret, out var error);

        Assert.True(imported, error);
        Assert.NotNull(secret);
        Assert.Null(secret.KeyId);
        Assert.Equal("RS256", secret.Algorithm);
    }

    [Fact]
    public void TryImportKey_WhenPemAlgorithmDoesNotFitTheKey_ReturnsError()
    {
        var request = CreateImportRequest(
            SecretKeyImportFormats.Pem,
            Rsa.ExportSubjectPublicKeyInfoPem(),
            algorithm: "ES256"
        );

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("'ES256'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenSecretTypeIsUnknown_ReturnsError()
    {
        var request = new SecretKeyImportRequest
        {
            SecretId = "secret-1",
            SecretType = "unknown",
            Format = SecretKeyImportFormats.Pem,
            KeyMaterial = Rsa.ExportSubjectPublicKeyInfoPem(),
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.MaxValue,
        };

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("'unknown'", error, StringComparison.Ordinal);
    }

    #endregion
}
