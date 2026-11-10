// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Buffers;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logout;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Diagnostics;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using Xunit;

namespace NCode.Identity.Bff.Tests.Logout;

public class BffBackChannelLogoutHandlerTests : BaseTests
{
    private const string Issuer = "https://op.example";
    private const string Token = "the-logout-token";

    private static DateTimeOffset Now { get; } = new(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);

    private Mock<ILogoutTokenValidator> Validator { get; }
    private Mock<IBffSessionStore> Store { get; }
    private Mock<IBffSessionManager> Manager { get; }
    private Mock<IBffAuditRecorder> Audit { get; }
    private BffFrontendCatalog Frontends { get; }
    private ClientRegistration Registration { get; }

    public BffBackChannelLogoutHandlerTests()
    {
        Validator = CreateStrictMock<ILogoutTokenValidator>();
        Store = CreateStrictMock<IBffSessionStore>();
        Manager = CreateStrictMock<IBffSessionManager>();
        Audit = CreateStrictMock<IBffAuditRecorder>();
        Frontends = BffTestFrontends.CreateCatalog(options => options.Authority = Issuer);
        Registration = BffTestFrontends.Get(Frontends).Registration;
    }

    private BffBackChannelLogoutHandler CreateHandler() =>
        new(
            Frontends,
            Validator.Object,
            BffTestTelemetry.Instrument(Store.Object),
            Manager.Object,
            Audit.Object
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

    private static BffSessionMetadata CreateMetadata(
        string issuer,
        string? sessionId,
        string frontend = BffTestFrontends.Name
    ) =>
        new()
        {
            Frontend = frontend,
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
        Validator
            .Setup(x =>
                x.ValidateAsync(
                    Registration,
                    It.Is<LogoutTokenValidationRequest>(r => r.LogoutToken == Token),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupRejected(string reason) =>
        Audit
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
        Store
            .Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(record)
            .Verifiable();

    private void SetupEnd(string key, BffSessionMetadata metadata) =>
        Manager
            .Setup(x =>
                x.EndAsync(
                    It.IsAny<HttpContext>(),
                    key,
                    metadata,
                    BffSessionEndReasons.BackchannelLogout,
                    CancellationToken.None
                )
            )
            .ReturnsAsync(true)
            .Verifiable();

    private void SetupHandled(LogoutTokenValidationResult result) =>
        Validator
            .Setup(x => x.TryRecordHandledAsync(Registration, result, CancellationToken.None))
            .ReturnsAsync(true)
            .Verifiable();

    private void SetupReceived() =>
        Audit
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

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_FormOverTheKeyLengthLimit_WritesBadRequest()
    {
        SetupRejected(OpenIdProtocol.ErrorCodes.InvalidRequest);
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Body = new MemoryStream(
            SecureEncoding.UTF8.GetBytes($"{new string('k', 2049)}=value")
        );
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_NoToken_WritesBadRequest()
    {
        SetupRejected(OpenIdProtocol.ErrorCodes.InvalidRequest);
        var httpContext = CreateHttpContext(form: true, token: null);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext, CancellationToken.None);

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

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_ProviderUnavailable_WritesBadRequest()
    {
        Validator
            .Setup(x =>
                x.ValidateAsync(
                    Registration,
                    It.Is<LogoutTokenValidationRequest>(r => r.LogoutToken == Token),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OpenIdProviderException("jwks unavailable"))
            .Verifiable();
        SetupRejected(BffLoginFailureReasons.ProviderUnavailable);
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SessionIdPath_EndsMatchingSessionsAndSkipsForeignIssuer()
    {
        const string sid = "sid-1";
        var result = CreateValid(sid, "sub-1");
        SetupValidate(result);
        Store
            .Setup(x => x.FindBySessionIdAsync(Issuer, sid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["key-1", "key-2", "key-3", "key-4"])
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

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SessionOfAnotherFrontend_IsNotEnded()
    {
        const string sid = "sid-1";
        var result = CreateValid(sid, "sub-1");
        SetupValidate(result);
        Store
            .Setup(x => x.FindBySessionIdAsync(Issuer, sid, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["key-1"])
            .Verifiable();
        SetupGet("key-1", CreateRecord("key-1", CreateMetadata(Issuer, sid, "other")));
        SetupHandled(result);
        SetupReceived();
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_SubjectPath_EndsSessions()
    {
        const string subject = "sub-1";
        var result = CreateValid(null, subject);
        SetupValidate(result);
        Store
            .Setup(x => x.FindBySubjectAsync(Issuer, subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(["key-1"])
            .Verifiable();
        var metadata = CreateMetadata(Issuer, "other-sid");
        SetupGet("key-1", CreateRecord("key-1", metadata));
        SetupEnd("key-1", metadata);
        SetupHandled(result);
        SetupReceived();
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task HandleAsync_StoreFailure_WritesBadRequestAndDoesNotRecordTokenId()
    {
        const string sid = "sid-1";
        var result = CreateValid(sid, null);
        SetupValidate(result);
        Store
            .Setup(x => x.FindBySessionIdAsync(Issuer, sid, It.IsAny<CancellationToken>()))
            .Throws(new BffStoreUnavailableException("store down"))
            .Verifiable();
        SetupRejected(BffBackChannelLogoutHandler.SessionEndFailedReason);
        var httpContext = CreateHttpContext(form: true);
        var handler = CreateHandler();

        await handler.HandleAsync(httpContext, CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
    }

    #endregion
}
