// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Describes the form of a COSE_Key parameter value that a JSON Web Key member can represent.
/// </summary>
// The forms are a closed set because a COSE_Key is imported through the registered secret key types, which read a JSON
// Web Key, and a JSON Web Key key member is either base64url bytes or text; a COSE_Key parameter of another form has
// no JSON Web Key form and cannot be imported. See ADR-1409.
[PublicAPI]
public enum CoseKeyParameterEncoding
{
    /// <summary>
    /// A byte string, the base64url text of the JSON Web Key member.
    /// </summary>
    ByteString,

    /// <summary>
    /// An integer that identifies a registered curve, the curve name of the JSON Web Key member.
    /// </summary>
    Curve,
}
