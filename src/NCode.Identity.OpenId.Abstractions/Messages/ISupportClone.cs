// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Specifies that the current instance supports cloning.
/// </summary>
/// <typeparam name="T">The type of the instance to clone.</typeparam>
[PublicAPI]
public interface ISupportClone<out T>
{
    /// <summary>
    /// Creates a deep copy of the current instance.
    /// </summary>
    /// <returns>The cloned instance.</returns>
    T Clone();
}
