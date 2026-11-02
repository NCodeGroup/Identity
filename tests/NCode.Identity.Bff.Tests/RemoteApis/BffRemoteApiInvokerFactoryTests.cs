// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiInvokerFactoryTests : BaseTests
{
    private static BffRemoteApiInvokerFactory CreateFactory() =>
        new(
            new HttpContextAccessor(),
            new Moq.Mock<IBffTokenAccessor>().Object,
            new Moq.Mock<IDPoPProofFactory>().Object,
            new BffClientRegistrationProvider(
                Microsoft.Extensions.Options.Options.Create(new BffOptions())
            )
        );

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
