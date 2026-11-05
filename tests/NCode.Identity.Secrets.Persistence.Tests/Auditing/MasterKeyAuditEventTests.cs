// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.Auditing;

public sealed class MasterKeyAuditEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid KeyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RetiredKeyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static MasterKeyAuditEvent CreateEvent(
        string change = MasterKeyChanges.Rotated,
        Guid? retiredKeyId = null,
        int? retiredVersion = null
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
            Reason = MasterKeyChangeReasons.RotationPeriodElapsed,
            OwnerType = SecretOwnerTypes.Tenant,
            OwnerId = "acme",
            KeyId = KeyId,
            Version = 2,
            RootKeyProvider = "local",
            RetiredKeyId = retiredKeyId,
            RetiredVersion = retiredVersion,
        };

    private static JsonElement ParseRecord(IAuditEvent auditEvent)
    {
        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(auditEvent, ref output);
        return JsonElement.Parse(output.WrittenSpan);
    }

    [Theory]
    [InlineData(MasterKeyChanges.Rotated, "master_key.rotated")]
    [InlineData(MasterKeyChanges.Destroyed, "master_key.destroyed")]
    public void EventName_IsTheMasterKeyPrefixAndTheChange(string change, string expected)
    {
        Assert.Equal(expected, CreateEvent(change).EventName);
        Assert.Equal(expected, MasterKeyAuditEventNames.For(change));
    }

    [Fact]
    public void EventCategory_IsKey()
    {
        Assert.Equal(AuditEventCategories.Key, CreateEvent().EventCategory);
    }

    [Fact]
    public void Resources_ListTheMasterKeyThenItsOwner()
    {
        Assert.Equal(
            [
                new AuditResource(AuditResourceTypes.MasterKey, KeyId.ToString("D")),
                new AuditResource("tenant", "acme"),
            ],
            CreateEvent().Resources
        );
    }

    [Fact]
    public void Serialize_ARotation_WritesTheKeyAndTheRetiredKeyAsTheDetails()
    {
        var record = ParseRecord(CreateEvent(retiredKeyId: RetiredKeyId, retiredVersion: 1));

        Assert.Equal("master_key.rotated", record.GetProperty("event_name").GetString());
        Assert.Equal("system", record.GetProperty("actor").GetProperty("type").GetString());
        Assert.Equal(
            """{"reason":"rotation_period_elapsed","key_id":"11111111-1111-1111-1111-111111111111","version":2,"root_key_provider":"local","retired_key_id":"22222222-2222-2222-2222-222222222222","retired_version":1}""",
            record.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void Serialize_WithoutARetiredKey_OmitsIt()
    {
        var details = ParseRecord(CreateEvent(MasterKeyChanges.Destroyed)).GetProperty("details");

        Assert.False(details.TryGetProperty("retired_key_id", out _));
        Assert.False(details.TryGetProperty("retired_version", out _));
    }
}
