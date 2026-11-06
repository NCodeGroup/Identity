// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hosting;

public class BffStartupCheckTests : BaseTests
{
    private BffStartupCheck CreateCheck(string configuredMethod)
    {
        var authenticator = CreateStrictMock<IClientAuthenticator>();
        authenticator
            .SetupGet(x => x.MethodName)
            .Returns(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt)
            .Verifiable();

        var source = new BffTestOptionsSource(
            BffTestFrontends.CreateBffOptions(x =>
                x.Frontends[BffTestFrontends.Name].ClientAuthentication =
                    new BffClientAuthenticationOptions { Method = configuredMethod }
            )
        );
        var frontends = BffTestFrontends.CreateCatalog(
            source,
            new BffClientAuthenticationCheck([authenticator.Object])
        );

        return new BffStartupCheck(frontends);
    }

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_ValidSet_Completes()
    {
        var check = CreateCheck(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

        await check.StartAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StartAsync_UnregisteredMethod_Throws()
    {
        var check = CreateCheck(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            check.StartAsync(CancellationToken.None)
        );

        Assert.Contains(
            $"Bff:Frontends:{BffTestFrontends.Name}:ClientAuthentication:Method",
            exception.Message
        );
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_Always_Completes()
    {
        var check = CreateCheck(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);

        await check.StopAsync(CancellationToken.None);
    }

    #endregion
}
