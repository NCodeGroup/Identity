// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

public sealed class CapabilitiesProvidersTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    [Fact]
    public async Task SecretTypeCapabilitiesProvider_AddsSecretTypesSection()
    {
        var mockDescriptor = MockRepository.Create<ISecretKeyTypeDescriptor>();
        mockDescriptor.Setup(x => x.KeyType).Returns("rsa").Verifiable();
        mockDescriptor.Setup(x => x.IsAsymmetric).Returns(true).Verifiable();
        mockDescriptor.Setup(x => x.SupportsPublicKeyExport).Returns(true).Verifiable();
        mockDescriptor.Setup(x => x.PublicKeyImportFormats).Returns(["jwk"]).Verifiable();

        var mockCatalog = MockRepository.Create<ISecretKeyTypeCatalog>();
        mockCatalog.Setup(x => x.Descriptors).Returns([mockDescriptor.Object]).Verifiable();

        var provider = new SecretTypeCapabilitiesProvider(mockCatalog.Object);
        var capabilities = new Dictionary<string, object>();

        await provider.HandleAsync(
            new DiscoverManagementCapabilitiesCommand(null!, capabilities),
            CancellationToken.None
        );

        var section = Assert.IsType<List<SecretTypeCapabilityResource>>(
            capabilities[SecretTypeCapabilitiesProvider.SectionName]
        );
        var item = Assert.Single(section);
        Assert.Equal("rsa", item.SecretType);
        Assert.True(item.IsAsymmetric);
        Assert.True(item.SupportsPublicKeyExport);
        Assert.True(item.SupportsPublicKeyImport);
    }

    [Fact]
    public async Task EccCurveCapabilitiesProvider_AddsEccCurvesSection()
    {
        var mockCatalog = MockRepository.Create<IEccCurveCatalog>();
        mockCatalog
            .Setup(x => x.Curves)
            .Returns([
                new EccCurveSpecification
                {
                    CurveName = "P-256",
                    CurveSizeBits = 256,
                    Curve = ECCurve.NamedCurves.nistP256,
                },
            ])
            .Verifiable();

        var provider = new EccCurveCapabilitiesProvider(mockCatalog.Object);
        var capabilities = new Dictionary<string, object>();

        await provider.HandleAsync(
            new DiscoverManagementCapabilitiesCommand(null!, capabilities),
            CancellationToken.None
        );

        var section = Assert.IsType<List<EccCurveCapabilityResource>>(
            capabilities[EccCurveCapabilitiesProvider.SectionName]
        );
        var item = Assert.Single(section);
        Assert.Equal("P-256", item.CurveName);
        Assert.Equal(256, item.CurveSizeBits);
    }

    [Fact]
    public async Task AlgorithmCapabilitiesProvider_PublishesSignatureAndKeyManagementAlgorithms()
    {
        // Resolve the real algorithm collection and key-type catalog so the section is asserted against the exact
        // set of algorithms and key types the server supports — the same source of truth the provider reads.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var provider = new AlgorithmCapabilitiesProvider(
            serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>()
        );
        var capabilities = new Dictionary<string, object>();

        await provider.HandleAsync(
            new DiscoverManagementCapabilitiesCommand(null!, capabilities),
            CancellationToken.None
        );

        var section = Assert.IsType<List<AlgorithmCapabilityResource>>(
            capabilities[AlgorithmCapabilitiesProvider.SectionName]
        );

        // Only signature and key-management algorithms are published (no AEAD or compression algorithms).
        Assert.NotEmpty(section);
        Assert.All(
            section,
            algorithm =>
                Assert.Contains(
                    algorithm.Use,
                    new[] { SecretKeyUses.Signature, SecretKeyUses.Encryption }
                )
        );

        var rs256 = Assert.Single(
            section,
            algorithm => algorithm.Code == AlgorithmCodes.DigitalSignature.RsaSha256
        );
        Assert.Equal(SecretKeyUses.Signature, rs256.Use);
        var rs256KeyType = Assert.Single(rs256.KeyTypes);
        Assert.Equal(SecretKeyTypes.Rsa, rs256KeyType.SecretType);
        Assert.NotEmpty(rs256KeyType.KeyBitSizes);
        Assert.Empty(rs256KeyType.Curves);

        // ES256 is bound to P-256, so the console can offer that curve alone.
        var es256 = Assert.Single(
            section,
            algorithm => algorithm.Code == AlgorithmCodes.DigitalSignature.EcdsaSha256
        );
        Assert.Equal(["P-256"], Assert.Single(es256.KeyTypes).Curves);

        var rsaOaep = Assert.Single(
            section,
            algorithm => algorithm.Code == AlgorithmCodes.KeyManagement.RsaOaep
        );
        Assert.Equal(SecretKeyUses.Encryption, rsaOaep.Use);
        Assert.Equal(SecretKeyTypes.Rsa, Assert.Single(rsaOaep.KeyTypes).SecretType);
    }

    [Fact]
    public async Task AlgorithmCapabilitiesProvider_WhenAnAlgorithmAcceptsSeveralKeyTypes_ListsEachWithItsSizes()
    {
        // ECDH-ES serves every key type that has a registered agreement (ADR-1389); a second agreement adds a second key
        // type with sizes of its own, which the section lists after the first, in the algorithm's order of preference.
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.AddSingleton<IEcdhKeyAgreement, RsaKeyTypeAgreementStub>();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var provider = new AlgorithmCapabilitiesProvider(
            serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            serviceProvider.GetRequiredService<ISecretKeyTypeCatalog>()
        );
        var capabilities = new Dictionary<string, object>();

        await provider.HandleAsync(
            new DiscoverManagementCapabilitiesCommand(null!, capabilities),
            CancellationToken.None
        );

        var section = Assert.IsType<List<AlgorithmCapabilityResource>>(
            capabilities[AlgorithmCapabilitiesProvider.SectionName]
        );
        var ecdhEs = Assert.Single(
            section,
            algorithm => algorithm.Code == AlgorithmCodes.KeyManagement.EcdhEs
        );
        Assert.Collection(
            ecdhEs.KeyTypes,
            ecc =>
            {
                Assert.Equal(SecretKeyTypes.Ecc, ecc.SecretType);
                Assert.Collection(
                    ecc.KeyBitSizes,
                    size =>
                        Assert.Equal((256, 384, 128), (size.MinBits, size.MaxBits, size.SkipBits)),
                    size => Assert.Equal((521, 521, 0), (size.MinBits, size.MaxBits, size.SkipBits))
                );
                Assert.Equal(["P-256", "P-384", "P-521"], ecc.Curves);
            },
            rsa =>
            {
                Assert.Equal(SecretKeyTypes.Rsa, rsa.SecretType);
                var size = Assert.Single(rsa.KeyBitSizes);
                Assert.Equal((2048, 4096, 1024), (size.MinBits, size.MaxBits, size.SkipBits));
            }
        );
    }

    // Declares a second key type for the ECDH-ES algorithms; the capabilities listing never performs an agreement.
    private sealed class RsaKeyTypeAgreementStub : IEcdhKeyAgreement
    {
        public AlgorithmKeyType KeyType { get; } =
            new(typeof(RsaSecretKey), [new KeySizes(minSize: 2048, maxSize: 4096, skipSize: 1024)]);

        public bool IsKeyCompatible(SecretKey secretKey) => throw new NotSupportedException();

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
}
