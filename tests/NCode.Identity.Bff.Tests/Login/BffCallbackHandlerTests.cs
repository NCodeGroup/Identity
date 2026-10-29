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
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Cookies;
using NCode.Identity.Bff.Login;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Login;

public class BffCallbackHandlerTests : BaseTests
{
    private const string Issuer = "https://op.example.com";
    private const string State = "state-value";

    // 32 random bytes in unpadded base64url.
    private static readonly string LoginSecret = new('a', 43);

    private readonly Mock<BffLoginTransactionService> _transactionService;
    private readonly Mock<IProviderMetadataResolver> _metadataResolver;
    private readonly Mock<IBffAuditRecorder> _auditRecorder;

    public BffCallbackHandlerTests()
    {
        _transactionService = CreateStrictMock<BffLoginTransactionService>(
            CreateStrictMock<IBffSessionStore>().Object
        );
        _metadataResolver = CreateStrictMock<IProviderMetadataResolver>();
        _auditRecorder = CreateStrictMock<IBffAuditRecorder>();
    }

    private BffCallbackHandler CreateHandler()
    {
        var options = Microsoft.Extensions.Options.Options.Create(CreateOptions());
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        return new BffCallbackHandler(
            options,
            new BffClientRegistrationProvider(options),
            _transactionService.Object,
            new BffCookies(options, timeProvider),
            _metadataResolver.Object,
            CreateStrictMock<ITokenClient>().Object,
            CreateStrictMock<IIdTokenValidator>().Object,
            [],
            CreateStrictMock<IBffSessionManager>().Object,
            _auditRecorder.Object,
            timeProvider,
            CreateLooseMock<ILogger<BffCallbackHandler>>().Object
        );
    }

    private static BffOptions CreateOptions() => new() { Authority = Issuer, ClientId = "bff" };

    private static DefaultHttpContext CreateHttpContext(string query)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("app.example.com");
        httpContext.Request.QueryString = new QueryString(query);
        httpContext.Request.Headers.Cookie = $"{BffDefaults.LoginCookieName}={LoginSecret}";
        httpContext.Response.Body = new MemoryStream();
        return httpContext;
    }

    private void SetupTake(string? state, BffLoginTransactionResult result) =>
        _transactionService
            .Setup(x => x.TakeAsync(state, LoginSecret, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupTransaction(bool issSupported)
    {
        var transaction = new BffLoginTransaction { Issuer = Issuer };
        SetupTake(State, new BffLoginTransactionResult(transaction, null));
        var metadata = new OpenIdProviderMetadata
        {
            Issuer = Issuer,
            AuthorizationResponseIssParameterSupported = issSupported,
        };
        _metadataResolver
            .Setup(x => x.GetMetadataAsync(Issuer, It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata)
            .Verifiable();
    }

    private void SetupAudit(string reason) =>
        _auditRecorder
            .Setup(x =>
                x.LoginFailedAsync(It.IsAny<HttpContext>(), reason, It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private async Task AssertFailsAsync(DefaultHttpContext httpContext, string reason)
    {
        await CreateHandler().HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status302Found, httpContext.Response.StatusCode);
        Assert.Equal($"/?error={reason}", httpContext.Response.Headers.Location.ToString());
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_NoState_FailsWithUnknownState()
    {
        SetupTake(null, new BffLoginTransactionResult(null, BffLoginFailureReasons.UnknownState));
        SetupAudit(BffLoginFailureReasons.UnknownState);

        await AssertFailsAsync(CreateHttpContext("?code=abc"), BffLoginFailureReasons.UnknownState);
    }

    [Fact]
    public async Task HandleAsync_NoTransaction_FailsWithUnknownState()
    {
        SetupTake(State, new BffLoginTransactionResult(null, BffLoginFailureReasons.UnknownState));
        SetupAudit(BffLoginFailureReasons.UnknownState);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.UnknownState
        );
    }

    [Fact]
    public async Task HandleAsync_InitiatorMismatch_FailsWithInitiatorMismatch()
    {
        SetupTake(
            State,
            new BffLoginTransactionResult(null, BffLoginFailureReasons.InitiatorMismatch)
        );
        SetupAudit(BffLoginFailureReasons.InitiatorMismatch);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.InitiatorMismatch
        );
    }

    [Fact]
    public async Task HandleAsync_ErrorResponse_FailsWithAuthorizationError()
    {
        SetupTake(State, new BffLoginTransactionResult(new BffLoginTransaction(), null));
        SetupAudit(BffLoginFailureReasons.AuthorizationError);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&error=access_denied"),
            BffLoginFailureReasons.AuthorizationError
        );
    }

    [Fact]
    public async Task HandleAsync_IssuerMismatch_FailsWithIssuerMismatch()
    {
        SetupTransaction(issSupported: true);
        SetupAudit(BffLoginFailureReasons.IssuerMismatch);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc&iss=https%3A%2F%2Fevil.example.com"),
            BffLoginFailureReasons.IssuerMismatch
        );
    }

    [Fact]
    public async Task HandleAsync_MissingIssWhenSupported_FailsWithIssuerMismatch()
    {
        SetupTransaction(issSupported: true);
        SetupAudit(BffLoginFailureReasons.IssuerMismatch);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}&code=abc"),
            BffLoginFailureReasons.IssuerMismatch
        );
    }

    [Fact]
    public async Task HandleAsync_NoCode_FailsWithInvalidResponse()
    {
        SetupTransaction(issSupported: false);
        SetupAudit(BffLoginFailureReasons.InvalidResponse);

        await AssertFailsAsync(
            CreateHttpContext($"?state={State}"),
            BffLoginFailureReasons.InvalidResponse
        );
    }

    #endregion
}
