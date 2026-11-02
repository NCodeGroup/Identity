// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides a readonly collection of <see cref="KnownParameter"/> instances that can be accessed by name.
/// </summary>
[PublicAPI]
public interface IKnownParameterCollection : IReadOnlyCollection<KnownParameter>
{
    /// <summary>
    /// Attempts to get a <see cref="KnownParameter"/> that has the specified name.
    /// </summary>
    /// <param name="parameterName">The name of the <see cref="KnownParameter"/> to locate.</param>
    /// <param name="knownParameter">When this method returns, the <see cref="KnownParameter"/> with the specified
    /// name, if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if the <see cref="KnownParameter"/> with the specified name was found; otherwise,
    /// <c>false</c>.</returns>
    bool TryGet(string parameterName, [MaybeNullWhen(false)] out KnownParameter knownParameter);
}
