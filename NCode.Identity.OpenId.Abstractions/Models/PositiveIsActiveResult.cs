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
/// Contains an <see cref="IsActiveResult.IsActive"/> property indicating whether the result of the operation or context is active or not.
/// The initial value is <c>true</c> and consumers can call the <see cref="SetInactive"/> method to indicate that the result is not active.
/// </summary>
[PublicAPI]
public class PositiveIsActiveResult : IsActiveResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PositiveIsActiveResult"/> class
    /// with <see cref="IsActiveResult.IsActive"/> initially set to <c>true</c>.
    /// </summary>
    public PositiveIsActiveResult() : base(initial: true)
    {
        // nothing
    }

    /// <summary>
    /// Sets <see cref="IsActiveResult.IsActive"/> to indicate that the result of the operation or context is not active.
    /// </summary>
    public void SetInactive() => IsActive = false;
}
