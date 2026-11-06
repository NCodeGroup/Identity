// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Moq;
using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public sealed class BffCircuitHandlerTests : BaseTests
{
    private Mock<AuthenticationStateProvider> MockAuthenticationStateProvider { get; }
    private Mock<IBffConnectionSessionAccessor> MockConnections { get; }
    private BffCircuitHandler Handler { get; }

    public BffCircuitHandlerTests()
    {
        MockAuthenticationStateProvider = CreateStrictMock<AuthenticationStateProvider>();
        MockConnections = CreateStrictMock<IBffConnectionSessionAccessor>();
        Handler = new BffCircuitHandler(
            MockAuthenticationStateProvider.Object,
            MockConnections.Object
        );
    }

    private static CircuitInboundActivityContext CreateContext() =>
        (CircuitInboundActivityContext)
            RuntimeHelpers.GetUninitializedObject(typeof(CircuitInboundActivityContext));

    #region CreateInboundActivityHandler Tests

    [Fact]
    public async Task CreateInboundActivityHandler_WithConnection_MakesItCurrent()
    {
        var mockSession = CreateStrictMock<IBffConnectionSession>();
        var mockScope = CreateStrictMock<IDisposable>();
        var context = CreateContext();
        var events = new List<string>();

        var identity = new BffConnectionIdentity(new ClaimsIdentity("test"), mockSession.Object);
        var state = new AuthenticationState(new ClaimsPrincipal(identity));
        MockAuthenticationStateProvider
            .Setup(x => x.GetAuthenticationStateAsync())
            .ReturnsAsync(state)
            .Verifiable();
        MockConnections
            .Setup(x => x.Use(mockSession.Object))
            .Callback(() => events.Add("use"))
            .Returns(mockScope.Object)
            .Verifiable();
        mockScope.Setup(x => x.Dispose()).Callback(() => events.Add("dispose")).Verifiable();

        var handler = Handler.CreateInboundActivityHandler(c =>
        {
            Assert.Same(context, c);
            events.Add("next");
            return Task.CompletedTask;
        });
        await handler(context);

        Assert.Equal(["use", "next", "dispose"], events);
    }

    [Fact]
    public async Task CreateInboundActivityHandler_Anonymous_CallsNext()
    {
        var context = CreateContext();
        var called = false;

        var state = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        MockAuthenticationStateProvider
            .Setup(x => x.GetAuthenticationStateAsync())
            .ReturnsAsync(state)
            .Verifiable();

        var handler = Handler.CreateInboundActivityHandler(c =>
        {
            Assert.Same(context, c);
            called = true;
            return Task.CompletedTask;
        });
        await handler(context);

        Assert.True(called);
    }

    #endregion
}
