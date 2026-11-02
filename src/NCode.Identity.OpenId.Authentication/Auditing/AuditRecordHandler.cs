// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Hands every audit event to the <see cref="AuditRecordQueue"/>, from which the <see cref="AuditRecordFlushService"/>
/// writes it to the audit store. Mapping and queuing are all that run with the audited operation, so the store's latency
/// never reaches it.
/// </summary>
// See ADR-1303.
internal sealed class AuditRecordHandler(
    IOptions<AuditRecordOptions> options,
    AuditRecordQueue queue
) : IEventHandler<IAuditEvent>
{
    private AuditRecordOptions Options { get; } = options.Value;

    private AuditRecordQueue Queue { get; } = queue;

    /// <inheritdoc />
    public ValueTask HandleAsync(IAuditEvent @event, CancellationToken cancellationToken)
    {
        if (Options.Enabled)
            Queue.TryEnqueue(AuditRecordMapper.Map(@event));

        return ValueTask.CompletedTask;
    }
}
