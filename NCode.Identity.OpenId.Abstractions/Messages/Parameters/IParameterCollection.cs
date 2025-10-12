#region Copyright Preamble

// Copyright @ 2025 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
