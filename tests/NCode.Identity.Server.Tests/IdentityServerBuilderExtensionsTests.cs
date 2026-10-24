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

using System.Threading.Channels;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Dispatch.Options;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Frontend;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class IdentityServerBuilderExtensionsTests : BaseTests
{
    #region AddConfiguration Tests

    [Fact]
    public void AddConfiguration_WhenTenantResolutionConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = "StaticSingle",
                ["OpenId:TenantResolution:Root:TenantId"] = "control",
                ["OpenId:TenantResolution:Root:DisplayName"] = "Control Plane",
                ["OpenId:TenantResolution:StaticSingle:TenantId"] = "acme",
                ["OpenId:TenantResolution:StaticSingle:TenantPath"] = "/identity",
                ["OpenId:TenantResolution:StaticSingle:Issuer"] = "https://op.example/identity",
            }
        );

        var options = serviceProvider.GetRequiredService<IOptions<TenantResolutionOptions>>().Value;

        Assert.Equal("StaticSingle", options.StrategyCode);
        Assert.Equal("control", options.Root.TenantId);
        Assert.Equal("Control Plane", options.Root.DisplayName);
        Assert.NotNull(options.StaticSingle);
        Assert.Equal("acme", options.StaticSingle.TenantId);
        Assert.Equal("/identity", options.StaticSingle.TenantPath);
        Assert.Equal("https://op.example/identity", options.StaticSingle.Issuer);
    }

    [Fact]
    public void AddConfiguration_WhenTenantResolutionIsPlacedUnderTenant_Throws()
    {
        // The shape the reference host once shipped: tenant selection written into the materialization section, where
        // it bound to nothing and was silently ignored.
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:Tenant:Mode"] = "StaticSingle",
                ["OpenId:Tenant:StaticSingle:TenantPath"] = "/",
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<OpenIdOptions>>().Value
        );

        Assert.Contains("'OpenId:Tenant'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Mode'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'StaticSingle'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenTenantResolutionHasUnknownKey_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?> { ["OpenId:TenantResolution:Mode"] = "StaticSingle" }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<TenantResolutionOptions>>().Value
        );

        Assert.Contains("'OpenId:TenantResolution'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Mode'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenStrategyOptionsHaveUnknownKey_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StaticSingle:Path"] = "/identity",
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<TenantResolutionOptions>>().Value
        );

        Assert.Contains("'Path'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenOpenIdSectionHasUnknownSubsection_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolutions:StrategyCode"] = "DynamicByHost",
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<OpenIdOptions>>().Value
        );

        Assert.Contains("'OpenId:TenantResolutions'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenServerAndTenantConfigured_BindsThem()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:Server:ServerId"] = "primary",
                ["OpenId:Server:ReconciliationLeaseDuration"] = "00:20:00",
                ["OpenId:Server:Settings:grant_types_supported:0"] = "authorization_code",
                ["OpenId:Tenant:TenantCacheLifetime"] = "00:30:00",
            }
        );

        var options = serviceProvider.GetRequiredService<IOptions<OpenIdOptions>>().Value;

        Assert.Equal("primary", options.Server.ServerId);
        Assert.Equal(TimeSpan.FromMinutes(20), options.Server.ReconciliationLeaseDuration);
        Assert.Equal(TimeSpan.FromMinutes(30), options.Tenant.TenantCacheLifetime);
        Assert.NotNull(options.Server.Settings);
        Assert.Equal("OpenId:Server:Settings", options.Server.Settings.Path);
        Assert.Equal("authorization_code", options.Server.Settings["grant_types_supported:0"]);
    }

    [Fact]
    public void AddConfiguration_WhenServerSectionAbsent_StillAssignsTheSettingsSection()
    {
        using var serviceProvider = BuildConfiguredServiceProvider([]);

        var options = serviceProvider.GetRequiredService<IOptions<OpenIdOptions>>().Value;

        Assert.NotNull(options.Server.Settings);
        Assert.Equal("OpenId:Server:Settings", options.Server.Settings.Path);
    }

    [Fact]
    public void AddConfiguration_WhenMutualTlsConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:MutualTls:CertificateHeaderName"] = "X-Client-Cert",
            }
        );

        var options = serviceProvider
            .GetRequiredService<IOptions<MutualTlsForwardingOptions>>()
            .Value;

        Assert.Equal("X-Client-Cert", options.CertificateHeaderName);
    }

    [Fact]
    public void AddConfiguration_WhenGrantRetentionConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:GrantRetention:Retention"] = "7.00:00:00",
                ["OpenId:GrantRetention:PruneInterval"] = "00:30:00",
            }
        );

        var options = serviceProvider.GetRequiredService<IOptions<GrantRetentionOptions>>().Value;

        Assert.Equal(TimeSpan.FromDays(7), options.Retention);
        Assert.Equal(TimeSpan.FromMinutes(30), options.PruneInterval);
    }

    [Fact]
    public void AddConfiguration_WhenGrantRetentionHasUnknownKey_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?> { ["OpenId:GrantRetention:Interval"] = "00:30:00" }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<GrantRetentionOptions>>().Value
        );

        Assert.Contains("'OpenId:GrantRetention'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'Interval'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenClientCertificateRevocationConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:ClientCertificateRevocation:FetchTimeout"] = "00:00:10",
                ["OpenId:ClientCertificateRevocation:FailureCacheDuration"] = "00:05:00",
            }
        );

        var options = serviceProvider
            .GetRequiredService<IOptions<ClientCertificateRevocationOptions>>()
            .Value;

        Assert.Equal(TimeSpan.FromSeconds(10), options.FetchTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), options.FailureCacheDuration);
    }

    [Fact]
    public void AddConfiguration_WhenClientCertificateRevocationHasUnknownKey_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:ClientCertificateRevocation:Timeout"] = "00:00:10",
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<ClientCertificateRevocationOptions>>().Value
        );

        Assert.Contains(
            "'OpenId:ClientCertificateRevocation'",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("'Timeout'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenEndedSessionTerminationConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:EndedSessionTermination:Interval"] = "00:05:00",
            }
        );

        var options = serviceProvider
            .GetRequiredService<IOptions<EndedSessionTerminationOptions>>()
            .Value;

        Assert.Equal(TimeSpan.FromMinutes(5), options.Interval);
    }

    [Fact]
    public void AddConfiguration_WhenEndedSessionTerminationHasUnknownKey_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:EndedSessionTermination:PruneInterval"] = "00:05:00",
            }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<EndedSessionTerminationOptions>>().Value
        );

        Assert.Contains(
            "'OpenId:EndedSessionTermination'",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("'PruneInterval'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenTenantUsageConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantUsage:FlushInterval"] = "00:00:30",
                ["OpenId:TenantUsage:Retention"] = "30.00:00:00",
            }
        );

        var options = serviceProvider.GetRequiredService<IOptions<TenantUsageOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(30), options.FlushInterval);
        Assert.Equal(TimeSpan.FromDays(30), options.Retention);
    }

    [Fact]
    public void AddConfiguration_WhenInProcessJobDispatcherConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["OpenId:InProcessJobDispatcher:QueueCapacity"] = "64",
                ["OpenId:InProcessJobDispatcher:FullMode"] = "DropOldest",
                ["OpenId:InProcessJobDispatcher:MaxConcurrentJobs"] = "4",
            }
        );

        var options = serviceProvider
            .GetRequiredService<IOptions<InProcessJobDispatcherOptions>>()
            .Value;

        Assert.Equal(64, options.QueueCapacity);
        Assert.Equal(BoundedChannelFullMode.DropOldest, options.FullMode);
        Assert.Equal(4, options.MaxConcurrentJobs);
    }

    [Fact]
    public void AddConfiguration_WhenInProcessJobDispatcherHasUnknownKey_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?> { ["OpenId:InProcessJobDispatcher:Capacity"] = "64" }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<InProcessJobDispatcherOptions>>().Value
        );

        Assert.Contains(
            "'OpenId:InProcessJobDispatcher'",
            exception.Message,
            StringComparison.Ordinal
        );
        Assert.Contains("'Capacity'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenBootstrapAdminConfigured_BindsIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = "bootstrap",
                ["BootstrapAdmin:ClientSecret"] = "c2VjcmV0",
            }
        );

        var options = serviceProvider.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;

        Assert.Equal("bootstrap", options.ClientId);
        Assert.True(options.IsEnabled);
    }

    [Fact]
    public void AddConfiguration_WhenBootstrapAdminHasUnknownKey_Throws()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?> { ["BootstrapAdmin:Secret"] = "c2VjcmV0" }
        );

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value
        );

        Assert.Contains("'BootstrapAdmin'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddConfiguration_WhenSectionNameSpecified_BindsEverySubsectionFromIt()
    {
        using var serviceProvider = BuildConfiguredServiceProvider(
            new Dictionary<string, string?>
            {
                ["Identity:Server:ServerId"] = "primary",
                ["Identity:TenantResolution:StaticSingle:TenantId"] = "acme",
                ["Identity:MutualTls:CertificateHeaderName"] = "X-Client-Cert",
                ["OpenId:Unrelated"] = "ignored",
            },
            sectionName: "Identity"
        );

        var openIdOptions = serviceProvider.GetRequiredService<IOptions<OpenIdOptions>>().Value;
        var tenantResolutionOptions = serviceProvider
            .GetRequiredService<IOptions<TenantResolutionOptions>>()
            .Value;
        var mutualTlsOptions = serviceProvider
            .GetRequiredService<IOptions<MutualTlsForwardingOptions>>()
            .Value;

        Assert.Equal("primary", openIdOptions.Server.ServerId);
        Assert.Equal("Identity:Server:Settings", openIdOptions.Server.Settings?.Path);
        Assert.Equal("acme", tenantResolutionOptions.StaticSingle?.TenantId);
        Assert.Equal("X-Client-Cert", mutualTlsOptions.CertificateHeaderName);
    }

    [Fact]
    public async Task AddConfiguration_WhenHostStartsWithUnknownKey_FailsToStart()
    {
        // The shape the reference host once shipped. Strict binding alone would only throw on first use; the host must
        // refuse to start instead.
        // The provider has no setup: the strict mock proves the warmup never runs for an invalid configuration.
        using var host = BuildHost(
            new Dictionary<string, string?> { ["OpenId:Tenant:Mode"] = "StaticSingle" },
            out _
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            host.StartAsync()
        );

        Assert.Contains("'OpenId:Tenant'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddConfiguration_WhenHostStartsWithValidConfiguration_Starts()
    {
        using var host = BuildHost(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = "StaticSingle",
                ["OpenId:TenantResolution:StaticSingle:TenantPath"] = "/",
            },
            out var secretProtectorProvider
        );

        // The warmup that AddConfiguration registers loads the protector of the server when the host starts.
        secretProtectorProvider
            .Setup(x =>
                x.GetProtectorAsync(
                    SecretOwner.ForServer(OpenIdServerOptions.DefaultServerId),
                    It.Is<IReadOnlyCollection<string>>(values => values.Count == 0),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<ISecretProtector>(CreateStrictMock<ISecretProtector>().Object))
            .Verifiable();

        await host.StartAsync();
        await host.StopAsync();
    }

    private IHost BuildHost(
        Dictionary<string, string?> configurationData,
        out Mock<ISecretProtectorProvider> secretProtectorProvider
    )
    {
        var hostBuilder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        hostBuilder.Configuration.AddInMemoryCollection(configurationData);

        // A real host composes the provider through AddIdentityServer; this bare host supplies a strict stand-in.
        secretProtectorProvider = CreateStrictMock<ISecretProtectorProvider>();
        hostBuilder.Services.AddSingleton(secretProtectorProvider.Object);

        // A started host reads its Data Protection key ring, which would otherwise be the machine's key directory.
        hostBuilder.Services.Configure<KeyManagementOptions>(options =>
            options.XmlRepository = new InMemoryXmlRepository()
        );

        var builder = CreateStrictMock<IIdentityServerBuilder>();
        builder.Setup(x => x.ServiceCollection).Returns(hostBuilder.Services).Verifiable();

        builder.Object.AddConfiguration(hostBuilder.Configuration);

        return hostBuilder.Build();
    }

    private ServiceProvider BuildConfiguredServiceProvider(
        Dictionary<string, string?> configurationData,
        string? sectionName = null
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var serviceCollection = new ServiceCollection();
        var builder = CreateStrictMock<IIdentityServerBuilder>();
        builder.Setup(x => x.ServiceCollection).Returns(serviceCollection).Verifiable();

        var result = builder.Object.AddConfiguration(configuration, sectionName);
        Assert.Same(builder.Object, result);

        return serviceCollection.BuildServiceProvider();
    }

    #endregion

    #region AddFrontendBootstrap Tests

    [Fact]
    public void AddFrontendBootstrap_BindsOptionsFromDefaultSection()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?>
            {
                ["Frontend:ClientId"] = "spa",
                ["Frontend:Issuer"] = "https://issuer.example",
                ["Frontend:Features:dcr"] = "true",
                ["Frontend:Branding:DisplayName"] = "Acme",
            }
        );

        Assert.Equal("spa", options.ClientId);
        Assert.Equal("https://issuer.example", options.Issuer);
        Assert.True(options.Features["dcr"]);
        Assert.Equal("Acme", options.Branding?.DisplayName);
    }

    [Fact]
    public void AddFrontendBootstrap_BindsOptionsFromCustomSection()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?> { ["Ui:ClientId"] = "custom-spa" },
            sectionName: "Ui"
        );

        Assert.Equal("custom-spa", options.ClientId);
    }

    [Fact]
    public void AddFrontendBootstrap_AppliesConfigureOverrideAfterBinding()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?> { ["Frontend:ClientId"] = "from-config" },
            configureOptions: o => o.ClientId = "from-code"
        );

        Assert.Equal("from-code", options.ClientId);
    }

    [Fact]
    public void AddFrontendBootstrap_WhenSectionHasUnknownKey_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ResolveOptions(new Dictionary<string, string?> { ["Frontend:ClientID2"] = "spa" })
        );

        Assert.Contains("'Frontend'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'ClientID2'", exception.Message, StringComparison.Ordinal);
    }

    private FrontendBootstrapOptions ResolveOptions(
        Dictionary<string, string?> configurationData,
        string? sectionName = null,
        Action<FrontendBootstrapOptions>? configureOptions = null
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var serviceCollection = new ServiceCollection();
        var builder = CreateStrictMock<IIdentityServerBuilder>();
        builder.Setup(x => x.ServiceCollection).Returns(serviceCollection).Verifiable();

        var result = builder.Object.AddFrontendBootstrap(
            configuration,
            sectionName,
            configureOptions
        );
        Assert.Same(builder.Object, result);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IOptions<FrontendBootstrapOptions>>().Value;
    }

    #endregion

    // A key ring repository that keeps its elements in memory, so a started host never reads the machine's key
    // directory. It hands out copies, as a database would.
    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private List<XElement> Elements { get; } = [];

        public IReadOnlyCollection<XElement> GetAllElements() =>
            Elements.Select(element => new XElement(element)).ToList();

        public void StoreElement(XElement element, string friendlyName) =>
            Elements.Add(new XElement(element));
    }
}
