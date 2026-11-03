// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.Tests.ContributedKeyTypes;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Base64Url = NCode.Encoders.Base64Url;
using JoseException = NCode.Identity.Jose.Exceptions.JoseException;

namespace NCode.Identity.Jose.Tests.Algorithms.KeyManagement;

/// <summary>
/// Composes the ECDH-ES algorithms with a key type the test contributes, the made-up <c>XKP</c> type, and an
/// <see cref="IEcdhKeyAgreement"/> for it, so the <c>kty</c> and the members of the <c>epk</c> can come only from that
/// type's descriptors and not from the algorithm.
/// </summary>
// See ADR-1389.
public class ContributedKeyTypeEcdhTests : BaseTests
{
    private const string Enc = "A128GCM";

    private ServiceProvider ServiceProvider { get; }
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }
    private IJoseSerializer JoseSerializer { get; }

    public ContributedKeyTypeEcdhTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISecretKeyTypeDescriptor, XkpSecretKeyTypeDescriptor>()
        );
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEcdhKeyAgreement, XkpEcdhKeyAgreement>()
        );
        ServiceProvider = serviceCollection.BuildServiceProvider();

        SecretKeyTypeCatalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        JoseSerializer = ServiceProvider.GetRequiredService<IJoseSerializer>();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
        await base.DisposeAsyncCore();
    }

    #region Scaffolding

    private XkpSecretKey CreateRecipientKey()
    {
        Assert.True(SecretKeyTypeCatalog.TryGet(XkpKeyType.SecretKeyTypeName, out var descriptor));
        byte[] keyMaterial = [];
        var generated = descriptor.GenerateKeyMaterial(
            new SecretKeyGenerationParameters { KeySizeBits = 256 },
            bytes =>
            {
                keyMaterial = bytes.ToArray();
                return string.Empty;
            }
        );
        var secretKey = descriptor.DeserializeKeyMaterial(
            new KeyMetadata { KeyId = "recipient" },
            generated.SecretEncoding,
            keyMaterial
        );
        return Assert.IsType<XkpSecretKey>(secretKey);
    }

    private KeyManagementAlgorithm GetAlgorithm(string code)
    {
        Assert.True(AlgorithmCollection.TryGetKeyManagementAlgorithm(code, out var algorithm));
        return algorithm;
    }

    // An ECDH agreement over the XKP type's P-256 key material.
    private sealed class XkpEcdhKeyAgreement : IEcdhKeyAgreement
    {
        public AlgorithmKeyType KeyType { get; } =
            new(typeof(XkpSecretKey), [new KeySizes(minSize: 256, maxSize: 256, skipSize: 0)]);

        public bool IsKeyCompatible(SecretKey secretKey) => secretKey is XkpSecretKey;

        public int GetSharedSecretSizeBytes(SecretKey secretKey) => 32;

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        )
        {
            using var recipient = ECDiffieHellman.Create();
            recipient.ImportSubjectPublicKeyInfo(
                ((XkpSecretKey)recipientKey).SubjectPublicKeyInfo,
                out _
            );
            using var recipientPublicKey = recipient.PublicKey;
            using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            ephemeral.DeriveRawSecretAgreement(recipientPublicKey).CopyTo(sharedSecret);
            ephemeralPublicKeyWriter.Write(ephemeral.ExportSubjectPublicKeyInfo());
        }

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        )
        {
            using var recipient = ECDiffieHellman.Create();
            recipient.ImportPkcs8PrivateKey(
                ((XkpSecretKey)recipientKey).Pkcs8PrivateKey
                    ?? throw new InvalidOperationException("The key holds only its public half."),
                out _
            );
            using var ephemeral = ECDiffieHellman.Create();
            ephemeral.ImportSubjectPublicKeyInfo(
                ((XkpSecretKey)ephemeralPublicKey).SubjectPublicKeyInfo,
                out _
            );
            using var ephemeralPublic = ephemeral.PublicKey;
            recipient.DeriveRawSecretAgreement(ephemeralPublic).CopyTo(sharedSecret);
        }
    }

    #endregion

    #region KeyTypes Tests

    [Theory]
    [InlineData("ECDH-ES")]
    [InlineData("ECDH-ES+A128KW")]
    [InlineData("ECDH-ES+A192KW")]
    [InlineData("ECDH-ES+A256KW")]
    public void KeyTypes_WhenAgreementIsContributed_ListsItsKeyTypeAfterTheCoreOnes(string code)
    {
        var algorithm = GetAlgorithm(code);

        Assert.Equal(
            [typeof(EccSecretKey), typeof(XkpSecretKey)],
            algorithm.KeyTypes.Select(keyType => keyType.KeyType)
        );
        Assert.True(algorithm.IsKeyCompatible(CreateRecipientKey()));
    }

    [Fact]
    public async Task KeyTypes_WhenAgreementIsRegisteredBeforeTheCore_StillListsTheCoreOnesFirst()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEcdhKeyAgreement, XkpEcdhKeyAgreement>()
        );
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISecretKeyTypeDescriptor, XkpSecretKeyTypeDescriptor>()
        );
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();
        var collection = serviceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;

        Assert.True(collection.TryGetKeyManagementAlgorithm("ECDH-ES", out var algorithm));
        Assert.Equal(
            [typeof(EccSecretKey), typeof(XkpSecretKey)],
            algorithm.KeyTypes.Select(keyType => keyType.KeyType)
        );
    }

    #endregion

    #region NewKey Tests

    [Fact]
    public void NewKey_WhenContributedKeyType_WritesTheEpkThroughItsDescriptor()
    {
        var recipientKey = CreateRecipientKey();
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        GetAlgorithm("ECDH-ES").NewKey(recipientKey, header, new byte[16]);

        var epk = Assert.IsType<Dictionary<string, object>>(Assert.Contains("epk", header));
        Assert.Equal(["kty", XkpKeyType.PublicMember], epk.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(XkpKeyType.JwkKeyType.Name, epk["kty"]);
    }

    #endregion

    #region TryUnwrapKey Tests

    [Theory]
    [InlineData("ECDH-ES", "enc")]
    [InlineData("ECDH-ES+A128KW", "alg")]
    public void TryUnwrapKey_WhenContributedKeyType_AgreesOnTheSenderKey(string code, string field)
    {
        var algorithm = GetAlgorithm(code);
        var recipientKey = CreateRecipientKey();
        var header = new Dictionary<string, object> { [field] = code == "ECDH-ES" ? Enc : code };

        var cek = new byte[16];
        var encryptedCekWriter = new ArrayBufferWriter<byte>();
        algorithm.WrapNewKey(recipientKey, header, cek, encryptedCekWriter);

        var unwrapped = new byte[16];
        var result = algorithm.TryUnwrapKey(
            recipientKey,
            JsonSerializer.SerializeToElement(header),
            encryptedCekWriter.WrittenSpan,
            unwrapped,
            out var bytesWritten
        );

        Assert.True(result);
        Assert.Equal(16, bytesWritten);
        Assert.Equal(cek, unwrapped);
    }

    [Fact]
    public void TryUnwrapKey_WhenEpkCarriesContributedPrivateMember_Throws()
    {
        var recipientKey = CreateRecipientKey();
        var header = new Dictionary<string, object> { ["enc"] = Enc };
        var algorithm = GetAlgorithm("ECDH-ES");
        algorithm.NewKey(recipientKey, header, new byte[16]);

        var epk = new JsonObject
        {
            ["kty"] = XkpKeyType.JwkKeyType.Name,
            [XkpKeyType.PublicMember] = Assert.IsType<string>(
                ((Dictionary<string, object>)header["epk"])[XkpKeyType.PublicMember]
            ),
            [XkpKeyType.PrivateMember] = Base64Url.Encode(new byte[32]),
        };
        // Read without the contributed type's rules, 'sk' is not private: only those rules can refuse it.
        Assert.False(new JsonWebKey(epk.Deserialize<JsonElement>()).ContainsPrivateKeyMaterial);
        header["epk"] = epk;

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.TryUnwrapKey(
                recipientKey,
                JsonSerializer.SerializeToElement(header),
                [],
                new byte[16],
                out _
            )
        );

        Assert.Equal(
            "The 'epk' header must contain only public key parameters, but it contains 'sk'.",
            exception.Message
        );
    }

    [Fact]
    public void TryUnwrapKey_WhenEpkIsAnotherKeyType_Throws()
    {
        using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ephemeral.ExportParameters(includePrivateParameters: false);
        var header = new JsonObject
        {
            ["enc"] = Enc,
            ["epk"] = new JsonObject
            {
                ["kty"] = "EC",
                ["crv"] = "P-256",
                ["x"] = Base64Url.Encode(parameters.Q.X),
                ["y"] = Base64Url.Encode(parameters.Q.Y),
            },
        };

        var exception = Assert.Throws<JoseException>(() =>
            GetAlgorithm("ECDH-ES")
                .TryUnwrapKey(
                    CreateRecipientKey(),
                    header.Deserialize<JsonElement>(),
                    [],
                    new byte[16],
                    out _
                )
        );

        Assert.Equal("The 'kty' field was expected to be 'XKP'.", exception.Message);
    }

    #endregion

    #region JoseSerializer Tests

    [Fact]
    public void Decode_WhenJweIsEncryptedToContributedKeyType_ReturnsThePayload()
    {
        var recipientKey = CreateRecipientKey();
        Assert.True(AlgorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(Enc, out var enc));
        var options = new JoseEncryptionOptions(
            new JoseEncryptionCredentials(recipientKey, GetAlgorithm("ECDH-ES+A256KW"), enc)
        );
        var payload = new Dictionary<string, object> { ["sub"] = "alice" };

        var token = JoseSerializer.CreateEncoder(options).Encode(payload);
        var decoded = JoseSerializer.Decode(JoseSerializer.Parse(token), recipientKey);

        Assert.Equal(JsonSerializer.Serialize(payload), decoded);
    }

    #endregion
}
