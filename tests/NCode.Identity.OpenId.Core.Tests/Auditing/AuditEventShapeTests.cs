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

    #endregion
}
