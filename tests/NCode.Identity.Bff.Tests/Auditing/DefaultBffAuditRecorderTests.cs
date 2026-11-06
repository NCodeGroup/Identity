// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Tests.Frontends;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Bff.Tests.Auditing;

public class DefaultBffAuditRecorderTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 0, 0, TimeSpan.Zero);

    private Mock<IEventPublisher> MockPublisher { get; }
    private DefaultBffAuditRecorder Recorder { get; }
    private BffAuditEvent? Captured { get; set; }

    public DefaultBffAuditRecorderTests()
    {
        MockPublisher = CreateStrictMock<IEventPublisher>();
        var frontends = BffTestFrontends.CreateCatalog(x => x.ClientId = "bff-client");
        var timeProvider = new FakeTimeProvider(Now);
        Recorder = new DefaultBffAuditRecorder(MockPublisher.Object, frontends, timeProvider);
    }

    private void SetupPublish() =>
        MockPublisher
            .Setup(x => x.PublishAsync(It.IsAny<BffAuditEvent>(), CancellationToken.None))
            .Callback<BffAuditEvent, CancellationToken>((e, _) => Captured = e)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #region LoginSucceededAsync Tests

    [Fact]
    public async Task LoginSucceededAsync_Always_PublishesAnAuthenticationSuccess()
    {
        SetupPublish();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";

        await Recorder.LoginSucceededAsync(httpContext, "sub-1", CancellationToken.None);

        var auditEvent = Assert.IsType<BffAuditEvent>(Captured);
        Assert.Equal(BffAuditEventNames.LoginSucceeded, auditEvent.Name);
        Assert.Equal(AuditEventCategories.Authentication, auditEvent.Category);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal(BffAuditEventNames.Source, auditEvent.EventSource);
        Assert.Equal(AuditActor.EndUser("sub-1"), auditEvent.Actor);
        Assert.Equal("sub-1", auditEvent.SubjectId);
        Assert.Equal("bff-client", auditEvent.ClientId);
        Assert.Equal(Now, auditEvent.EventTime);
        Assert.Equal("GET", auditEvent.Request?.HttpMethod);
    }

    #endregion

    #region SessionEndedAsync Tests

    [Fact]
    public async Task SessionEndedAsync_LoggedOut_UsesTheEndUserAsActor()
    {
        SetupPublish();

        await Recorder.SessionEndedAsync(null, "sub-1", "logged_out", CancellationToken.None);

        var auditEvent = Assert.IsType<BffAuditEvent>(Captured);
        Assert.Equal(BffAuditEventNames.SessionEnded, auditEvent.Name);
        Assert.Equal(AuditEventCategories.Session, auditEvent.Category);
        Assert.Equal("logged_out", auditEvent.Reason);
        Assert.Equal(AuditActor.EndUser("sub-1"), auditEvent.Actor);
        Assert.Null(auditEvent.Request);
    }

    [Fact]
    public async Task SessionEndedAsync_OtherReason_UsesTheSystemAsActor()
    {
        SetupPublish();

        await Recorder.SessionEndedAsync(null, "sub-1", "expired", CancellationToken.None);

        var auditEvent = Assert.IsType<BffAuditEvent>(Captured);
        Assert.Equal(AuditActor.System, auditEvent.Actor);
        Assert.Equal(AuditOutcome.Success, auditEvent.Outcome);
        Assert.Equal("expired", auditEvent.Reason);
    }

    #endregion

    #region AntiforgeryRejectedAsync Tests

    [Fact]
    public async Task AntiforgeryRejectedAsync_Always_PublishesADeniedProtocolEvent()
    {
        SetupPublish();

        await Recorder.AntiforgeryRejectedAsync(
            new DefaultHttpContext(),
            "missing_header",
            CancellationToken.None
        );

        var auditEvent = Assert.IsType<BffAuditEvent>(Captured);
        Assert.Equal(AuditEventCategories.Protocol, auditEvent.Category);
        Assert.Equal(AuditOutcome.Denied, auditEvent.Outcome);
        Assert.Equal("missing_header", auditEvent.Error?.Code);
        Assert.Equal(AuditActor.Anonymous, auditEvent.Actor);
    }

    #endregion

    #region LoginFailedAsync Tests

    [Fact]
    public async Task LoginFailedAsync_RequestWithoutAFrontend_PublishesANullClientId()
    {
        var frontends = BffTestFrontends.CreateCatalog(
            new BffOptions
            {
                Frontends =
                {
                    ["admin"] = BffTestFrontends.CreateOptions(x =>
                        x.Selection = new BffFrontendSelectionOptions
                        {
                            Rule = BffFrontendSelectorNames.Host,
                            Values = ["admin.example.com"],
                        }
                    ),
                },
            }
        );
        var recorder = new DefaultBffAuditRecorder(
            MockPublisher.Object,
            frontends,
            new FakeTimeProvider(Now)
        );
        SetupPublish();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("app.example.com");

        await recorder.LoginFailedAsync(httpContext, "denied", CancellationToken.None);

        var auditEvent = Assert.IsType<BffAuditEvent>(Captured);
        Assert.Null(auditEvent.ClientId);
    }

    #endregion
}
