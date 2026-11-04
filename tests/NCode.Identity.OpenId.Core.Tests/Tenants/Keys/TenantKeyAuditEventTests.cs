// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.Json;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Tenants.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyAuditEventTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static TenantKeyAuditEvent CreateEvent(string? tenantId = "acme") =>
        new()
        {
            EventId = Guid.NewGuid(),
            EventTime = Now,
            EventSource = AuditEventSources.OpenId,
            TenantId = tenantId,
            Actor = AuditActor.System,
            Outcome = AuditOutcome.Success,
            Change = TenantKeyChanges.Generated,
            Reason = TenantKeyChangeReasons.Successor,
            SecretId = "secret-1",
            KeyId = "kid-1",
            SecretType = "rsa",
            Algorithm = "RS256",
            Use = "sig",
            ActivatesWhen = Now.AddDays(2),
            RetiresWhen = Now.AddDays(92),
            ExpiresWhen = Now.AddDays(99),
        };

    private static JsonElement ParseRecord(IAuditEvent auditEvent)
    {
        var output = new ArrayBufferWriter<byte>();
        AuditRecordWriter.Write(auditEvent, ref output);
        return JsonElement.Parse(output.WrittenSpan);
    }

    #region Shape Tests

    [Fact]
    public void EventName_IsTheTenantKeyPrefixAndTheChange()
    {
        Assert.Equal("tenant.key.generated", CreateEvent().EventName);
    }

    [Fact]
    public void EventCategory_IsKey()
    {
        Assert.Equal(AuditEventCategories.Key, CreateEvent().EventCategory);
    }

    [Fact]
    public void Severity_IsInfo()
    {
        Assert.Equal(AuditSeverities.Info, CreateEvent().Severity);
    }

    [Fact]
    public void Resources_ListTheSecretThenItsTenant()
    {
        Assert.Equal(
            [
                new AuditResource(AuditResourceTypes.TenantSecret, "secret-1"),
                new AuditResource(AuditResourceTypes.Tenant, "acme"),
            ],
            CreateEvent().Resources
        );
    }

    [Fact]
    public void Resources_WithoutATenant_ListOnlyTheSecret()
    {
        Assert.Equal(
            [new AuditResource(AuditResourceTypes.TenantSecret, "secret-1")],
            CreateEvent(tenantId: null).Resources
        );
    }

    [Fact]
    public void Serialize_WritesTheReasonTheKeyAndItsLifecycleAsTheDetails()
    {
        var record = ParseRecord(CreateEvent());

        Assert.Equal("tenant.key.generated", record.GetProperty("event_name").GetString());
        Assert.Equal("system", record.GetProperty("actor").GetProperty("type").GetString());
        Assert.False(record.TryGetProperty("request", out _));
        Assert.Equal(
            """{"reason":"successor","key_id":"kid-1","secret_type":"rsa","algorithm":"RS256","use":"sig","activates_when":"2026-11-06T12:00:00+00:00","retires_when":"2027-02-04T12:00:00+00:00","expires_when":"2027-02-11T12:00:00+00:00"}""",
            record.GetProperty("details").GetRawText()
        );
    }

    [Fact]
    public void Serialize_WhenTheKeyIsUnbound_OmitsTheAlgorithmAndUse()
    {
        var auditEvent = CreateEvent() with { Algorithm = null, Use = null };

        var details = ParseRecord(auditEvent).GetProperty("details");

        Assert.False(details.TryGetProperty("algorithm", out _));
        Assert.False(details.TryGetProperty("use", out _));
    }

    #endregion
}
