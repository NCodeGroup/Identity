// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class GuardedHttpLoggingRegistrationTests : BaseTests
{
    private const string RequestBodySentinel = "request-sentinel";
    private const string ResponseBodySentinel = "response-sentinel";
    private const string QuerySentinel = "query-sentinel";
    private const string AuthorizationSentinel = "authorization-sentinel";
    private const string DPoPSentinel = "dpop-sentinel";
    private const string CookieSentinel = "cookie-sentinel";
    private const string LocationSentinel = "location-sentinel";

    private static Endpoint CreateEndpoint(params object[] metadata) =>
        new(null, new EndpointMetadataCollection(metadata), "probe");

    #region UseHttpLogging Tests

    [Fact]
    public async Task UseHttpLogging_WhenTheEndpointOptsInToBothBodies_LogsBothBodies()
    {
        var endpoint = CreateEndpoint(
            HttpBodyLoggingMetadata.RequestBody,
            HttpBodyLoggingMetadata.ResponseBody
        );

        var records = await SendAsync(endpoint, CancellationToken.None);

        Assert.Contains(
            records,
            x => x.Message.Contains(RequestBodySentinel, StringComparison.Ordinal)
        );
        Assert.Contains(
            records,
            x => x.Message.Contains(ResponseBodySentinel, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task UseHttpLogging_WhenTheEndpointOptsInToTheRequestBodyOnly_LogsNoResponseBody()
    {
        var endpoint = CreateEndpoint(HttpBodyLoggingMetadata.RequestBody);

        var records = await SendAsync(endpoint, CancellationToken.None);

        Assert.Contains(
            records,
            x => x.Message.Contains(RequestBodySentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(ResponseBodySentinel, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task UseHttpLogging_WhenTheEndpointDoesNotOptIn_LogsNoBody()
    {
        var endpoint = CreateEndpoint();

        var records = await SendAsync(endpoint, CancellationToken.None);

        Assert.Contains(records, x => x.Message.Contains("/probe", StringComparison.Ordinal));
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(RequestBodySentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(ResponseBodySentinel, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task UseHttpLogging_WhenNoEndpointIsSelected_LogsNoBody()
    {
        var records = await SendAsync(endpoint: null, CancellationToken.None);

        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(RequestBodySentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(ResponseBodySentinel, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task UseHttpLogging_WhenTheEndpointHasConfidentialBodies_LogsNoBody()
    {
        var endpoint = CreateEndpoint(
            HttpBodyLoggingMetadata.RequestBody,
            HttpBodyLoggingMetadata.ResponseBody,
            ConfidentialBodyMetadata.Instance
        );

        var records = await SendAsync(endpoint, CancellationToken.None);

        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(RequestBodySentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(ResponseBodySentinel, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task UseHttpLogging_WhenTheFrameworkAttributeEnablesEveryField_LogsNoBodyAndNoQuery()
    {
        var endpoint = CreateEndpoint(new HttpLoggingAttribute(HttpLoggingFields.All));

        var records = await SendAsync(endpoint, CancellationToken.None);

        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(RequestBodySentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(ResponseBodySentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(QuerySentinel, StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task UseHttpLogging_WhenTheEndpointOptsInToBothBodies_WritesNoCredentialHeaderValue()
    {
        var endpoint = CreateEndpoint(
            HttpBodyLoggingMetadata.RequestBody,
            HttpBodyLoggingMetadata.ResponseBody
        );

        var records = await SendAsync(endpoint, CancellationToken.None);

        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(AuthorizationSentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(DPoPSentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(CookieSentinel, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(LocationSentinel, StringComparison.Ordinal)
        );
    }

    #endregion

    #region AddGuardedHttpLogging Tests

    [Fact]
    public async Task AddGuardedHttpLogging_Always_PermitsOnlyPropertiesHeadersAndDuration()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddGuardedHttpLogging();

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<HttpLoggingOptions>>().Value;

        Assert.Equal(
            HttpLoggingFields.RequestPropertiesAndHeaders
                | HttpLoggingFields.ResponsePropertiesAndHeaders
                | HttpLoggingFields.Duration,
            options.LoggingFields
        );
        Assert.DoesNotContain(
            "Location",
            options.ResponseHeaders,
            StringComparer.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task AddGuardedHttpLogging_WhenABodyFieldIsConfigured_FailsValidation()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddGuardedHttpLogging();
        serviceCollection.Configure<HttpLoggingOptions>(x =>
            x.LoggingFields |= HttpLoggingFields.RequestBody
        );

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<HttpLoggingOptions>>();

        Assert.Throws<OptionsValidationException>(() => options.Value);
    }

    [Fact]
    public async Task AddGuardedHttpLogging_WhenACredentialHeaderIsAllowListed_StopsTheHostFromStarting()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddGuardedHttpLogging();
        builder.Services.Configure<HttpLoggingOptions>(x =>
            x.RequestHeaders.Add(HeaderNames.Authorization)
        );

        using var host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() =>
            host.StartAsync(CancellationToken.None)
        );
    }

    [Fact]
    public async Task AddGuardedHttpLogging_WithPermittedOptions_LetsTheHostStart()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Services.AddGuardedHttpLogging();

        using var host = builder.Build();

        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AddGuardedHttpLogging_WhenTheDefaultLevelIsRaised_CapsTheHostingDiagnosticsCategoryAtWarning()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(x => x.AddFakeLogging().SetMinimumLevel(LogLevel.Debug));
        serviceCollection.AddGuardedHttpLogging();
        serviceCollection.AddLogging(x => x.AddFilter(null, LogLevel.Debug));

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var records = LogHostingDiagnostics(serviceProvider);

        AssertOnlyTheWarningIsWritten(records);
    }

    [Theory]
    [InlineData(false)]
    public async Task AddGuardedHttpLogging_WhenAProviderScopedLevelIsRaised_CapsTheHostingDiagnosticsCategoryAtWarning(
        bool filterFirst
    )
    {
        var serviceCollection = new ServiceCollection();
        if (filterFirst)
        {
            serviceCollection.AddLogging(x =>
                x.AddFakeLogging().AddFilter<FakeLoggerProvider>(level => level >= LogLevel.Debug)
            );
            serviceCollection.AddGuardedHttpLogging();
        }
        else
        {
            serviceCollection.AddLogging(x => x.AddFakeLogging());
            serviceCollection.AddGuardedHttpLogging();
            serviceCollection.AddLogging(x =>
                x.AddFilter<FakeLoggerProvider>(level => level >= LogLevel.Debug)
            );
        }

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var records = LogHostingDiagnostics(serviceProvider);

        AssertOnlyTheWarningIsWritten(records);
    }

    [Fact]
    public async Task AddGuardedHttpLogging_WhenTheHostingDiagnosticsCategoryIsConfiguredAtInformation_CapsItAtWarning()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"Logging:LogLevel:{GuardedHttpLoggingDefaults.HostingDiagnosticsCategory}"] =
                        "Information",
                }
            )
            .Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(x => x.AddConfiguration(configuration.GetSection("Logging")));
        serviceCollection.AddLogging(x => x.AddFakeLogging());
        serviceCollection.AddGuardedHttpLogging();

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var records = LogHostingDiagnostics(serviceProvider);

        AssertOnlyTheWarningIsWritten(records);
    }

    #endregion

    private static IReadOnlyList<FakeLogRecord> LogHostingDiagnostics(
        ServiceProvider serviceProvider
    )
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger(
            GuardedHttpLoggingDefaults.HostingDiagnosticsCategory
        );

        logger.Log(LogLevel.Information, 0, "information-record", null, (state, _) => state);
        logger.Log(LogLevel.Warning, 0, "warning-record", null, (state, _) => state);

        return serviceProvider.GetFakeLogCollector().GetSnapshot();
    }

    private static void AssertOnlyTheWarningIsWritten(IReadOnlyList<FakeLogRecord> records)
    {
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains("information-record", StringComparison.Ordinal)
        );
        Assert.Contains(
            records,
            x => x.Message.Contains("warning-record", StringComparison.Ordinal)
        );
    }

    private static async ValueTask<IReadOnlyList<FakeLogRecord>> SendAsync(
        Endpoint? endpoint,
        CancellationToken cancellationToken
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(x => x.AddFakeLogging());
        serviceCollection.AddGuardedHttpLogging();

        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var app = new ApplicationBuilder(serviceProvider);
        app.Use(
            (context, next) =>
            {
                context.SetEndpoint(endpoint);
                return next(context);
            }
        );
        app.UseHttpLogging();
        app.Run(async context =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null, context.RequestAborted);

            context.Response.Headers.Location =
                $"https://client.example/cb?code={LocationSentinel}";
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"value":"{{ResponseBodySentinel}}"}""",
                context.RequestAborted
            );
        });

        var requestBody = SecureEncoding.UTF8.GetBytes($$"""{"value":"{{RequestBodySentinel}}"}""");

        var context = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            RequestAborted = cancellationToken,
        };
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/probe";
        context.Request.QueryString = new QueryString($"?code={QuerySentinel}");
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = requestBody.Length;
        context.Request.Body = new MemoryStream(requestBody);
        context.Request.Headers.Authorization = $"Bearer {AuthorizationSentinel}";
        context.Request.Headers["DPoP"] = DPoPSentinel;
        context.Request.Headers.Cookie = $"session={CookieSentinel}";
        context.Response.Body = new MemoryStream();

        await app.Build()(context);

        return serviceProvider.GetFakeLogCollector().GetSnapshot();
    }
}
