// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.Auditing;

public sealed class RootKeyAuditEventTests
{
    private const string KeyReference = "arn:aws:kms:us-west-2:111122223333:key/abcd";

    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static RootKeyAuditEvent CreateEvent(
        string change = RootKeyChanges.Set,
        string outcome = AuditOutcome.Success,
        string? failure = null,
        string? accessIdentity = null
    ) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = Now,
            EventSource = AuditEventSources.OpenId,
            TenantId = "acme",
            Actor = AuditActor.System,
            Outcome = outcome,
            Change = change,
            Reason = RootKeyChangeReasons.Requested,
            OwnerType = SecretOwnerTypes.Tenant,
            OwnerId = "acme",
            KeyWrapperName = "aws-kms",
            KeyReference = KeyReference,
            AccessIdentity = accessIdentity,
            State = OwnerRootKeyStates.Active,
            Failure = failure,
        };

    private static JsonElement ParseRecord(IAuditEvent auditEvent)
    {
        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(auditEvent, ref output);
        return JsonElement.Parse(output.WrittenSpan);
    }

    [Theory]
    [InlineData(RootKeyChanges.Set, "root_key.set")]
    [InlineData(RootKeyChanges.Removed, "root_key.removed")]
    [InlineData(RootKeyChanges.Unavailable, "root_key.unavailable")]
    [InlineData(RootKeyChanges.Restored, "root_key.restored")]
    public void EventName_IsTheRootKeyPrefixAndTheChange(string change, string expected)
    {
        Assert.Equal(expected, CreateEvent(change).EventName);
        Assert.Equal(expected, RootKeyAuditEventNames.For(change));
    }

    [Fact]
    public void EventCategory_IsKey()
    {
        Assert.Equal(AuditEventCategories.Key, CreateEvent().EventCategory);
    }

    [Fact]
    public void Resources_ListTheRootKeyThenItsOwner()
    {
        Assert.Equal(
            [
                new AuditResource(AuditResourceTypes.RootKey, KeyReference),
                new AuditResource("tenant", "acme"),
            ],
            CreateEvent().Resources
        );
    }

    [Fact]
    public void Serialize_WithoutAFailure_WritesTheProviderReferenceAndState()
    {
        var record = ParseRecord(CreateEvent());

        Assert.Equal("root_key.set", record.GetProperty("event_name").GetString());
        Assert.Equal(
            """{"reason":"requested","provider":"aws-kms","key_reference":"arn:aws:kms:us-west-2:111122223333:key/abcd","state":"active"}""",
            record.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void Serialize_WithAnAccessIdentity_WritesItAfterTheKeyReference()
    {
        var record = ParseRecord(
            CreateEvent(accessIdentity: "arn:aws:iam::111122223333:role/tenant-access")
        );

        Assert.Equal(
            """{"reason":"requested","provider":"aws-kms","key_reference":"arn:aws:kms:us-west-2:111122223333:key/abcd","access_identity":"arn:aws:iam::111122223333:role/tenant-access","state":"active"}""",
            record.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void Serialize_WithoutAnAccessIdentity_OmitsIt()
    {
        var record = ParseRecord(CreateEvent());

        Assert.False(record.GetProperty("details").TryGetProperty("access_identity", out _));
    }

    [Fact]
    public void Serialize_WithAFailure_WritesIt()
    {
        var record = ParseRecord(
            CreateEvent(RootKeyChanges.Unavailable, AuditOutcome.Failure, "AccessDenied: revoked")
        );

        Assert.Equal(
            "AccessDenied: revoked",
            record.GetProperty("details").GetProperty("failure").GetString()
        );
    }

    [Fact]
    public void Severity_ByOutcome_IsInformationalForSuccessAndWarningForFailure()
    {
        Assert.NotEqual(
            CreateEvent().Severity,
            CreateEvent(outcome: AuditOutcome.Failure).Severity
        );
        Assert.Equal(
            "warning",
            CreateEvent(RootKeyChanges.Unavailable, AuditOutcome.Failure).Severity
        );
    }
}
