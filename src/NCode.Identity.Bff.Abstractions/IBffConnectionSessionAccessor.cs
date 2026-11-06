// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides the connection session that is current for the calling asynchronous flow.
/// </summary>
[PublicAPI]
public interface IBffConnectionSessionAccessor
{
    /// <summary>
    /// Gets the connection session that is current for the calling asynchronous flow, or <see langword="null"/> when
    /// none is.
    /// </summary>
    IBffConnectionSession? Current { get; }

    /// <summary>
    /// Makes <paramref name="session"/> current for the calling asynchronous flow until the result is disposed.
    /// </summary>
    /// <param name="session">The connection session to make current.</param>
    /// <returns>A scope that restores the previously current connection session when disposed.</returns>
    IDisposable Use(IBffConnectionSession session);
}
