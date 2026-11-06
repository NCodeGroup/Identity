// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides the BFF options of the frontend set in effect.
/// </summary>
/// <remarks>
/// A package that serves next to the BFF reads its options through this accessor on every use, so that it follows the
/// frontends the BFF serves and not the values that the host started with.
/// </remarks>
// See ADR-1442.
public interface IBffOptionsAccessor
{
    /// <summary>
    /// Gets the BFF options of the frontend set in effect: the last configuration change that validated as a whole.
    /// </summary>
    /// <exception cref="InvalidOperationException">The first set of frontends is not valid.</exception>
    BffOptions Current { get; }
}
