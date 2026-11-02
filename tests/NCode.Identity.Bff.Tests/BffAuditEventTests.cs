// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text.Json;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffAuditEventTests : BaseTests
{
    private static BffAuditEvent CreateEvent(string? reason) =>
        new()
        {
            EventSource = BffAuditEventNames.Source,
            Outcome = "success",
            Name = BffAuditEventNames.LoginFailed,
            Category = "authentication",
            Reason = reason,
        };

    private static string WriteDetails(BffAuditEvent auditEvent)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            auditEvent.WriteAuditDetails(writer);
            writer.WriteEndObject();
        }

        return SecureEncoding.UTF8.GetString(stream.ToArray());
    }

    #region EventName and EventCategory Tests

    [Fact]
    public void EventName_Always_EchoesName()
    {
        var auditEvent = CreateEvent(null);

        Assert.Equal(BffAuditEventNames.LoginFailed, auditEvent.EventName);
    }

    [Fact]
    public void EventCategory_Always_EchoesCategory()
    {
        var auditEvent = CreateEvent(null);

        Assert.Equal("authentication", auditEvent.EventCategory);
    }

    #endregion

    #region WriteAuditDetails Tests

    [Fact]
    public void WriteAuditDetails_WithReason_WritesReason()
    {
        var auditEvent = CreateEvent("x");

        var json = WriteDetails(auditEvent);

        Assert.Equal("{\"reason\":\"x\"}", json);
    }

    [Fact]
    public void WriteAuditDetails_WithoutReason_WritesNothing()
    {
        var auditEvent = CreateEvent(null);

        var json = WriteDetails(auditEvent);

        Assert.Equal("{}", json);
    }

    #endregion

    #region HasAuditDetails Property Tests

    [Fact]
    public void HasAuditDetails_WithReason_IsTrue()
    {
        var auditEvent = CreateEvent("x");

        Assert.True(auditEvent.HasAuditDetails);
    }

    [Fact]
    public void HasAuditDetails_WithoutReason_IsFalse()
    {
        var auditEvent = CreateEvent(null);

        Assert.False(auditEvent.HasAuditDetails);
    }

    #endregion

    #region AddMetricAttributes Tests

    [Fact]
    public void AddMetricAttributes_WithReason_AddsTheReason()
    {
        var auditEvent = CreateEvent(BffLoginFailureReasons.IssuerMismatch);
        var tags = new TagList();

        auditEvent.AddMetricAttributes(ref tags);

        var tag = Assert.Single(tags);
        Assert.Equal(BffAuditEvent.ReasonMetricAttribute, tag.Key);
        Assert.Equal(BffLoginFailureReasons.IssuerMismatch, tag.Value);
    }

    [Fact]
    public void AddMetricAttributes_WithoutReason_AddsNothing()
    {
        var auditEvent = CreateEvent(null);
        var tags = new TagList();

        auditEvent.AddMetricAttributes(ref tags);

        Assert.Empty(tags);
    }

    #endregion
}
