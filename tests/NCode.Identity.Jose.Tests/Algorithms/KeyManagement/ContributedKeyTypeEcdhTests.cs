// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.KeyManagement;
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
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            new(typeof(XkpSecretKey), [new KeySizes(minSize: 256, maxSize: 256, skipSize: 0)]);

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

    // The XKP descriptor with a JSON Web Key form a faulty package could register: a JWK key type of its own, or an export
    // of its own; every other member is the real descriptor's.
    private sealed class FaultyXkpSecretKeyTypeDescriptor(
        ISecretKeyTypeDescriptor inner,
        JsonWebKeyTypeDescriptor jwkKeyType,
        Func<SecretKey, ExportedPublicKey?> exportPublicKey
    ) : ISecretKeyTypeDescriptor
    {
        public string KeyType => inner.KeyType;

        public Type KeyClrType => inner.KeyClrType;

        public JsonWebKeyTypeDescriptor JwkKeyType => jwkKeyType;

        public bool IsAsymmetric => inner.IsAsymmetric;

        public bool SupportsPublicKeyExport => inner.SupportsPublicKeyExport;

        public IReadOnlyCollection<string> PublicKeyImportFormats => inner.PublicKeyImportFormats;

        public string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
            inner.ValidateGeneration(parameters);

        public GeneratedSecretKeyMaterial GenerateKeyMaterial(
            SecretKeyGenerationParameters parameters,
            SecretKeyMaterialProtector protect
        ) => inner.GenerateKeyMaterial(parameters, protect);

        public ImportedSecretKeyMaterial ImportPublicKeyMaterial(
            SecretKeyImportParameters parameters,
            SecretKeyMaterialProtector protect
        ) => inner.ImportPublicKeyMaterial(parameters, protect);

        public SecretKey DeserializeKeyMaterial(
            KeyMetadata metadata,
            string secretEncoding,
            ReadOnlySpan<byte> keyMaterial
        ) => inner.DeserializeKeyMaterial(metadata, secretEncoding, keyMaterial);

        public ExportedPublicKey? ExportPublicKey(SecretKey secretKey) =>
            exportPublicKey(secretKey);
    }

    private ISecretKeyTypeDescriptor GetXkpDescriptor()
    {
        Assert.True(SecretKeyTypeCatalog.TryGet(XkpKeyType.SecretKeyTypeName, out var descriptor));
        return descriptor;
    }

    // The ECDH-ES algorithm over the XKP agreement, whose key type the specified descriptor describes.
    private EcdhKeyManagementAlgorithm CreateAlgorithm(ISecretKeyTypeDescriptor descriptor)
    {
        var mockCatalog = CreateStrictMock<ISecretKeyTypeCatalog>();
        ISecretKeyTypeDescriptor? registered = descriptor;
        mockCatalog
            .Setup(x => x.TryGet(XkpKeyType.SecretKeyTypeName, out registered))
            .Returns(true)
            .Verifiable();
        return new EcdhKeyManagementAlgorithm(mockCatalog.Object, [new XkpEcdhKeyAgreement()]);
    }

    // The real descriptor's export of the key, with the members of the JSON Web Key changed as specified.
    private ExportedPublicKey? ExportWith(SecretKey secretKey, Action<JsonObject> change)
    {
        var exported = GetXkpDescriptor().ExportPublicKey(secretKey);
        Assert.NotNull(exported);
        var members = Assert.IsType<JsonObject>(JsonNode.Parse(exported.Jwk.ToJsonString()));
        change(members);
        return new ExportedPublicKey
        {
            Jwk = new JsonWebKey(members, XkpKeyType.JwkKeyType),
            Pem = exported.Pem,
        };
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
            algorithm.KeyTypes.Select(keyType => keyType.KeyClrType)
        );
        Assert.True(algorithm.IsKeyCompatible(CreateRecipientKey()));
    }

    [Fact]
    public async Task KeyTypes_WhenAgreementIsRegisteredBeforeTheCore_ListsItsKeyTypeFirst()
    {
        var serviceCollection = new ServiceCollection();
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
            [typeof(XkpSecretKey), typeof(EccSecretKey)],
            algorithm.KeyTypes.Select(keyType => keyType.KeyClrType)
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

    // RFC 7518 §4.6.1.1: the ephemeral private key never leaves the sender, so a key type whose required members name a
    // private member cannot carry an epk.
    [Fact]
    public void NewKey_WhenTheKeyTypeRequiresAPrivateMember_Throws()
    {
        var inner = GetXkpDescriptor();
        var algorithm = CreateAlgorithm(
            new FaultyXkpSecretKeyTypeDescriptor(
                inner,
                new JsonWebKeyTypeDescriptor(
                    XkpKeyType.JwkKeyType.Name,
                    [XkpKeyType.PublicMember, XkpKeyType.PrivateMember],
                    [XkpKeyType.PrivateMember]
                ),
                inner.ExportPublicKey
            )
        );
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.NewKey(CreateRecipientKey(), header, new byte[16])
        );

        Assert.Equal(
            "The key type 'XKP' lists the private member 'sk' among its required members.",
            exception.Message
        );
        Assert.DoesNotContain("epk", header.Keys);
    }

    [Fact]
    public void NewKey_WhenTheExportedKeyLacksARequiredMember_Throws()
    {
        var inner = GetXkpDescriptor();
        var algorithm = CreateAlgorithm(
            new FaultyXkpSecretKeyTypeDescriptor(
                inner,
                new JsonWebKeyTypeDescriptor(
                    XkpKeyType.JwkKeyType.Name,
                    ["crv", XkpKeyType.PublicMember],
                    [XkpKeyType.PrivateMember]
                ),
                inner.ExportPublicKey
            )
        );
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.NewKey(CreateRecipientKey(), header, new byte[16])
        );

        Assert.Equal(
            "The ephemeral public key's JSON Web Key is missing the 'crv' member.",
            exception.Message
        );
        Assert.DoesNotContain("epk", header.Keys);
    }

    [Fact]
    public void NewKey_WhenTheKeyTypeCannotExportTheEphemeralKey_Throws()
    {
        var algorithm = CreateAlgorithm(
            new FaultyXkpSecretKeyTypeDescriptor(
                GetXkpDescriptor(),
                XkpKeyType.JwkKeyType,
                static _ => null
            )
        );
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        var exception = Assert.Throws<JoseException>(() =>
            algorithm.NewKey(CreateRecipientKey(), header, new byte[16])
        );

        Assert.Equal(
            "The ephemeral public key cannot be represented as a JSON Web Key.",
            exception.Message
        );
        Assert.DoesNotContain("epk", header.Keys);
    }

    // A required member that is not a string, which a contributed key type may have, is carried as its JSON value.
    [Fact]
    public void NewKey_WhenARequiredMemberIsNotAString_WritesItsJsonValue()
    {
        const string sizeMember = "len";
        var algorithm = CreateAlgorithm(
            new FaultyXkpSecretKeyTypeDescriptor(
                GetXkpDescriptor(),
                new JsonWebKeyTypeDescriptor(
                    XkpKeyType.JwkKeyType.Name,
                    [XkpKeyType.PublicMember, sizeMember],
                    [XkpKeyType.PrivateMember]
                ),
                secretKey => ExportWith(secretKey, members => members[sizeMember] = 256)
            )
        );
        var header = new Dictionary<string, object> { ["enc"] = Enc };

        algorithm.NewKey(CreateRecipientKey(), header, new byte[16]);

        var epk = Assert.IsType<Dictionary<string, object>>(Assert.Contains("epk", header));
        Assert.Equal(
            ["kty", sizeMember, XkpKeyType.PublicMember],
            epk.Keys.Order(StringComparer.Ordinal)
        );
        var size = Assert.IsType<JsonElement>(epk[sizeMember]);
        Assert.Equal(JsonValueKind.Number, size.ValueKind);
        Assert.Equal(256, size.GetInt32());
        Assert.IsType<string>(epk[XkpKeyType.PublicMember]);
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
