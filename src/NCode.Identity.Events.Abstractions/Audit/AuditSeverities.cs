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

using JetBrains.Annotations;

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Provides the canonical severity codes for an audit record.
/// </summary>
/// <remarks>
/// Severity is an extensible string vocabulary, not a closed enumeration, so a consumer or sibling
/// package can introduce additional severities without a breaking change. Values are lower-case and
/// stable, suitable for use as a log or index dimension.
/// </remarks>
[PublicAPI]
public static class AuditSeverities
{
    /// <summary>
    /// The severity code for a routine audited operation that needs no attention.
    /// </summary>
    public const string Info = "info";

    /// <summary>
    /// The severity code for an audited operation that failed or was refused and may warrant review.
    /// </summary>
    public const string Warning = "warning";

    /// <summary>
    /// The severity code for an audited operation that indicates a likely security incident (for
    /// example the replay of a refresh token).
    /// </summary>
    public const string Critical = "critical";
}
