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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Events;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets;
using NCode.Identity.SharedState;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Bff.Tests;

public sealed class DefaultRegistrationTests : IAsyncLifetime
{
    private ServiceCollection Services { get; } = new();

    private IServiceBuilder<OpenIdClientLibrary> ClientBuilder { get; }

    private IServiceBuilder<EventsLibrary> EventsBuilder { get; }

    private ServiceProvider? Provider { get; set; }

    public DefaultRegistrationTests()
    {
        Services.AddLogging();
        ClientBuilder = Services
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        EventsBuilder = Services.AddEventServices();

        Services
            .AddSharedStateServices()
            .AddInProcessExpiringKeySet()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        Services.AddSingleton(new Mock<IHostEnvironment>().Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Provider is not null)
            await Provider.DisposeAsync();
    }

    private ServiceProvider BuildProvider()
    {
        Provider = Services.BuildServiceProvider();
        return Provider;
    }

    #region AddBffLibrary Tests

    [Fact]
    public void AddBffLibrary_RegistersTheSessionServices()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IBffSessionStore>());
        Assert.NotNull(provider.GetRequiredService<IBffSessionManager>());
    }

    [Fact]
    public void AddBffLibrary_WhenCalledTwice_Throws()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        Assert.Throws<InvalidOperationException>(() => ClientBuilder.AddBffLibrary(EventsBuilder));
    }

    #endregion

    #region AddBffConfiguration Tests

    [Fact]
    public void AddBffConfiguration_WithUnknownKey_ThrowsOnValue()
    {
        const string key = $"{BffDefaults.ConfigurationSectionName}:NoSuchKey";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = "x" })
            .Build();
        ClientBuilder.AddBffLibrary(EventsBuilder).AddBffConfiguration(configuration);

        var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<BffOptions>>();
        Assert.ThrowsAny<Exception>(() => options.Value);
    }

    #endregion
}
