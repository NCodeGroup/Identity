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

using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class NonceOrStateRequirementTests : BaseTests
{
    #region Scaffolding

    private (AuthorizationRequestProfileContext context, Func<string?> capturedCode) CreateContext(
        string? nonce,
        string? state,
        params string[] scopes
    )
    {
        string? capturedCode = null;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);

        var mockContext = CreateLooseMock<OpenIdContext>();
        mockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object);

        var mockRequest = CreateLooseMock<IAuthorizationRequest>();
        IReadOnlyList<string> values = scopes;
        mockRequest.SetupGet(x => x.Scopes).Returns(values);
        mockRequest.SetupGet(x => x.Nonce).Returns(nonce);
        mockRequest.SetupGet(x => x.State).Returns(state);

        var mockClient = CreateLooseMock<OpenIdClient>();

        return (
            new AuthorizationRequestProfileContext(
                mockContext.Object,
                mockClient.Object,
                mockRequest.Object,
                new AuthorizationResponseDelivery(
                    "https://client.example/callback",
                    CreateLooseMock<IAuthorizationResponseModeHandler>().Object,
                    State: null
                )
            ),
            () => capturedCode
        );
    }

    #endregion

    #region EvaluateAsync Tests

    [Fact]
    public async Task EvaluateAsync_WhenOpenIdRequestHasNonce_Completes()
    {
        var requirement = new NonceOrStateRequirement();
        var (context, _) = CreateContext(
            nonce: "nonce",
            state: null,
            OpenIdConstants.ScopeTypes.OpenId
        );

        var exception = await Record.ExceptionAsync(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Null(exception);
    }

    [Fact]
    public async Task EvaluateAsync_WhenOpenIdRequestHasOnlyState_ThrowsInvalidRequest()
    {
        var requirement = new NonceOrStateRequirement();
        var (context, capturedCode) = CreateContext(
            nonce: null,
            state: "state",
            OpenIdConstants.ScopeTypes.OpenId
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, capturedCode());
    }

    [Fact]
    public async Task EvaluateAsync_WhenOAuthRequestHasState_Completes()
    {
        var requirement = new NonceOrStateRequirement();
        var (context, _) = CreateContext(nonce: null, state: "state", "accounts");

        var exception = await Record.ExceptionAsync(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Null(exception);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task EvaluateAsync_WhenOAuthRequestHasNoState_ThrowsInvalidRequest(string? state)
    {
        var requirement = new NonceOrStateRequirement();
        var (context, capturedCode) = CreateContext(nonce: "nonce", state, "accounts");

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await requirement.EvaluateAsync(context, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequest, capturedCode());
    }

    #endregion
}
