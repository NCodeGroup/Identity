// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Provides a client that counts how many times it is disposed.
/// </summary>
internal sealed class FakeDisposableClient : IDisposable
{
    private int _disposeCount;

    /// <summary>
    /// Gets the number of times that <see cref="Dispose"/> was called.
    /// </summary>
    public int DisposeCount => Volatile.Read(ref _disposeCount);

    /// <inheritdoc />
    public void Dispose() => Interlocked.Increment(ref _disposeCount);
}
