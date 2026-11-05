// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Dispatch;

namespace NCode.Identity.OpenId.SecretProtection;

/// <summary>
/// A self-contained, serializable request to rewrap the values that the retired master keys of an owner protect. It is
/// dispatched after a rotation, once every replica has had time to reload the owner's keys, and only brings the work
/// forward: the maintenance pass rewraps whatever the job did not.
/// </summary>
// See ADR-1407.
internal sealed record MasterKeyRewrapJob : IDispatchJob
{
    /// <summary>
    /// Gets the kind of owner whose values are rewrapped.
    /// </summary>
    public required string OwnerType { get; init; }

    /// <summary>
    /// Gets the identifier of the owner whose values are rewrapped.
    /// </summary>
    public required string OwnerId { get; init; }
}
