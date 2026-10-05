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
/// Provides the canonical outcome codes for an <see cref="IAuditEvent.Outcome"/>.
/// </summary>
/// <remarks>
/// Outcome is an extensible string vocabulary, not a closed enumeration, so a consumer or sibling
/// package can introduce additional outcomes (for example <c>denied</c>, <c>error</c>, or
/// <c>challenge</c>) without a breaking change. Values are lower-case and stable, suitable for use as
/// a log or metric dimension.
/// </remarks>
[PublicAPI]
public static class AuditOutcome
{
    /// <summary>
    /// The outcome code indicating the audited operation completed successfully.
    /// </summary>
    public const string Success = "success";

    /// <summary>
    /// The outcome code indicating the audited operation failed.
    /// </summary>
    public const string Failure = "failure";

    /// <summary>
    /// The outcome code indicating the audited operation was denied (for example an authorization
    /// request that was refused, as opposed to an unexpected failure).
    /// </summary>
    public const string Denied = "denied";
}
