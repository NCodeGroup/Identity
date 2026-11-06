// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiInvokerFactoryTests : BaseTests
{
    private BffRemoteApiInvokerFactory CreateFactory()
    {
        var timeProvider = new FakeTimeProvider();
        var frontends = BffTestFrontends.CreateCatalog();
        var cookies = new BffCookies(timeProvider);
        var resolver = new BffSessionResolver(
            CreateStrictMock<IBffSessionManager>().Object,
            cookies,
            [],
            frontends,
            timeProvider
        );
        return new BffRemoteApiInvokerFactory(
            new HttpContextAccessor(),
            new DefaultBffConnectionSessionAccessor(),
            new BffTokenContexts(frontends, resolver, cookies),
            new Moq.Mock<IBffTokenAccessor>().Object,
            new Moq.Mock<IDPoPProofFactory>().Object
        );
    }

    #region CreateInvoker Tests

    [Fact]
    public void CreateInvoker_NullTokenRequest_Throws()
    {
        using var factory = CreateFactory();

        Assert.Throws<ArgumentNullException>(() => factory.CreateInvoker(null!));
    }

    [Fact]
    public void CreateInvoker_EachCallReturnsANewInvoker()
    {
        using var factory = CreateFactory();
        var tokenRequest = new BffTokenRequest(BffTokenRequirementNames.User);

        using var first = factory.CreateInvoker(tokenRequest);
        using var second = factory.CreateInvoker(tokenRequest);

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task CreateInvoker_DisposingAnInvoker_KeepsTheSharedPool()
    {
        using var factory = CreateFactory();
        var tokenRequest = new BffTokenRequest(BffTokenRequirementNames.User);
        var first = factory.CreateInvoker(tokenRequest);
        first.Dispose();
        using var second = factory.CreateInvoker(tokenRequest);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.example.invalid/");

        // The second invoker still reaches its handler: the token handler runs and finds no current request.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            second.SendAsync(request, CancellationToken.None)
        );
    }

    #endregion
}
