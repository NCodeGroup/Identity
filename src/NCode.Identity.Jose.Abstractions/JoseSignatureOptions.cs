#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using JetBrains.Annotations;
using NCode.Identity.Jose.Credentials;

namespace NCode.Identity.Jose;

/// <summary>
/// Contains the credentials and options for a single signature of a JWS token.
/// A JWS token always has at least one signature; the compact serialization carries exactly one (and cannot
/// carry an unprotected header), while the JSON serialization may carry several.
/// </summary>
/// <param name="signingCredentials">The <see cref="JoseSigningCredentials"/> that are used to produce this signature.</param>
[PublicAPI]
public sealed class JoseSignatureOptions(JoseSigningCredentials signingCredentials)
{
    /// <summary>
    /// Gets the <see cref="JoseSigningCredentials"/> that are used to produce this signature.
    /// </summary>
    public JoseSigningCredentials SigningCredentials { get; } = signingCredentials;

    /// <summary>
    /// Gets the value for the <c>typ</c> JOSE header parameter that is emitted into this signature's protected header.
    /// The default value is <c>JWT</c>. Use <c>null</c> or an empty string to omit the header.
    /// </summary>
    public string? TokenType { get; init; } = JoseTokenTypes.Jwt;

    /// <summary>
    /// Gets a boolean indicating whether to add the secret's <c>kid</c> value to this signature's protected header.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool AddKeyIdHeader { get; init; } = true;

    /// <summary>
    /// Gets any additional members to include in this signature's JOSE Protected Header, which <b>is</b>
    /// integrity protected by the signature. The default value is <c>null</c>.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? ProtectedHeaders { get; init; }

    /// <summary>
    /// Gets any additional members to include in this signature's JWS Unprotected Header, which is <b>not</b>
    /// integrity protected by the signature. This is only valid for the JSON serialization. The default value
    /// is <c>null</c>.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>>? UnprotectedHeaders { get; init; }
}
