// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Registration;

namespace NCode.Identity.Cryptography.Tests;

public class DefaultRegistrationTests
{
    #region Helpers

    private sealed class TestServiceCollection : List<ServiceDescriptor>, IServiceCollection;

    #endregion

    #region AddCryptographyLibrary Tests

    [Fact]
    public void AddCryptographyLibrary_Always_ReturnsTheMarkerBuilder()
    {
        var serviceCollection = new TestServiceCollection();

        var builder = serviceCollection.AddCryptographyLibrary();

        Assert.IsAssignableFrom<IServiceBuilder<CryptographyLibrary>>(builder);
        Assert.Same(serviceCollection, builder.ServiceCollection);
    }

    [Fact]
    public void AddCryptographyLibrary_Always_RegistersTheLibraryServices()
    {
        var serviceCollection = new TestServiceCollection();

        serviceCollection.AddCryptographyLibrary();

        var service = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(ICryptoService)
        );
        Assert.Equal(ServiceLifetime.Singleton, service.Lifetime);
        Assert.Equal(typeof(DefaultCryptoService), service.ImplementationType);

        var encoders = serviceCollection
            .Where(x => x.ServiceType == typeof(IBinaryEncoder))
            .ToList();
        Assert.Equal(3, encoders.Count);
        Assert.All(encoders, x => Assert.Equal(ServiceLifetime.Singleton, x.Lifetime));
        Assert.Equal(
            [typeof(Base64UrlBinaryEncoder), typeof(Base64BinaryEncoder), typeof(HexBinaryEncoder)],
            encoders.Select(x => x.ImplementationType)
        );
        Assert.Equal(
            [BinaryEncodings.Base64Url, BinaryEncodings.Base64, BinaryEncodings.Hex],
            [
                new Base64UrlBinaryEncoder().Name,
                new Base64BinaryEncoder().Name,
                new HexBinaryEncoder().Name,
            ]
        );
    }

    [Fact]
    public void AddCryptographyLibrary_WhenCalledTwice_Throws()
    {
        var serviceCollection = new TestServiceCollection();
        serviceCollection.AddCryptographyLibrary();

        Assert.Throws<InvalidOperationException>(() => serviceCollection.AddCryptographyLibrary());

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(ICryptoService));
        Assert.Equal(3, serviceCollection.Count(x => x.ServiceType == typeof(IBinaryEncoder)));
    }

    #endregion
}
