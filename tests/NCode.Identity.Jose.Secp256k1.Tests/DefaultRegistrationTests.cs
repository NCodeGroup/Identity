// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Secp256k1.KeyTypes;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.Secp256k1.Tests;

public class DefaultRegistrationTests
{
    #region Helpers

    private static ServiceProvider BuildServiceProvider(Action<IServiceCollection> register)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        register(serviceCollection);
        return serviceCollection.BuildServiceProvider();
    }

    private static IAlgorithmCollection GetAlgorithms(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IAlgorithmCollectionProvider>().Collection;

    #endregion

    #region AddSecp256k1 Tests

    [Secp256k1Fact]
    public async Task AddSecp256k1_WhenPlatformSupportsTheCurve_RegistersTheCurveAndEs256K()
    {
        await using var serviceProvider = BuildServiceProvider(services => services.AddSecp256k1());

        var catalog = serviceProvider.GetRequiredService<IEccCurveCatalog>();
        Assert.True(catalog.TryGetByName("secp256k1", out var specification));
        Assert.Same(Secp256k1Curve.Specification, specification);
        Assert.True(GetAlgorithms(serviceProvider).TryGetSignatureAlgorithm("ES256K", out _));
    }

    [Secp256k1Fact]
    public async Task AddSecp256k1_WhenCalledTwice_RegistersOnce()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSecp256k1();
        serviceCollection.AddSecp256k1();

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(EccCurveSpecification));
        Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(EccSignatureAlgorithmSpecification)
        );

        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();
        Assert.True(GetAlgorithms(serviceProvider).TryGetSignatureAlgorithm("ES256K", out _));
    }

    [Secp256k1Fact]
    public async Task AddSecp256k1_WhenTheApplicationRegistersAnotherCurve_KeepsBoth()
    {
        var brainpool = new EccCurveSpecification
        {
            CurveName = "brainpoolP256r1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.brainpoolP256r1,
        };

        await using var serviceProvider = BuildServiceProvider(services =>
        {
            services.AddSingleton(brainpool);
            services.AddSecp256k1();
        });

        var catalog = serviceProvider.GetRequiredService<IEccCurveCatalog>();
        Assert.True(catalog.TryGetByName("brainpoolP256r1", out _));
        Assert.True(catalog.TryGetByName("secp256k1", out _));
    }

    [Secp256k1Fact]
    public async Task AddSecp256k1_WhenRegistered_KeepsP256TheCurveSelectedBySize()
    {
        await using var serviceProvider = BuildServiceProvider(services => services.AddSecp256k1());

        var catalog = serviceProvider.GetRequiredService<IEccCurveCatalog>();
        Assert.True(catalog.TryGetByCurveSizeBits(256, out var bySize));
        Assert.Equal("P-256", bySize.CurveName);
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_WhenPlatformDoesNotSupportTheCurve_RegistersNothing()
    {
        await using var serviceProvider = BuildServiceProvider(services =>
            Secp256k1Registration.Register(services, isPlatformSupported: false)
        );

        Assert.False(
            serviceProvider.GetRequiredService<IEccCurveCatalog>().TryGetByName("secp256k1", out _)
        );
        Assert.False(GetAlgorithms(serviceProvider).TryGetSignatureAlgorithm("ES256K", out _));
        Assert.True(GetAlgorithms(serviceProvider).TryGetSignatureAlgorithm("ES256", out _));
    }

    #endregion
}
