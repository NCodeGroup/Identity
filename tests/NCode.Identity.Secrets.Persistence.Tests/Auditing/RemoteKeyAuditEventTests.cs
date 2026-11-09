// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Secrets.Persistence.Auditing;

public sealed class RemoteKeyAuditEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static RemoteKeyAuditEvent CreateEvent(
        string change = RemoteKeyChanges.Destroyed,
        string? ownerId = null,
        string? secretId = null
    ) =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = Now,
            EventSource = AuditEventSources.OpenId,
            TenantId = "acme",
            Actor = AuditActor.System,
            Outcome = AuditOutcome.Success,
            Change = change,
            Reason = RemoteKeyChangeReasons.Orphaned,
            Provider = "aws-kms",
            KeyId = "arn:aws:kms:us-west-2:111122223333:key/abcd",
            GroupName = "ncid-signing",
            OwnerId = ownerId,
            SecretId = secretId,
            KeyCreatedWhen = Now.AddDays(-2),
        };

    private static JsonElement ParseRecord(IAuditEvent auditEvent)
    {
        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(auditEvent, ref output);
        return JsonElement.Parse(output.WrittenSpan);
    }

    [Fact]
    public void EventName_IsTheRemoteKeyPrefixAndTheChange()
    {
        Assert.Equal("remote_key.destroyed", CreateEvent().EventName);
        Assert.Equal("remote_key.destroyed", RemoteKeyAuditEventNames.Destroyed);
        Assert.Equal(
            "remote_key.destroyed",
            RemoteKeyAuditEventNames.For(RemoteKeyChanges.Destroyed)
        );
    }

    [Fact]
    public void EventCategory_IsKey()
    {
        Assert.Equal(AuditEventCategories.Key, CreateEvent().EventCategory);
    }

    [Fact]
    public void Resources_ListTheKeyByItsReference()
    {
        Assert.Equal(
            [
                new AuditResource(
                    AuditResourceTypes.RemoteKey,
                    "aws-kms:arn:aws:kms:us-west-2:111122223333:key/abcd"
                ),
            ],
            CreateEvent().Resources
        );
    }

    [Fact]
    public void Serialize_WithAnOwnerAndASecret_WritesTheKeyAndItsOrigin()
    {
        var record = ParseRecord(CreateEvent(ownerId: "acme", secretId: "secret-1"));

        Assert.Equal("remote_key.destroyed", record.GetProperty("event_name").GetString());
        Assert.Equal("system", record.GetProperty("actor").GetProperty("type").GetString());
        Assert.Equal(
            """{"reason":"orphaned","provider":"aws-kms","key_id":"arn:aws:kms:us-west-2:111122223333:key/abcd","group":"ncid-signing","owner":"acme","secret_id":"secret-1","key_created_when":"2026-11-02T12:00:00+00:00"}""",
            record.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void Serialize_WithoutAnOwnerOrASecret_OmitsThem()
    {
        var details = ParseRecord(CreateEvent()).GetProperty("details");

        Assert.False(details.TryGetProperty("owner", out _));
        Assert.False(details.TryGetProperty("secret_id", out _));
        Assert.True(details.TryGetProperty("key_created_when", out _));
    }
}
