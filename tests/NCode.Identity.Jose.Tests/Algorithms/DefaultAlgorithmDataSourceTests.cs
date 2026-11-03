// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Tests.Algorithms;

public class DefaultAlgorithmDataSourceTests : BaseTests
{
    private const string BrainpoolCode = "X-ES256-BP";

    #region Helpers

    private static EccCurveSpecification Brainpool256 { get; } =
        new()
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
        };

    private static EccSignatureAlgorithmSpecification CreateSpecification(string code) =>
        new()
        {
            Code = code,
            Curve = Brainpool256,
            HashAlgorithmName = HashAlgorithmName.SHA256,
        };

    private static ServiceProvider BuildServiceProvider(
        params IEnumerable<EccSignatureAlgorithmSpecification> specifications
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.AddSingleton(Brainpool256);
        foreach (var specification in specifications)
        {
            serviceCollection.AddSingleton(specification);
        }

        return serviceCollection.BuildServiceProvider();
    }

    private static IAlgorithmCollection GetCollection(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;

    // The service provider's key factory names the key's curve from the curve catalog the test registers into.
    private static EccSecretKey CreateKey(IServiceProvider serviceProvider, ECCurve curve)
    {
        using var key = ECDsa.Create(curve);
        return serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateEcc(new KeyMetadata { KeyId = "keyId" }, key);
    }

    #endregion

    #region Collection Tests

    [Theory]
    [InlineData("ES256", "P-256", 256)]
    [InlineData("ES384", "P-384", 384)]
    [InlineData("ES512", "P-521", 521)]
    public async Task Collection_BuiltInEcdsaAlgorithms_NameTheirCurve(
        string code,
        string curve,
        int keySizeBits
    )
    {
        await using var serviceProvider = BuildServiceProvider();

        Assert.True(
            GetCollection(serviceProvider).TryGetSignatureAlgorithm(code, out var algorithm)
        );
        var keyType = Assert.Single(algorithm.KeyTypes);
        var named = Assert.Single(keyType.Curves);
        Assert.Equal(curve, named.Name);
        Assert.Equal(keySizeBits, named.KeySizeBits);
        Assert.True(keyType.IsLegalSize(keySizeBits));
    }

    [Fact]
    public async Task Collection_WhenEcdsaAlgorithmIsRegistered_ServesItBoundToItsCurve()
    {
        await using var serviceProvider = BuildServiceProvider(CreateSpecification(BrainpoolCode));

        Assert.True(
            GetCollection(serviceProvider)
                .TryGetSignatureAlgorithm(BrainpoolCode, out var algorithm)
        );
        Assert.Equal(
            "brainpoolP256r1",
            Assert.Single(Assert.Single(algorithm.KeyTypes).Curves).Name
        );
        Assert.True(
            algorithm.IsKeyCompatible(
                CreateKey(serviceProvider, ECCurve.NamedCurves.brainpoolP256r1)
            )
        );
        Assert.False(
            algorithm.IsKeyCompatible(CreateKey(serviceProvider, ECCurve.NamedCurves.nistP256))
        );
    }

    [Fact]
    public async Task Collection_WhenEcdsaAlgorithmIsRegistered_KeepsEs256BoundToP256()
    {
        await using var serviceProvider = BuildServiceProvider(CreateSpecification(BrainpoolCode));

        Assert.True(
            GetCollection(serviceProvider).TryGetSignatureAlgorithm("ES256", out var es256)
        );
        Assert.False(
            es256.IsKeyCompatible(CreateKey(serviceProvider, ECCurve.NamedCurves.brainpoolP256r1))
        );
    }

    // A key on a curve the catalog does not hold cannot be generated, imported or named, so the algorithm is refused.
    [Fact]
    public async Task Collection_WhenEcdsaAlgorithmsCurveIsNotRegistered_Throws()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.AddSingleton(CreateSpecification(BrainpoolCode));
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCollection(serviceProvider)
        );
        Assert.Contains("'brainpoolP256r1'", exception.Message, StringComparison.Ordinal);
    }

    // The curve is matched by its OID too, so a specification that names a registered curve but is on another one is
    // refused.
    [Fact]
    public async Task Collection_WhenEcdsaAlgorithmsCurveHasARegisteredNameButAnotherOid_Throws()
    {
        await using var serviceProvider = BuildServiceProvider(
            new EccSignatureAlgorithmSpecification
            {
                Code = BrainpoolCode,
                Curve = new EccCurveSpecification
                {
                    CurveName = "P-256",
                    CurveSizeBits = 256,
                    Curve = ECCurve.NamedCurves.brainpoolP256r1,
                },
                HashAlgorithmName = HashAlgorithmName.SHA256,
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCollection(serviceProvider)
        );
        Assert.Contains($"'{BrainpoolCode}'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Collection_WhenRegisteredCodeIsABuiltInOne_Throws()
    {
        await using var serviceProvider = BuildServiceProvider(CreateSpecification("ES256"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCollection(serviceProvider)
        );
        Assert.Contains("'ES256'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Collection_WhenTwoRegisteredAlgorithmsShareACode_Throws()
    {
        await using var serviceProvider = BuildServiceProvider(
            CreateSpecification(BrainpoolCode),
            CreateSpecification(BrainpoolCode)
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCollection(serviceProvider)
        );
        Assert.Contains($"'{BrainpoolCode}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public async Task Collection_WhenRegisteredCodeIsEmptyOrWhitespace_Throws(string code)
    {
        await using var serviceProvider = BuildServiceProvider(CreateSpecification(code));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCollection(serviceProvider)
        );
        Assert.Contains("empty or whitespace code", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Collection_WhenRegisteredSpecificationIsNull_Throws()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        serviceCollection.AddSingleton<EccSignatureAlgorithmSpecification>(_ => null!);
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            GetCollection(serviceProvider)
        );
        Assert.Contains("specification is null", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
