// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public class DefaultRegistrationTests : BaseTests
{
    private static IServiceBuilder<BffLibrary> CreateBffBuilder(
        ServiceCollection serviceCollection
    ) => serviceCollection.NewBuilder<BffLibrary>();

    #region AddBffBlazorLibrary Tests

    [Fact]
    public void AddBffBlazorLibrary_ReplacesAuthenticationStateProvider()
    {
        var serviceCollection = new ServiceCollection();
        CreateBffBuilder(serviceCollection).AddBffBlazorLibrary();

        var descriptor = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(AuthenticationStateProvider)
        );

        Assert.Equal(typeof(BffAuthenticationStateProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBffBlazorLibrary_AfterRazorComponents_ReplacesAuthenticationStateProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddRazorComponents().AddInteractiveServerComponents();
        CreateBffBuilder(serviceCollection).AddBffBlazorLibrary();

        var descriptor = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(AuthenticationStateProvider)
        );

        Assert.Equal(typeof(BffAuthenticationStateProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBffBlazorLibrary_RegistersCircuitHandler()
    {
        var serviceCollection = new ServiceCollection();
        CreateBffBuilder(serviceCollection).AddBffBlazorLibrary();

        var descriptor = Assert.Single(
            serviceCollection,
            x =>
                x.ServiceType == typeof(CircuitHandler)
                && x.ImplementationType == typeof(BffCircuitHandler)
        );

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBffBlazorLibrary_CalledTwice_Throws()
    {
        var serviceCollection = new ServiceCollection();
        var builder = CreateBffBuilder(serviceCollection);
        builder.AddBffBlazorLibrary();

        Assert.Throws<InvalidOperationException>(() => builder.AddBffBlazorLibrary());
    }

    #endregion
}
