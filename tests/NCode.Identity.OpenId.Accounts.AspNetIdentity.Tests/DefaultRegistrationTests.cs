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

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NCode.Persistence;
using NCode.Registration;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

public sealed class DefaultRegistrationTests : IDisposable
{
    private const string ComposedPassword = "caf\u00E9";
    private const string DecomposedPassword = "cafe\u0301";
    private const string MadeHash = "hash";

    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    public void Dispose() => _mocks.Verify();

    #region Helpers

    private static IPasswordHasher<IdentityUser> ResolveHasher(IServiceCollection serviceCollection)
    {
        using var provider = serviceCollection.BuildServiceProvider();
        using var scope = provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IPasswordHasher<IdentityUser>>();
    }

    #endregion

    #region AddAspNetIdentityLocalAccountStore Tests

    [Fact]
    public void AddAspNetIdentityLocalAccountStore_WhenIdentityIsAddedAfterIt_ResolvesTheNormalizingHasher()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .NewBuilder<StoreManagerLibrary>()
            .AddAspNetIdentityLocalAccountStore<IdentityUser>();
        serviceCollection.AddIdentityCore<IdentityUser>();

        var hasher = ResolveHasher(serviceCollection);

        Assert.IsType<NormalizingPasswordHasher<IdentityUser>>(hasher);
    }

    [Fact]
    public void AddAspNetIdentityLocalAccountStore_WhenIdentityIsAddedBeforeIt_ResolvesTheNormalizingHasher()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddIdentityCore<IdentityUser>();
        serviceCollection
            .NewBuilder<StoreManagerLibrary>()
            .AddAspNetIdentityLocalAccountStore<IdentityUser>();

        var hasher = ResolveHasher(serviceCollection);

        Assert.IsType<NormalizingPasswordHasher<IdentityUser>>(hasher);
    }

    [Fact]
    public void AddAspNetIdentityLocalAccountStore_WhenTheHostRegisteredItsOwnHasher_WrapsIt()
    {
        var innerHasher = _mocks.Create<IPasswordHasher<IdentityUser>>();
        innerHasher
            .Setup(x => x.HashPassword(It.IsAny<IdentityUser>(), ComposedPassword))
            .Returns(MadeHash)
            .Verifiable();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(innerHasher.Object);
        serviceCollection
            .NewBuilder<StoreManagerLibrary>()
            .AddAspNetIdentityLocalAccountStore<IdentityUser>();

        var hasher = ResolveHasher(serviceCollection);

        var hash = hasher.HashPassword(new IdentityUser("alice"), DecomposedPassword);
        Assert.Equal(MadeHash, hash);
    }

    [Fact]
    public void AddAspNetIdentityLocalAccountStore_WhenTheHostRegisteredANormalizingHasher_DoesNotWrapItAgain()
    {
        var innerHasher = _mocks.Create<IPasswordHasher<IdentityUser>>();
        var registered = new NormalizingPasswordHasher<IdentityUser>(innerHasher.Object);
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IPasswordHasher<IdentityUser>>(registered);
        serviceCollection
            .NewBuilder<StoreManagerLibrary>()
            .AddAspNetIdentityLocalAccountStore<IdentityUser>();

        var hasher = ResolveHasher(serviceCollection);

        Assert.Same(registered, hasher);
    }

    [Fact]
    public void AddAspNetIdentityLocalAccountStore_WhenTheHostRegisteredASingletonHasherType_KeepsItsLifetime()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<
            IPasswordHasher<IdentityUser>,
            PasswordHasher<IdentityUser>
        >();
        serviceCollection
            .NewBuilder<StoreManagerLibrary>()
            .AddAspNetIdentityLocalAccountStore<IdentityUser>();

        var descriptor = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IPasswordHasher<IdentityUser>)
        );
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);

        using var provider = serviceCollection.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IPasswordHasher<IdentityUser>>();
        var second = secondScope.ServiceProvider.GetRequiredService<
            IPasswordHasher<IdentityUser>
        >();

        Assert.IsType<NormalizingPasswordHasher<IdentityUser>>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AddAspNetIdentityLocalAccountStore_WhenTheHostRegisteredATransientHasherFactory_WrapsItAndKeepsItsLifetime()
    {
        var innerHasher = _mocks.Create<IPasswordHasher<IdentityUser>>();
        innerHasher
            .Setup(x => x.HashPassword(It.IsAny<IdentityUser>(), ComposedPassword))
            .Returns(MadeHash)
            .Verifiable();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddTransient<IPasswordHasher<IdentityUser>>(_ => innerHasher.Object);
        serviceCollection
            .NewBuilder<StoreManagerLibrary>()
            .AddAspNetIdentityLocalAccountStore<IdentityUser>();

        var descriptor = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IPasswordHasher<IdentityUser>)
        );
        Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);

        using var provider = serviceCollection.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var first = scope.ServiceProvider.GetRequiredService<IPasswordHasher<IdentityUser>>();
        var second = scope.ServiceProvider.GetRequiredService<IPasswordHasher<IdentityUser>>();

        Assert.NotSame(first, second);
        var hash = first.HashPassword(new IdentityUser("alice"), DecomposedPassword);
        Assert.Equal(MadeHash, hash);
    }

    #endregion
}
