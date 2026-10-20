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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
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
    private readonly ServiceProvider _serviceProvider;
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly ECDsa _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public DefaultClientKeyImporterTests()
    {
        // The real key-type catalog, secret importer, and algorithms are the policy's sources of truth.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddIdentityLibrary();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddPersistenceLibrary();
        secrets.AddJoseLibrary();
        _serviceProvider = serviceCollection.BuildServiceProvider();

        Importer = new DefaultClientKeyImporter(
            _serviceProvider.GetRequiredService<ILogger<DefaultClientKeyImporter>>(),
            _serviceProvider.GetRequiredService<ICryptoService>(),
            _serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            _serviceProvider.GetRequiredService<ISecretImporter>(),
            _serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>()
        );
    }

    private DefaultClientKeyImporter Importer { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        _rsa.Dispose();
        _ecdsa.Dispose();
    }

    #region Scaffolding

    private JsonObject RsaKey(string? kid)
    {
        var parameters = _rsa.ExportParameters(includePrivateParameters: false);
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
        var parameters = _ecdsa.ExportParameters(includePrivateParameters: false);
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
            Format = Enum.Parse<SecretKeyImportFormat>(format),
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
    public void TryImportRegisteredKeySet_WhenKidIsMissing_ReturnsError()
    {
        var imported = TryImportRegistered(KeySet(RsaKey(kid: null)), out _, out var error);

        Assert.False(imported);
        Assert.Contains("'kid'", error, StringComparison.Ordinal);
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
        var request = CreateImportRequest("Jwk", jwk.ToJsonString());

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
            "Jwk",
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
        var request = CreateImportRequest("Jwk", jwk.ToJsonString(), keyId, use, algorithm);

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains(member, error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenJwkCarriesPrivateMaterial_ReturnsError()
    {
        var jwk = With(RsaKey("client-kid"), "d", "AAAA");
        var request = CreateImportRequest("Jwk", jwk.ToJsonString());

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("private or symmetric", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenJwkKeyOperationsContradictTheRequestedUse_ReturnsError()
    {
        var jwk = With(RsaKey("client-kid"), "key_ops", new JsonArray("verify"));
        var request = CreateImportRequest("Jwk", jwk.ToJsonString(), use: "enc");

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("inconsistent", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenMaterialIsNotAJwk_ReturnsError()
    {
        var request = CreateImportRequest("Jwk", "not json");

        var imported = Importer.TryImportKey(request, out _, out var error);

        Assert.False(imported);
        Assert.Contains("not a JSON Web Key", error, StringComparison.Ordinal);
    }

    [Fact]
    public void TryImportKey_WhenPem_UsesTheRequestedMetadata()
    {
        var request = CreateImportRequest(
            "Pem",
            _rsa.ExportSubjectPublicKeyInfoPem(),
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
            "Pem",
            _rsa.ExportSubjectPublicKeyInfoPem(),
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
            Format = SecretKeyImportFormat.Pem,
            KeyMaterial = _rsa.ExportSubjectPublicKeyInfoPem(),
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
