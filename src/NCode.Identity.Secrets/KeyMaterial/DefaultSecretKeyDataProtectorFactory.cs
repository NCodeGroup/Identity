// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NCode.Extensions.DataProtection;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Default implementation of <see cref="IDataProtectorFactory{T}"/> for <see cref="SecretKey"/> instances
/// that uses ephemeral data protection keys stored only in memory for the lifetime of the current process.
/// </summary>
/// <param name="loggerFactory">The <see cref="ILoggerFactory"/> used by the underlying
/// <see cref="EphemeralDataProtectionProvider"/> for logging.</param>
/// <remarks>
/// <para>
/// This factory creates <see cref="IDataProtector"/> instances backed by <see cref="EphemeralDataProtectionProvider"/>,
/// which generates cryptographic keys that exist only in memory and are not persisted to any storage.
/// </para>
/// <para>
/// <b>Important:</b> Data protected using this factory cannot be unprotected after the application restarts
/// or by any other process. This is intentional for <see cref="SecretKey"/> instances that should only be
/// accessible within the current running process.
/// </para>
/// <para>
/// Use this implementation when:
/// </para>
/// <list type="bullet">
/// <item><description>Secret keys are transient and do not need to survive process restarts.</description></item>
/// <item><description>Secret keys should be isolated to the local machine and current process only.</description></item>
/// <item><description>You want to avoid the complexity of managing persistent data protection key storage.</description></item>
/// </list>
/// </remarks>
internal class DefaultSecretKeyDataProtectorFactory(ILoggerFactory loggerFactory)
    : DataProtectorFactory<SecretKey>(new EphemeralDataProtectionProvider(loggerFactory))
{
    // nothing
}
