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
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A wrong password takes as long whether or not an account has the user name, and a password refused at the attempt
// limit takes the work of a wrong one (ADR-0239, ADR-0244).
public class PasswordVerificationTimingTests
{
    private const string ClientId = "it-verification-timing-client";
    private const string ClientSecret = "verification-timing-client-secret";
    private const string UnknownUserName = "no-such-user";
    private const string PasswordlessUserName = "passwordless-user";
    private const string LimitedUserName = "limited-user";
    private const string WrongPassword = "verification-timing-wrong-password";
    private const string LimitedPassword = "verification-timing-right-password";

    #region Scaffolding

    // Delegates to the registered hasher and counts the verifications of its dummy hash.
    private sealed class RecordingPasswordHasher(IPasswordHasher inner) : IPasswordHasher
    {
        private int _dummyVerificationCount;

        public int DummyVerificationCount => Volatile.Read(ref _dummyVerificationCount);

        public string DummyPasswordHash => inner.DummyPasswordHash;

        public string HashPassword(ReadOnlySpan<byte> password) => inner.HashPassword(password);

        public PasswordVerificationResult VerifyHashedPassword(
            string hashedPassword,
            ReadOnlySpan<byte> password
        )
        {
            if (string.Equals(hashedPassword, inner.DummyPasswordHash, StringComparison.Ordinal))
            {
                Interlocked.Increment(ref _dummyVerificationCount);
            }

            return inner.VerifyHashedPassword(hashedPassword, password);
        }
    }

    // Wraps the registered hasher in the recording one, so the host verifies every password through it.
    private sealed class RecordingPasswordHasherFactory : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureTestServices(serviceCollection =>
            {
                var registration = serviceCollection.Last(service =>
                    service.ServiceType == typeof(IPasswordHasher)
                );
                var implementationType =
                    registration.ImplementationType
                    ?? throw new InvalidOperationException(
                        "The registration of the password hasher has no implementation type."
                    );

                serviceCollection.AddSingleton<IPasswordHasher>(serviceProvider =>
                {
                    var inner = (IPasswordHasher)
                        ActivatorUtilities.CreateInstance(serviceProvider, implementationType);
                    return new RecordingPasswordHasher(inner);
                });
            });
        }
    }

    // The hasher is created with the host, so this is called after the factory has served its first request.
    private static RecordingPasswordHasher GetHasher(HostApplicationFactory factory) =>
        Assert.IsType<RecordingPasswordHasher>(
            factory.Services.GetRequiredService<IPasswordHasher>()
        );

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async Task<(HttpStatusCode StatusCode, string Body)> RequestTokenAsync(
        HttpClient client,
        string userName,
        string password
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.Password,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["username"] = userName,
                ["password"] = password,
                ["scope"] = OpenIdConstants.ScopeTypes.OpenId,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);
        var body = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, body);
    }

    private static async Task AssertSignInRefusedAsync(HttpClient client, string userName)
    {
        using var response = await InteractiveLogin.SubmitLoginAsync(
            client,
            userName,
            WrongPassword,
            "/foo"
        );
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Null(InteractiveLogin.ReadSessionCookie(response));
    }

    #endregion

    #region Verification Tests

    [Fact]
    public async Task PasswordGrant_WhenNoAccountHasTheUserName_VerifiesAgainstTheDummyHash()
    {
        using var factory = new RecordingPasswordHasherFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            OpenIdProtocol.GrantTypes.Password
        );
        var client = CreateClient(factory);

        var (statusCode, body) = await RequestTokenAsync(client, UnknownUserName, WrongPassword);

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.Equal(1, GetHasher(factory).DummyVerificationCount);
    }

    [Fact]
    public async Task SignIn_WhenNoAccountHasTheUserName_VerifiesAgainstTheDummyHash()
    {
        using var factory = new RecordingPasswordHasherFactory();
        var client = CreateClient(factory);

        await AssertSignInRefusedAsync(client, UnknownUserName);

        Assert.Equal(1, GetHasher(factory).DummyVerificationCount);
    }

    [Fact]
    public async Task SignIn_WhenTheAccountHasNoPassword_VerifiesAgainstTheDummyHash()
    {
        using var factory = new RecordingPasswordHasherFactory();
        await factory.SeedLocalAccountAsync(PasswordlessUserName);
        var client = CreateClient(factory);

        await AssertSignInRefusedAsync(client, PasswordlessUserName);

        Assert.Equal(1, GetHasher(factory).DummyVerificationCount);
    }

    [Fact]
    public async Task SignIn_WhenTheLimitIsReached_VerifiesAgainstTheDummyHash()
    {
        using var factory = new RecordingPasswordHasherFactory();
        await factory.SeedTenantSettingsAsync(
            new Dictionary<string, object> { [OpenIdSettingNames.PasswordFailedAttemptsLimit] = 1 }
        );
        await factory.SeedLocalAccountAsync(LimitedUserName, LimitedPassword);
        var client = CreateClient(factory);

        await AssertSignInRefusedAsync(client, LimitedUserName);

        Assert.Equal(0, GetHasher(factory).DummyVerificationCount);

        using var response = await InteractiveLogin.SubmitLoginAsync(
            client,
            LimitedUserName,
            LimitedPassword,
            "/foo"
        );

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Null(InteractiveLogin.ReadSessionCookie(response));
        Assert.Equal(1, GetHasher(factory).DummyVerificationCount);
    }

    #endregion
}
