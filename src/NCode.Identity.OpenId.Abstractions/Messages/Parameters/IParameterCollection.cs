// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides a mutable collection of <see cref="IParameter"/> instances that can be accessed by their name or descriptor.
/// </summary>
public interface IParameterCollection : IReadOnlyParameterCollection
{
    /// <summary>
    /// Removes the parameter with the specified name from the collection.
    /// </summary>
    /// <param name="parameterName">The name of the parameter to remove.</param>
    /// <returns><c>true</c> if the parameter is successfully removed; otherwise, <c>false</c>. This method also returns <c>false</c> if the parameter was not found in the collection.</returns>
    bool Remove(string parameterName);

    /// <summary>
    /// Sets the specified parameter in the collection.
    /// </summary>
    /// <param name="parameter">The parameter to set in the collection.</param>
    void Set(IParameter parameter);
}
