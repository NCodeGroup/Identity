// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public sealed class HttpLoggingTests
{
    private const string ClientSecret = "client-secret-sentinel-778";

    private const string BasicSecret = "basic-secret-sentinel-778";

    private const string DPoPProof = "dpop-sentinel-778";

    private const string IdTokenHint = "id-token-hint-sentinel-778";

    private const string LoginHint = "login-hint-sentinel-778";

    private const string HttpLoggingCategory =
        "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware";

    private static string BasicParameter { get; } =
        Convert.ToBase64String(SecureEncoding.UTF8.GetBytes($"log-probe:{BasicSecret}"));

    private static Dictionary<string, string?> DebugLogging { get; } =
        new() { ["Logging:LogLevel:Default"] = "Debug", ["Logging:LogLevel:Microsoft"] = "Debug" };

    #region PostToken Tests

    [Fact]
    public async Task PostToken_WithDebugLoggingOutsideDevelopment_WritesNoBodyAndNoCredentialHeaderValue()
    {
        using var factory = new StrictPostureHostApplicationFactory(DebugLogging);
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddFakeLogging())
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
        // The response record is written, so the absence of a response body is the guard's doing.
        Assert.Contains(
            records,
            x =>
                x.Category == HttpLoggingCategory
                && x.Message.Contains("StatusCode:", StringComparison.Ordinal)
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

    #region GetAuthorization Tests

    [Fact]
    public async Task GetAuthorization_WithDebugLoggingOutsideDevelopment_WritesNoQueryString()
    {
        using var factory = new StrictPostureHostApplicationFactory(DebugLogging);
        using var staging = factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddFakeLogging())
        );
        var clientOptions = new WebApplicationFactoryClientOptions { AllowAutoRedirect = false };
        using var client = staging.CreateClient(clientOptions);
        var requestUri =
            $"{OpenIdConstants.EndpointPaths.Authorization}?client_id=log-probe&response_type=code&scope=openid&id_token_hint={IdTokenHint}&login_hint={LoginHint}";

        using var response = await client.GetAsync(requestUri);

        var records = staging.Services.GetFakeLogCollector().GetSnapshot();
        Assert.Contains(
            records,
            x =>
                x.Category == HttpLoggingCategory
                && x.Message.Contains(
                    OpenIdConstants.EndpointPaths.Authorization,
                    StringComparison.Ordinal
                )
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(IdTokenHint, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            records,
            x => x.Message.Contains(LoginHint, StringComparison.Ordinal)
        );
    }

    #endregion

    #region Endpoint Tests

    [Theory]
    [InlineData(OpenIdConstants.EndpointPaths.Token)]
    [InlineData(OpenIdConstants.EndpointPaths.PushedAuthorizationRequest)]
    [InlineData(OpenIdConstants.EndpointPaths.Introspection)]
    [InlineData(OpenIdConstants.EndpointPaths.Revocation)]
    [InlineData(InteractiveAuthenticationDefaults.LoginPath)]
    [InlineData(InteractiveAuthenticationDefaults.PasswordPath)]
    [InlineData("/local-accounts/{localAccountId}/password")]
    [InlineData("/clients/{clientId}/secrets")]
    public void Endpoint_ThatCarriesCredentials_HasConfidentialBodies(string pathSuffix)
    {
        using var factory = new HostApplicationFactory();

        var endpoints = factory
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .Where(x =>
                x.RoutePattern.RawText?.EndsWith(pathSuffix, StringComparison.Ordinal) == true
            )
            .ToList();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, x => Assert.True(x.HasConfidentialBodies));
    }

    #endregion
}
