// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Accounts.Management.Contracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.Management;

/// <summary>
/// Validates a local-account creation request and stages the account on a store manager. The management create
/// endpoint and every other caller that creates a local account share this one implementation. Register a custom
/// implementation to replace or extend the default behavior.
/// </summary>
[PublicAPI]
public interface ILocalAccountCreator
{
    /// <summary>
    /// Validates the request (username, password policy, claims and metadata) and stages the principal, its
    /// self-issued identity and the local account on the store manager without saving. The caller saves the store
    /// manager to persist the staged rows. Nothing is staged when <see cref="LocalAccountCreation.Error"/> is set.
    /// </summary>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="tenantId">The identifier of the tenant the account is created in.</param>
    /// <param name="request">The <see cref="CreateLocalAccountRequest"/> describing the account.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="LocalAccountCreation"/> carrying the staged account, or the <see cref="LocalAccountCreation.Error"/> describing the first failed precondition.</returns>
    ValueTask<LocalAccountCreation> StageAsync(
        IStoreManager storeManager,
        string tenantId,
        CreateLocalAccountRequest request,
        CancellationToken cancellationToken
    );
}
