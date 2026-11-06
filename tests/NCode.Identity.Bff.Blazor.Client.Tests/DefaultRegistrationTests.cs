// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Blazor.Client.Options;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class DefaultRegistrationTests
{
    #region AddBffBlazorClient Tests

    [Fact]
    public void AddBffBlazorClient_RegistersProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<AuthenticationStateProvider, ServerAuthenticationStateProvider>();

        var builder = services.AddBffBlazorClient();

        Assert.NotNull(builder);
        Assert.True(services.IsRegistered<BffBlazorClientLibrary>());
        var descriptor = Assert.Single(
            services,
            x => x.ServiceType == typeof(AuthenticationStateProvider)
        );
        Assert.Equal(typeof(BffClientAuthenticationStateProvider), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(IValidateOptions<BffBlazorClientOptions>)
                && x.ImplementationType == typeof(BffBlazorClientOptionsValidator)
        );
        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(BffClientSession) && x.Lifetime == ServiceLifetime.Singleton
        );
        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(BffAntiforgeryHeaderHandler)
                && x.Lifetime == ServiceLifetime.Transient
        );
    }

    [Fact]
    public void AddBffBlazorClient_CalledTwice_Throws()
    {
        var services = new ServiceCollection();
        services.AddBffBlazorClient();

        Assert.Throws<InvalidOperationException>(() => services.AddBffBlazorClient());
    }

    #endregion

    private sealed class ServerAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            throw new NotSupportedException();
    }
}
