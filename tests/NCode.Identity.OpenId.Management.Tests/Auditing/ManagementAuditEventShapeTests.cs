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

namespace NCode.Identity.OpenId.Management.Auditing;

public sealed class ManagementAuditEventShapeTests
{
    #region GrantRevokedAuditEvent Tests

    [Fact]
    public void EventName_WhenAGrantIsRevoked_IsGrantRevoked()
    {
        var auditEvent = CreateGrantRevokedEvent("refresh_token");

        Assert.Equal(ManagementAuditEventNames.GrantRevoked, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenAGrantIsRevoked_IsManagement()
    {
        var auditEvent = CreateGrantRevokedEvent("refresh_token");

        Assert.Equal(AuditEventCategories.Management, auditEvent.EventCategory);
    }

    [Fact]
    public void Resources_WhenAGrantIsRevoked_IsEmpty()
    {
        var auditEvent = CreateGrantRevokedEvent("refresh_token");

        Assert.Empty(auditEvent.Resources);
    }

    [Fact]
    public void Serialize_WhenTheGrantTypeIsKnown_WritesTheGrantTypeAsTheDetails()
    {
        var auditEvent = CreateGrantRevokedEvent("refresh_token");

        using var record = JsonDocument.Parse(AuditRecordWriter.SerializeToUtf8Bytes(auditEvent));

        Assert.Equal(
            """{"grant_type":"refresh_token"}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void Serialize_WhenTheGrantTypeIsUnknown_WritesEmptyDetails()
    {
        var auditEvent = CreateGrantRevokedEvent(null);

        using var record = JsonDocument.Parse(AuditRecordWriter.SerializeToUtf8Bytes(auditEvent));

        Assert.Equal("{}", record.RootElement.GetProperty("details").GetRawText());
    }

    #endregion

    #region GrantsRevokedBulkAuditEvent Tests

    [Fact]
    public void EventName_WhenGrantsAreRevokedInBulk_IsGrantsRevokedBulk()
    {
        var auditEvent = CreateGrantsRevokedBulkEvent("sid-1");

        Assert.Equal(ManagementAuditEventNames.GrantsRevokedBulk, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenGrantsAreRevokedInBulk_IsManagement()
    {
        var auditEvent = CreateGrantsRevokedBulkEvent("sid-1");

        Assert.Equal(AuditEventCategories.Management, auditEvent.EventCategory);
    }

    [Fact]
    public void Resources_WhenTheRevocationIsFilteredBySession_ListsTheSession()
    {
        var auditEvent = CreateGrantsRevokedBulkEvent("sid-1");

        var resource = Assert.Single(auditEvent.Resources);

        Assert.Equal(new AuditResource(AuditResourceTypes.Session, "sid-1"), resource);
    }

    [Fact]
    public void Resources_WhenTheRevocationIsNotFilteredBySession_IsEmpty()
    {
        var auditEvent = CreateGrantsRevokedBulkEvent(null);

        Assert.Empty(auditEvent.Resources);
    }

    [Fact]
    public void Serialize_WhenGrantsAreRevokedInBulk_WritesTheRevokedCountAsTheDetails()
    {
        var auditEvent = CreateGrantsRevokedBulkEvent("sid-1");

        using var record = JsonDocument.Parse(AuditRecordWriter.SerializeToUtf8Bytes(auditEvent));

        Assert.Equal(
            """{"revoked_count":3}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    #endregion

    #region SessionsRevokedAuditEvent Tests

    [Fact]
    public void EventName_WhenSessionsAreRevoked_IsSessionsRevoked()
    {
        var auditEvent = CreateSessionsRevokedEvent("sid-1");

        Assert.Equal(ManagementAuditEventNames.SessionsRevoked, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_WhenSessionsAreRevoked_IsManagement()
    {
        var auditEvent = CreateSessionsRevokedEvent("sid-1");

        Assert.Equal(AuditEventCategories.Management, auditEvent.EventCategory);
    }

    [Fact]
    public void Resources_WhenASingleSessionIsRevoked_ListsTheSession()
    {
        var auditEvent = CreateSessionsRevokedEvent("sid-1");

        var resource = Assert.Single(auditEvent.Resources);

        Assert.Equal(new AuditResource(AuditResourceTypes.Session, "sid-1"), resource);
    }

    [Fact]
    public void Resources_WhenTheRevocationIsSubjectWide_IsEmpty()
    {
        var auditEvent = CreateSessionsRevokedEvent(null);

        Assert.Empty(auditEvent.Resources);
    }

    [Fact]
    public void Serialize_WhenSessionsAreRevoked_WritesTheRevokedCountAsTheDetails()
    {
        var auditEvent = CreateSessionsRevokedEvent("sid-1");

        using var record = JsonDocument.Parse(AuditRecordWriter.SerializeToUtf8Bytes(auditEvent));

        Assert.Equal(
            """{"revoked_count":2}""",
            record.RootElement.GetProperty("details").GetRawText()
        );
    }

    #endregion

    #region Helpers

    private static GrantRevokedAuditEvent CreateGrantRevokedEvent(string? grantType) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.Management,
            Outcome = AuditOutcome.Success,
            GrantType = grantType,
        };

    private static GrantsRevokedBulkAuditEvent CreateGrantsRevokedBulkEvent(string? sessionId) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.Management,
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            RevokedCount = 3,
        };

    private static SessionsRevokedAuditEvent CreateSessionsRevokedEvent(string? sessionId) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.Management,
            Outcome = AuditOutcome.Success,
            SessionId = sessionId,
            RevokedCount = 2,
        };

    #endregion
}
