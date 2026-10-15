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

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Describes the optional criteria used to select persisted grants when listing or bulk-revoking them. Every criterion
/// is optional; an unset criterion does not constrain the result, and combining criteria narrows it (logical AND).
/// </summary>
/// <remarks>
/// This is a growable filter: new selection criteria are added as optional init-only properties so the
/// <see cref="IGrantStore"/> query and revocation surface can evolve without a breaking signature change.
/// </remarks>
[PublicAPI]
public readonly record struct GrantFilter
{
    /// <summary>
    /// Gets the subject whose grants are selected, or <c>null</c> to not constrain by subject.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// Gets the client whose grants are selected, or <c>null</c> to not constrain by client.
    /// </summary>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets the session whose grants are selected, or <c>null</c> to not constrain by session.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Gets the grant type selected, or <c>null</c> to not constrain by grant type.
    /// </summary>
    public string? GrantType { get; init; }

    /// <summary>
    /// Gets a value indicating whether no criterion is set (the filter selects every grant). Callers that must not
    /// perform an unscoped operation (such as a bulk revoke) should reject an empty filter.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrEmpty(SubjectId)
        && string.IsNullOrEmpty(ClientId)
        && string.IsNullOrEmpty(SessionId)
        && string.IsNullOrEmpty(GrantType);
}
