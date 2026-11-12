// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.EventSource;
using Microsoft.Extensions.Options;
using NCode.Identity.Dispatch.Options;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests;

// The reference host logs only to standard output and to a diagnostics tool that attaches (project-template ADR-0115).
/// <summary>
/// Provides unit tests for the <c>Program</c> entry point of the host project.
/// </summary>
public class ProgramTests
{
    #region Logging Tests

    [Fact]
    public async Task Logging_WhenTheHostIsBuilt_RegistersOnlyTheConsoleAndEventSourceProviders()
    {
        await using var factory = new HostApplicationFactory();

        var providers = factory
            .Services.GetServices<ILoggerProvider>()
            .Select(provider => provider.GetType())
            .OrderBy(type => type.Name, StringComparer.Ordinal);

        Assert.Equal([typeof(ConsoleLoggerProvider), typeof(EventSourceLoggerProvider)], providers);
    }

    [Fact]
    public async Task Logging_WhenTheJsonFormatterIsSelected_WritesEachEntryOnOneLine()
    {
        // The values the host image sets through its Logging__Console__ environment variables (ADR-1248).
        await using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Logging:Console:FormatterName"] = "json",
                ["Logging:Console:FormatterOptions:UseUtcTimestamp"] = "true",
                ["Logging:Console:FormatterOptions:TimestampFormat"] = "O",
            }
        );

        var optionsMonitor = factory.Services.GetRequiredService<
            IOptionsMonitor<ConsoleLoggerOptions>
        >();
        var formatterName = optionsMonitor.CurrentValue.FormatterName;
        Assert.Equal("json", formatterName);

        var formatter = factory
            .Services.GetServices<ConsoleFormatter>()
            .Single(candidate => candidate.Name == formatterName);

        var exception = CreateException();
        var entry = new LogEntry<string>(
            LogLevel.Error,
            "Test",
            new EventId(1),
            "message",
            exception,
            static (state, _) => state
        );
        using var writer = new StringWriter();
        formatter.Write(in entry, null, writer);
        var text = writer.ToString().TrimEnd('\r', '\n');

        Assert.DoesNotContain("\n", text);
        var jsonElement = JsonElement.Parse(text);
        var exceptionText = jsonElement.GetProperty("Exception").GetString();
        var timestamp = jsonElement.GetProperty("Timestamp").GetString();
        Assert.Contains("second line", exceptionText);
        Assert.EndsWith("+00:00", timestamp);
    }

    #endregion

    #region Server Header Tests

    [Fact]
    public async Task Kestrel_WhenTheHostIsBuilt_SendsNoServerHeader()
    {
        await using var factory = new HostApplicationFactory();

        var options = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;

        Assert.False(options.AddServerHeader);
    }

    #endregion

    #region Configuration Tests

    [Fact]
    public async Task Configuration_WhenTheInProcessJobDispatcherIsConfigured_BindsIt()
    {
        await using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:InProcessJobDispatcher:MaxConcurrentJobs"] = "4",
                ["OpenId:InProcessJobDispatcher:FullMode"] = "DropOldest",
            }
        );

        var options = factory
            .Services.GetRequiredService<IOptions<InProcessJobDispatcherOptions>>()
            .Value;

        Assert.Equal(4, options.MaxConcurrentJobs);
        Assert.Equal(BoundedChannelFullMode.DropOldest, options.FullMode);
    }

    #endregion

    #region RemoteKeyProviders Tests

    [Theory]
    [InlineData(
        "SecretProtection:RemoteKeyProviders:AwsKms:Region",
        "us-west-2",
        RemoteKeyProviderNames.AwsKms
    )]
    [InlineData(
        "SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri",
        "https://contoso.vault.azure.net",
        RemoteKeyProviderNames.AzureKeyVault
    )]
    [InlineData(
        "SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing",
        "projects/p/locations/l/keyRings/r",
        RemoteKeyProviderNames.GoogleCloudKms
    )]
    public async Task RemoteKeyProviders_WhenAVendorSectionIsConfigured_StartsWithItsProviderRegistered(
        string key,
        string value,
        string providerName
    )
    {
        await using var host = new HostApplicationFactory();
        await using var factory = host.WithWebHostBuilder(builder =>
            UseRemoteKeyProviderSettings(builder, new KeyValuePair<string, string>(key, value))
        );

        var catalog = factory.Services.GetRequiredService<IRemoteKeyProviderCatalog>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/oauth2/jwks");

        Assert.True(catalog.TryGetProvider(providerName, out _));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RemoteKeyProviders_WhenEveryVendorSectionIsConfigured_StartsWithEveryProviderRegistered()
    {
        await using var host = new HostApplicationFactory();
        await using var factory = host.WithWebHostBuilder(builder =>
            UseRemoteKeyProviderSettings(
                builder,
                new KeyValuePair<string, string>(
                    "SecretProtection:RemoteKeyProviders:AwsKms:Region",
                    "us-west-2"
                ),
                new KeyValuePair<string, string>(
                    "SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri",
                    "https://contoso.vault.azure.net"
                ),
                new KeyValuePair<string, string>(
                    "SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing",
                    "projects/p/locations/l/keyRings/r"
                )
            )
        );

        var catalog = factory.Services.GetRequiredService<IRemoteKeyProviderCatalog>();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/oauth2/jwks");

        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AwsKms, out _));
        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AzureKeyVault, out _));
        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.GoogleCloudKms, out _));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The settings land in the host settings that Program.cs reads while its top-level statements run, where the vendor
    // IfConfigured calls check their sections; a configuration overlay arrives only when the host builds.
    private static void UseRemoteKeyProviderSettings(
        IWebHostBuilder builder,
        params KeyValuePair<string, string>[] vendorSettings
    )
    {
        foreach (var (key, value) in vendorSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.UseSetting("SecretProtection:RemoteKeys:Deployment", "program-tests");
        builder.UseSetting("SecretProtection:RemoteKeys:SweepEnabled", "false");
    }

    #endregion

    // An exception that was thrown, so it has a stack trace, with a message that spans two lines.
    private static InvalidOperationException CreateException()
    {
        try
        {
            throw new InvalidOperationException("first line\nsecond line");
        }
        catch (InvalidOperationException exception)
        {
            return exception;
        }
    }
}
