// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public sealed class HttpLoggingTests
{
    private const string ClientSecret = "client-secret-sentinel-778";

    private const string BasicSecret = "basic-secret-sentinel-778";

    private const string DPoPProof = "dpop-sentinel-778";

    private const string HttpLoggingCategory =
        "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";

    private static string BasicParameter { get; } =
        Convert.ToBase64String(SecureEncoding.UTF8.GetBytes($"log-probe:{BasicSecret}"));

    // Qualified because the NCode.Identity.OpenId.Environments namespace shadows the framework's Environments class.
    private static string StagingEnvironment { get; } =
        Microsoft.Extensions.Hosting.Environments.Staging;

    #region PostToken Tests

    [Fact]
    public async Task PostToken_WithDebugLoggingOutsideDevelopment_WritesNoBodyAndNoCredentialHeaderValue()
    {
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"] = "Debug",
                ["Logging:LogLevel:Microsoft"] = "Debug",
            }
        );
        using var staging = factory.WithWebHostBuilder(builder =>
            builder
                .UseEnvironment(StagingEnvironment)
                .ConfigureLogging(logging => logging.AddFakeLogging())
        );
        using var client = staging.CreateClient();
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "log-probe",
                ["client_secret"] = ClientSecret,
            }
        );
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            OpenIdConstants.EndpointPaths.Token
        )
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", BasicParameter);
        request.Headers.TryAddWithoutValidation("DPoP", DPoPProof);

        using var response = await client.SendAsync(request);

        var records = staging.Services.GetFakeLogCollector().GetSnapshot();
        Assert.Contains(
            records,
            x =>
                x.Category == HttpLoggingCategory
                && x.Message.Contains(OpenIdConstants.EndpointPaths.Token, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains("RequestBody:", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains("ResponseBody:", StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(ClientSecret, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(BasicSecret, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(BasicParameter, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(DPoPProof, StringComparison.Ordinal)
        );
    }

    #endregion
}
