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

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.OpenId.Management.Auditing;

public sealed class DefaultManagementAuditRecorderTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private static HttpContext CreateHttpContext(string? actorId) =>
        new DefaultHttpContext
        {
            User = actorId is null
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actorId)])),
        };

    #region RecordGrantRevokedAsync Tests

    [Fact]
    public async Task RecordGrantRevokedAsync_PublishesGrantRevokedAuditEventWithMetadata()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        GrantRevokedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<GrantRevokedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (GrantRevokedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultManagementAuditRecorder(
            mockPublisher.Object,
            TimeProvider.System
        );

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordGrantRevokedAsync(
            httpContext,
            "tenant-1",
            "subject-1",
            "client-1",
            "authorization_code",
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("grant.revoked", captured.Action);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("authorization_code", captured.GrantType);
        Assert.Equal("admin-1", captured.ActorId);
        Assert.NotEqual(Guid.Empty, captured.EventId);
    }

    #endregion

    #region RecordGrantsRevokedAsync Tests

    [Fact]
    public async Task RecordGrantsRevokedAsync_PublishesBulkAuditEventWithCount()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        GrantsRevokedBulkAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<GrantsRevokedBulkAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (GrantsRevokedBulkAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultManagementAuditRecorder(
            mockPublisher.Object,
            TimeProvider.System
        );

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordGrantsRevokedAsync(
            httpContext,
            "tenant-1",
            "subject-1",
            clientId: null,
            revokedCount: 7,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("grant.revoked.bulk", captured.Action);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("subject-1", captured.SubjectId);
        Assert.Null(captured.ClientId);
        Assert.Equal(7, captured.RevokedCount);
        Assert.Equal("admin-1", captured.ActorId);
    }

    #endregion

    #region RecordClientChangedAsync Tests

    [Fact]
    public async Task RecordClientChangedAsync_PublishesClientChangedAuditEventWithSnapshot()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ClientChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<ClientChangedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (ClientChangedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultManagementAuditRecorder(
            mockPublisher.Object,
            TimeProvider.System
        );

        var httpContext = CreateHttpContext("admin-1");
        var resourceValues = JsonSerializer.SerializeToElement(new { isDisabled = false });

        await recorder.RecordClientChangedAsync(
            httpContext,
            "tenant-1",
            "client-1",
            ResourceChangeTypes.Created,
            resourceValues,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("client.created", captured.Action);
        Assert.Equal(ManagementResourceTypes.Client, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Created, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("client-1", captured.ResourceId);
        Assert.Equal("admin-1", captured.ActorId);
        Assert.NotNull(captured.ResourceValues);
    }

    #endregion

    #region RecordClientSecretChangedAsync Tests

    [Fact]
    public async Task RecordClientSecretChangedAsync_PublishesSecretChangedAuditEventWithIds()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        ClientSecretChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<ClientSecretChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (ClientSecretChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultManagementAuditRecorder(
            mockPublisher.Object,
            TimeProvider.System
        );

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordClientSecretChangedAsync(
            httpContext,
            "tenant-1",
            "client-1",
            "secret-1",
            ResourceChangeTypes.Deleted,
            resourceValues: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("client.secret.deleted", captured.Action);
        Assert.Equal(ManagementResourceTypes.ClientSecret, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Deleted, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("client-1", captured.ClientId);
        Assert.Equal("secret-1", captured.ResourceId);
        Assert.Equal("admin-1", captured.ActorId);
        Assert.Null(captured.ResourceValues);
    }

    #endregion

    #region RecordTenantChangedAsync Tests

    [Fact]
    public async Task RecordTenantChangedAsync_PublishesTenantChangedAuditEventWithSnapshot()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        TenantChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(It.IsAny<TenantChangedAuditEvent>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (TenantChangedAuditEvent auditEvent, CancellationToken _) => captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultManagementAuditRecorder(
            mockPublisher.Object,
            TimeProvider.System
        );

        var httpContext = CreateHttpContext("admin-1");
        var resourceValues = JsonSerializer.SerializeToElement(new { isDisabled = false });

        await recorder.RecordTenantChangedAsync(
            httpContext,
            "tenant-1",
            ResourceChangeTypes.Updated,
            resourceValues,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("tenant.updated", captured.Action);
        Assert.Equal(ManagementResourceTypes.Tenant, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Updated, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("tenant-1", captured.ResourceId);
        Assert.Equal("admin-1", captured.ActorId);
        Assert.NotNull(captured.ResourceValues);
    }

    #endregion

    #region RecordTenantSecretChangedAsync Tests

    [Fact]
    public async Task RecordTenantSecretChangedAsync_PublishesSecretChangedAuditEventWithIds()
    {
        var mockPublisher = MockRepository.Create<IEventPublisher>();

        TenantSecretChangedAuditEvent? captured = null;
        mockPublisher
            .Setup(x =>
                x.PublishAsync(
                    It.IsAny<TenantSecretChangedAuditEvent>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (TenantSecretChangedAuditEvent auditEvent, CancellationToken _) =>
                    captured = auditEvent
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var recorder = new DefaultManagementAuditRecorder(
            mockPublisher.Object,
            TimeProvider.System
        );

        var httpContext = CreateHttpContext("admin-1");

        await recorder.RecordTenantSecretChangedAsync(
            httpContext,
            "tenant-1",
            "secret-1",
            ResourceChangeTypes.Created,
            resourceValues: null,
            CancellationToken.None
        );

        Assert.NotNull(captured);
        Assert.Equal("tenant.secret.created", captured.Action);
        Assert.Equal(ManagementResourceTypes.TenantSecret, captured.ResourceType);
        Assert.Equal(ResourceChangeTypes.Created, captured.ChangeType);
        Assert.Equal(AuditOutcome.Success, captured.Outcome);
        Assert.Equal("tenant-1", captured.TenantId);
        Assert.Equal("secret-1", captured.ResourceId);
        Assert.Equal("admin-1", captured.ActorId);
        Assert.Null(captured.ResourceValues);
    }

    #endregion
}
