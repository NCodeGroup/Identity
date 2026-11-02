// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class DefaultClientAuthenticationServiceConstructionTests : BaseTests
{
    private Mock<IAuditEventRecorder> MockAuditEventRecorder { get; }
    private Mock<IRequestRateLimiter> MockRateLimiter { get; }

    public DefaultClientAuthenticationServiceConstructionTests()
    {
        MockAuditEventRecorder = CreateStrictMock<IAuditEventRecorder>();
        MockRateLimiter = CreateStrictMock<IRequestRateLimiter>();
    }

    #region Scaffolding

    private DefaultClientAuthenticationService CreateService(
        IEnumerable<IClientAuthenticationHandler> handlers,
        IEnumerable<string> names
    ) =>
        new(
            handlers,
            CreateCatalog(names),
            MockAuditEventRecorder.Object,
            MockRateLimiter.Object,
            CreateLooseMock<ILogger<DefaultClientAuthenticationService>>().Object
        );

    private static DefaultClientAuthenticationMethodCatalog CreateCatalog(
        IEnumerable<string> names
    ) =>
        new([
            new StaticDataSource<ClientAuthenticationMethodDescriptor>([
                .. names.Select(name => new ClientAuthenticationMethodDescriptor { Name = name }),
            ]),
        ]);

    private IClientAuthenticationHandler CreateNamedHandler(string method)
    {
        var mockHandler = CreateStrictMock<IClientAuthenticationHandler>();
        mockHandler.SetupGet(x => x.AuthenticationMethod).Returns(method).Verifiable();
        return mockHandler.Object;
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenAHandlerHasNoDescriptor_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateService([CreateNamedHandler("a")], ["b"])
        );

        Assert.Contains(
            "authenticates 'a', which no client-authentication method descriptor names.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_WhenTwoHandlersAuthenticateOneMethod_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateService([CreateNamedHandler("a"), CreateNamedHandler("a")], ["a"])
        );

        Assert.Equal(
            "More than one client-authentication handler authenticates 'a'.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_WhenADescriptorHasNoHandler_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateService([CreateNamedHandler("a")], ["a", "b"])
        );

        Assert.Equal(
            "No client-authentication handler authenticates the registered method 'b'.",
            exception.Message
        );
    }

    #endregion

    private sealed class StaticDataSource<T>(IEnumerable<T> items) : ICollectionDataSource<T>
    {
        public IEnumerable<T> Collection { get; } = [.. items];

        public IChangeToken GetChangeToken() => NullChangeToken.Singleton;
    }
}
