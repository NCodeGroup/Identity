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
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// Provides the seam through which a host provisions and mutates local accounts — the server-owned credential, profile,
/// and status records behind self-issued connections. It is the write counterpart to <see cref="ILocalAccountSource"/>
/// (which reads and validates): an implementation hashes the credential, generates the opaque account id and security
/// stamp, and persists the account together with its one-to-one self-issued federated identity and owning principal
/// (ADR-0057). A reference implementation is supplied by an opt-in persistence package.
/// </summary>
/// <remarks>
/// Every operation participates in the caller's <see cref="IStoreManager"/> unit of work and stages its changes without
/// committing them, so the caller controls the transaction boundary (and may compose additional work, such as recording
/// ownership, in the same unit of work). The caller is responsible for saving the unit of work.
/// </remarks>
[PublicAPI]
public interface ILocalAccountProvisioner
{
    /// <summary>
    /// Creates a new local account in the ambient tenant from the supplied credential and profile, together with its
    /// one-to-one self-issued federated identity and owning principal (ADR-0057), returning the server-generated
    /// identifier (the self-issued connection's subject, also the account's <see cref="LocalAccount.Subject"/>).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, carrying the owning tenant.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the write participates in.</param>
    /// <param name="request">The account creation request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the server-generated
    /// account identifier.</returns>
    ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        LocalAccountCreationRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the mutable profile fields (username, email, and email-verified status) of an existing local account.
    /// The credential and enabled status are managed through <see cref="ResetPasswordAsync"/> and
    /// <see cref="SetEnabledAsync"/> respectively.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, carrying the owning tenant.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the write participates in.</param>
    /// <param name="request">The account update request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        LocalAccountUpdateRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Enables or disables an existing local account. A disabled account must not be allowed to authenticate or to
    /// satisfy an active-status check.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, carrying the owning tenant.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the write participates in.</param>
    /// <param name="localAccountId">The opaque identifier of the local account to enable or disable.</param>
    /// <param name="isEnabled">The desired enabled state.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask SetEnabledAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        string localAccountId,
        bool isEnabled,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Replaces the credential of an existing local account and rotates its security stamp, so that outstanding
    /// sessions and tokens issued before the reset are invalidated.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> for the current request, carrying the owning tenant.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> whose unit of work the write participates in.</param>
    /// <param name="request">The password-reset request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ResetPasswordAsync(
        OpenIdContext openIdContext,
        IStoreManager storeManager,
        LocalAccountPasswordResetRequest request,
        CancellationToken cancellationToken
    );
}
