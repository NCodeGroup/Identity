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

using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A consent denial answers the parked authorization request with access_denied. See ADR-0229.
public class InteractiveConsentTests
{
    private const string UserName = "consent-user";
    private const string Password = "consent-password";
    private const string RedirectUri = "https://client.example/callback";

    [Fact]
    public async Task PostConsent_WhenTheEndUserDenies_AnswersTheClientWithAccessDenied()
    {
        const string clientId = "it-consent-deny";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object> { [OpenIdSettingNames.RequireConsent] = true },
            RedirectUri
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters;
        using var authorizeRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/oauth2/authorize" + query
        );
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={sessionCookie}"
        );
        using var authorizeResponse = await client.SendAsync(authorizeRequest);

        Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
        var consentLocation = authorizeResponse.Headers.Location;
        Assert.NotNull(consentLocation);
        var consentUrl = new Uri(new Uri("http://localhost"), consentLocation);
        var returnUrl = HttpUtility.ParseQueryString(consentUrl.Query)["ReturnUrl"];
        Assert.False(string.IsNullOrEmpty(returnUrl), consentLocation.OriginalString);

        using var response = await InteractiveLogin.PostFormAsync(
            client,
            consentUrl.PathAndQuery,
            "/account/consent",
            sessionCookie,
            new Dictionary<string, string> { ["action"] = "deny", ["ReturnUrl"] = returnUrl }
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.OriginalString);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("access_denied", parameters["error"]);
        Assert.Equal("abc123", parameters["state"]);
    }
}
