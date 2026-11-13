// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides the names of the sources a setup token comes from.
/// </summary>
[PublicAPI]
public static class SetupTokenSources
{
    /// <summary>
    /// The setup token comes from configuration.
    /// </summary>
    public const string Configured = "configured";

    /// <summary>
    /// The host generates the setup token and logs it once.
    /// </summary>
    public const string Generated = "generated";
}
