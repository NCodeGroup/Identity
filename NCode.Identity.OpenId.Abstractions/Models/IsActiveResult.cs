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

namespace NCode.Identity.OpenId.Models;

/// <summary>
/// Contains an <see cref="IsActive"/> property indicating whether the result of the operation or context is active or not.
/// </summary>
[PublicAPI]
public abstract class IsActiveResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IsActiveResult"/> class with the specified initial value.
    /// </summary>
    /// <param name="initial">The initial value of the <see cref="IsActive"/> property.</param>
    protected IsActiveResult(bool initial)
    {
        IsActive = initial;
    }

    /// <summary>
    /// Gets a <see cref="bool"/> value indicating whether the result of the operation or context is active or not.
    /// </summary>
    public bool IsActive { get; protected set; }
}
