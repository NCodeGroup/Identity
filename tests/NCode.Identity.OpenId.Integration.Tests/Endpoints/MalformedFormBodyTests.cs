// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// A form body that is malformed or over the form limits is the client's error, so every endpoint that reads a form
/// answers <c>400</c> instead of failing with a server error.
/// </summary>
public class MalformedFormBodyTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    private HttpClient CreateClient() =>
        Factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private async Task<HttpResponseMessage> PostMalformedFormAsync(
        string path,
        string? sessionCookie = null
    )
    {
        var client = CreateClient();
        // The form key limit is 2048 characters.
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                new string('k', 2049) + "=v",
                SecureEncoding.UTF8,
                "application/x-www-form-urlencoded"
            ),
        };
        if (sessionCookie is not null)
        {
            request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        }

        return await client.SendAsync(request);
    }

    private static async Task AssertInvalidRequestAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("invalid_request", JsonElement.Parse(body).GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostToken_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/token");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostRevocation_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/revoke");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostIntrospection_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/introspect");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostPushedAuthorization_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/par");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostDeviceAuthorization_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/device_authorization");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostBackchannelAuthentication_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/bc-authorize");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostAuthorization_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/authorize");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostEndSession_WithAMalformedForm_AnswersInvalidRequest()
    {
        using var response = await PostMalformedFormAsync("/oauth2/end_session");

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostDeviceVerification_WhenSignedIn_AnswersInvalidRequest()
    {
        const string userName = "malformed-form-user";
        const string password = "malformed-form-passphrase-7";
        await Factory.SeedLocalAccountAsync(userName, password);
        var sessionCookie = await InteractiveLogin.SignInAsync(CreateClient(), userName, password);

        using var response = await PostMalformedFormAsync("/oauth2/device", sessionCookie);

        await AssertInvalidRequestAsync(response);
    }

    [Fact]
    public async Task PostInteractiveLogin_WithAMalformedForm_AnswersBadRequest()
    {
        using var response = await PostMalformedFormAsync("/account/login");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
