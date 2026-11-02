// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Represents a request to the token endpoint of an OpenID provider. The client writes the
/// <c>grant_type</c>, <c>scope</c> and <c>resource</c> parameters; a subclass writes only its own parameters.
/// </summary>
[PublicAPI]
public abstract class TokenRequest
{
    /// <summary>
    /// Gets the value of the <c>grant_type</c> parameter.
    /// </summary>
    public abstract string GrantType { get; }

    /// <summary>
    /// Gets the optional space-delimited <c>scope</c> parameter.
    /// </summary>
    public string? Scope { get; init; }

    /// <summary>
    /// Gets the <c>resource</c> indicators (RFC 8707), each written as its own parameter.
    /// </summary>
    public List<string> Resources { get; init; } = [];

    /// <summary>
    /// Gets the RFC 7638 thumbprint (<c>jkt</c>) of the DPoP key that must sign this request's proof, as an earlier
    /// <see cref="TokenResult"/> or <c>AuthorizationRequestResult</c> reported it. <see langword="null"/> uses the
    /// current key of <c>ClientRegistration.DPoPKeyRing</c>. When set, a proof from that key is required.
    /// </summary>
    public string? DPoPJwkThumbprint { get; init; }

    /// <summary>
    /// Adds the parameters that are specific to this grant type.
    /// </summary>
    /// <param name="parameters">The collection to add the parameters to.</param>
    public abstract void AddGrantParameters(ICollection<KeyValuePair<string, string>> parameters);
}
