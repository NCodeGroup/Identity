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

using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class DefaultAuditEventRecorderTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    #region RecordTokenIssuedAsync Tests

    [Fact]
    public async Task RecordTokenIssuedAsync_PublishesTokenIssuedAuditEventWithMetadata()
    {
        var mockContext = MockRepository.Create<OpenIdContext>();
        var mockTenant = MockRepository.Create<OpenIdTenant>();
        var mockClient = MockRepository.Create<OpenIdClient>();
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.Setup(x => x.TenantId).Returns("tenant-1").Verifiable();
        mockClient.Setup(x => x.ClientId).Returns("client-1").Verifiable();

        TokenIssuedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<TokenIssuedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (TokenIssuedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultAuditEventRecorder(mockPublisher.Object, TimeProvider.System);

        var securityToken = new SecurityToken
        {
            TokenType = "access_token",
            TokenValue = "the-secret-token-value",
            TokenLifetime = default,
        };

        await recorder.RecordTokenIssuedAsync(
            mockContext.Object,
            mockClient.Object,
            "subject-1",
            securityToken,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("token.issued", captured.Action);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("access_token", captured.TokenType);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.NotEqual(Guid.Empty, captured.EventId);
    }

    #endregion
}
