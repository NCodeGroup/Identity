#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jose.DPoP;
using NCode.Identity.Jwk;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Tests.DPoP;

/// <summary>
/// Composes the DPoP proof service with a key type the test contributes: a made-up <c>XKP</c> JWK key type whose
/// public member <c>pub</c> and private member <c>sk</c> no standard key type uses, so the proof's thumbprint and
/// private-material screening can come only from that type's own JWK rules.
/// </summary>
public class ContributedKeyTypeDPoPProofTests : BaseTests
{
    private const string SecretKeyTypeName = "xkp";
    private const string PublicMember = "pub";
    private const string PrivateMember = "sk";
    private const string HttpMethod = "POST";
    private const string HttpUri = "https://op.test/token";
    private const string AlgorithmCode = "XKP256";

    private static JsonWebKeyTypeDescriptor XkpJwkKeyType { get; } =
        new("XKP", [PublicMember], [PrivateMember]);

    private ServiceProvider ServiceProvider { get; }
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; }
    private IAlgorithmCollection AlgorithmCollection { get; }
    private IDPoPProofService ProofService { get; }
    private IJoseSerializer JoseSerializer { get; }

    public ContributedKeyTypeDPoPProofTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<ISecretKeyTypeDescriptor, XkpSecretKeyTypeDescriptor>()
        );
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<ICollectionDataSource<Algorithm>, XkpAlgorithmDataSource>()
        );
        ServiceProvider = serviceCollection.BuildServiceProvider();

        SecretKeyTypeCatalog = ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>();
        AlgorithmCollection = ServiceProvider
            .GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        ProofService = ServiceProvider.GetRequiredService<IDPoPProofService>();
        JoseSerializer = ServiceProvider.GetRequiredService<IJoseSerializer>();
    }

    protected override async ValueTask DisposeAsyncCore()
    {
        await ServiceProvider.DisposeAsync();
    }

    #region Scaffolding

    private XkpSecretKey CreateSigningKey()
    {
        Assert.True(
            SecretKeyTypeCatalog.TryGetByJwkKeyType(XkpJwkKeyType.Name, out var descriptor)
        );
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
            new KeyMetadata { KeyId = "dpop" },
            generated.SecretEncoding,
            keyMaterial
        );
        return Assert.IsType<XkpSecretKey>(secretKey);
    }

    private JoseSigningCredentials CreateCredentials(XkpSecretKey secretKey)
    {
        Assert.True(AlgorithmCollection.TryGetSignatureAlgorithm(AlgorithmCode, out var algorithm));
        return new JoseSigningCredentials(secretKey, algorithm);
    }

    private static DPoPProofValidationParameters CreateParameters() =>
        new()
        {
            HttpMethod = HttpMethod,
            HttpUri = HttpUri,
            AllowedAlgorithms = [AlgorithmCode],
            Lifetime = TimeSpan.FromSeconds(60),
        };

    private sealed class XkpSecretKey(
        KeyMetadata metadata,
        int keySizeBits,
        byte[] subjectPublicKeyInfo,
        byte[]? pkcs8PrivateKey
    ) : AsymmetricSecretKey(metadata, keySizeBits, certificateRawData: null)
    {
        public byte[] SubjectPublicKeyInfo { get; } = subjectPublicKeyInfo;

        public byte[]? Pkcs8PrivateKey { get; } = pkcs8PrivateKey;

        public override string KeyType => SecretKeyTypeName;

        public override void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)
        {
            var span = destination.GetSpan(SubjectPublicKeyInfo.Length);
            SubjectPublicKeyInfo.CopyTo(span);
            destination.Advance(SubjectPublicKeyInfo.Length);
        }
    }

    private sealed class XkpSecretKeyTypeDescriptor(ISecretKeyFactory secretKeyFactory)
        : AsymmetricSecretKeyTypeDescriptor(secretKeyFactory)
    {
        public override string KeyType => SecretKeyTypeName;

        public override Type KeyClrType => typeof(XkpSecretKey);

        public override JsonWebKeyTypeDescriptor JwkKeyType => XkpJwkKeyType;

        public override IReadOnlyCollection<string> PublicKeyImportFormats { get; } =
        [SecretKeyImportFormats.Jwk];

        public override string? ValidateGeneration(SecretKeyGenerationParameters parameters) =>
            RejectCurveIfPresent(parameters);

        public override GeneratedSecretKeyMaterial GenerateKeyMaterial(
            SecretKeyGenerationParameters parameters,
            SecretKeyMaterialProtector protect
        )
        {
            using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            return ExportProtectedPkcs8(ecdsa, protect);
        }

        protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ParseJwkPublicKey(
            JsonElement root
        ) => ReadSubjectPublicKeyInfo(GetRequiredBase64UrlMember(root, PublicMember));

        protected override (byte[] SubjectPublicKeyInfo, int KeySizeBits) ImportPublicKeyPem(
            string pem
        ) => throw new NotSupportedException();

        protected override bool TryGetCertificateKeySizeBits(
            X509Certificate2 certificate,
            out int keySizeBits
        ) => throw new NotSupportedException();

        protected override SecretKey CreatePrivateKeyFromPkcs8(
            KeyMetadata metadata,
            ReadOnlySpan<byte> pkcs8
        )
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(pkcs8, out _);
            return new XkpSecretKey(
                metadata,
                ecdsa.KeySize,
                ecdsa.ExportSubjectPublicKeyInfo(),
                pkcs8.ToArray()
            );
        }

        protected override SecretKey CreatePublicKeyFromSpki(
            KeyMetadata metadata,
            ReadOnlySpan<byte> subjectPublicKeyInfo
        )
        {
            var (spki, keySizeBits) = ReadSubjectPublicKeyInfo(subjectPublicKeyInfo.ToArray());
            return new XkpSecretKey(metadata, keySizeBits, spki, pkcs8PrivateKey: null);
        }

        protected override JsonObject? WritePublicKeyJwk(AsymmetricSecretKey secretKey) =>
            new()
            {
                [JsonWebKeyParameterNames.KeyType] = XkpJwkKeyType.Name,
                [PublicMember] = Base64Url.EncodeToString(
                    ((XkpSecretKey)secretKey).SubjectPublicKeyInfo
                ),
            };

        private static (byte[] SubjectPublicKeyInfo, int KeySizeBits) ReadSubjectPublicKeyInfo(
            byte[] subjectPublicKeyInfo
        )
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out _);
            return (subjectPublicKeyInfo, ecdsa.KeySize);
        }
    }

    private sealed class XkpSignatureAlgorithm : SignatureAlgorithm
    {
        public override string Code => AlgorithmCode;

        public override Type KeyType => typeof(XkpSecretKey);

        public override IEnumerable<KeySizes> KeyBitSizes { get; } =
        [new KeySizes(minSize: 256, maxSize: 256, skipSize: 0)];

        public override HashAlgorithmName HashAlgorithmName => HashAlgorithmName.SHA256;

        public override int GetSignatureSizeBytes(int keySizeBits) => 64;

        public override bool TrySign(
            SecretKey secretKey,
            ReadOnlySpan<byte> inputData,
            Span<byte> signature,
            out int bytesWritten
        )
        {
            var privateKey =
                ((XkpSecretKey)secretKey).Pkcs8PrivateKey
                ?? throw new InvalidOperationException("The key holds only its public half.");
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportPkcs8PrivateKey(privateKey, out _);
            return ecdsa.TrySignData(
                inputData,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation,
                out bytesWritten
            );
        }

        public override bool Verify(
            SecretKey secretKey,
            ReadOnlySpan<byte> inputData,
            ReadOnlySpan<byte> signature
        )
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(((XkpSecretKey)secretKey).SubjectPublicKeyInfo, out _);
            return ecdsa.VerifyData(
                inputData,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation
            );
        }
    }

    private sealed class XkpAlgorithmDataSource : ICollectionDataSource<Algorithm>
    {
        public IEnumerable<Algorithm> Collection { get; } = [new XkpSignatureAlgorithm()];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }

    #endregion

    #region CreateProof Tests

    [Fact]
    public void CreateProof_WhenContributedKeyType_ValidatesWithKeyThumbprint()
    {
        var secretKey = CreateSigningKey();
        Assert.True(SecretKeyTypeCatalog.TryGet(secretKey.KeyType, out var descriptor));
        var exported = descriptor.ExportPublicKey(secretKey);
        Assert.NotNull(exported);
        var proof = ProofService.CreateProof(
            new DPoPProofCreationOptions
            {
                SigningCredentials = CreateCredentials(secretKey),
                HttpMethod = HttpMethod,
                HttpUri = HttpUri,
            }
        );

        var valid = ProofService.TryValidateProof(
            proof,
            CreateParameters(),
            out var result,
            out var error
        );

        Assert.True(valid, error);
        Assert.NotNull(result);
        Assert.Equal(AlgorithmCode, result.Algorithm);
        Assert.Equal(XkpJwkKeyType.Name, result.PublicKey.KeyType);
        Assert.Same(XkpJwkKeyType, result.PublicKey.KeyTypeDescriptor);
        Assert.Equal(exported.Jwk.ComputeThumbprint(), result.JwkThumbprint);
    }

    #endregion

    #region TryValidateProof Tests

    [Fact]
    public void TryValidateProof_WhenJwkCarriesContributedPrivateMember_Fails()
    {
        var secretKey = CreateSigningKey();
        Assert.NotNull(secretKey.Pkcs8PrivateKey);
        var jwk = new JsonWebKey(
            new JsonObject
            {
                [JsonWebKeyParameterNames.KeyType] = XkpJwkKeyType.Name,
                [PublicMember] = Base64Url.EncodeToString(secretKey.SubjectPublicKeyInfo),
                [PrivateMember] = Base64Url.EncodeToString(secretKey.Pkcs8PrivateKey),
            }
        );
        // Read without the contributed type's rules, 'sk' is not private: only those rules can refuse it.
        Assert.False(jwk.ContainsPrivateKeyMaterial);
        var signature = new JoseSignatureOptions(CreateCredentials(secretKey))
        {
            TokenType = JoseTokenTypes.DPoPJwt,
            AddKeyIdHeader = false,
            ProtectedHeaders =
            [
                new KeyValuePair<string, object>(JoseClaimNames.Header.Jwk, jwk.AsElement()),
            ],
        };
        var payload = new Dictionary<string, object>
        {
            [JoseClaimNames.Payload.Htm] = HttpMethod,
            [JoseClaimNames.Payload.Htu] = HttpUri,
            [JoseClaimNames.Payload.Jti] = "jti-1",
            [JoseClaimNames.Payload.Iat] = TimeProvider.System.GetUtcNow().ToUnixTimeSeconds(),
        };
        var proof = JoseSerializer.CreateEncoder(new JoseSigningOptions(signature)).Encode(payload);

        var valid = ProofService.TryValidateProof(
            proof,
            CreateParameters(),
            out var result,
            out var error
        );

        Assert.False(valid);
        Assert.Null(result);
        Assert.Contains("private key material", error, StringComparison.Ordinal);
    }

    #endregion
}
