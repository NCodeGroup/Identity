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
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Bff.Auditing;

/// <summary>
/// Provides the default implementation of <see cref="IBffAuditRecorder"/> that publishes a
/// <see cref="BffAuditEvent"/> through the event seam.
/// </summary>
internal class DefaultBffAuditRecorder(
    IEventPublisher publisher,
    IOptions<BffOptions> options,
    TimeProvider timeProvider
) : IBffAuditRecorder
{
    private IEventPublisher Publisher { get; } = publisher;
    private IOptions<BffOptions> Options { get; } = options;
    private TimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc />
    public ValueTask LoginSucceededAsync(
        HttpContext httpContext,
        string subject,
        CancellationToken cancellationToken
    ) =>
        PublishAsync(
            BffAuditEventNames.LoginSucceeded,
            AuditEventCategories.Authentication,
            AuditOutcome.Success,
            AuditActor.EndUser(subject),
            httpContext,
            subject,
            null,
            null,
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask LoginFailedAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken
    ) =>
        PublishAsync(
            BffAuditEventNames.LoginFailed,
            AuditEventCategories.Authentication,
            AuditOutcome.Failure,
            AuditActor.Anonymous,
            httpContext,
            null,
            null,
            new AuditError(reason),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask SessionEndedAsync(
        HttpContext? httpContext,
        string subject,
        string reason,
        CancellationToken cancellationToken
    )
    {
        var actor = string.Equals(reason, BffSessionEndReasons.LoggedOut, StringComparison.Ordinal)
            ? AuditActor.EndUser(subject)
            : AuditActor.System;

        return PublishAsync(
            BffAuditEventNames.SessionEnded,
            AuditEventCategories.Session,
            AuditOutcome.Success,
            actor,
            httpContext,
            subject,
            reason,
            null,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public ValueTask TokenRefreshFailedAsync(
        HttpContext httpContext,
        string subject,
        string reason,
        CancellationToken cancellationToken
    ) =>
        PublishAsync(
            BffAuditEventNames.TokenRefreshFailed,
            AuditEventCategories.Token,
            AuditOutcome.Failure,
            AuditActor.EndUser(subject),
            httpContext,
            subject,
            null,
            new AuditError(reason),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask BackchannelLogoutReceivedAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) =>
        PublishAsync(
            BffAuditEventNames.BackchannelLogoutReceived,
            AuditEventCategories.Session,
            AuditOutcome.Success,
            AuditActor.System,
            httpContext,
            null,
            null,
            null,
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask BackchannelLogoutRejectedAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken
    ) =>
        PublishAsync(
            BffAuditEventNames.BackchannelLogoutRejected,
            AuditEventCategories.Session,
            AuditOutcome.Failure,
            AuditActor.Anonymous,
            httpContext,
            null,
            null,
            new AuditError(reason),
            cancellationToken
        );

    /// <inheritdoc />
    public ValueTask AntiforgeryRejectedAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken
    ) =>
        PublishAsync(
            BffAuditEventNames.AntiforgeryRejected,
            AuditEventCategories.Protocol,
            AuditOutcome.Denied,
            AuditActor.Anonymous,
            httpContext,
            null,
            null,
            new AuditError(reason),
            cancellationToken
        );

    private ValueTask PublishAsync(
        string name,
        string category,
        string outcome,
        AuditActor actor,
        HttpContext? httpContext,
        string? subject,
        string? reason,
        AuditError? error,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var request = httpContext is null ? null : BffAuditRequestFactory.Create(httpContext);
        var auditEvent = new BffAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = BffAuditEventNames.Source,
            Name = name,
            Category = category,
            ClientId = Options.Value.ClientId,
            SubjectId = subject,
            Actor = actor,
            Request = request,
            Outcome = outcome,
            Reason = reason,
            Error = error,
        };

        return Publisher.PublishAsync(auditEvent, cancellationToken);
    }
}
