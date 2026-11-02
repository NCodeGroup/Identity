// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the ability to wrap a master key under the offline recovery public key, so the stored secrets can still
/// be read after the root key is lost. Only the public half of the recovery key is ever deployed; the private half
/// is kept offline.
/// </summary>
// See ADR-1235.
internal interface IRecoveryKeyWrapper : IKeyWrapper
{
    /// <summary>
    /// Gets a value indicating whether a recovery public key is configured. When it is not,
    /// <see cref="IKeyWrapper.WrapAsync"/> throws an <see cref="InvalidOperationException"/>.
    /// </summary>
    bool IsConfigured { get; }
}
