// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace NCode.Identity.Events;

/// <summary>
/// Lets an <see cref="IEvent"/> declare metric attributes of its own, beyond the event type and audit action and
/// outcome, on the counter of published events. Each attribute must be bounded — a server-chosen value from a small
/// set, such as a token type — and never a tenant, client, subject, address, or anything a caller supplies.
/// </summary>
// See ADR-1197.
public interface ISupportMetricAttributes
{
    /// <summary>
    /// Adds this event's bounded metric attributes, named in the <c>ncode.identity.{area}.…</c> namespace.
    /// </summary>
    /// <param name="tags">The attributes of the measurement being recorded.</param>
    void AddMetricAttributes(ref TagList tags);
}
