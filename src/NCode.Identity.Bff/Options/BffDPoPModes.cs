// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Defines the values of <see cref="BffDPoPOptions.Mode"/>.
/// </summary>
[PublicAPI]
public static class BffDPoPModes
{
    /// <summary>
    /// The BFF uses DPoP when the OpenID Provider supports it.
    /// </summary>
    public const string WhenSupported = "when_supported";

    /// <summary>
    /// The BFF always binds its access tokens to a DPoP key.
    /// </summary>
    public const string Required = "required";
}
