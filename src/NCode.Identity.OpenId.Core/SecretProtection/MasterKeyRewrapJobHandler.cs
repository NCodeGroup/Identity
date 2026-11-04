// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Dispatch;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// Rewraps the values that the retired master keys of the owner named by a <see cref="MasterKeyRewrapJob"/> protect.
/// A failure propagates, so the dispatcher retries the job; a repeated run is harmless because a rewrapped value no
/// longer names a retired key.
/// </summary>
// See ADR-1407.
internal sealed class MasterKeyRewrapJobHandler(IMasterKeyManager masterKeyManager)
    : IDispatchJobHandler<MasterKeyRewrapJob>
{
    private IMasterKeyManager MasterKeyManager { get; } = masterKeyManager;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        MasterKeyRewrapJob job,
        DispatchContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(job);

        await MasterKeyManager.RewrapAsync(
            new SecretOwner(job.OwnerType, job.OwnerId),
            cancellationToken
        );
    }
}
