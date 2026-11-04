// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Holds the <see cref="ISecretProtector"/> of the server so that code which cannot await, such as the Data Protection
/// key ring, can protect and unprotect synchronously. The protector is loaded once when the host starts and is read
/// from other threads afterwards.
/// </summary>
// See ADR-1235.
internal class PlatformSecretProtectorHolder
{
    // A volatile field rather than an auto-property: the protector is published once at startup and read from other
    // threads, and a property cannot be declared volatile.
    private volatile ISecretProtector? _protector;

    /// <summary>
    /// Gets or sets the <see cref="ISecretProtector"/> of the server, or <see langword="null"/> until it has been
    /// loaded.
    /// </summary>
    public ISecretProtector? Protector
    {
        get => _protector;
        set => _protector = value;
    }

    /// <summary>
    /// Gets the <see cref="ISecretProtector"/> of the server.
    /// </summary>
    /// <returns>The loaded <see cref="ISecretProtector"/> of the server.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the protector has not been loaded yet.</exception>
    public ISecretProtector GetRequired() =>
        Protector
        ?? throw new InvalidOperationException(
            "The server's secret protector is not loaded: the Data Protection key ring was used before the host started its hosted services."
        );
}
