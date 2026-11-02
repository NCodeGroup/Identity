// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Persistence;

/// <summary>
/// Contains constants for the maximum lengths of various fields.
/// </summary>
[PublicAPI]
public static class MaxLengths
{
    /// <summary>
    /// Specifies the maximum length of a resource's type discriminator.
    /// </summary>
    public const int ResourceType = 100;

    /// <summary>
    /// Specifies the maximum length of a resource's identifier.
    /// </summary>
    public const int ResourceId = 300;

    /// <summary>
    /// Specifies the maximum length of a concurrency token.
    /// </summary>
    public const int ConcurrencyToken = 50;
}
