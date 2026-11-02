// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Common abstraction for all <c>JOSE</c> algorithms.
/// </summary>
[PublicAPI]
public abstract class Algorithm
{
    /// <summary>
    /// Gets an <see cref="AlgorithmType"/> value that describes the type of the current algorithm.
    /// </summary>
    public abstract AlgorithmType Type { get; }

    /// <summary>
    /// Gets a <see cref="string"/> value that uniquely identifies the current algorithm.
    /// </summary>
    public abstract string Code { get; }
}
