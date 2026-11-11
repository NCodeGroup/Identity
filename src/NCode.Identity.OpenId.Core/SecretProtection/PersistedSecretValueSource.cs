// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Finds the stored secrets that master keys protect: for a tenant owner, the secrets of the tenant, of its clients and
/// of its resource servers. Any other owner, the platform included, holds none.
/// </summary>
// See ADR-1407.
internal sealed class PersistedSecretValueSource(
    IStoreManagerFactory storeManagerFactory,
    ISecretProtectorProvider protectorProvider,
    ILogger<PersistedSecretValueSource> logger
) : PagedProtectedValueSource(storeManagerFactory, protectorProvider, logger)
{
    /// <inheritdoc />
    protected override bool Handles(SecretOwner owner) => owner.TenantId is not null;

    /// <inheritdoc />
    protected override ValueTask<PagedResult<PersistedProtectedValue>> GetPageAsync(
        IStoreManager storeManager,
        SecretOwner owner,
        IReadOnlyCollection<string> prefixes,
        PageRequest page,
        CancellationToken cancellationToken
    ) =>
        storeManager
            .GetStore<ISecretEnvelopeStore>()
            .GetPageAsync(owner, prefixes, page, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask<bool> UpdateAsync(
        IStoreManager storeManager,
        PersistedProtectedValue value,
        CancellationToken cancellationToken
    ) => storeManager.GetStore<ISecretEnvelopeStore>().UpdateAsync(value, cancellationToken);

    /// <inheritdoc />
    protected override SecretProtectionContext GetContext(PersistedProtectedValue value) =>
        SecretProtectionContext.ForPersistedSecret(value.ItemId);
}
