// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class IBffSessionScopeTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);

    private Mock<BffSessionResolver> CreateResolver(BffFrontendCatalog catalog) =>
        CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            new BffCookies(_timeProvider),
            Array.Empty<IBffSessionValidator>(),
            catalog,
            _timeProvider
        );

    #region From Tests

    [Fact]
    public void From_ForeignContext_Throws()
    {
        var foreign = CreateStrictMock<IBffTokenContext>();

        Assert.Throws<InvalidOperationException>(() => IBffSessionScope.From(foreign.Object));
    }

    [Fact]
    public void From_RequestContext_ReturnsIt()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var resolver = CreateResolver(catalog);
        var context = new BffRequestTokenContext(
            new DefaultHttpContext(),
            catalog,
            resolver.Object,
            new BffCookies(_timeProvider)
        );

        Assert.Same(context, IBffSessionScope.From(context));
    }

    #endregion
}
