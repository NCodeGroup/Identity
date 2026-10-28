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
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class AuditEventShapeTests
{
    #region TokenIssuedAuditEvent Tests

    [Fact]
    public void EventName_WhenATokenIsIssued_IsTokenIssued()
    {
        var auditEvent = CreateTokenIssuedEvent();

        Assert.Equal(OpenIdAuditEventNames.TokenIssued, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenATokenIsIssued_IsToken()
    {
        var auditEvent = CreateTokenIssuedEvent();

        Assert.Equal(AuditEventCategories.Token, auditEvent.EventCategory);
    }

    [Fact]
    public void Serialize_WhenATokenIsIssued_WritesTheTokenTypeAsTheDetails()
    {
        var auditEvent = CreateTokenIssuedEvent();

        using var record = JsonDocument.Parse(AuditRecordWriter.SerializeToUtf8Bytes(auditEvent));

        Assert.Equal(
            """{"token_type":"access_token"}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    #endregion

    #region TokenRevokedAuditEvent Tests

    [Fact]
    public void EventName_WhenATokenIsRevoked_IsTokenRevoked()
    {
        var auditEvent = CreateTokenRevokedEvent();

        Assert.Equal(OpenIdAuditEventNames.TokenRevoked, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenATokenIsRevoked_IsToken()
    {
        var auditEvent = CreateTokenRevokedEvent();

        Assert.Equal(AuditEventCategories.Token, auditEvent.EventCategory);
    }

    [Fact]
    public void Severity_WhenATokenIsRevoked_IsInfo()
    {
        var auditEvent = CreateTokenRevokedEvent();

        Assert.Equal(AuditSeverities.Info, auditEvent.Severity);
    }

    #endregion

    #region RefreshTokenReplayAuditEvent Tests

    [Fact]
    public void EventName_WhenARefreshTokenIsReplayed_IsRefreshTokenReplay()
    {
        var auditEvent = CreateRefreshTokenReplayEvent();

        Assert.Equal(OpenIdAuditEventNames.RefreshTokenReplay, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenARefreshTokenIsReplayed_IsToken()
    {
        var auditEvent = CreateRefreshTokenReplayEvent();

        Assert.Equal(AuditEventCategories.Token, auditEvent.EventCategory);
    }

    [Fact]
    public void Severity_WhenARefreshTokenIsReplayed_IsCritical()
    {
        var auditEvent = CreateRefreshTokenReplayEvent();

        Assert.Equal(AuditSeverities.Critical, auditEvent.Severity);
    }

    #endregion

    #region RefreshTokenRetryAuditEvent Tests

    [Fact]
    public void EventName_WhenARefreshTokenIsRetried_IsRefreshTokenRetry()
    {
        var auditEvent = CreateRefreshTokenRetryEvent();

        Assert.Equal(OpenIdAuditEventNames.RefreshTokenRetry, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenARefreshTokenIsRetried_IsToken()
    {
        var auditEvent = CreateRefreshTokenRetryEvent();

        Assert.Equal(AuditEventCategories.Token, auditEvent.EventCategory);
    }

    #endregion

    #region AuthorizationGrantedAuditEvent Tests

    [Fact]
    public void EventName_WhenAuthorizationIsGranted_IsAuthorizationGranted()
    {
        var auditEvent = CreateAuthorizationGrantedEvent();

        Assert.Equal(OpenIdAuditEventNames.AuthorizationGranted, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenAuthorizationIsGranted_IsAuthorization()
    {
        var auditEvent = CreateAuthorizationGrantedEvent();

        Assert.Equal(AuditEventCategories.Authorization, auditEvent.EventCategory);
    }

    #endregion

    #region AuthorizationDeniedAuditEvent Tests

    [Fact]
    public void EventName_WhenAuthorizationIsDenied_IsAuthorizationDenied()
    {
        var auditEvent = CreateAuthorizationDeniedEvent();

        Assert.Equal(OpenIdAuditEventNames.AuthorizationDenied, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenAuthorizationIsDenied_IsAuthorization()
    {
        var auditEvent = CreateAuthorizationDeniedEvent();

        Assert.Equal(AuditEventCategories.Authorization, auditEvent.EventCategory);
    }

    #endregion

    #region ClientAuthenticationFailedAuditEvent Tests

    [Fact]
    public void EventName_WhenClientAuthenticationFails_IsClientAuthentication()
    {
        var auditEvent = CreateClientAuthenticationFailedEvent();

        Assert.Equal(OpenIdAuditEventNames.ClientAuthentication, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenClientAuthenticationFails_IsAuthentication()
    {
        var auditEvent = CreateClientAuthenticationFailedEvent();

        Assert.Equal(AuditEventCategories.Authentication, auditEvent.EventCategory);
    }

    [Fact]
    public void Serialize_WhenClientAuthenticationFails_WritesTheAuthenticationMethodAsTheDetails()
    {
        var auditEvent = CreateClientAuthenticationFailedEvent();

        using var record = JsonDocument.Parse(AuditRecordWriter.SerializeToUtf8Bytes(auditEvent));

        Assert.Equal(
            """{"authentication_method":"private_key_jwt"}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    #endregion

    #region SubjectAuthenticationAuditEvent Tests

    [Fact]
    public void EventName_WhenASubjectAuthenticates_IsSubjectAuthentication()
    {
        var auditEvent = CreateSubjectAuthenticationEvent();

        Assert.Equal(OpenIdAuditEventNames.SubjectAuthentication, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenASubjectAuthenticates_IsAuthentication()
    {
        var auditEvent = CreateSubjectAuthenticationEvent();

        Assert.Equal(AuditEventCategories.Authentication, auditEvent.EventCategory);
    }

    #endregion

    #region OpenIdErrorEvent Tests

    [Fact]
    public void EventName_WhenAProtocolErrorIsReturned_IsError()
    {
        var auditEvent = CreateOpenIdErrorEvent();

        Assert.Equal(OpenIdAuditEventNames.Error, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenAProtocolErrorIsReturned_IsProtocol()
    {
        var auditEvent = CreateOpenIdErrorEvent();

        Assert.Equal(AuditEventCategories.Protocol, auditEvent.EventCategory);
    }

    #endregion

    #region SessionCreatedAuditEvent Tests

    [Fact]
    public void EventName_WhenASessionIsCreated_IsSessionCreated()
    {
        var auditEvent = CreateSessionCreatedEvent();

        Assert.Equal(OpenIdAuditEventNames.SessionCreated, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenASessionIsCreated_IsSession()
    {
        var auditEvent = CreateSessionCreatedEvent();

        Assert.Equal(AuditEventCategories.Session, auditEvent.EventCategory);
    }

    [Fact]
    public void Resources_WhenASessionIsCreated_ListsTheSession()
    {
        var auditEvent = CreateSessionCreatedEvent();

        var resource = Assert.Single(auditEvent.Resources);

        Assert.Equal(new AuditResource(AuditResourceTypes.Session, "sid-1"), resource);
    }

    #endregion

    #region SessionTerminatedAuditEvent Tests

    [Fact]
    public void EventName_WhenASessionIsTerminated_IsSessionTerminated()
    {
        var auditEvent = CreateSessionTerminatedEvent("sid-1");

        Assert.Equal(OpenIdAuditEventNames.SessionTerminated, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenASessionIsTerminated_IsSession()
    {
        var auditEvent = CreateSessionTerminatedEvent("sid-1");

        Assert.Equal(AuditEventCategories.Session, auditEvent.EventCategory);
    }

    [Fact]
    public void Resources_WhenASessionIsTerminated_ListsTheSession()
    {
        var auditEvent = CreateSessionTerminatedEvent("sid-1");

        var resource = Assert.Single(auditEvent.Resources);

        Assert.Equal(new AuditResource(AuditResourceTypes.Session, "sid-1"), resource);
    }

    [Fact]
    public void Resources_WhenTheTerminationIsSubjectWide_IsEmpty()
    {
        var auditEvent = CreateSessionTerminatedEvent(null);

        Assert.Empty(auditEvent.Resources);
    }

    [Fact]
    public void Serialize_WhenASessionIsTerminated_WritesTheTerminationReasonAsTheDetails()
    {
        var auditEvent = CreateSessionTerminatedEvent("sid-1");

        using var record = JsonDocument.Parse(AuditRecordWriter.SerializeToUtf8Bytes(auditEvent));

        Assert.Equal(
            """{"termination_reason":"logout"}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    #endregion

    #region Helpers

    private static TokenIssuedAuditEvent CreateTokenIssuedEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
            TokenType = "access_token",
        };

    private static TokenRevokedAuditEvent CreateTokenRevokedEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
        };

    private static RefreshTokenReplayAuditEvent CreateRefreshTokenReplayEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Failure,
        };

    private static RefreshTokenRetryAuditEvent CreateRefreshTokenRetryEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
        };

    private static AuthorizationGrantedAuditEvent CreateAuthorizationGrantedEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
        };

    private static AuthorizationDeniedAuditEvent CreateAuthorizationDeniedEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Failure,
        };

    private static ClientAuthenticationFailedAuditEvent CreateClientAuthenticationFailedEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Failure,
            AuthenticationMethod = "private_key_jwt",
        };

    private static SubjectAuthenticationAuditEvent CreateSubjectAuthenticationEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
        };

    private static OpenIdErrorEvent CreateOpenIdErrorEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Failure,
            ErrorCode = "invalid_request",
        };

    private static SessionCreatedAuditEvent CreateSessionCreatedEvent() =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
            SessionId = "sid-1",
        };

    private static SessionTerminatedAuditEvent CreateSessionTerminatedEvent(string? sessionId) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            TerminationReason = "logout",
        };

    #endregion
}
