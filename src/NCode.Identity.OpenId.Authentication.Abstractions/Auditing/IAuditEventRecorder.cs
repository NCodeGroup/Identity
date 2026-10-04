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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Models;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Records audit events for the authentication subsystem by deriving the common audit envelope from
/// the ambient request context and publishing a strongly-typed audit event.
/// </summary>
/// <remarks>
/// This facade centralizes audit-envelope derivation so publishing sites state only what is specific
/// to their operation. Audit events carry identifiers and metadata only, never secret material.
/// </remarks>
[PublicAPI]
public interface IAuditEventRecorder
{
    /// <summary>
    /// Records that a security token was issued.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
    /// <param name="openIdClient">The <see cref="OpenIdClient"/> the token was issued to.</param>
    /// <param name="subjectId">The identifier of the subject the token was issued for, if any.</param>
    /// <param name="securityToken">The issued token; only its non-sensitive metadata is recorded.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask RecordTokenIssuedAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? subjectId,
        SecurityToken securityToken,
        CancellationToken cancellationToken
    );
}
