// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Restores the connection session that was current before <see cref="DefaultBffConnectionSessionAccessor.Use"/>.
/// </summary>
/// <param name="accessor">The accessor to restore.</param>
/// <param name="previous">The connection session to restore.</param>
internal sealed class BffConnectionSessionRestore(
    DefaultBffConnectionSessionAccessor accessor,
    IBffConnectionSession? previous
) : IDisposable
{
    private DefaultBffConnectionSessionAccessor Accessor { get; } = accessor;
    private IBffConnectionSession? Previous { get; } = previous;

    private int _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Accessor.Restore(Previous);
    }
}
