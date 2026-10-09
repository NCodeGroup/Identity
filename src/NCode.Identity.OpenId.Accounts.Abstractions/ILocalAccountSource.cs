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
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Provides the pluggable seam through which the OpenID runtime interrogates the host's local accounts: the
/// server-owned credential, profile, and status records behind self-issued connections. A host registers an
/// implementation (or an opt-in reference package supplies one) to enable the resource-owner password grant,
/// active-status validation, and profile-claim enrichment; when none is registered, the no-op default leaves the
/// runtime's behavior unchanged.
/// </summary>
/// <remarks>
/// Additional capabilities (such as lockout, two-factor, or password-reset) are introduced as separate capability
/// interfaces that the runtime feature-detects on the registered source, so this core seam stays stable as the surface
/// grows.
/// </remarks>
[PublicAPI]
public interface ILocalAccountSource
{
    /// <summary>
    /// Validates a username and password credential against the local account store.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request (carrying the tenant and
    /// its settings).</param>
    /// <param name="userName">The username (login handle) presented by the end-user.</param>
    /// <param name="password">The UTF-8 bytes of the password presented by the end-user, carried in a caller-owned
    /// buffer so the sensitive material can be zeroed after use and never lingers as a managed <see cref="string"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the authenticated
    /// <see cref="LocalAccount"/> when the credential is valid; otherwise <c>null</c>.</returns>
    ValueTask<LocalAccount?> ValidateCredentialsAsync(
        OpenIdContext openIdContext,
        string userName,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a local account by its <see cref="LocalAccount.Subject"/> value (the subject of its self-issued
    /// connection identity), used to read the account's status and profile claims for an already-authenticated subject.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request (carrying the tenant and
    /// its settings).</param>
    /// <param name="subject">The account's self-issued subject value.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="LocalAccount"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<LocalAccount?> FindBySubjectAsync(
        OpenIdContext openIdContext,
        string subject,
        CancellationToken cancellationToken
    );
}
