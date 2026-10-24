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

using System.Diagnostics;

namespace NCode.Identity.Events;

/// <summary>
/// Lets an <see cref="IEvent"/> declare metric attributes of its own, beyond the event type and audit action and
/// outcome, on the counter of published events. Each attribute must be bounded — a server-chosen value from a small
/// set, such as a token type — and never a tenant, client, subject, address, or anything a caller supplies.
/// </summary>
// See ADR-0197.
public interface ISupportMetricAttributes
{
    /// <summary>
    /// Adds this event's bounded metric attributes, named in the <c>ncode.identity.{area}.…</c> namespace.
    /// </summary>
    /// <param name="tags">The attributes of the measurement being recorded.</param>
    void AddMetricAttributes(ref TagList tags);
}
