#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.OpenId.Subject;

/// <summary>
/// Contains the result of validating whether the subject is active or not.
/// </summary>
[PublicAPI]
public class ValidateSubjectIsActiveResult
{
    /// <summary>
    /// Gets a <see cref="bool"/> value indicating whether the subject is active.
    /// The initial value is <c>true</c> and handlers can call the <see cref="SetInactive"/> method
    /// to indicate that the subject is not active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Sets the result to indicate that the subject is not active.
    /// </summary>
    public void SetInactive() => IsActive = false;
}
