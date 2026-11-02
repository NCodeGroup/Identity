// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class AuditEventMetricAttributesTests
{
    #region AddMetricAttributes Tests

    [Fact]
    public void AddMetricAttributes_WhenATokenIsIssued_AddsItsTokenType()
    {
        var auditEvent = new TokenIssuedAuditEvent
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            TenantId = "tenant-1",
            Outcome = AuditOutcome.Success,
            TokenType = "access_token",
        };
        var tags = new TagList();

        auditEvent.AddMetricAttributes(ref tags);

        var tag = Assert.Single(tags);
        Assert.Equal(TokenIssuedAuditEvent.TokenTypeMetricAttribute, tag.Key);
        Assert.Equal("access_token", tag.Value);
    }

    [Fact]
    public void AddMetricAttributes_WhenClientAuthenticationFails_AddsTheAttemptedMethod()
    {
        var auditEvent = new ClientAuthenticationFailedAuditEvent
        {
            EventId = Guid.NewGuid(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            TenantId = "tenant-1",
            Outcome = AuditOutcome.Failure,
            AuthenticationMethod = OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
        };
        var tags = new TagList();

        auditEvent.AddMetricAttributes(ref tags);

        var tag = Assert.Single(tags);
        Assert.Equal(
            ClientAuthenticationFailedAuditEvent.AuthenticationMethodMetricAttribute,
            tag.Key
        );
        Assert.Equal(OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt, tag.Value);
    }

    #endregion
}
