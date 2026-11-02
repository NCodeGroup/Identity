// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
