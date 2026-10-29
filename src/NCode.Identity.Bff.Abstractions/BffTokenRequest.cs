#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.Bff;

/// <summary>
/// Describes the access token that a caller asks the BFF to resolve for the current session.
/// </summary>
[PublicAPI]
public sealed class BffTokenRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffTokenRequest"/> class.
    /// </summary>
    /// <param name="requirement">The name of the token requirement, such as <c>user</c>.</param>
    public BffTokenRequest(string requirement)
    {
        ArgumentException.ThrowIfNullOrEmpty(requirement);
        Requirement = requirement;
    }

    /// <summary>
    /// Gets the name of the token requirement.
    /// </summary>
    public string Requirement { get; }

    /// <summary>
    /// Gets the resource indicator the access token is requested for, or <see langword="null"/> for none.
    /// </summary>
    public string? Resource { get; init; }

    /// <summary>
    /// Gets the scope the access token is requested for, or <see langword="null"/> for none.
    /// </summary>
    public string? Scope { get; init; }
}
