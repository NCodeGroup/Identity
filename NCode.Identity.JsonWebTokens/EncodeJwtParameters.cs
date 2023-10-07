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

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Contains the parameters used to encode a Json Web Token (JWT) security token.
/// </summary>
public class EncodeJwtParameters
{
    /// <summary>
    /// Gets or sets the <see cref="JoseSigningOptions"/> that is used to sign the token.
    /// </summary>
    public JoseSigningOptions? SigningOptions { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="JoseEncryptingOptions"/> that is used to encrypt the token.
    /// </summary>
    public JoseEncryptingOptions? EncryptingOptions { get; set; }

    /// <summary>
    /// Gets or sets the value of the 'iss' claim.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the value of the 'aud' claim.
    /// </summary>
    public StringValues Audience { get; set; }

    /// <summary>
    /// Gets or sets the value of the 'iat' claim.
    /// </summary>
    public DateTimeOffset? IssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the value of the 'nbf' claim.
    /// </summary>
    public DateTimeOffset? NotBefore { get; set; }

    /// <summary>
    /// Gets or sets the value of the 'exp' claim.
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
