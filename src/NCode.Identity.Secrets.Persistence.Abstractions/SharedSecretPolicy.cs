// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the policy constants for shared secrets an external party presents.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class SharedSecretPolicy
{
    /// <summary>
    /// Specifies the fewest bits of random entropy a generated shared secret, such as an OAuth <c>client_secret</c>,
    /// may have.
    /// </summary>
    public const int MinimumEntropyBits = 256;
}
