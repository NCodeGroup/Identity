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

using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Contains the credentials that protect a JWT the server addresses to a client: the tenant's key that signs it and the
/// client's key it is encrypted to. A response with neither is returned unprotected.
/// </summary>
/// <param name="SigningCredentials">The credentials that sign the JWT, or <c>null</c> when it is not signed.</param>
/// <param name="EncryptionCredentials">The credentials that encrypt the JWT, or <c>null</c> when it is not
/// encrypted.</param>
internal readonly record struct ClientJwtCredentials(
    JoseSigningCredentials? SigningCredentials,
    JoseEncryptionCredentials? EncryptionCredentials
)
{
    /// <summary>
    /// Gets a value indicating whether the JWT is signed, encrypted, or both.
    /// </summary>
    public bool IsProtected => SigningCredentials is not null || EncryptionCredentials is not null;
}
