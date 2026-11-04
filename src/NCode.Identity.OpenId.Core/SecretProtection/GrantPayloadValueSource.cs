// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Finds the grant payloads that master keys protect: a grant is encrypted under the key of its tenant, so only a tenant
/// owner holds any.
/// </summary>
// See ADR-1407.
internal sealed class GrantPayloadValueSource(
    IStoreManagerFactory storeManagerFactory,
    ISecretProtectorProvider protectorProvider,
    ILogger<GrantPayloadValueSource> logger
) : PagedProtectedValueSource(storeManagerFactory, protectorProvider, logger)
{
    /// <inheritdoc />
    protected override bool Handles(SecretOwner owner) =>
        string.Equals(owner.OwnerType, SecretOwnerTypes.Tenant, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    protected override ValueTask<PagedResult<PersistedProtectedValue>> GetPageAsync(
        IStoreManager storeManager,
        SecretOwner owner,
        PageRequest page,
        CancellationToken cancellationToken
    ) =>
        storeManager
            .GetStore<IGrantStore>()
            .GetProtectedPayloadPageAsync(owner.OwnerId, page, cancellationToken);

    /// <inheritdoc />
    protected override ValueTask UpdateAsync(
        IStoreManager storeManager,
        PersistedProtectedValue value,
        CancellationToken cancellationToken
    ) => storeManager.GetStore<IGrantStore>().UpdateProtectedPayloadAsync(value, cancellationToken);

    /// <inheritdoc />
    protected override SecretProtectionContext GetContext(PersistedProtectedValue value) =>
        SecretProtectionContext.ForGrantPayload(value.ItemId);
}
