// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.ClientCertificates;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Compares the distinguished names that certificates and revocation information carry.
/// </summary>
internal static class RevocationNames
{
    /// <summary>
    /// Determines whether two distinguished names are the same name, comparing them attribute by attribute rather than
    /// byte by byte. Returns <see langword="false"/> when either name cannot be read.
    /// </summary>
    public static bool Matches(X500DistinguishedName left, X500DistinguishedName right)
    {
        try
        {
            return DistinguishedName.FromX500(left).Matches(DistinguishedName.FromX500(right));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
