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
/// Provides the canonical actor type codes for an <see cref="AuditActor.Type"/>.
/// </summary>
/// <remarks>
/// Actor type is an extensible string vocabulary, not a closed enumeration, so a consumer or sibling
/// package can introduce additional actor types (for example <c>service_account</c>) without a
/// breaking change. Values are lower-case and stable, suitable for use as a log or index dimension.
/// </remarks>
[PublicAPI]
public static class AuditActorTypes
{
    /// <summary>
    /// The actor type code for an end user (a resource owner) identified by a subject identifier.
    /// </summary>
    public const string EndUser = "end_user";

    /// <summary>
    /// The actor type code for an OAuth client identified by a client identifier.
    /// </summary>
    public const string Client = "client";

    /// <summary>
    /// The actor type code for an administrator operating the management plane.
    /// </summary>
    public const string Administrator = "administrator";

    /// <summary>
    /// The actor type code for the system itself acting without a caller (for example an expiry or
    /// a background job).
    /// </summary>
    public const string System = "system";

    /// <summary>
    /// The actor type code for a caller that has not been identified.
    /// </summary>
    public const string Anonymous = "anonymous";
}
