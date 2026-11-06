// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Keeps the connection session that is current for the calling asynchronous flow in an <see cref="AsyncLocal{T}"/>.
/// </summary>
internal sealed class DefaultBffConnectionSessionAccessor : IBffConnectionSessionAccessor
{
    private AsyncLocal<IBffConnectionSession?> CurrentSession { get; } = new();

    /// <inheritdoc />
    public IBffConnectionSession? Current => CurrentSession.Value;

    /// <inheritdoc />
    public IDisposable Use(IBffConnectionSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var previous = CurrentSession.Value;
        CurrentSession.Value = session;
        return new BffConnectionSessionRestore(this, previous);
    }

    /// <summary>
    /// Makes <paramref name="session"/> the current connection session of the calling asynchronous flow.
    /// </summary>
    /// <param name="session">The connection session to make current, or <see langword="null"/> for none.</param>
    internal void Restore(IBffConnectionSession? session) => CurrentSession.Value = session;
}
