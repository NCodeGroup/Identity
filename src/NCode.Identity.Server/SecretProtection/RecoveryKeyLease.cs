// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Server.SecretProtection;

/// <summary>
/// Holds the two sensitive buffers of the recovery private key until the command ends, and zeroes both when it is
/// disposed.
/// </summary>
// See ADR-1408.
internal sealed class RecoveryKeyLease(IDisposable bytesLease, IDisposable charsLease) : IDisposable
{
    /// <summary>
    /// Gets or sets the PEM text of the recovery private key, which lives in the sensitive character buffer.
    /// </summary>
    public ReadOnlyMemory<char> Pem { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        bytesLease.Dispose();
        charsLease.Dispose();
    }
}
