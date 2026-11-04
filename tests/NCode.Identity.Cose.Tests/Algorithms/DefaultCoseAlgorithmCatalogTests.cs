// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.EdDsa;
using NCode.Identity.Secrets;

namespace NCode.Identity.Cose.Tests.Algorithms;

public class DefaultCoseAlgorithmCatalogTests
{
    private static readonly CoseAlgorithm[] Defaults =
    [
        CoseAlgorithms.Es256,
        CoseAlgorithms.Es384,
        CoseAlgorithms.Es512,
        CoseAlgorithms.Esp256,
        CoseAlgorithms.Esp384,
        CoseAlgorithms.Esp512,
        CoseAlgorithms.Ps256,
        CoseAlgorithms.Ps384,
        CoseAlgorithms.Ps512,
        CoseAlgorithms.HmacSha256,
        CoseAlgorithms.HmacSha384,
        CoseAlgorithms.HmacSha512,
        CoseAlgorithms.A128Gcm,
        CoseAlgorithms.A192Gcm,
        CoseAlgorithms.A256Gcm,
        CoseAlgorithms.Direct,
        CoseAlgorithms.A128Kw,
        CoseAlgorithms.A192Kw,
        CoseAlgorithms.A256Kw,
        CoseAlgorithms.RsaesOaepSha256,
        CoseAlgorithms.RsaesOaepSha512,
    ];

    private static IAlgorithmCollectionProvider CreateProvider(bool withEdDsa = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var jose = services.AddSecretsLibrary().AddJoseLibrary();
        if (withEdDsa)
            jose.AddEdDsa();

        return services.BuildServiceProvider().GetRequiredService<IAlgorithmCollectionProvider>();
    }

    private static DefaultCoseAlgorithmCatalog CreateCatalog(
        IEnumerable<CoseAlgorithm> algorithms,
        bool withEdDsa = false
    ) => new(algorithms, CreateProvider(withEdDsa));

    #region Constructor

    [Fact]
    public void Constructor_DefaultSetWithoutExtensions_BindsEveryAvailableAlgorithm()
    {
        var catalog = CreateCatalog([
            .. Defaults,
            CoseAlgorithms.EdDsa,
            CoseAlgorithms.Ed25519,
            CoseAlgorithms.Ed448,
            CoseAlgorithms.Es256K,
        ]);

        foreach (var algorithm in Defaults)
            Assert.True(catalog.TryGet(algorithm.Identifier, out _), algorithm.Name);

        Assert.False(catalog.TryGet(CoseAlgorithmIdentifiers.EdDsa, out _));
        Assert.False(catalog.TryGet(CoseAlgorithmIdentifiers.Ed25519, out _));
        Assert.False(catalog.TryGet(CoseAlgorithmIdentifiers.Ed448, out _));
        Assert.False(catalog.TryGet(CoseAlgorithmIdentifiers.Es256K, out _));
        Assert.Equal(Defaults.Length, catalog.Algorithms.Count);
    }

    [Fact]
    public void Constructor_WithEdDsa_BindsTheEdwardsAlgorithms()
    {
        var catalog = CreateCatalog(
            [CoseAlgorithms.EdDsa, CoseAlgorithms.Ed25519, CoseAlgorithms.Ed448],
            withEdDsa: true
        );

        Assert.True(catalog.TryGet(CoseAlgorithmIdentifiers.EdDsa, out var binding));
        Assert.Equal("EdDSA", binding.JoseAlgorithm.Code);
        Assert.True(catalog.TryGet(CoseAlgorithmIdentifiers.Ed25519, out _));
        Assert.True(catalog.TryGet(CoseAlgorithmIdentifiers.Ed448, out _));
    }

    [Fact]
    public void Constructor_MlDsa_BindsExactlyWhenThePlatformSupportsIt()
    {
        var catalog = CreateCatalog([
            CoseAlgorithms.MlDsa44,
            CoseAlgorithms.MlDsa65,
            CoseAlgorithms.MlDsa87,
        ]);

        Assert.Equal(MLDsa.IsSupported ? 3 : 0, catalog.Algorithms.Count);
        Assert.Equal(MLDsa.IsSupported, catalog.TryGet(CoseAlgorithmIdentifiers.MlDsa44, out _));
    }

    [Fact]
    public void Constructor_Es256AndEsp256_BindTheSameJoseAlgorithm()
    {
        var catalog = CreateCatalog([CoseAlgorithms.Es256, CoseAlgorithms.Esp256]);

        Assert.True(catalog.TryGet(CoseAlgorithmIdentifiers.Es256, out var es256));
        Assert.True(catalog.TryGet(CoseAlgorithmIdentifiers.Esp256, out var esp256));
        Assert.Same(es256.JoseAlgorithm, esp256.JoseAlgorithm);
    }

    [Fact]
    public void Constructor_TruncatedHmac_BindsWithTheTagSize()
    {
        var catalog = CreateCatalog([CoseAlgorithms.HmacSha256Truncated64]);

        Assert.True(
            catalog.TryGet(CoseAlgorithmIdentifiers.HmacSha256Truncated64, out var binding)
        );
        Assert.Equal(8, binding.Algorithm.TagSizeBytes);
    }

    [Fact]
    public void Constructor_DuplicateIdentifier_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CreateCatalog([CoseAlgorithms.Es256, CoseAlgorithms.Es256])
        );
    }

    [Fact]
    public void Constructor_NullDescriptor_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => CreateCatalog([null!]));
    }

    [Fact]
    public void Constructor_EmptyName_Throws()
    {
        var algorithm = new CoseAlgorithm
        {
            Identifier = 1000,
            Name = " ",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha256,
        };

        Assert.Throws<InvalidOperationException>(() => CreateCatalog([algorithm]));
    }

    [Fact]
    public void Constructor_SignatureBoundToHmac_Throws()
    {
        var algorithm = new CoseAlgorithm
        {
            Identifier = 1000,
            Name = "bad",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.HmacSha256,
        };

        Assert.Throws<InvalidOperationException>(() => CreateCatalog([algorithm]));
    }

    [Fact]
    public void Constructor_MacBoundToEcdsa_Throws()
    {
        var algorithm = new CoseAlgorithm
        {
            Identifier = 1000,
            Name = "bad",
            Kind = CoseAlgorithmKind.Mac,
            JoseAlgorithmCode = AlgorithmCodes.DigitalSignature.EcdsaSha256,
        };

        Assert.Throws<InvalidOperationException>(() => CreateCatalog([algorithm]));
    }

    [Theory]
    [InlineData(CoseAlgorithmKind.Signature, AlgorithmCodes.DigitalSignature.EcdsaSha256, 8)]
    [InlineData(CoseAlgorithmKind.Mac, AlgorithmCodes.DigitalSignature.HmacSha256, 4)]
    [InlineData(CoseAlgorithmKind.Mac, AlgorithmCodes.DigitalSignature.HmacSha256, 33)]
    public void Constructor_UnusableTagSize_Throws(
        CoseAlgorithmKind kind,
        string joseCode,
        int tagSize
    )
    {
        var algorithm = new CoseAlgorithm
        {
            Identifier = 1000,
            Name = "bad",
            Kind = kind,
            JoseAlgorithmCode = joseCode,
            TagSizeBytes = tagSize,
        };

        Assert.Throws<InvalidOperationException>(() => CreateCatalog([algorithm]));
    }

    [Fact]
    public void Constructor_UnavailableJoseAlgorithm_LeavesTheDescriptorOut()
    {
        var algorithm = new CoseAlgorithm
        {
            Identifier = 1000,
            Name = "unavailable",
            Kind = CoseAlgorithmKind.Signature,
            JoseAlgorithmCode = "no-such-algorithm",
        };

        var catalog = CreateCatalog([algorithm]);

        Assert.Empty(catalog.Algorithms);
    }

    #endregion
}
