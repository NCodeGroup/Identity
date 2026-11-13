// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Describes the root administrator that setup creates.
/// </summary>
[PublicAPI]
public sealed class SetupRootOptions
{
    /// <summary>
    /// Gets or sets the root administrator's username.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the root administrator's email address, or <c>null</c> when none is recorded.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the root administrator's password.
    /// </summary>
    public string? Password { get; set; }
}
