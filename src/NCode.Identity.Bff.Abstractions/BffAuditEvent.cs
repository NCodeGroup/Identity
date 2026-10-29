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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Bff;

/// <summary>
/// An audit event raised by the <c>Backend for Frontend (BFF)</c>.
/// </summary>
[PublicAPI]
public sealed record BffAuditEvent : AuditEvent, ISupportAuditDetails
{
    /// <summary>
    /// Gets the name of the event, such as one of the values of <see cref="BffAuditEventNames"/>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the category of the event.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets the reason for the event, such as a login failure reason or a session end reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <inheritdoc />
    public override string EventName => Name;

    /// <inheritdoc />
    public override string EventCategory => Category;

    /// <inheritdoc />
    /// <remarks>
    /// Writes the <c>reason</c> when there is one; no token, handle or cookie value is ever written.
    /// </remarks>
    public void WriteAuditDetails(Utf8JsonWriter writer)
    {
        if (Reason is not null)
            writer.WriteString("reason", Reason);
    }
}
