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

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.Bff.Auditing;
using NCode.Identity.Bff.Options;
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
        var bffOptions = new BffOptions { ClientId = "bff-client" };
        var options = Microsoft.Extensions.Options.Options.Create(bffOptions);
        var timeProvider = new MutableTimeProvider(Now);
        Recorder = new DefaultBffAuditRecorder(MockPublisher.Object, options, timeProvider);
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
}
