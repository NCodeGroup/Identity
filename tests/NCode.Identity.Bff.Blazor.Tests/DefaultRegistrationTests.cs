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
    private static IServiceBuilder<BffLibrary> CreateBffBuilder(ServiceCollection services) =>
        services.NewBuilder<BffLibrary>();

    #region AddBffBlazorLibrary

    [Fact]
    public void AddBffBlazorLibrary_ReplacesAuthenticationStateProvider()
    {
        var services = new ServiceCollection();
        CreateBffBuilder(services).AddBffBlazorLibrary();

        var descriptor = Assert.Single(
            services,
            x => x.ServiceType == typeof(AuthenticationStateProvider)
        );

        Assert.Equal(typeof(BffAuthenticationStateProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBffBlazorLibrary_AfterRazorComponents_ReplacesAuthenticationStateProvider()
    {
        var services = new ServiceCollection();
        services.AddRazorComponents().AddInteractiveServerComponents();
        CreateBffBuilder(services).AddBffBlazorLibrary();

        var descriptor = Assert.Single(
            services,
            x => x.ServiceType == typeof(AuthenticationStateProvider)
        );

        Assert.Equal(typeof(BffAuthenticationStateProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBffBlazorLibrary_RegistersCircuitHandler()
    {
        var services = new ServiceCollection();
        CreateBffBuilder(services).AddBffBlazorLibrary();

        var descriptor = Assert.Single(
            services,
            x =>
                x.ServiceType == typeof(CircuitHandler)
                && x.ImplementationType == typeof(BffCircuitHandler)
        );

        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void AddBffBlazorLibrary_CalledTwice_Throws()
    {
        var services = new ServiceCollection();
        var builder = CreateBffBuilder(services);
        builder.AddBffBlazorLibrary();

        Assert.Throws<InvalidOperationException>(() => builder.AddBffBlazorLibrary());
    }

    #endregion
}
