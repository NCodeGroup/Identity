// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// The keys of a YARP route's metadata that make the route a BFF remote API. No key contains a <c>:</c>, which YARP's
/// configuration loader would read as a section separator.
/// </summary>
[PublicAPI]
public static class BffReverseProxyMetadataKeys
{
    /// <summary>
    /// The key whose value names the <see cref="IBffTokenRequirement"/> of the route's token, such as <c>user</c> or
    /// <c>none</c>. A route without it is not a BFF remote API.
    /// </summary>
    public const string TokenRequirement = "BffTokenRequirement";

    /// <summary>
    /// The key whose value is the resource indicator that the route's token is requested for.
    /// </summary>
    public const string Resource = "BffResource";

    /// <summary>
    /// The key whose value is the scope that the route's token is requested for.
    /// </summary>
    public const string Scope = "BffScope";

    /// <summary>
    /// The key whose value is the name of the BFF frontend that the route serves. A route without it serves every
    /// frontend.
    /// </summary>
    public const string Frontend = "BffFrontend";
}
