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
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using Xunit;

namespace NCode.Identity.SharedState.Valkey.Tests;

public sealed class DefaultRegistrationTests : BaseTests
{
    private const string ConnectionString = "localhost:6379";

    private IExpiringKeySet AdapterRegisteredBefore { get; }

    private IExpiringCounterSet CounterAdapterRegisteredBefore { get; }

    public DefaultRegistrationTests()
    {
        AdapterRegisteredBefore = CreateStrictMock<IExpiringKeySet>().Object;
        CounterAdapterRegisteredBefore = CreateStrictMock<IExpiringCounterSet>().Object;
    }

    private static IConfiguration CreateConfiguration(
        params IEnumerable<(string Key, string? Value)> values
    ) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => KeyValuePair.Create(x.Key, x.Value)))
            .Build();

    private ServiceCollection AddValkey(IConfiguration configuration)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(AdapterRegisteredBefore);
        serviceCollection.AddSingleton(CounterAdapterRegisteredBefore);
        serviceCollection.AddSharedStateServices().AddValkey(configuration);
        return serviceCollection;
    }

    #region AddValkey Tests

    [Fact]
    public async Task AddValkey_WhenConnectionStringConfigured_ReplacesTheAdapter()
    {
        var serviceCollection = AddValkey(
            CreateConfiguration(("ConnectionStrings:valkey", ConnectionString))
        );

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.IsType<ValkeyExpiringKeySet>(Assert.Single(provider.GetServices<IExpiringKeySet>()));
        Assert.IsType<ValkeyExpiringCounterSet>(
            Assert.Single(provider.GetServices<IExpiringCounterSet>())
        );
    }

    [Fact]
    public async Task AddValkey_WhenConnectionStringConfigured_RegistersAReadinessHealthCheck()
    {
        var serviceCollection = AddValkey(
            CreateConfiguration(("ConnectionStrings:valkey", ConnectionString))
        );

        await using var provider = serviceCollection.BuildServiceProvider();

        var registration = Assert.Single(
            provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
        );
        Assert.Equal(DefaultRegistration.HealthCheckName, registration.Name);
        Assert.Contains(DefaultRegistration.ReadinessTag, registration.Tags);
    }

    [Fact]
    public async Task AddValkey_WhenNoKeyPrefixConfigured_UsesTheDefault()
    {
        var serviceCollection = AddValkey(
            CreateConfiguration(("ConnectionStrings:valkey", ConnectionString))
        );

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.Equal(
            ValkeyOptions.DefaultKeyPrefix,
            provider.GetRequiredService<IOptions<ValkeyOptions>>().Value.KeyPrefix
        );
    }

    [Fact]
    public async Task AddValkey_WhenKeyPrefixConfigured_BindsIt()
    {
        var serviceCollection = AddValkey(
            CreateConfiguration(
                ("ConnectionStrings:valkey", ConnectionString),
                ("Valkey:KeyPrefix", "tenant-a:")
            )
        );

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.Equal(
            "tenant-a:",
            provider.GetRequiredService<IOptions<ValkeyOptions>>().Value.KeyPrefix
        );
    }

    [Fact]
    public async Task AddValkey_WhenSectionHasAnUnknownKey_FailsToBind()
    {
        var serviceCollection = AddValkey(
            CreateConfiguration(
                ("ConnectionStrings:valkey", ConnectionString),
                ("Valkey:KeyPrefx", "typo:")
            )
        );

        await using var provider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IOptions<ValkeyOptions>>().Value
        );
        Assert.Contains("Valkey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddValkey_WhenNoConnectionString_KeepsTheAdapterRegisteredBeforeIt()
    {
        var serviceCollection = AddValkey(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.Same(AdapterRegisteredBefore, provider.GetRequiredService<IExpiringKeySet>());
        Assert.Same(
            CounterAdapterRegisteredBefore,
            provider.GetRequiredService<IExpiringCounterSet>()
        );
        Assert.Null(provider.GetService<IValkeyConnection>());
    }

    [Fact]
    public void AddValkey_WhenSectionPresentWithoutConnectionString_Throws()
    {
        var configuration = CreateConfiguration(("Valkey:KeyPrefix", "tenant-a:"));

        var exception = Assert.Throws<InvalidOperationException>(() => AddValkey(configuration));

        Assert.Contains("ConnectionStrings:valkey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddValkey_WhenConnectionStringMalformed_ThrowsWithoutEchoingIt()
    {
        const string Malformed = "localhost:6379,password=secret,unknownSetting=1";
        var configuration = CreateConfiguration(("ConnectionStrings:valkey", Malformed));

        var exception = Assert.Throws<InvalidOperationException>(() => AddValkey(configuration));

        Assert.Contains("ConnectionStrings:valkey", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddValkey_WhenConnectionStringNamesNoServer_Throws()
    {
        var configuration = CreateConfiguration(("ConnectionStrings:valkey", "ssl=true"));

        var exception = Assert.Throws<InvalidOperationException>(() => AddValkey(configuration));

        Assert.Contains("names no server", exception.Message, StringComparison.Ordinal);
    }

    #endregion
}
