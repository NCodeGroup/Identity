// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides the constants that bound the setup surface.
/// </summary>
[PublicAPI]
public static class SetupDefaults
{
    /// <summary>
    /// The path prefix under which every setup route is mapped.
    /// </summary>
    public const string PathPrefix = "/setup";

    /// <summary>
    /// The minimum length of a configured setup token.
    /// </summary>
    public const int MinimumTokenLength = 32;

    /// <summary>
    /// The minimum length of the root administrator's password.
    /// </summary>
    public const int MinimumRootPasswordLength = 15;

    /// <summary>
    /// The number of root-creation attempts allowed per client address each minute.
    /// </summary>
    public const int RootAttemptsPerMinute = 10;
}
