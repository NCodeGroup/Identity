// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose;

/// <summary>
/// Contains the header parameter labels of CBOR Object Signing and Encryption (COSE, RFC 9052 §3.1) that this library
/// knows. The labels are the known ones, never all of them: a header carries any integer or text label.
/// </summary>
[PublicAPI]
public static class CoseHeaderLabels
{
    /// <summary>
    /// The <c>alg</c> parameter, the algorithm of the layer.
    /// </summary>
    public const long Algorithm = 1;

    /// <summary>
    /// The <c>crit</c> parameter, the labels a recipient of the message must understand.
    /// </summary>
    public const long Critical = 2;

    /// <summary>
    /// The <c>content type</c> parameter.
    /// </summary>
    public const long ContentType = 3;

    /// <summary>
    /// The <c>kid</c> parameter, a key identifier.
    /// </summary>
    public const long KeyId = 4;

    /// <summary>
    /// The <c>IV</c> parameter, an initialization vector.
    /// </summary>
    public const long InitializationVector = 5;

    /// <summary>
    /// The <c>Partial IV</c> parameter, the varying part of an initialization vector.
    /// </summary>
    public const long PartialInitializationVector = 6;

    /// <summary>
    /// The <c>CWT Claims</c> parameter (RFC 9597).
    /// </summary>
    public const long CwtClaims = 15;

    /// <summary>
    /// The <c>typ</c> parameter, the type of the content (RFC 9596).
    /// </summary>
    public const long Type = 16;

    /// <summary>
    /// The <c>x5bag</c> parameter, an unordered bag of X.509 certificates (RFC 9360).
    /// </summary>
    public const long X509Bag = 32;

    /// <summary>
    /// The <c>x5chain</c> parameter, an ordered chain of X.509 certificates (RFC 9360).
    /// </summary>
    public const long X509Chain = 33;

    /// <summary>
    /// The <c>x5t</c> parameter, the SHA-256 thumbprint of an X.509 certificate (RFC 9360).
    /// </summary>
    public const long X509ThumbprintSha256 = 34;

    /// <summary>
    /// The <c>x5u</c> parameter, a URI that refers to an X.509 certificate (RFC 9360).
    /// </summary>
    public const long X509Url = 35;
}
