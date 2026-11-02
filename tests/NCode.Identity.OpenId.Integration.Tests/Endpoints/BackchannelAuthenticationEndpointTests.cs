// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Endpoints.BackchannelAuthentication.Commands;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class BackchannelAuthenticationEndpointTests
{
    private const string ClientId = "it-ciba-no-store";
    private const string ClientSecret = "ciba-confidential-secret-value";
    private const string LoginHint = "alice";

    #region PostBackchannelAuthentication Tests

    [Fact]
    public async Task PostBackchannelAuthentication_WhenTheLoginHintResolves_MarksTheResponseNoStore()
    {
        using var factory = new HostApplicationFactory();
        var handler = new ResolvedLoginHintHandler();
        using var cibaFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection.AddSingleton<
                    ICommandResponseHandler<
                        AuthenticateBackchannelRequestCommand,
                        AuthenticateSubjectDisposition
                    >
                >(handler)
            )
        );
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = cibaFactory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var form = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = "openid",
                ["login_hint"] = LoginHint,
            }
        );
        using var response = await client.PostAsync("/oauth2/bc-authorize", form);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var root = JsonElement.Parse(body);
        Assert.True(root.TryGetProperty("auth_req_id", out _));

        NoStoreAssert.IsNoStore(response);
    }

    #endregion

    // The host ships no CIBA login-hint resolver, so the stub resolves every hint to one subject.
    private sealed class ResolvedLoginHintHandler
        : ICommandResponseHandler<
            AuthenticateBackchannelRequestCommand,
            AuthenticateSubjectDisposition
        >
    {
        public ValueTask<AuthenticateSubjectDisposition> HandleAsync(
            AuthenticateBackchannelRequestCommand command,
            CancellationToken cancellationToken
        )
        {
            var identity = new ClaimsIdentity([new Claim("sub", LoginHint)], "test");
            var subject = new ClaimsPrincipal(identity);
            var ticket = new SubjectAuthentication(
                "test",
                new AuthenticationProperties(),
                subject,
                LoginHint
            );
            var disposition = new AuthenticateSubjectDisposition(ticket);
            return ValueTask.FromResult(disposition);
        }
    }
}
