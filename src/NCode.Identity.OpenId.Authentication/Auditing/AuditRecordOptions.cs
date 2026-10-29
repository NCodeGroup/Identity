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

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Contains the options for keeping audit records in the audit store, where an operator queries them after the logs that
/// also carry them have rotated.
/// </summary>
// See ADR-0303.
public sealed class AuditRecordOptions
{
    /// <summary>
    /// Gets the name of the subsection of the OpenID configuration section that binds these options.
    /// </summary>
    public const string SectionName = "AuditRecords";

    /// <summary>
    /// Gets or sets a value indicating whether audit records are kept in the audit store. The default is
    /// <see langword="true"/>. Audit records are logged whatever this says.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how often each replica writes the audit records it holds to the store. A replica that stops abruptly
    /// loses at most this much of its records from the store, never from the log. The default is five seconds. It is at
    /// least one millisecond and at most about 49.7 days, the periods a timer accepts.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets how many audit records a replica holds for the store before it drops new ones, which it counts and
    /// logs. The default is 10,000. It is at least one.
    /// </summary>
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets how long the store keeps an audit record. The default is 90 days, the event history of CloudTrail. It
    /// is at least one day.
    /// </summary>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(90);
}
