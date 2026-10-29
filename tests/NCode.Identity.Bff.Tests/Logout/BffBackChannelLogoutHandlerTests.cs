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

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Client;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Logout;

public class BffBackChannelLogoutHandlerTests : BaseTests
{
    private const string Issuer = "https://op.example";
    private const string Token = "the-logout-token";

    private static readonly DateTimeOffset Now = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private readonly Mock<ILogoutTokenValidator> _validator;
    private readonly Mock<IBffSessionStore> _store;
    private readonly Mock<IBffSessionManager> _manager;
    private readonly Mock<IBffAuditRecorder> _audit;
    private readonly BffClientRegistrationProvider _registrationProvider;

    public BffBackChannelLogoutHandlerTests()
    {
        _validator = CreateStrictMock<ILogoutTokenValidator>();
        _store = CreateStrictMock<IBffSessionStore>();
        _manager = CreateStrictMock<IBffSessionManager>();
        _audit = CreateStrictMock<IBffAuditRecorder>();
        _registrationProvider = new BffClientRegistrationProvider(
            Microsoft.Extensions.Options.Options.Create(
                new BffOptions
                {
                    Authority = Issuer,
                    ClientId = "bff",
                    ReturnUrls = ["/"],
                }
            )
        );
    }

    private BffBackChannelLogoutHandler CreateHandler() =>
        new(
            _registrationProvider,
            _validator.Object,
            _store.Object,
            _manager.Object,
            _audit.Object
        );

    private static DefaultHttpContext CreateHttpContext(bool form, string? token = Token)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        if (!form)
            return httpContext;

        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        var values = new Dictionary<string, StringValues>();
        if (token is not null)
            values[OpenIdProtocol.Parameters.LogoutToken] = token;
        httpContext.Request.Form = new FormCollection(values);
        return httpContext;
    }

    private static BffSessionMetadata CreateMetadata(string issuer, string? sessionId) =>
        new()
        {
            Frontend = BffDefaults.FrontendName,
            Issuer = issuer,
            Subject = "sub-1",
            SessionId = sessionId,
            CreatedAt = Now,
            LastAccessedAt = Now,
            AbsoluteExpiresAt = Now.AddHours(8),
            IdleExpiresAt = Now.AddHours(1),
        };

    private static BffSessionRecord CreateRecord(string key, BffSessionMetadata metadata) =>
        new(key, 1, metadata, new byte[] { 1 });

    private static LogoutTokenValidationResult CreateValid(string? sessionId, string? subject) =>
        new(default(JsonElement), Issuer, "jti-1", Now.AddMinutes(2), subject, sessionId);

    private void SetupValidate(LogoutTokenValidationResult result) =>
        _validator
            .Setup(x =>
                x.ValidateAsync(
                    _registrationProvider.Registration,
                    It.Is<LogoutTokenValidationRequest>(r => r.LogoutToken == Token),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.FromResult(result))
            .Verifiable();

    private void SetupRejected(string reason) =>
        _audit
            .Setup(x =>
                x.BackchannelLogoutRejectedAsync(
                    It.IsAny<HttpContext>(),
                    reason,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupGet(string key, BffSessionRecord? record) =>
        _store
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult(record))
            .Verifiable();

    private void SetupEnd(string key, BffSessionMetadata metadata) =>
        _manager
            .Setup(x =>
                x.EndAsync(
                    It.IsAny<HttpContext>(),
                    key,
                    metadata,
                    BffSessionEndReasons.BackchannelLogout,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private void SetupHandled(LogoutTokenValidationResult result) =>
        _validator
            .Setup(x =>
                x.TryRecordHandledAsync(
                    _registrationProvider.Registration,
                    result,
                    CancellationToken.None
                )
            )
            .Returns(ValueTask.FromResult(true))
            .Verifiable();

    private void SetupReceived() =>
        _audit
            .Setup(x =>
                x.BackchannelLogoutReceivedAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_NoForm_WritesBadRequest()
    {
        SetupRejected(OpenIdProtocol.ErrorCodes.InvalidRequest);
        var httpContext = CreateHttpContext(form: false);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NoToken_WritesBadRequest()
    {
        SetupRejected(OpenIdProtocol.ErrorCodes.InvalidRequest);
        var httpContext = CreateHttpContext(form: true, token: null);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidToken_WritesBadRequest()
    {
        const string failureCode = "invalid_signature";
        SetupValidate(new LogoutTokenValidationResult(failureCode, "bad signature"));
        SetupRejected(failureCode);
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_ProviderUnavailable_WritesBadRequest()
    {
        _validator
            .Setup(x =>
                x.ValidateAsync(
                    _registrationProvider.Registration,
                    It.Is<LogoutTokenValidationRequest>(r => r.LogoutToken == Token),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OpenIdProviderException("jwks unavailable"))
            .Verifiable();
        SetupRejected(BffLoginFailureReasons.ProviderUnavailable);
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SessionIdPath_EndsMatchingSessionsAndSkipsForeignIssuer()
    {
        const string sid = "sid-1";
        var result = CreateValid(sid, "sub-1");
        SetupValidate(result);
        _store
            .Setup(x => x.FindBySessionIdAsync(Issuer, sid, It.IsAny<CancellationToken>()))
            .Returns(
                ValueTask.FromResult<IReadOnlyList<string>>(["key-1", "key-2", "key-3", "key-4"])
            )
            .Verifiable();
        var metadata1 = CreateMetadata(Issuer, sid);
        var metadata2 = CreateMetadata(Issuer, sid);
        SetupGet("key-1", CreateRecord("key-1", metadata1));
        SetupGet("key-2", CreateRecord("key-2", metadata2));
        SetupGet("key-3", CreateRecord("key-3", CreateMetadata("https://other.example", sid)));
        SetupGet("key-4", null);
        SetupEnd("key-1", metadata1);
        SetupEnd("key-2", metadata2);
        SetupHandled(result);
        SetupReceived();
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SubjectPath_EndsSessions()
    {
        const string subject = "sub-1";
        var result = CreateValid(null, subject);
        SetupValidate(result);
        _store
            .Setup(x => x.FindBySubjectAsync(Issuer, subject, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromResult<IReadOnlyList<string>>(["key-1"]))
            .Verifiable();
        var metadata = CreateMetadata(Issuer, "other-sid");
        SetupGet("key-1", CreateRecord("key-1", metadata));
        SetupEnd("key-1", metadata);
        SetupHandled(result);
        SetupReceived();
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_StoreFailure_WritesBadRequestAndDoesNotRecordTokenId()
    {
        const string sid = "sid-1";
        var result = CreateValid(sid, null);
        SetupValidate(result);
        _store
            .Setup(x => x.FindBySessionIdAsync(Issuer, sid, It.IsAny<CancellationToken>()))
            .Throws(new BffStoreUnavailableException("store down"))
            .Verifiable();
        SetupRejected(BffBackChannelLogoutHandler.SessionEndFailedReason);
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    #endregion
}
