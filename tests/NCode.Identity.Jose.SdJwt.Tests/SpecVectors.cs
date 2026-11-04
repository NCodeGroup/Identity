// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.SdJwt.Tests;

/// <summary>
/// Disclosures and digests from the examples of RFC 9901, so the parser and the digest computation are pinned to
/// values another implementation produced. Each digest is the SHA-256 of the ASCII of the encoded disclosure, as
/// <c>printf '%s' "$disclosure" | openssl dgst -sha256 -binary | basenc --base64url | tr -d '='</c> confirms.
/// </summary>
public static class SpecVectors
{
    /// <summary>
    /// <c>["_26bc4LT-ac6q2KI6cBW5es", "family_name", "Möbius"]</c>, an object property disclosure (RFC 9901 §4.2.1).
    /// </summary>
    public const string FamilyNameDisclosure =
        "WyJfMjZiYzRMVC1hYzZxMktJNmNCVzVlcyIsICJmYW1pbHlfbmFtZSIsICJNw7ZiaXVzIl0";

    public const string FamilyNameSalt = "_26bc4LT-ac6q2KI6cBW5es";

    public const string FamilyNameDigest = "X9yH0Ajrdm1Oij4tWso9UzzKJvPoDxwmuEcO3XAdRC0";

    /// <summary>
    /// <c>["6Ij7tM-a5iVPGboS5tmvVA", "email", "johndoe@example.com"]</c>, an object property disclosure.
    /// </summary>
    public const string EmailDisclosure =
        "WyI2SWo3dE0tYTVpVlBHYm9TNXRtdlZBIiwgImVtYWlsIiwgImpvaG5kb2VAZXhhbXBsZS5jb20iXQ";

    public const string EmailDigest = "JzYjH4svliH0R3PyEMfeZu6Jt69u5qehZo7F7EPYlSE";

    /// <summary>
    /// <c>["lklxF5jMYlGTPUovMNIvCA", "FR"]</c>, an array element disclosure (RFC 9901 §4.2.2).
    /// </summary>
    public const string NationalityDisclosure = "WyJsa2x4RjVqTVlsR1RQVW92TU5JdkNBIiwgIkZSIl0";

    public const string NationalitySalt = "lklxF5jMYlGTPUovMNIvCA";

    public const string NationalityDigest = "w0I8EKcdCtUPkGCNUrfwVp2xEgNjtoIDlOxc9-PlOhs";
}
