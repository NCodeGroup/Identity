// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Describes one parameter of a COSE_Key key type and the JSON Web Key member it maps onto.
/// </summary>
[PublicAPI]
public sealed class CoseKeyParameter
{
    /// <summary>
    /// Gets the label of the parameter in the COSE_Key map.
    /// </summary>
    public required int Label { get; init; }

    /// <summary>
    /// Gets the name of the JSON Web Key member the parameter maps onto.
    /// </summary>
    public required string JwkMember { get; init; }

    /// <summary>
    /// Gets the form of the parameter value.
    /// </summary>
    public required CoseKeyParameterEncoding Encoding { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter holds private key material, which this library neither reads nor
    /// writes.
    /// </summary>
    public required bool IsPrivate { get; init; }
}
