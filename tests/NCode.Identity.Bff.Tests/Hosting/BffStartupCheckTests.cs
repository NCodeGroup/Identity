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

using Moq;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hosting;

public class BffStartupCheckTests
{
    private static BffStartupCheck CreateCheck(string configuredMethod)
    {
        var authenticator = new Mock<IClientAuthenticator>();
        authenticator
            .SetupGet(x => x.MethodName)
            .Returns(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

        var options = new BffOptions
        {
            ClientAuthentication = new BffClientAuthenticationOptions { Method = configuredMethod },
        };

        return new BffStartupCheck(
            new[] { authenticator.Object },
            Microsoft.Extensions.Options.Options.Create(options)
        );
    }

    [Fact]
    public async Task StartAsync_WhenNoAuthenticatorMatches_Throws()
    {
        var check = CreateCheck(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            check.StartAsync(CancellationToken.None)
        );

        Assert.Contains("Bff:ClientAuthentication:Method", exception.Message);
    }

    [Fact]
    public async Task StartAsync_WhenAnAuthenticatorMatches_Completes()
    {
        var check = CreateCheck(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

        await check.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_Completes()
    {
        var check = CreateCheck(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

        await check.StopAsync(CancellationToken.None);
    }
}
