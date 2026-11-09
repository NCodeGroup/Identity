// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffConnectionSessionsTests : BaseTests
{
    private const string Handle = "handle-1";
    private const string LogoutUrl = "/bff/logout";

    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _timeProvider = new(Now);

    private Mock<BffSessionResolver> CreateResolver(BffFrontendCatalog catalog) =>
        CreateStrictMock<BffSessionResolver>(
            CreateStrictMock<IBffSessionManager>().Object,
            new BffCookies(_timeProvider),
            Array.Empty<IBffSessionValidator>(),
            catalog,
            _timeProvider
        );

    #region Create Tests

    [Fact]
    public void Create_ReturnsConnectionSessionOfFrontend()
    {
        var catalog = BffTestFrontends.CreateCatalog();
        var frontend = BffTestFrontends.Get(catalog);
        var resolver = CreateResolver(catalog);
        var sessions = new BffConnectionSessions(resolver.Object, catalog);

        var session = sessions.Create(frontend, Handle, LogoutUrl);

        Assert.Equal(BffTestFrontends.Name, session.Frontend);
        Assert.Null(session.HttpContext);
    }

    #endregion
}
