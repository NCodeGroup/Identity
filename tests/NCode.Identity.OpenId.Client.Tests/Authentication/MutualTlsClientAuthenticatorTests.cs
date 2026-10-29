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

using NCode.Identity.OpenId.Client.Authentication;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Authentication;

public class MutualTlsClientAuthenticatorTests : BaseTests
{
    private const string Endpoint = "https://op.example.test/token";

    private static MutualTlsClientAuthenticator CreateAuthenticator(string method) =>
        method == OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth
            ? new TlsClientAuthClientAuthenticator()
            : new SelfSignedTlsClientAuthClientAuthenticator();

    #region MethodName Tests

    [Theory]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth)]
    public void MethodName_Basic_IsTheMethod(string method)
    {
        var authenticator = CreateAuthenticator(method);

        Assert.Equal(method, authenticator.MethodName);
    }

    #endregion

    #region UsesMutualTls Tests

    [Theory]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth)]
    public void UsesMutualTls_Basic_IsTrue(string method)
    {
        var authenticator = CreateAuthenticator(method);

        Assert.True(authenticator.UsesMutualTls);
    }

    #endregion

    #region AuthenticateAsync Tests

    [Theory]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.TlsClientAuth)]
    [InlineData(OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth)]
    public async Task AuthenticateAsync_AppendsClientId(string method)
    {
        var authenticator = CreateAuthenticator(method);
        var registration = new ClientRegistration("https://op.example.test", "client:1", method);
        var parameters = new List<KeyValuePair<string, string>>();
        var context = new ClientAuthenticationContext(
            registration,
            new OpenIdProviderMetadata(),
            Endpoint,
            new HttpRequestMessage(HttpMethod.Post, Endpoint),
            parameters
        );

        await authenticator.AuthenticateAsync(context, CancellationToken.None);

        Assert.Equal([new KeyValuePair<string, string>("client_id", "client:1")], parameters);
        Assert.Null(context.Request.Headers.Authorization);
    }

    [Fact]
    public async Task AuthenticateAsync_WithNullContext_Throws()
    {
        var authenticator = new TlsClientAuthClientAuthenticator();

        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await authenticator.AuthenticateAsync(null!, CancellationToken.None)
        );
    }

    #endregion
}
