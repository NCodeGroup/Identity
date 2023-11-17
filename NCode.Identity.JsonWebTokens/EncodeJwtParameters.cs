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

using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using NCode.Jose;
using NCode.Jose.Credentials;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Contains the parameters used to encode a Json Web Token (JWT) security token.
/// </summary>
public class EncodeJwtParameters
{
    /// <summary>
    /// Gets or sets the value that will be used in the <c>typ</c> header parameter.
    /// The default value is <c>JWT</c>.
    /// </summary>
    public string TokenType { get; set; } = JoseTokenTypes.Jwt;

    /// <summary>
    /// Gets or sets a boolean indicating whether to add the secret's <c>kid</c> value (if present) to the JWT header.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool AddKeyIdHeader { get; set; } = true;

    /// <summary>
    /// Gets or sets the <see cref="JoseSigningCredentials"/> that is used to sign the token.
    /// If both <see cref="SigningCredentials"/> and <see cref="EncryptionCredentials"/> are specified,
    /// then the result will be a nested JWS+JWE token. Both credentials cannot be <c>null</c>.
    /// </summary>
    public JoseSigningCredentials? SigningCredentials { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="JoseEncryptionCredentials"/> that is used to encrypt the token.
    /// If both <see cref="SigningCredentials"/> and <see cref="EncryptionCredentials"/> are specified,
    /// then the result will be a nested JWS+JWE token. Both credentials cannot be <c>null</c>.
    /// </summary>
    public JoseEncryptionCredentials? EncryptionCredentials { get; set; }

    /// <summary>
    /// Gets or sets the value of the <c>iss</c> claim.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the value of the <c>aud</c> claim.
    /// </summary>
    public StringValues Audience { get; set; }

    /// <summary>
    /// Gets or sets the value of the <c>iat</c> claim.
    /// </summary>
    public DateTimeOffset? IssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the value of the <c>nbf</c> claim.
    /// </summary>
    public DateTimeOffset? NotBefore { get; set; }

    /// <summary>
    /// Gets or sets the value of the <c>exp</c> claim.
    /// </summary>
    public DateTimeOffset? Expires { get; set; }

    /// <summary>
    /// Gets or sets the collection of <see cref="Claim"/> values that will be added to the payload.
    /// </summary>
    public IEnumerable<Claim>? SubjectClaims { get; set; }

    /// <summary>
    /// Gets or sets the extra claims that will be added to the payload.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraPayloadClaims { get; set; }

    /// <summary>
    /// Gets or sets the extra claims that will be added to the signature header.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraSignatureHeaderClaims { get; set; }

    /// <summary>
    /// Gets or sets the extra claims that will be added to the encryption header.
    /// </summary>
    public IReadOnlyDictionary<string, object>? ExtraEncryptionHeaderClaims { get; set; }
}
