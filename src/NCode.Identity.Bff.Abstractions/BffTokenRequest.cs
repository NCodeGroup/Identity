// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
