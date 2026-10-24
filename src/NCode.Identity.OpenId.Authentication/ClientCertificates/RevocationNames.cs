#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
