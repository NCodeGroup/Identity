// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures DPoP sender-constraining of the BFF's access tokens.
/// </summary>
[PublicAPI]
public sealed class BffDPoPOptions
{
    /// <summary>
    /// Gets or sets the DPoP mode, one of <see cref="BffDPoPModes"/>. The default is
    /// <see cref="BffDPoPModes.WhenSupported"/>. The configuration key is <c>Bff:DPoP:Mode</c>.
    /// </summary>
    public string Mode { get; set; } = BffDPoPModes.WhenSupported;

    /// <summary>
    /// Gets or sets the name of the key ring that holds the DPoP keys. The configuration key is
    /// <c>Bff:DPoP:KeyRing</c>.
    /// </summary>
    public string? KeyRing { get; set; }
}
