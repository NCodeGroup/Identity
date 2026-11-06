// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class BffClientAuthenticationCheckTests : BaseTests
{
    private BffClientAuthenticationCheck CreateCheck()
    {
        var authenticator = CreateStrictMock<IClientAuthenticator>();
        authenticator
            .SetupGet(x => x.MethodName)
            .Returns(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt)
            .Verifiable();

        return new BffClientAuthenticationCheck([authenticator.Object]);
    }

    private static BffFrontendSet CreateSet(string method) =>
        BffFrontendSet.Create(
            BffTestFrontends.CreateBffOptions(x =>
                x.Frontends[BffTestFrontends.Name].ClientAuthentication =
                    new BffClientAuthenticationOptions { Method = method }
            ),
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()]
        );

    #region Check Tests

    [Fact]
    public void Check_RegisteredMethod_AddsNoFailure()
    {
        var check = CreateCheck();
        var set = CreateSet(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt);
        List<string> failures = [];

        check.Check(set, failures);

        Assert.Empty(failures);
    }

    [Fact]
    public void Check_UnregisteredMethod_AddsTheFailure()
    {
        var check = CreateCheck();
        var set = CreateSet(OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt);
        List<string> failures = [];

        check.Check(set, failures);

        var failure = Assert.Single(failures);
        Assert.Equal(
            $"No client authenticator is registered for the method '{OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt}' configured at Bff:Frontends:{BffTestFrontends.Name}:ClientAuthentication:Method.",
            failure
        );
    }

    #endregion
}
