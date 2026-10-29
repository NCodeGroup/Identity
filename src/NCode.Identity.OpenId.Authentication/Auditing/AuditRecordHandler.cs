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

using Microsoft.Extensions.Options;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Hands every audit event to the <see cref="AuditRecordQueue"/>, from which the <see cref="AuditRecordFlushService"/>
/// writes it to the audit store. Mapping and queuing are all that run with the audited operation, so the store's latency
/// never reaches it.
/// </summary>
// See ADR-0303.
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
