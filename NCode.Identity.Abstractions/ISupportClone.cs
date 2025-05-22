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

using JetBrains.Annotations;

namespace NCode.Identity;

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
