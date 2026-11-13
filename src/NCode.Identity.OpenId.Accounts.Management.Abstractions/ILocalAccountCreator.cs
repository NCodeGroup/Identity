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
    /// Validates that the username is not taken in the tenant and that the password satisfies the tenant's password
    /// policy, then stages the owning federated principal (with the request's profile and system metadata, when
    /// present), the account's self-issued federated identity and the local account (with its claims and password
    /// hash) on the store manager. The method does not save: the caller saves the store manager to persist the staged
    /// rows, alone or together with other changes in the same unit of work. Nothing is staged when
    /// <see cref="LocalAccountCreation.Error"/> is set.
    /// </summary>
    /// <param name="storeManager">The <see cref="IStoreManager"/> that reads the existing accounts and the tenant and receives the staged rows.</param>
    /// <param name="tenantId">The identifier of the tenant the account is created in.</param>
    /// <param name="request">The <see cref="CreateLocalAccountRequest"/> describing the account.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="LocalAccountCreation"/> carrying the staged principal identifier and account, or, when a check
    /// refuses the request, a <see cref="LocalAccountCreation"/> whose <see cref="LocalAccountCreation.Error"/> holds the
    /// first refusal. The default implementation reports 409 when the username is taken, 404 when the tenant is not
    /// found and 400 when the password policy rejects the password.
    /// </returns>
    ValueTask<LocalAccountCreation> StageAsync(
        IStoreManager storeManager,
        string tenantId,
        CreateLocalAccountRequest request,
        CancellationToken cancellationToken
    );
}
