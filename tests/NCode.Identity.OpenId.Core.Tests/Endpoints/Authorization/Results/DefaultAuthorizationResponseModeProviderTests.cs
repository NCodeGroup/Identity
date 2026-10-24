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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Results;

public class DefaultAuthorizationResponseModeProviderTests : BaseTests
{
    private sealed class StubResponseModeHandler(
        string responseMode,
        string? securedResponseMode,
        bool deliversInQuery
    ) : IAuthorizationResponseModeHandler
    {
        public string ResponseMode { get; } = responseMode;

        public string? SecuredResponseMode { get; } = securedResponseMode;

        public bool DeliversInQuery { get; } = deliversInQuery;

        public ValueTask ExecuteAsync(
            HttpContext httpContext,
            AuthorizationResult result,
            IOpenIdMessageResponse message,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }

    #region Scaffolding

    private static IAuthorizationResponseModeHandler[] CreateHandlers() =>
        [
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Query,
                securedResponseMode: null,
                deliversInQuery: true
            ),
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Fragment,
                securedResponseMode: null,
                deliversInQuery: false
            ),
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.QueryJwt,
                securedResponseMode: OpenIdConstants.ResponseModes.Query,
                deliversInQuery: true
            ),
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.FragmentJwt,
                securedResponseMode: OpenIdConstants.ResponseModes.Fragment,
                deliversInQuery: false
            ),
        ];

    private static IAuthorizationResponseModeHandler GetHandler(
        IAuthorizationResponseModeHandler[] handlers,
        string responseMode
    ) => Assert.Single(handlers, x => x.ResponseMode == responseMode);

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTwoHandlersDeliverTheSameMode_Throws()
    {
        IAuthorizationResponseModeHandler[] handlers =
        [
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Query,
                securedResponseMode: null,
                deliversInQuery: true
            ),
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Query,
                securedResponseMode: null,
                deliversInQuery: true
            ),
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAuthorizationResponseModeProvider(handlers)
        );

        Assert.Contains(
            "More than one handler delivers the 'query' response mode.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Constructor_WhenTwoHandlersSecureTheSameMode_Throws()
    {
        IAuthorizationResponseModeHandler[] handlers =
        [
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Query,
                securedResponseMode: null,
                deliversInQuery: true
            ),
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.QueryJwt,
                securedResponseMode: OpenIdConstants.ResponseModes.Query,
                deliversInQuery: true
            ),
            new StubResponseModeHandler(
                "query.other",
                securedResponseMode: OpenIdConstants.ResponseModes.Query,
                deliversInQuery: true
            ),
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAuthorizationResponseModeProvider(handlers)
        );

        Assert.Contains(
            "More than one handler secures the 'query' response mode.",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Constructor_WhenAHandlerSecuresAnUnregisteredMode_Throws()
    {
        IAuthorizationResponseModeHandler[] handlers =
        [
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.QueryJwt,
                securedResponseMode: OpenIdConstants.ResponseModes.Query,
                deliversInQuery: true
            ),
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAuthorizationResponseModeProvider(handlers)
        );

        Assert.Contains("but no handler delivers it.", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"secures the '{OpenIdConstants.ResponseModes.Query}' response mode",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Constructor_WhenAHandlerIsNamedTheJwtShorthand_Throws()
    {
        IAuthorizationResponseModeHandler[] handlers =
        [
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Jwt,
                securedResponseMode: null,
                deliversInQuery: false
            ),
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DefaultAuthorizationResponseModeProvider(handlers)
        );

        Assert.Contains("is the JARM shorthand", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region TryResolve Tests

    [Theory]
    [InlineData(new[] { "code" }, OpenIdConstants.ResponseModes.Query)]
    [InlineData(new[] { "code", "id_token" }, OpenIdConstants.ResponseModes.Fragment)]
    public void TryResolve_WhenNoModeIsRequested_ReturnsTheResponseTypesDefault(
        string[] responseTypes,
        string expectedResponseMode
    )
    {
        var handlers = CreateHandlers();
        var provider = new DefaultAuthorizationResponseModeProvider(handlers);

        var result = provider.TryResolve(
            requestedResponseMode: null,
            responseTypes,
            out var handler
        );

        Assert.True(result);
        Assert.Same(GetHandler(handlers, expectedResponseMode), handler);
    }

    [Theory]
    [InlineData(new[] { "code" }, OpenIdConstants.ResponseModes.QueryJwt)]
    [InlineData(new[] { "id_token" }, OpenIdConstants.ResponseModes.FragmentJwt)]
    public void TryResolve_WhenTheJwtShorthandIsRequested_ReturnsTheHandlerThatSecuresTheDefault(
        string[] responseTypes,
        string expectedResponseMode
    )
    {
        var handlers = CreateHandlers();
        var provider = new DefaultAuthorizationResponseModeProvider(handlers);

        var result = provider.TryResolve(
            OpenIdConstants.ResponseModes.Jwt,
            responseTypes,
            out var handler
        );

        Assert.True(result);
        Assert.Same(GetHandler(handlers, expectedResponseMode), handler);
    }

    [Theory]
    [InlineData(OpenIdConstants.ResponseModes.Query)]
    [InlineData(OpenIdConstants.ResponseModes.Fragment)]
    [InlineData(OpenIdConstants.ResponseModes.QueryJwt)]
    [InlineData(OpenIdConstants.ResponseModes.FragmentJwt)]
    public void TryResolve_WhenARegisteredModeIsRequested_ReturnsItsHandler(
        string requestedResponseMode
    )
    {
        var handlers = CreateHandlers();
        var provider = new DefaultAuthorizationResponseModeProvider(handlers);

        var result = provider.TryResolve(requestedResponseMode, ["code"], out var handler);

        Assert.True(result);
        Assert.Same(GetHandler(handlers, requestedResponseMode), handler);
    }

    [Fact]
    public void TryResolve_WhenAnUnregisteredModeIsRequested_ReturnsFalse()
    {
        var provider = new DefaultAuthorizationResponseModeProvider(CreateHandlers());

        var result = provider.TryResolve(
            OpenIdConstants.ResponseModes.FormPost,
            ["code"],
            out var handler
        );

        Assert.False(result);
        Assert.Null(handler);
    }

    [Fact]
    public void TryResolve_WhenNoHandlerSecuresTheDefault_ReturnsFalseForTheJwtShorthand()
    {
        IAuthorizationResponseModeHandler[] handlers =
        [
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Query,
                securedResponseMode: null,
                deliversInQuery: true
            ),
            new StubResponseModeHandler(
                OpenIdConstants.ResponseModes.Fragment,
                securedResponseMode: null,
                deliversInQuery: false
            ),
        ];
        var provider = new DefaultAuthorizationResponseModeProvider(handlers);

        var result = provider.TryResolve(
            OpenIdConstants.ResponseModes.Jwt,
            ["code"],
            out var handler
        );

        Assert.False(result);
        Assert.Null(handler);
    }

    #endregion
}
