// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa;
using NCode.Identity.Jose.EdDsa.Keys;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class DefaultTenantKeyPolicyTests : BaseTests, IAsyncLifetime
{
    private static TenantKeySpecification Rs256 { get; } =
        new()
        {
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
        };

    private static TenantKeySpecification RemoteRs256 { get; } =
        Rs256 with
        {
            RemoteKeyProvider = "aws-kms",
        };

    private const string BrainpoolAlgorithm = "X-ES256-BP";

    private static EccCurveSpecification Brainpool256 { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
            Domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.brainpoolP256r1),
        };

    private static ConcurrentDictionary<
        (string SecretType, int KeySizeBits),
        string
    > PublicKeys { get; } = new();

    private ServiceProvider ServiceProvider { get; }

    public DefaultTenantKeyPolicyTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddJoseLibrary();
        secrets.AddPersistenceLibrary();
        // A second 256-bit curve and an ECDSA algorithm bound to it, as the opt-in secp256k1 package contributes ES256K.
        serviceCollection.AddSingleton(Brainpool256);
        serviceCollection.AddSingleton(
            new EccSignatureAlgorithmSpecification
            {
                Code = BrainpoolAlgorithm,
                Curve = Brainpool256,
                HashAlgorithmName = HashAlgorithmName.SHA256,
            }
        );
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ICollectionDataSource<Algorithm>,
                UntypedKeyAlgorithmDataSource
            >()
        );
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEcdhKeyAgreement, RsaKeyTypeAgreementStub>()
        );
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Policy = CreatePolicy(
            ServiceProvider,
            ServiceProvider.GetRequiredService<IRemoteKeyDescriptorCatalog>()
        );
    }

    private DefaultTenantKeyPolicy Policy { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await ServiceProvider.DisposeAsync();

    // Gives the ECDH-ES algorithms a second key type, RSA keys of 2048 to 4096 bits, so they accept several key types as
    // they do with a package that contributes OKP keys (ADR-1389); provisioning never performs an agreement.
    private sealed class RsaKeyTypeAgreementStub : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            new(typeof(RsaSecretKey), [new KeySizes(minSize: 2048, maxSize: 4096, skipSize: 1024)]);

        public int GetSharedSecretSizeBytes(SecretKey secretKey) =>
            throw new NotSupportedException();

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    // A registered signature algorithm keyed by the abstract SecretKey, which no secret key type describes.
    private const string UntypedKeyAlgorithm = "X-UNTYPED";

    // A registered signature algorithm keyed by OKP keys, whose secret key type the EdDSA package registers and this
    // composition does not, so no registered secret key type is one it accepts.
    private const string OkpKeyAlgorithm = "X-OKP";

    // A registered signature algorithm keyed by RSA keys of one size, and one keyed by RSA keys of a size or more.
    private const string ExactSizeKeyAlgorithm = "X-RSA-3072";
    private const string OpenEndedSizeKeyAlgorithm = "X-RSA-2048-UP";

    // A signature algorithm that accepts keys of one runtime type, of the given sizes or of no particular size.
    private sealed class StubKeySignatureAlgorithm(
        string code,
        Type keyClrType,
        params KeySizes[] keyBitSizes
    ) : SignatureAlgorithm
    {
        public override string Code => code;

        public override IReadOnlyList<AlgorithmKeyType> KeyTypes { get; } =
        [new(keyClrType, keyBitSizes)];

        public override HashAlgorithmName HashAlgorithmName => HashAlgorithmName.SHA256;

        public override int GetSignatureSizeBytes(int keySizeBits) => 0;

        public override bool TrySign(
            SecretKey secretKey,
            ReadOnlySpan<byte> inputData,
            Span<byte> signature,
            out int bytesWritten
        ) => throw new NotSupportedException();

        public override bool Verify(
            SecretKey secretKey,
            ReadOnlySpan<byte> inputData,
            ReadOnlySpan<byte> signature
        ) => throw new NotSupportedException();
    }

    // Gives the ECDH-ES algorithms an EC curve that the elliptic-curve catalog does not register; provisioning never
    // performs an agreement.
    private sealed class UnregisteredCurveAgreementStub : IEcdhKeyAgreement
    {
        public const string CurveName = "brainpoolP256r1";

        public AlgorithmKeyType AlgorithmKeyType { get; } =
            AlgorithmKeyType.OnCurves(typeof(EccSecretKey), [new AlgorithmCurve(CurveName, 256)]);

        public int GetSharedSecretSizeBytes(SecretKey secretKey) =>
            throw new NotSupportedException();

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    private sealed class UntypedKeyAlgorithmDataSource : ICollectionDataSource<Algorithm>
    {
        public IEnumerable<Algorithm> Collection { get; } =
        [
            new StubKeySignatureAlgorithm(UntypedKeyAlgorithm, typeof(SecretKey)),
            new StubKeySignatureAlgorithm(OkpKeyAlgorithm, typeof(OkpSecretKey)),
            new StubKeySignatureAlgorithm(
                ExactSizeKeyAlgorithm,
                typeof(RsaSecretKey),
                new KeySizes(minSize: 3072, maxSize: 3072, skipSize: 0)
            ),
            new StubKeySignatureAlgorithm(
                OpenEndedSizeKeyAlgorithm,
                typeof(RsaSecretKey),
                new KeySizes(minSize: 2048, maxSize: int.MaxValue, skipSize: 8)
            ),
        ];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }

    #region ResolveSpecifications Tests

    [Fact]
    public void ResolveSpecifications_WhenNoKeysConfigured_ResolvesTheDefaultSet()
    {
        var resolution = Policy.ResolveSpecifications(new OpenIdTenantKeyProvisioningOptions());

        Assert.Empty(resolution.Errors);
        Assert.Collection(
            resolution.Specifications,
            specification =>
                Assert.Equal(
                    CreateSpecification(
                        AlgorithmCodes.DigitalSignature.RsaSha256,
                        SecretKeyUses.Signature,
                        SecretKeyTypes.Rsa,
                        3072
                    ),
                    specification
                ),
            specification =>
                Assert.Equal(
                    CreateSpecification(
                        AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
                        SecretKeyUses.Signature,
                        SecretKeyTypes.Rsa,
                        3072
                    ),
                    specification
                ),
            specification =>
                Assert.Equal(
                    CreateSpecification(
                        AlgorithmCodes.DigitalSignature.EcdsaSha256,
                        SecretKeyUses.Signature,
                        SecretKeyTypes.Ecc,
                        256,
                        "P-256"
                    ),
                    specification
                )
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenDisabled_ResolvesNothing()
    {
        var resolution = Policy.ResolveSpecifications(
            new OpenIdTenantKeyProvisioningOptions { Enabled = false }
        );

        Assert.Empty(resolution.Specifications);
        Assert.Empty(resolution.Errors);
    }

    [Fact]
    public void ResolveSpecifications_WhenKeysConfigured_ReplacesTheDefaultSet()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "ES384" })
        );

        Assert.Empty(resolution.Errors);
        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal(
            CreateSpecification("ES384", SecretKeyUses.Signature, SecretKeyTypes.Ecc, 384, "P-384"),
            specification
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenKeySizeUnset_UsesTheSmallestSizeTheAlgorithmAccepts()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "RS256" })
        );

        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal(2048, specification.KeySizeBits);
    }

    [Fact]
    public void ResolveSpecifications_WhenKeyManagementAlgorithm_ResolvesAnEncryptionKey()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.KeyManagement.RsaOaep256,
                    KeySizeBits = 3072,
                }
            )
        );

        Assert.Empty(resolution.Errors);
        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.RsaOaep256,
                SecretKeyUses.Encryption,
                SecretKeyTypes.Rsa,
                3072
            ),
            specification
        );
    }

    [Theory]
    [InlineData("XX999", "is not a registered signature or key-management algorithm")]
    [InlineData("HS256", "uses a symmetric key")]
    [InlineData(UntypedKeyAlgorithm, "uses a key type that has no registered secret key type")]
    [InlineData("", "does not name an algorithm")]
    public void ResolveSpecifications_WhenAlgorithmCannotBeProvisioned_ReportsIt(
        string algorithm,
        string expectedReason
    )
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = "ES256" },
                new OpenIdTenantKeyOptions { Algorithm = algorithm }
            )
        );

        var error = Assert.Single(resolution.Errors);
        Assert.Contains(expectedReason, error, StringComparison.Ordinal);
        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal("ES256", specification.Algorithm);
    }

    [Fact]
    public void ResolveSpecifications_WhenAlgorithmListedTwice_ReportsIt()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = "ES256" },
                new OpenIdTenantKeyOptions { Algorithm = "ES256" }
            )
        );

        var error = Assert.Single(resolution.Errors);
        Assert.Contains("'ES256' is listed more than once", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSpecifications_WhenKeySizeNotAccepted_ReportsIt()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "RS256", KeySizeBits = 1024 })
        );

        Assert.Empty(resolution.Specifications);
        var error = Assert.Single(resolution.Errors);
        Assert.Contains("'1024' bits is not valid", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSpecifications_WhenAlgorithmAcceptsSeveralKeyTypes_GeneratesTheFirst()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = AlgorithmCodes.KeyManagement.EcdhEs }
            )
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.EcdhEs,
                SecretKeyUses.Encryption,
                SecretKeyTypes.Ecc,
                256,
                "P-256"
            ),
            Assert.Single(resolution.Specifications)
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenSecretTypeSelectsAnotherKeyTypeOfTheAlgorithm_GeneratesIt()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                    SecretType = SecretKeyTypes.Rsa,
                }
            )
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.EcdhEs,
                SecretKeyUses.Encryption,
                SecretKeyTypes.Rsa,
                2048
            ),
            Assert.Single(resolution.Specifications)
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenSecretTypeIsNotOneTheAlgorithmAccepts_ListsTheAcceptedTypes()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = "RS256", SecretType = SecretKeyTypes.Ecc }
            )
        );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(
            "The algorithm 'RS256' does not accept 'ecc' keys; expected 'rsa'.",
            Assert.Single(resolution.Errors)
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenSecretTypeIsNotRegistered_ListsTheRegisteredTypes()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = "RS256", SecretType = "unregistered" }
            )
        );

        Assert.Empty(resolution.Specifications);
        var error = Assert.Single(resolution.Errors);
        Assert.StartsWith(
            "The secret type 'unregistered' is not a registered secret key type; expected ",
            error,
            StringComparison.Ordinal
        );
        Assert.Contains($"'{SecretKeyTypes.Rsa}'", error, StringComparison.Ordinal);
        Assert.Contains($"'{SecretKeyTypes.Ecc}'", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveSpecifications_WhenNoRegisteredSecretTypeIsOneTheAlgorithmAccepts_ListsNone()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = OkpKeyAlgorithm,
                    SecretType = SecretKeyTypes.Rsa,
                }
            )
        );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(
            $"The algorithm '{OkpKeyAlgorithm}' does not accept 'rsa' keys; expected none.",
            Assert.Single(resolution.Errors)
        );
    }

    // The algorithm accepts a curve the elliptic-curve catalog does not register, so the algorithm resolves the key and
    // its key type cannot generate it.
    [Fact]
    public async Task ResolveSpecifications_WhenTheKeyTypeCannotGenerateTheResolvedKey_ReportsIt()
    {
        var serviceCollection = new ServiceCollection();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddJoseLibrary();
        secrets.AddPersistenceLibrary();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEcdhKeyAgreement, UnregisteredCurveAgreementStub>()
        );
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions
                    {
                        Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                        SecretType = SecretKeyTypes.Ecc,
                        Curve = UnregisteredCurveAgreementStub.CurveName,
                    }
                )
            );

        Assert.Empty(resolution.Specifications);
        var error = Assert.Single(resolution.Errors);
        Assert.StartsWith(
            "A key for the algorithm 'ECDH-ES' cannot be generated: ",
            error,
            StringComparison.Ordinal
        );
        Assert.Contains(
            $"'{UnregisteredCurveAgreementStub.CurveName}'",
            error,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenAlgorithmIsBoundToACurve_GeneratesTheKeyOnThatCurve()
    {
        // The contributed curve shares P-256's size, so only the curve the algorithm names, carried into the
        // specification, generates a key the algorithm accepts.
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = BrainpoolAlgorithm })
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(
                BrainpoolAlgorithm,
                SecretKeyUses.Signature,
                SecretKeyTypes.Ecc,
                256,
                "brainpoolP256r1"
            ),
            Assert.Single(resolution.Specifications)
        );
    }

    [Theory]
    [InlineData("Ed25519", 256)]
    [InlineData("Ed448", 456)]
    public async Task ResolveSpecifications_WhenOkpAlgorithmIsFullySpecified_GeneratesTheKeyOnItsCurve(
        string algorithm,
        int keySizeBits
    )
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();
        var policy = CreatePolicy(serviceProvider);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = algorithm })
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(algorithm, SecretKeyUses.Signature, "okp", keySizeBits, algorithm),
            Assert.Single(resolution.Specifications)
        );
    }

    [Fact]
    public async Task ResolveSpecifications_WhenPolymorphicEdDsa_GeneratesTheKeyOnItsFirstCurve()
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();
        var policy = CreatePolicy(serviceProvider);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "EdDSA" })
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification("EdDSA", SecretKeyUses.Signature, "okp", 256, "Ed25519"),
            Assert.Single(resolution.Specifications)
        );
    }

    // See ADR-1390: X25519 and X448 keys have distinct sizes, so the size selects the curve.
    [Theory]
    [InlineData(null, 256, "X25519")]
    [InlineData(448, 448, "X448")]
    public async Task ResolveSpecifications_WhenOkpSecretTypeIsSelectedForEcdhEs_GeneratesTheKeyOnTheCurveOfItsSize(
        int? configuredKeySizeBits,
        int keySizeBits,
        string curve
    )
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();
        var policy = CreatePolicy(serviceProvider);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                    SecretType = "okp",
                    KeySizeBits = configuredKeySizeBits,
                }
            )
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.EcdhEs,
                SecretKeyUses.Encryption,
                "okp",
                keySizeBits,
                curve
            ),
            Assert.Single(resolution.Specifications)
        );
    }

    // A configured curve selects among the algorithm's curves and gives the key its size.
    [Theory]
    [InlineData(AlgorithmCodes.DigitalSignature.EdDsa, null, "Ed448", 456)]
    [InlineData(AlgorithmCodes.KeyManagement.EcdhEs, "okp", "X448", 448)]
    [InlineData(AlgorithmCodes.KeyManagement.EcdhEs, null, "P-384", 384)]
    public async Task ResolveSpecifications_WhenCurveIsConfigured_GeneratesTheKeyOnIt(
        string algorithm,
        string? secretType,
        string curve,
        int keySizeBits
    )
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();
        var policy = CreatePolicy(serviceProvider);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = algorithm,
                    SecretType = secretType,
                    Curve = curve,
                }
            )
        );

        Assert.Empty(resolution.Errors);
        var specification = Assert.Single(resolution.Specifications);
        Assert.Equal(curve, specification.Curve);
        Assert.Equal(keySizeBits, specification.KeySizeBits);
    }

    [Theory]
    [InlineData("Ed25519", null)]
    [InlineData("X448", 256)]
    public async Task ResolveSpecifications_WhenCurveIsNotOneTheAlgorithmAcceptsWithTheSize_ReportsIt(
        string curve,
        int? keySizeBits
    )
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();
        var policy = CreatePolicy(serviceProvider);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                    SecretType = "okp",
                    Curve = curve,
                    KeySizeBits = keySizeBits,
                }
            )
        );

        Assert.Empty(resolution.Specifications);
        Assert.Contains($"'{curve}'", Assert.Single(resolution.Errors), StringComparison.Ordinal);
    }

    // The JOSE library registers its EC agreement before any package adds its own, so EC keys are ECDH-ES's first key
    // type.
    [Fact]
    public async Task ResolveSpecifications_WhenEcdhEsNamesNoSecretType_GeneratesAnEcKey()
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();
        var policy = CreatePolicy(serviceProvider);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = AlgorithmCodes.KeyManagement.EcdhEs }
            )
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.EcdhEs,
                SecretKeyUses.Encryption,
                SecretKeyTypes.Ecc,
                256,
                "P-256"
            ),
            Assert.Single(resolution.Specifications)
        );
    }

    // Gives the ECDH-ES algorithms a Brainpool curve beside the core's NIST curves, as a package that contributes an
    // agreement over it does, so two of ECDH-ES's EC curves share the 256-bit size; provisioning never performs an
    // agreement.
    private sealed class BrainpoolAgreementStub : IEcdhKeyAgreement
    {
        public AlgorithmKeyType AlgorithmKeyType { get; } =
            AlgorithmKeyType.OnCurves(
                typeof(EccSecretKey),
                [new AlgorithmCurve(Brainpool256.CurveName, Brainpool256.CurveSizeBits)]
            );

        public int GetSharedSecretSizeBytes(SecretKey secretKey) =>
            throw new NotSupportedException();

        public void AgreeWithEphemeralKey(
            SecretKey recipientKey,
            IBufferWriter<byte> ephemeralPublicKeyWriter,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();

        public void Agree(
            SecretKey recipientKey,
            SecretKey ephemeralPublicKey,
            Span<byte> sharedSecret
        ) => throw new NotSupportedException();
    }

    // ECDH-ES over the core's NIST curves and a Brainpool curve, and over the OKP curves of the EdDSA package.
    private static ServiceProvider BuildBrainpoolEcdhServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddJoseLibrary().AddEdDsa();
        secrets.AddPersistenceLibrary();
        serviceCollection.AddSingleton(Brainpool256);
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IEcdhKeyAgreement, BrainpoolAgreementStub>()
        );
        return serviceCollection.BuildServiceProvider();
    }

    // See ADR-1398: P-256 and brainpoolP256r1 are both 256 bits, so the size selects neither, and the EC key type,
    // which accepts the size, reports it rather than yielding to the OKP key type, whose X25519 curve has it too.
    [Theory]
    [InlineData(null)]
    [InlineData(SecretKeyTypes.Ecc)]
    public async Task ResolveSpecifications_WhenTwoCurvesHaveTheConfiguredSize_ReportsIt(
        string? secretType
    )
    {
        await using var serviceProvider = BuildBrainpoolEcdhServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions
                    {
                        Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                        SecretType = secretType,
                        KeySizeBits = 256,
                    }
                )
            );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(
            "The key size '256' bits does not select a curve for algorithm 'ECDH-ES', which accepts 'P-256', "
                + "'brainpoolP256r1' at that size; name the curve.",
            Assert.Single(resolution.Errors)
        );
    }

    [Theory]
    [InlineData("brainpoolP256r1", null)]
    [InlineData("brainpoolP256r1", 256)]
    [InlineData("P-256", 256)]
    public async Task ResolveSpecifications_WhenTheCurveSelectsAmongCurvesOfOneSize_GeneratesTheKeyOnIt(
        string curve,
        int? keySizeBits
    )
    {
        await using var serviceProvider = BuildBrainpoolEcdhServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions
                    {
                        Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                        Curve = curve,
                        KeySizeBits = keySizeBits,
                    }
                )
            );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.EcdhEs,
                SecretKeyUses.Encryption,
                SecretKeyTypes.Ecc,
                256,
                curve
            ),
            Assert.Single(resolution.Specifications)
        );
    }

    // The algorithm's first curve is its preference when nothing selects one.
    [Fact]
    public async Task ResolveSpecifications_WhenNeitherCurveNorSizeIsConfigured_GeneratesTheKeyOnTheFirstCurve()
    {
        await using var serviceProvider = BuildBrainpoolEcdhServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions { Algorithm = AlgorithmCodes.KeyManagement.EcdhEs }
                )
            );

        Assert.Empty(resolution.Errors);
        Assert.Equal("P-256", Assert.Single(resolution.Specifications).Curve);
    }

    // See ADR-1398: a curve fixes the size of its keys, which the key type's descriptor checks.
    [Theory]
    [InlineData("P-256", 384)]
    [InlineData("brainpoolP256r1", 521)]
    public async Task ResolveSpecifications_WhenTheSizeIsNotTheConfiguredCurves_ReportsIt(
        string curve,
        int keySizeBits
    )
    {
        await using var serviceProvider = BuildBrainpoolEcdhServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions
                    {
                        Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                        Curve = curve,
                        KeySizeBits = keySizeBits,
                    }
                )
            );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(
            $"A key for the algorithm 'ECDH-ES' cannot be generated: The ECC key size '{keySizeBits}' bits is invalid "
                + $"for the curve '{curve}', whose keys are 256 bits.",
            Assert.Single(resolution.Errors)
        );
    }

    // See ADR-1398: a configuration no key type accepts is reported with what every key type of the algorithm accepts,
    // so the OKP curves are listed beside the EC curves and not left out.
    [Fact]
    public async Task ResolveSpecifications_WhenNoKeyTypeAcceptsTheConfiguredCurve_ListsEveryKeyTypesCurves()
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions
                    {
                        Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                        Curve = "X25519x",
                    }
                )
            );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(
            "The curve 'X25519x' is not valid for algorithm 'ECDH-ES'; expected 'P-256' (256 bits), 'P-384' "
                + "(384 bits), 'P-521' (521 bits) for 'ecc' keys; 'X25519' (256 bits), 'X448' (448 bits) for 'okp' "
                + "keys.",
            Assert.Single(resolution.Errors)
        );
    }

    [Fact]
    public async Task ResolveSpecifications_WhenNoKeyTypeAcceptsTheConfiguredSize_ListsEveryKeyTypesCurves()
    {
        await using var serviceProvider = BuildEdDsaServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions
                    {
                        Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                        KeySizeBits = 512,
                    }
                )
            );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(
            "The key size '512' bits is not valid for algorithm 'ECDH-ES'; expected 'P-256' (256 bits), 'P-384' "
                + "(384 bits), 'P-521' (521 bits) for 'ecc' keys; 'X25519' (256 bits), 'X448' (448 bits) for 'okp' "
                + "keys.",
            Assert.Single(resolution.Errors)
        );
    }

    // A key type without curves is listed with its sizes, or with no curve when a curve is configured.
    [Theory]
    [InlineData(
        null,
        1024,
        "The key size '1024' bits is not valid for algorithm 'ECDH-ES'; expected 'P-256' (256 bits), 'P-384' "
            + "(384 bits), 'P-521' (521 bits) for 'ecc' keys; 2048 to 4096 bits in steps of 1024 for 'rsa' keys."
    )]
    [InlineData(
        "P-999",
        null,
        "The curve 'P-999' is not valid for algorithm 'ECDH-ES'; expected 'P-256' (256 bits), 'P-384' (384 bits), "
            + "'P-521' (521 bits) for 'ecc' keys; no curve for 'rsa' keys."
    )]
    [InlineData(
        "P-999",
        256,
        "The curve 'P-999' is not valid for algorithm 'ECDH-ES'; expected 'P-256' (256 bits), 'P-384' (384 bits), "
            + "'P-521' (521 bits) for 'ecc' keys; no curve for 'rsa' keys."
    )]
    public void ResolveSpecifications_WhenNoKeyTypeAcceptsTheConfiguredKey_ListsEveryKeyTypesSizesOrCurves(
        string? curve,
        int? keySizeBits,
        string expected
    )
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                    Curve = curve,
                    KeySizeBits = keySizeBits,
                }
            )
        );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(expected, Assert.Single(resolution.Errors));
    }

    [Fact]
    public void ResolveSpecifications_WhenTheOnlyKeyTypeRefusesTheSize_ListsItsSizes()
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "RS256", KeySizeBits = 1024 })
        );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(
            "The key size '1024' bits is not valid for algorithm 'RS256'; expected 2048 to 16384 bits in steps of 64 "
                + "for 'rsa' keys.",
            Assert.Single(resolution.Errors)
        );
    }

    // A key type of one size is listed with that size, and one of a size or more with its smallest.
    [Theory]
    [InlineData(
        ExactSizeKeyAlgorithm,
        "The key size '1024' bits is not valid for algorithm 'X-RSA-3072'; expected 3072 bits for 'rsa' keys."
    )]
    [InlineData(
        OpenEndedSizeKeyAlgorithm,
        "The key size '1024' bits is not valid for algorithm 'X-RSA-2048-UP'; expected 2048 bits or more for 'rsa' "
            + "keys."
    )]
    public void ResolveSpecifications_WhenTheOnlyKeyTypeRefusesTheSize_ListsItsExactOrOpenEndedSize(
        string algorithm,
        string expected
    )
    {
        var resolution = Policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = algorithm, KeySizeBits = 1024 })
        );

        Assert.Empty(resolution.Specifications);
        Assert.Equal(expected, Assert.Single(resolution.Errors));
    }

    // The EC key type has no 448-bit curve, so it yields to the OKP key type, which has.
    [Fact]
    public async Task ResolveSpecifications_WhenOnlyALaterKeyTypeAcceptsTheSize_GeneratesThatKeyType()
    {
        await using var serviceProvider = BuildBrainpoolEcdhServiceProvider();

        var resolution = CreatePolicy(serviceProvider)
            .ResolveSpecifications(
                CreateOptions(
                    new OpenIdTenantKeyOptions
                    {
                        Algorithm = AlgorithmCodes.KeyManagement.EcdhEs,
                        KeySizeBits = 448,
                    }
                )
            );

        Assert.Empty(resolution.Errors);
        Assert.Equal(
            CreateSpecification(
                AlgorithmCodes.KeyManagement.EcdhEs,
                SecretKeyUses.Encryption,
                "okp",
                448,
                "X448"
            ),
            Assert.Single(resolution.Specifications)
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenAProviderHasNoRowForAProvisionedSignatureKey_ReportsIt()
    {
        var catalog = new StubRemoteKeyDescriptorCatalog(
            ["aws-kms"],
            [
                AlgorithmCodes.DigitalSignature.RsaSha256,
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
            ]
        );
        var policy = CreatePolicy(ServiceProvider, catalog);

        var resolution = policy.ResolveSpecifications(new OpenIdTenantKeyProvisioningOptions());

        Assert.Equal(3, resolution.Specifications.Count);
        var error = Assert.Single(resolution.Errors);
        Assert.Equal(
            "The remote key provider 'aws-kms' cannot hold the provisioned 'ES256' key (ecc, 256 bits, curve 'P-256').",
            error
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenAProviderHasNoRowForAKeyWithoutACurve_OmitsTheCurve()
    {
        var catalog = new StubRemoteKeyDescriptorCatalog(["aws-kms"], []);
        var policy = CreatePolicy(ServiceProvider, catalog);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "RS256", KeySizeBits = 3072 })
        );

        var error = Assert.Single(resolution.Errors);
        Assert.Equal(
            "The remote key provider 'aws-kms' cannot hold the provisioned 'RS256' key (rsa, 3072 bits).",
            error
        );
    }

    [Fact]
    public void ResolveSpecifications_WhenEveryProviderHasARowForEverySignatureKey_ReportsNothing()
    {
        var catalog = new StubRemoteKeyDescriptorCatalog(
            ["aws-kms", "azure-key-vault"],
            [
                AlgorithmCodes.DigitalSignature.RsaSha256,
                AlgorithmCodes.DigitalSignature.RsaSsaPssSha256,
                AlgorithmCodes.DigitalSignature.EcdsaSha256,
            ]
        );
        var policy = CreatePolicy(ServiceProvider, catalog);

        var resolution = policy.ResolveSpecifications(new OpenIdTenantKeyProvisioningOptions());

        Assert.Empty(resolution.Errors);
    }

    [Fact]
    public void ResolveSpecifications_WhenEachOfTwoProvidersLacksARow_ReportsEachInProviderOrder()
    {
        var catalog = new StubRemoteKeyDescriptorCatalog(["b-kms", "a-kms"], []);
        var policy = CreatePolicy(ServiceProvider, catalog);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(new OpenIdTenantKeyOptions { Algorithm = "RS256", KeySizeBits = 3072 })
        );

        Assert.Collection(
            resolution.Errors,
            error => Assert.Contains("'a-kms'", error),
            error => Assert.Contains("'b-kms'", error)
        );
    }

    [Fact]
    public void ResolveSpecifications_ProviderCannotHoldAProvisionedEncryptionKey_ReportsIt()
    {
        var catalog = new StubRemoteKeyDescriptorCatalog(
            ["aws-kms"],
            [AlgorithmCodes.DigitalSignature.RsaSha256]
        );
        var policy = CreatePolicy(ServiceProvider, catalog);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.DigitalSignature.RsaSha256,
                    KeySizeBits = 3072,
                },
                new OpenIdTenantKeyOptions { Algorithm = AlgorithmCodes.KeyManagement.EcdhEs }
            )
        );

        Assert.Equal(2, resolution.Specifications.Count);
        var error = Assert.Single(resolution.Errors);
        Assert.Equal(
            "The remote key provider 'aws-kms' cannot hold the provisioned 'ECDH-ES' key (ecc, 256 bits, curve 'P-256').",
            error
        );
    }

    [Fact]
    public void ResolveSpecifications_ProviderRowHoldsAnotherUse_ReportsIt()
    {
        var catalog = new StubRemoteKeyDescriptorCatalog(
            ["aws-kms"],
            [AlgorithmCodes.KeyManagement.EcdhEs]
        );
        var policy = CreatePolicy(ServiceProvider, catalog);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions { Algorithm = AlgorithmCodes.KeyManagement.EcdhEs }
            )
        );

        var error = Assert.Single(resolution.Errors);
        Assert.Equal(
            "The remote key provider 'aws-kms' holds 'ECDH-ES' keys for the use 'sig', not for the provisioned key's use 'enc' (ecc, 256 bits, curve 'P-256').",
            error
        );
    }

    [Fact]
    public void ResolveSpecifications_ProviderHoldsEveryProvisionedKey_ReportsNothing()
    {
        var catalog = new StubRemoteKeyDescriptorCatalog(
            ["aws-kms"],
            [AlgorithmCodes.DigitalSignature.RsaSha256, AlgorithmCodes.KeyManagement.RsaOaep256]
        );
        var policy = CreatePolicy(ServiceProvider, catalog);

        var resolution = policy.ResolveSpecifications(
            CreateOptions(
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.DigitalSignature.RsaSha256,
                    KeySizeBits = 3072,
                },
                new OpenIdTenantKeyOptions
                {
                    Algorithm = AlgorithmCodes.KeyManagement.RsaOaep256,
                    KeySizeBits = 3072,
                }
            )
        );

        Assert.Empty(resolution.Errors);
        Assert.Equal(2, resolution.Specifications.Count);
    }

    #endregion

    #region ForTenant Tests

    [Fact]
    public void ForTenant_WhenTheSettingNamesAProvider_SetsItOnSignatureSpecifications()
    {
        var mockSettings = CreateSettings("aws-kms");

        var result = Policy.ForTenant([Rs256], mockSettings.Object);

        var specification = Assert.Single(result);
        Assert.Equal(Rs256 with { RemoteKeyProvider = "aws-kms" }, specification);
    }

    [Fact]
    public void ForTenant_ProviderSet_HoldsEncryptionKeysThere()
    {
        var encryption = CreateSpecification(
            AlgorithmCodes.KeyManagement.RsaOaep256,
            SecretKeyUses.Encryption,
            SecretKeyTypes.Rsa,
            3072
        );
        var mockSettings = CreateSettings("aws-kms");

        var result = Policy.ForTenant([encryption], mockSettings.Object);

        var specification = Assert.Single(result);
        Assert.Equal(encryption with { RemoteKeyProvider = "aws-kms" }, specification);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ForTenant_WhenTheSettingIsEmptyOrUnset_LeavesEverySpecificationLocal(
        string? provider
    )
    {
        var mockSettings = CreateSettings(provider);

        var result = Policy.ForTenant([Rs256], mockSettings.Object);

        var specification = Assert.Single(result);
        Assert.Equal(Rs256, specification);
        Assert.Null(specification.RemoteKeyProvider);
    }

    #endregion

    #region IsSatisfiedBy Tests

    [Fact]
    public void IsSatisfiedBy_WhenSecretIsBoundToTheAlgorithm_ReturnsTrue()
    {
        var secret = CreateSecret(algorithm: "RS256", use: SecretKeyUses.Signature);

        Assert.True(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Theory]
    [InlineData("RS256")]
    [InlineData("PS256")]
    public void IsSatisfiedBy_WhenSecretIsUnbound_ReturnsTrueForEveryAlgorithmItFits(
        string algorithm
    )
    {
        var secret = CreateSecret(algorithm: null, use: null);

        Assert.True(
            Policy.IsSatisfiedBy(
                CreateSpecification(algorithm, SecretKeyUses.Signature, SecretKeyTypes.Rsa, 3072),
                secret
            )
        );
    }

    [Fact]
    public void IsSatisfiedBy_WhenSecretIsBoundToAnotherAlgorithm_ReturnsFalse()
    {
        var secret = CreateSecret(algorithm: "PS256", use: SecretKeyUses.Signature);

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Theory]
    [InlineData(SecretEncodings.Spki)]
    [InlineData(SecretEncodings.X509)]
    public void IsSatisfiedBy_WhenSecretHoldsOnlyPublicMaterial_ReturnsFalse(string encoding)
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: encoding
        );

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    // A key held in a form this policy does not know, such as an external keystore's, is checked by its public half.
    [Fact]
    public void IsSatisfiedBy_WhenSecretIsHeldInAnUnknownEncoding_ChecksItsPublicHalf()
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: "keystore_ref"
        );

        Assert.True(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Fact]
    public void IsSatisfiedBy_WhenSecretIsForAnotherUse_ReturnsFalse()
    {
        var secret = CreateSecret(algorithm: null, use: SecretKeyUses.Encryption);

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Theory]
    [InlineData(SecretKeyTypes.Ecc, 256)]
    [InlineData(SecretKeyTypes.Rsa, 1024)]
    [InlineData("unregistered", 3072)]
    public void IsSatisfiedBy_WhenSecretTypeOrSizeDoesNotFitTheAlgorithm_ReturnsFalse(
        string secretType,
        int keySizeBits
    )
    {
        var secret = CreateSecret(
            algorithm: null,
            use: SecretKeyUses.Signature,
            secretType: secretType,
            keySizeBits: keySizeBits
        );

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Theory]
    [InlineData(SecretKeyTypes.Ecc, 256, true)]
    [InlineData(SecretKeyTypes.Rsa, 2048, false)]
    public void IsSatisfiedBy_WhenAlgorithmAcceptsSeveralKeyTypes_RequiresTheSpecifiedOne(
        string secretType,
        int keySizeBits,
        bool expected
    )
    {
        var specification = CreateSpecification(
            AlgorithmCodes.KeyManagement.EcdhEs,
            SecretKeyUses.Encryption,
            SecretKeyTypes.Ecc,
            256
        );
        var secret = CreateSecret(
            algorithm: null,
            use: SecretKeyUses.Encryption,
            secretType: secretType,
            keySizeBits: keySizeBits
        );

        Assert.Equal(expected, Policy.IsSatisfiedBy(specification, secret));
    }

    [Theory]
    [InlineData("brainpoolP256r1", true)]
    [InlineData("P-256", false)]
    public void IsSatisfiedBy_WhenAlgorithmIsBoundToACurve_ChecksTheStoredPublicKeysCurve(
        string curveName,
        bool expected
    )
    {
        var curve =
            curveName == "P-256"
                ? ECCurve.NamedCurves.nistP256
                : ECCurve.NamedCurves.brainpoolP256r1;
        using var key = ECDsa.Create(curve);
        var specification = CreateSpecification(
            BrainpoolAlgorithm,
            SecretKeyUses.Signature,
            SecretKeyTypes.Ecc,
            256,
            "brainpoolP256r1"
        );
        var secret = CreateSecret(
            algorithm: null,
            use: SecretKeyUses.Signature,
            secretType: SecretKeyTypes.Ecc,
            keySizeBits: 256,
            publicKey: Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo())
        );

        Assert.Equal(expected, Policy.IsSatisfiedBy(specification, secret));
    }

    // ECDH-ES accepts EC keys on P-256, P-384 and P-521; a specification on P-384 names a P-384 key, so a P-256 key the
    // algorithm also accepts does not satisfy it.
    [Theory]
    [InlineData("P-384", true)]
    [InlineData("P-256", false)]
    public void IsSatisfiedBy_WhenSpecificationNamesACurve_RequiresTheKeyOnIt(
        string curveName,
        bool expected
    )
    {
        var curve =
            curveName == "P-256" ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384;
        using var key = ECDiffieHellman.Create(curve);
        var specification = CreateSpecification(
            AlgorithmCodes.KeyManagement.EcdhEs,
            SecretKeyUses.Encryption,
            SecretKeyTypes.Ecc,
            384,
            "P-384"
        );
        var secret = CreateSecret(
            algorithm: null,
            use: SecretKeyUses.Encryption,
            secretType: SecretKeyTypes.Ecc,
            keySizeBits: curveName == "P-256" ? 256 : 384,
            publicKey: Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo())
        );

        Assert.Equal(expected, Policy.IsSatisfiedBy(specification, secret));
    }

    // Without its public half the key's curve cannot be read, so the key cannot be shown to fit and one is provisioned.
    // A secret of the specification's type that no registered key type describes cannot be read, so one is provisioned.
    [Fact]
    public void IsSatisfiedBy_WhenSecretTypeIsNotRegistered_ReturnsFalse()
    {
        var specification = CreateSpecification(
            "RS256",
            SecretKeyUses.Signature,
            "unregistered",
            3072
        );
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            secretType: "unregistered",
            publicKey: CreatePublicKey(SecretKeyTypes.Rsa, 3072)
        );

        Assert.False(Policy.IsSatisfiedBy(specification, secret));
    }

    [Fact]
    public void IsSatisfiedBy_WhenPrivateSecretHasNoPublicHalf_ReturnsFalse()
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            withPublicKey: false
        );

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Fact]
    public void IsSatisfiedBy_WhenStoredPublicKeyCannotBeRead_ReturnsFalse()
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            publicKey: "AAAA"
        );

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    [Fact]
    public void IsSatisfiedBy_WhenRemoteSpecificationAndSecretIsRemoteOfTheProvider_ReturnsTrue()
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: SecretEncodings.Remote,
            encodedValue: "aws-kms:arn:aws:kms:us-east-1:111122223333:key/abc"
        );

        Assert.True(Policy.IsSatisfiedBy(RemoteRs256, secret));
    }

    [Theory]
    [InlineData("azure-key-vault:https://vault.example/keys/k/1")]
    [InlineData("aws-kms")]
    [InlineData("")]
    public void IsSatisfiedBy_WhenRemoteSpecificationAndSecretIsRemoteOfAnotherProviderOrUnparsable_ReturnsFalse(
        string encodedValue
    )
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: SecretEncodings.Remote,
            encodedValue: encodedValue
        );

        Assert.False(Policy.IsSatisfiedBy(RemoteRs256, secret));
    }

    [Theory]
    [InlineData(SecretEncodings.Pkcs8)]
    [InlineData("keystore_ref")]
    public void IsSatisfiedBy_WhenRemoteSpecificationAndSecretIsNotRemote_ReturnsFalse(
        string encoding
    )
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: encoding
        );

        Assert.False(Policy.IsSatisfiedBy(RemoteRs256, secret));
    }

    [Fact]
    public void IsSatisfiedBy_WhenLocalSpecificationAndSecretIsRemote_ReturnsFalse()
    {
        var secret = CreateSecret(
            algorithm: "RS256",
            use: SecretKeyUses.Signature,
            encoding: SecretEncodings.Remote,
            encodedValue: "aws-kms:key-1"
        );

        Assert.False(Policy.IsSatisfiedBy(Rs256, secret));
    }

    #endregion

    #region Helpers

    private static ServiceProvider BuildEdDsaServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddJoseLibrary().AddEdDsa();
        secrets.AddPersistenceLibrary();
        return serviceCollection.BuildServiceProvider();
    }

    private static DefaultTenantKeyPolicy CreatePolicy(IServiceProvider serviceProvider) =>
        CreatePolicy(
            serviceProvider,
            serviceProvider.GetRequiredService<IRemoteKeyDescriptorCatalog>()
        );

    private static DefaultTenantKeyPolicy CreatePolicy(
        IServiceProvider serviceProvider,
        IRemoteKeyDescriptorCatalog catalog
    ) =>
        new(
            serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            serviceProvider.GetRequiredService<ISecretSerializer>(),
            catalog
        );

    // A catalog whose providers are the named ones and whose mapping vocabulary holds a row for each of the provider's
    // algorithms in rowAlgorithms, whatever the secret type, size and curve. A hand-written stub, because the lookup
    // has an out parameter that a mock setup cannot compute from the selector.
    private sealed class StubRemoteKeyDescriptorCatalog(
        string[] providerNames,
        string[] rowAlgorithms
    ) : IRemoteKeyDescriptorCatalog
    {
        public IReadOnlyCollection<RemoteKeyProviderDescriptor> Providers { get; } =
            providerNames.Select(name => new RemoteKeyProviderDescriptor { Name = name }).ToList();

        public IReadOnlyCollection<RemoteKeyAlgorithm> Algorithms { get; } = [];

        public bool TryGetAlgorithm(
            RemoteKeyAlgorithmSelector selector,
            [NotNullWhen(true)] out RemoteKeyAlgorithm? algorithm
        )
        {
            var hasRow = rowAlgorithms.Contains(selector.Algorithm);
            algorithm = hasRow ? CreateRow(selector) : null;
            return hasRow;
        }
    }

    private static RemoteKeyAlgorithm CreateRow(RemoteKeyAlgorithmSelector selector) =>
        selector.Algorithm == AlgorithmCodes.KeyManagement.RsaOaep256
            ? new RemoteDecryptionAlgorithm
            {
                Provider = selector.Provider,
                Algorithm = selector.Algorithm,
                SecretType = selector.SecretType,
                KeySizeBits = selector.KeySizeBits,
                Curve = selector.Curve,
                KeySpec = "spec",
                EncryptionAlgorithm = "encryption-algorithm",
            }
            : new RemoteSigningAlgorithm
            {
                Provider = selector.Provider,
                Algorithm = selector.Algorithm,
                SecretType = selector.SecretType,
                KeySizeBits = selector.KeySizeBits,
                Curve = selector.Curve,
                KeySpec = "spec",
                SigningAlgorithm = "signing-algorithm",
            };

    private Mock<IReadOnlySettingCollection> CreateSettings(string? provider)
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.TenantKeyProvider, out provider))
            .Returns(provider is not null)
            .Verifiable();
        return mockSettings;
    }

    private static OpenIdTenantKeyProvisioningOptions CreateOptions(
        params OpenIdTenantKeyOptions[] keys
    ) => new() { Keys = [.. keys] };

    private static TenantKeySpecification CreateSpecification(
        string algorithm,
        string use,
        string secretType,
        int keySizeBits,
        string? curve = null
    ) =>
        new()
        {
            Algorithm = algorithm,
            Use = use,
            SecretType = secretType,
            KeySizeBits = keySizeBits,
            Curve = curve,
        };

    // Every private asymmetric secret stores its public half (ADR-1268), so the helper stores one of the secret's type
    // and size unless the test gives its own; a type the helper cannot generate a key of has none.
    private static string? CreatePublicKey(string secretType, int keySizeBits) =>
        secretType switch
        {
            SecretKeyTypes.Rsa => PublicKeys.GetOrAdd(
                (secretType, keySizeBits),
                static key =>
                {
                    using var rsa = RSA.Create(key.KeySizeBits);
                    return Base64Url.EncodeToString(rsa.ExportSubjectPublicKeyInfo());
                }
            ),
            SecretKeyTypes.Ecc when keySizeBits == 256 => PublicKeys.GetOrAdd(
                (secretType, keySizeBits),
                static _ =>
                {
                    using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
                    return Base64Url.EncodeToString(ecdsa.ExportSubjectPublicKeyInfo());
                }
            ),
            _ => null,
        };

    private static PersistedSecret CreateSecret(
        string? algorithm,
        string? use,
        string encoding = SecretEncodings.Pkcs8,
        string secretType = SecretKeyTypes.Rsa,
        int keySizeBits = 3072,
        string? publicKey = null,
        bool withPublicKey = true,
        string encodedValue = "AA"
    ) =>
        new PersistedSecretBuilder
        {
            PublicKey = withPublicKey
                ? publicKey ?? CreatePublicKey(secretType, keySizeBits)
                : null,
            SecretId = "secret",
            Use = use,
            Algorithm = algorithm,
            SecretType = secretType,
            SecretEncoding = encoding,
            EncodedValue = encodedValue,
            KeySizeBits = keySizeBits,
        }.Build();

    #endregion
}
